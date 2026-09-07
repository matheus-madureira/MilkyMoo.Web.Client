using System.Net;
using System.Text.Json;

namespace MilkyMoo.Services;

/// <summary>
/// Converte a resposta de erro da API numa mensagem em pt-BR pronta para a tela.
/// </summary>
/// <remarks>
/// A API não tem um contrato único de erro — são quatro formatos, todos em inglês:
/// <list type="bullet">
///   <item><c>{"title":"...","errors":{"Username":["..."]}}</c> — validação de request;</item>
///   <item><c>{"code":"User.UserCreationError","description":"..."}</c> — erro de domínio;</item>
///   <item><c>{"message":""}</c> — não autenticado;</item>
///   <item><c>{"title":"...","detail":"...","traceId":"..."}</c> — falha interna.</item>
/// </list>
/// O texto bruto nunca vai para a tela: além de estar em inglês, o <c>description</c> vaza detalhe interno
/// (por exemplo <c>{"error":"Realm not found."}</c>). O que sai daqui é sempre texto nosso.
/// </remarks>
public static class ApiErrorReader
{
    private const string Generic = "Não foi possível processar sua solicitação.";
    private const string InvalidData = "Verifique os dados informados e tente novamente.";
    private const string Unauthorized = "Sua sessão expirou. Entre novamente.";
    private const string Offline = "Não foi possível falar com o servidor. Verifique sua conexão.";

    /// <summary>Mensagem para uma falha de rede, antes de existir resposta.</summary>
    public static string NetworkError => Offline;

    /// <summary>Lê o corpo do erro e devolve a mensagem correspondente.</summary>
    public static async Task<string> ReadAsync(HttpResponseMessage response)
    {
        // 5xx não tem tradução útil: o corpo só traz "An unexpected error occurred" e um traceId.
        if ((int)response.StatusCode >= 500)
        {
            return Generic;
        }

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return Unauthorized;
        }

        string? fromBody = await TryReadBodyAsync(response);

        return fromBody ?? FallbackFor(response.StatusCode);
    }

    private static async Task<string?> TryReadBodyAsync(HttpResponseMessage response)
    {
        try
        {
            string body = await response.Content.ReadAsStringAsync();

            if (string.IsNullOrWhiteSpace(body))
            {
                return null;
            }

            using JsonDocument document = JsonDocument.Parse(body);

            if (document.RootElement.ValueKind is not JsonValueKind.Object)
            {
                return null;
            }

            return FromValidationErrors(document.RootElement)
                ?? FromDomainError(document.RootElement);
        }
        catch (Exception exception) when (exception is JsonException or HttpRequestException)
        {
            // Corpo vazio ou que não é JSON: o status ainda descreve o problema.
            return null;
        }
    }

    /// <summary>Formato de validação: traduz o que é conhecido e ignora o resto.</summary>
    private static string? FromValidationErrors(JsonElement root)
    {
        if (!root.TryGetProperty("errors", out JsonElement errors) ||
            errors.ValueKind is not JsonValueKind.Object)
        {
            return null;
        }

        foreach (JsonProperty field in errors.EnumerateObject())
        {
            if (field.Value.ValueKind is not JsonValueKind.Array)
            {
                continue;
            }

            foreach (JsonElement message in field.Value.EnumerateArray())
            {
                if (Translate(message.GetString()) is string translated)
                {
                    return translated;
                }
            }
        }

        return InvalidData;
    }

    /// <summary>Formato de domínio: <c>description</c> traz o motivo real embutido em inglês.</summary>
    private static string? FromDomainError(JsonElement root)
    {
        if (!root.TryGetProperty("description", out JsonElement description) ||
            description.GetString() is not string text)
        {
            return null;
        }

        return Translate(text) ?? InvalidData;
    }

    /// <summary>Mapeia os textos da API que já foram vistos em produção. Nulo quando não é conhecido.</summary>
    private static string? Translate(string? message) => message switch
    {
        null => null,
        _ when message.Contains("valid email address", StringComparison.OrdinalIgnoreCase) =>
            "Informe um e-mail válido.",
        _ when message.Contains("Realm not found", StringComparison.OrdinalIgnoreCase) =>
            "Configuração de tenant inválida. Avise o suporte.",
        _ when message.Contains("already exists", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("User exists", StringComparison.OrdinalIgnoreCase) =>
            "Já existe uma conta com este e-mail.",
        _ => null
    };

    private static string FallbackFor(HttpStatusCode status) => status switch
    {
        HttpStatusCode.BadRequest => InvalidData,
        HttpStatusCode.Conflict => "Já existe uma conta com este e-mail.",
        HttpStatusCode.NotFound => Generic,
        _ => Generic
    };
}
