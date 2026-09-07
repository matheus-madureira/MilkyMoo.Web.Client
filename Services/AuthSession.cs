using System.Net.Http.Json;
using System.Text.Json;

namespace MilkyMoo.Services;

/// <summary>
/// Dono do par de tokens: emite, renova, persiste e descarta. É a única fonte de verdade sobre "há sessão".
/// </summary>
/// <remarks>
/// <para>
/// Usa um <see cref="HttpClient"/> <b>cru</b>, sem o <see cref="AuthTokenHandler"/>. Se o refresh passasse
/// pelo handler, um 401 na renovação dispararia outra renovação, em laço.
/// </para>
/// <para>
/// O access token vive 5 minutos e o refresh 30. Como o Keycloak <b>rotaciona</b> o refresh token a cada
/// renovação, duas renovações simultâneas fariam a segunda usar um token já invalidado e derrubariam a
/// sessão — daí o <see cref="SemaphoreSlim"/> em <see cref="RefreshAsync"/>.
/// </para>
/// </remarks>
public sealed class AuthSession(HttpClient http, TokenStore store, ApiOptions options)
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    private AuthTokens? _tokens;

    /// <summary>Disparado sempre que a sessão nasce, é renovada ou termina.</summary>
    public event Action? Changed;

    /// <summary>Tokens correntes, ou <c>null</c> quando não há sessão.</summary>
    public AuthTokens? Tokens => _tokens;

    /// <summary>
    /// Há sessão enquanto o <b>refresh</b> token estiver vivo. O access token expira a cada 5 minutos e é
    /// renovado sob demanda; deslogar a cada 5 minutos seria inutilizável.
    /// </summary>
    public bool IsAuthenticated => _tokens?.IsRefreshValid == true;

    /// <summary>Lê o armazenamento local no boot e recupera a sessão, renovando se necessário.</summary>
    public async Task RestoreAsync()
    {
        AuthTokens? stored = await store.ReadAsync();

        if (stored is null)
        {
            return;
        }

        if (!stored.IsRefreshValid)
        {
            await store.ClearAsync();
            return;
        }

        _tokens = stored;

        if (!stored.IsAccessValid)
        {
            await RefreshAsync();
        }

        Changed?.Invoke();
    }

    /// <summary>Troca credenciais por tokens. Não lança: devolve a mensagem já traduzida.</summary>
    public async Task<AuthResult> SignInAsync(string username, string password)
    {
        try
        {
            using HttpRequestMessage request = Request(HttpMethod.Post, "api/v1/users/login");
            request.Content = JsonContent.Create(new { username, password });

            using HttpResponseMessage response = await http.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                return AuthResult.Failure(await ApiErrorReader.ReadAsync(response));
            }

            AuthTokens? tokens = AuthTokens.From(
                await response.Content.ReadFromJsonAsync<AuthTokens.TokenResponse>());

            if (tokens is null)
            {
                return AuthResult.Failure(ApiErrorReader.NetworkError);
            }

            await AcceptAsync(tokens);

            return AuthResult.Success();
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            return AuthResult.Failure(ApiErrorReader.NetworkError);
        }
    }

    /// <summary>
    /// Renova o par de tokens. Devolve <c>true</c> se a sessão continua válida depois da tentativa.
    /// Falhando, a sessão é encerrada — quem chamou não precisa limpar nada.
    /// </summary>
    /// <param name="staleAccessToken">
    /// O access token que motivou a renovação. Se, ao entrar no semáforo, o token corrente já for outro,
    /// então outra chamada renovou primeiro e não há o que fazer. Comparar o <b>valor</b>, e não a validade
    /// pelo relógio, é o que faz o caminho do 401 funcionar: um token revogado antes da hora continua
    /// "válido" para o relógio, e conferir só a expiração devolveria o mesmo token recusado ao retry.
    /// </param>
    public async Task<bool> RefreshAsync(string? staleAccessToken = null)
    {
        await _gate.WaitAsync();

        try
        {
            // Outra chamada pode ter renovado enquanto esta esperava no semáforo.
            if (_tokens is not null && staleAccessToken is not null && _tokens.AccessToken != staleAccessToken)
            {
                return true;
            }

            if (_tokens is not { IsRefreshValid: true } current)
            {
                await SignOutLocalAsync();
                return false;
            }

            using HttpRequestMessage request = Request(HttpMethod.Post, "api/v1/users/refresh-token");
            request.Content = JsonContent.Create(new { refreshToken = current.RefreshToken });

            using HttpResponseMessage response = await http.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                // 5xx não diz nada sobre o token — é defeito do servidor, e hoje /users/refresh-token
                // responde 500 até para um refresh token recém-emitido. Derrubar a sessão aqui poria o
                // usuário na tela de login por culpa da API. A sessão sobrevive; a próxima ação tenta de
                // novo. Só um 4xx significa "este token não vale mais".
                if ((int)response.StatusCode < 500)
                {
                    await SignOutLocalAsync();
                }

                return false;
            }

            AuthTokens? renewed = AuthTokens.From(
                await response.Content.ReadFromJsonAsync<AuthTokens.TokenResponse>());

            if (renewed is null)
            {
                await SignOutLocalAsync();
                return false;
            }

            await AcceptAsync(renewed);

            return true;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            // Rede fora do ar não invalida o refresh token: manter a sessão e deixar a próxima ação tentar.
            return false;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Descarta a sessão local e avisa quem escuta. Não fala com a API.</summary>
    public async Task SignOutLocalAsync()
    {
        _tokens = null;

        await store.ClearAsync();

        Changed?.Invoke();
    }

    /// <summary>Garante um access token utilizável, renovando se estiver perto de expirar.</summary>
    public async Task<string?> GetAccessTokenAsync()
    {
        if (_tokens is null)
        {
            return null;
        }

        if (_tokens.IsAccessValid)
        {
            return _tokens.AccessToken;
        }

        return await RefreshAsync(_tokens.AccessToken) ? _tokens?.AccessToken : null;
    }

    /// <summary>Monta a requisição já com o header de tenant, exigido por todas as rotas da API.</summary>
    private HttpRequestMessage Request(HttpMethod method, string path)
    {
        HttpRequestMessage request = new(method, path);
        request.Headers.Add(ApiOptions.TenantHeader, options.Tenant);

        return request;
    }

    private async Task AcceptAsync(AuthTokens tokens)
    {
        _tokens = tokens;

        await store.WriteAsync(tokens);

        Changed?.Invoke();
    }
}
