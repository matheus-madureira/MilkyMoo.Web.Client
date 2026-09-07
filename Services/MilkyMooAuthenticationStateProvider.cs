using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;

namespace MilkyMoo.Services;

/// <summary>
/// Traduz a sessão de <see cref="AuthSession"/> no <see cref="ClaimsPrincipal"/> que o
/// <c>AuthorizeRouteView</c> e o <c>AuthorizeView</c> consomem.
/// </summary>
public sealed class MilkyMooAuthenticationStateProvider : AuthenticationStateProvider
{
    /// <summary>
    /// Precisa ser não-vazio: um <see cref="ClaimsIdentity"/> sem tipo de autenticação tem
    /// <c>IsAuthenticated == false</c>, por mais claims que carregue.
    /// </summary>
    private const string AuthenticationType = "jwt";

    private static readonly AuthenticationState Anonymous = new(new ClaimsPrincipal(new ClaimsIdentity()));

    private readonly AuthSession _session;

    public MilkyMooAuthenticationStateProvider(AuthSession session)
    {
        _session = session;
        _session.Changed += OnSessionChanged;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (!_session.IsAuthenticated || _session.Tokens is not AuthTokens tokens)
        {
            return Task.FromResult(Anonymous);
        }

        ClaimsIdentity identity = new(ReadClaims(tokens.AccessToken), AuthenticationType);

        return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));
    }

    private void OnSessionChanged() =>
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());

    /// <summary>
    /// Lê o payload do JWT. Não valida assinatura — quem valida é a API; no browser isso seria teatro.
    /// </summary>
    private static IEnumerable<Claim> ReadClaims(string jwt)
    {
        if (Decode(jwt) is not JsonElement payload)
        {
            return [];
        }

        List<Claim> claims = [];

        foreach (JsonProperty property in payload.EnumerateObject())
        {
            switch (property.Name)
            {
                case "realm_access":
                    AddRoles(claims, property.Value);
                    break;

                case "resource_access":
                    foreach (JsonProperty resource in property.Value.EnumerateObject())
                    {
                        AddRoles(claims, resource.Value);
                    }

                    break;

                default:
                    AddScalar(claims, MapName(property.Name), property.Value);
                    break;
            }
        }

        return claims;
    }

    /// <summary>Nomes que o Blazor espera para <c>User.Identity.Name</c> e afins.</summary>
    private static string MapName(string claim) => claim switch
    {
        "sub" => ClaimTypes.NameIdentifier,
        "preferred_username" => ClaimTypes.Name,
        "email" => ClaimTypes.Email,
        "given_name" => ClaimTypes.GivenName,
        "family_name" => ClaimTypes.Surname,
        _ => claim
    };

    /// <summary>Os papéis vêm aninhados em <c>{ "roles": [...] }</c>; aqui viram claims planas.</summary>
    private static void AddRoles(List<Claim> claims, JsonElement container)
    {
        if (container.ValueKind is not JsonValueKind.Object ||
            !container.TryGetProperty("roles", out JsonElement roles) ||
            roles.ValueKind is not JsonValueKind.Array)
        {
            return;
        }

        foreach (JsonElement role in roles.EnumerateArray())
        {
            if (role.GetString() is string name)
            {
                claims.Add(new Claim(ClaimTypes.Role, name));
            }
        }
    }

    private static void AddScalar(List<Claim> claims, string name, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False:
                claims.Add(new Claim(name, value.ToString()));
                break;

            case JsonValueKind.Array:
                foreach (JsonElement item in value.EnumerateArray())
                {
                    if (item.ValueKind is JsonValueKind.String && item.GetString() is string text)
                    {
                        claims.Add(new Claim(name, text));
                    }
                }

                break;
        }
    }

    /// <summary>
    /// Decodifica a segunda parte do <c>header.payload.signature</c>. É Base64<b>Url</b>: sem o ajuste de
    /// <c>-</c>/<c>_</c> e do preenchimento, o <c>Convert.FromBase64String</c> lança e o usuário fica sem
    /// sessão "sem motivo".
    /// </summary>
    private static JsonElement? Decode(string jwt)
    {
        string[] parts = jwt.Split('.');

        if (parts.Length is not 3)
        {
            return null;
        }

        string payload = parts[1].Replace('-', '+').Replace('_', '/');

        payload = (payload.Length % 4) switch
        {
            2 => payload + "==",
            3 => payload + "=",
            0 => payload,
            _ => string.Empty
        };

        if (payload.Length is 0)
        {
            return null;
        }

        try
        {
            byte[] bytes = Convert.FromBase64String(payload);

            using JsonDocument document = JsonDocument.Parse(Encoding.UTF8.GetString(bytes));

            return document.RootElement.ValueKind is JsonValueKind.Object
                ? document.RootElement.Clone()
                : null;
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            return null;
        }
    }
}
