namespace MilkyMoo.Services;

/// <summary>
/// Cliente da API de autenticação, já com tenant, <c>Bearer</c> e renovação de token.
/// </summary>
/// <remarks>
/// É um tipo próprio, e não um <see cref="HttpClient"/> registrado no contêiner, de propósito: as telas de
/// etiqueta injetam <see cref="HttpClient"/> para ler <c>wwwroot/sample-data/*.json</c>. Registrar este
/// cliente como <see cref="HttpClient"/> apontaria essas telas para a API e quebraria a listagem.
/// </remarks>
public sealed class ApiHttpClient(HttpClient inner)
{
    public HttpClient Client => inner;
}
