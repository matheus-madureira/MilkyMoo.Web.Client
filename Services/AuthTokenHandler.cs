using System.Net;
using System.Net.Http.Headers;

namespace MilkyMoo.Services;

/// <summary>
/// Assina cada chamada da API: acrescenta o header de tenant, o <c>Bearer</c> e renova o token quando ele
/// expira — antes de enviar, e mais uma vez se a API responder 401.
/// </summary>
/// <remarks>
/// As rotas de login, renovação e cadastro são públicas e não recebem <c>Authorization</c>: mandar um token
/// morto para elas só provocaria um 401 desnecessário.
/// </remarks>
public sealed class AuthTokenHandler(AuthSession session, ApiOptions options) : DelegatingHandler
{
    private static readonly string[] PublicPaths =
    [
        "/api/v1/users/login",
        "/api/v1/users/refresh-token"
    ];

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        bool isPublic = IsPublic(request);

        string? accessToken = isPublic ? null : await session.GetAccessTokenAsync();

        Prepare(request, accessToken);

        HttpResponseMessage response = await base.SendAsync(request, cancellationToken);

        if (isPublic || response.StatusCode is not HttpStatusCode.Unauthorized)
        {
            return response;
        }

        // O token pode ter sido revogado antes da hora prevista. Uma única nova tentativa — passando o
        // token recusado, para a renovação saber que não basta olhar o relógio.
        if (!await session.RefreshAsync(accessToken))
        {
            return response;
        }

        HttpRequestMessage retry = await CloneAsync(request);
        Prepare(retry, await session.GetAccessTokenAsync());

        response.Dispose();

        return await base.SendAsync(retry, cancellationToken);
    }

    /// <summary>Cadastro (<c>POST /api/v1/users</c>) é anônimo; leitura e edição de usuários não são.</summary>
    private static bool IsPublic(HttpRequestMessage request)
    {
        string path = request.RequestUri?.AbsolutePath ?? string.Empty;

        return PublicPaths.Contains(path, StringComparer.OrdinalIgnoreCase)
            || (request.Method == HttpMethod.Post && path.Equals("/api/v1/users", StringComparison.OrdinalIgnoreCase));
    }

    private void Prepare(HttpRequestMessage request, string? accessToken)
    {
        request.Headers.Remove(ApiOptions.TenantHeader);
        request.Headers.Add(ApiOptions.TenantHeader, options.Tenant);

        request.Headers.Authorization = accessToken is null
            ? null
            : new AuthenticationHeaderValue("Bearer", accessToken);
    }

    /// <summary>
    /// Uma <see cref="HttpRequestMessage"/> já enviada não pode ser reenviada, então a nova tentativa vai
    /// numa cópia — com o corpo bufferizado, porque o original já foi consumido.
    /// </summary>
    private static async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage request)
    {
        HttpRequestMessage clone = new(request.Method, request.RequestUri);

        if (request.Content is not null)
        {
            byte[] body = await request.Content.ReadAsByteArrayAsync();

            ByteArrayContent content = new(body);

            foreach (KeyValuePair<string, IEnumerable<string>> header in request.Content.Headers)
            {
                content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            clone.Content = content;
        }

        foreach (KeyValuePair<string, IEnumerable<string>> header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }
}
