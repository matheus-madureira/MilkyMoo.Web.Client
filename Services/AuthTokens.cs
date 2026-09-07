using System.Text.Json.Serialization;

namespace MilkyMoo.Services;

/// <summary>
/// Par de tokens devolvido por <c>/users/login</c> e <c>/users/refresh-token</c>.
/// </summary>
/// <remarks>
/// A API devolve <c>expiresIn</c> / <c>refreshExpiresIn</c> em segundos, mas o que é guardado aqui é o
/// <b>instante absoluto</b> de expiração: um "300 segundos" salvo no armazenamento local não diz nada depois
/// de um reload, porque não se sabe quando foi emitido. A conversão acontece em <see cref="From"/>, no
/// momento da resposta.
/// </remarks>
public sealed record AuthTokens(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessExpiresAt,
    DateTimeOffset RefreshExpiresAt)
{
    /// <summary>Margem descontada da expiração para absorver latência e relógio fora de hora.</summary>
    private static readonly TimeSpan Skew = TimeSpan.FromSeconds(30);

    /// <summary>O access token ainda serve para assinar uma chamada.</summary>
    [JsonIgnore]
    public bool IsAccessValid => DateTimeOffset.UtcNow < AccessExpiresAt - Skew;

    /// <summary>Ainda dá para renovar a sessão. Enquanto for verdade, o usuário continua logado.</summary>
    [JsonIgnore]
    public bool IsRefreshValid => DateTimeOffset.UtcNow < RefreshExpiresAt - Skew;

    /// <summary>Converte a resposta da API, com durações relativas, no par com instantes absolutos.</summary>
    public static AuthTokens? From(TokenResponse? response)
    {
        if (response is null ||
            string.IsNullOrWhiteSpace(response.AccessToken) ||
            string.IsNullOrWhiteSpace(response.RefreshToken))
        {
            return null;
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;

        return new AuthTokens(
            response.AccessToken,
            response.RefreshToken,
            now.AddSeconds(response.ExpiresIn),
            now.AddSeconds(response.RefreshExpiresIn));
    }

    /// <summary>Corpo cru de <c>/users/login</c> e <c>/users/refresh-token</c>.</summary>
    public sealed record TokenResponse(
        string? AccessToken,
        string? RefreshToken,
        int ExpiresIn,
        int RefreshExpiresIn,
        string? TokenType,
        string? Scope);
}
