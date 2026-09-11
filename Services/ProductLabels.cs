namespace MilkyMoo.Services;

/// <summary>
/// Traduz para pt-BR os valores de enum que a API de produtos devolve em inglês.
/// </summary>
/// <remarks>
/// <para>
/// O caso <c>_</c> devolve o <b>valor cru</b>, e não uma exceção nem um texto genérico: as tabelas abaixo
/// saíram de uma amostra de 69 produtos, que não é o contrato. Um valor novo aparece feio na tela — e
/// aparecer feio é muito melhor do que sumir ou derrubar a lista.
/// </para>
/// <para>
/// Os textos de armazenagem seguem o vocabulário da operação, não a tradução literal: "pote da estufa fria" é
/// como se fala na unidade, enquanto "recipiente de exposição refrigerada" não diria nada a ninguém.
/// </para>
/// </remarks>
public static class ProductLabels
{
    /// <summary>Texto exibido quando o campo vem nulo ou vazio.</summary>
    public const string Unknown = "—";

    public static string State(string? value) => value switch
    {
        "OpenedOrHandled" => "Aberto ou manipulado",
        "SealedInOriginalPackaging" => "Lacrado na embalagem original",
        null or "" => Unknown,
        _ => value
    };

    public static string Conditioning(string? value) => value switch
    {
        "Refrigerated" => "Refrigerado",
        "Frozen" => "Congelado",
        "RoomTemperature" => "Temperatura ambiente",
        null or "" => Unknown,
        _ => value
    };

    public static string Storage(string? value) => value switch
    {
        "ColdDisplayContainer" => "Pote da estufa fria",
        "LargeColdDisplayContainer" => "Pote grande da estufa fria",
        "SmallColdDisplayContainer" => "Pote pequeno da estufa fria",
        "Refrigerator" => "Geladeira",
        "Freezer" => "Freezer",
        "OriginalPackaging" => "Embalagem original",
        "SuitableContainer" => "Recipiente adequado",
        "SuitableContainerForHandledProduct" => "Recipiente adequado para produto manipulado",
        "IceCreamMachine" => "Máquina de sorvete",
        null or "" => Unknown,
        _ => value
    };

    public static string CountingStart(string? value) => value switch
    {
        "PreparationOrHandlingDate" => "Data de preparo ou manipulação",
        "ReceiptOrStorageDate" => "Data de recebimento ou armazenagem",
        "PackageOpeningDate" => "Data de abertura da embalagem",
        "OpeningDateWithManufacturerLabel" => "Abertura, respeitando o rótulo do fabricante",
        null or "" => Unknown,
        _ => value
    };

    /// <summary>Tipo de validade, sem os dias. O texto completo é <c>Product.ShelfLifeText</c>.</summary>
    public static string ShelfLifeType(string? value) => value switch
    {
        "FixedDays" => "Prazo fixo",
        "ManufacturerLabel" => "Conforme rótulo",
        "Pending" => "A definir",
        null or "" => Unknown,
        _ => value
    };

    public static string Origin(string? value) => value switch
    {
        "System" => "Padrão do sistema",
        // Não visto na amostra — os 69 produtos são "System". É o par natural, e o caso "_" cobre o erro.
        "User" => "Cadastrado pela unidade",
        null or "" => Unknown,
        _ => value
    };
}
