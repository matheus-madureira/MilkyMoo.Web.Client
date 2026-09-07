using System.Text.Json;
using Microsoft.JSInterop;

namespace MilkyMoo.Services;

/// <summary>
/// Guarda o par de tokens no <c>localStorage</c> do browser, para a sessão sobreviver ao reload.
/// </summary>
/// <remarks>
/// Todo acesso vai em <c>try/catch</c>: em janela anônima, ou com armazenamento bloqueado por política do
/// browser, o próprio <c>localStorage</c> lança. Falhando, a sessão vira só-memória — o app continua
/// funcionando, apenas não sobrevive a um reload.
/// </remarks>
public sealed class TokenStore(IJSRuntime js)
{
    private const string Key = "milkymoo.auth";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<AuthTokens?> ReadAsync()
    {
        try
        {
            string? raw = await js.InvokeAsync<string?>("localStorage.getItem", Key);

            return string.IsNullOrWhiteSpace(raw)
                ? null
                : JsonSerializer.Deserialize<AuthTokens>(raw, Json);
        }
        catch (Exception exception) when (exception is JSException or JsonException or InvalidOperationException)
        {
            return null;
        }
    }

    public async Task WriteAsync(AuthTokens tokens)
    {
        try
        {
            await js.InvokeVoidAsync("localStorage.setItem", Key, JsonSerializer.Serialize(tokens, Json));
        }
        catch (JSException)
        {
            // Sessão só-memória: ver observação da classe.
        }
    }

    public async Task ClearAsync()
    {
        try
        {
            await js.InvokeVoidAsync("localStorage.removeItem", Key);
        }
        catch (JSException)
        {
            // Nada a limpar se o armazenamento nunca esteve disponível.
        }
    }
}
