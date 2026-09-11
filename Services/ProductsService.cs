using System.Net.Http.Json;
using System.Text.Json;

namespace MilkyMoo.Services;

/// <summary>
/// Leitura do catálogo de produtos. Nunca lança — toda falha volta como texto em pt-BR pronto para a tela.
/// </summary>
public sealed class ProductsService(ProductsApiClient api)
{
    /// <summary>Par id/nome, como a API devolve a categoria e o usuário.</summary>
    public sealed record ProductReference(Guid Id, string Name);

    /// <summary>Produto cadastrado, como devolvido por <c>GET /api/v1/products</c>.</summary>
    /// <remarks>
    /// <para>
    /// Os campos de enum são <see cref="string"/>, e não enums do C#: a amostra mostra só parte dos valores
    /// possíveis, e um valor novo num enum tipado lançaria <see cref="JsonException"/> — derrubando a lista
    /// inteira por causa de um item. A tradução trata o desconhecido em <see cref="ProductLabels"/>.
    /// </para>
    /// <para>
    /// O record mora aqui, e não no <c>ProductCard</c>: é este serviço que o desserializa, e um tipo de
    /// componente dentro de <c>Services</c> seria a única dependência de <c>Services</c> para
    /// <c>Components</c> no projeto — a seta aponta sempre no outro sentido.
    /// </para>
    /// </remarks>
    public sealed record Product(
        Guid Id,
        string Code,
        string Name,
        ProductReference Category,
        string? BrandOrSupplier,
        string? ProductState,
        string? Conditioning,
        string? StorageLocation,
        string? CountingStart,
        int? ShelfLifeDays,
        string? ShelfLifeType,
        string? Notes,
        string? Origin,
        bool IsActive,
        DateTimeOffset? CreatedAt,
        DateTimeOffset? UpdatedAt)
    {
        /// <summary>Quem cadastrou. Pode faltar em item legado.</summary>
        public ProductReference? User { get; init; }

        /// <summary>Texto de validade pronto para a tela, já resolvendo o par tipo/dias.</summary>
        /// <remarks>
        /// <c>ShelfLifeDays</c> só vem preenchido quando <c>ShelfLifeType</c> é <c>FixedDays</c>; nos demais
        /// casos é nulo, e concatenar direto produziria " dias".
        /// </remarks>
        public string ShelfLifeText => (ShelfLifeType, ShelfLifeDays) switch
        {
            ("FixedDays", int days) => days == 1 ? "1 dia" : $"{days} dias",
            ("ManufacturerLabel", _) => "Conforme rótulo",
            ("Pending", _) => "A definir",
            (null, int days) => days == 1 ? "1 dia" : $"{days} dias",
            _ => ProductLabels.Unknown
        };

        /// <summary>Indica que falta preencher validade, acondicionamento e local de armazenamento.</summary>
        public bool IsPending => ShelfLifeType == "Pending";

        /// <summary>Casa o termo buscado com o nome ou o código.</summary>
        /// <remarks>
        /// <see cref="StringComparison.OrdinalIgnoreCase"/> ignora a caixa, mas <b>não</b> o acento: buscar
        /// "maracuja" não acha "maracujá". Normalizar exigiria varrer diacríticos, o que traz dependência de
        /// globalização para um app que roda com trimming — e, com a lista inteira à vista, não compensa.
        /// </remarks>
        public bool Matches(string term) =>
            Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            Code.Contains(term, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Resultado da busca. <c>Error</c> nulo significa sucesso.</summary>
    public sealed record ProductsResult(IReadOnlyList<Product> Products, string? Error)
    {
        public static ProductsResult Success(IReadOnlyList<Product> products) => new(products, null);

        public static ProductsResult Failure(string error) => new([], error);
    }

    /// <summary>
    /// Todos os produtos do tenant.
    /// </summary>
    /// <remarks>
    /// A resposta é um array puro, sem envelope nem paginação — 69 itens na amostra. Não há cache: sair e
    /// voltar à tela busca de novo, o que evita o problema de invalidar antes de ele existir.
    /// </remarks>
    public async Task<ProductsResult> GetAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using HttpResponseMessage response = await api.Client.GetAsync("api/v1/products", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return ProductsResult.Failure(await ApiErrorReader.ReadAsync(response));
            }

            Product[]? products = await response.Content.ReadFromJsonAsync<Product[]>(cancellationToken);

            return ProductsResult.Success(products ?? []);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            return ProductsResult.Failure(ApiErrorReader.NetworkError);
        }
    }
}
