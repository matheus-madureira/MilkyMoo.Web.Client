namespace MilkyMoo.Services;

/// <summary>
/// Endereço da API de autenticação e o realm (tenant) usado em todas as chamadas.
/// Vem de <c>wwwroot/appsettings.json</c>, seção <c>Api</c>.
/// </summary>
/// <param name="BaseUrl">Base absoluta da API, com barra final.</param>
/// <param name="Tenant">Nome do realm no Keycloak. É <b>case-sensitive</b>.</param>
public sealed record ApiOptions(string BaseUrl, string Tenant)
{
    /// <summary>Nome do header que carrega o tenant. A API aceita em qualquer caixa.</summary>
    public const string TenantHeader = "tenant";

    /// <summary>
    /// Base da API de negócio (produtos, estoque), com barra final. Vem de <c>Api:ProductsBaseUrl</c>.
    /// </summary>
    /// <remarks>
    /// Separada de <see cref="BaseUrl"/> porque hoje ela roda local enquanto a autenticação já está
    /// publicada. O token continua sendo emitido pelo host de <see cref="BaseUrl"/> — confirmado no ar que a
    /// API local o aceita. Quando as duas convergirem, basta repetir o valor.
    /// </remarks>
    public string ProductsBaseUrl { get; init; } = BaseUrl;
}
