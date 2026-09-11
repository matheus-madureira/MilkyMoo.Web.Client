namespace MilkyMoo.Services;

/// <summary>
/// Cliente da API de negócio, já com tenant, <c>Bearer</c> e renovação de token.
/// </summary>
/// <remarks>
/// É um tipo próprio, e não um <see cref="HttpClient"/> registrado no contêiner, pelo mesmo motivo anotado no
/// <see cref="ApiHttpClient"/>: as telas de etiqueta injetam <see cref="HttpClient"/> para ler
/// <c>wwwroot/sample-data/*.json</c>, e registrar este cliente as apontaria para a API.
/// </remarks>
public sealed class ProductsApiClient(HttpClient inner)
{
    public HttpClient Client => inner;
}
