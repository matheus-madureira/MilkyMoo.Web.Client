namespace MilkyMoo.Services;

/// <summary>
/// Regras que as telas de entrada e cadastro aplicam antes de gastar uma ida à API.
/// </summary>
public static class AuthValidation
{
    /// <summary>Mínimo de caracteres exigido na senha.</summary>
    /// <remarks>
    /// É uma regra <b>nossa</b>: o realm aceita qualquer senha, inclusive de um caractere. Sem isto, nada
    /// impediria uma conta com senha trivial.
    /// </remarks>
    public const int MinimumPasswordLength = 8;

    /// <summary>
    /// Confere o formato do e-mail. A API exige que o nome de usuário seja um endereço válido e responde
    /// com erro quando não é; validar aqui evita a viagem.
    /// </summary>
    public static bool IsEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string email = value.Trim();

        int at = email.IndexOf('@');

        if (at <= 0 || at != email.LastIndexOf('@') || at == email.Length - 1)
        {
            return false;
        }

        string domain = email[(at + 1)..];

        return !email.Contains(' ')
            && domain.Contains('.')
            && !domain.StartsWith('.')
            && !domain.EndsWith('.')
            && !domain.Contains("..", StringComparison.Ordinal);
    }

    /// <summary>
    /// Aceita o destino pós-login apenas quando ele é interno.
    /// </summary>
    /// <remarks>
    /// Uma URL absoluta vinda da query string seria um redirecionamento aberto: bastaria mandar um link
    /// <c>/entrar?returnUrl=https://…</c> para jogar quem acabou de logar em outro site. Só caminho
    /// relativo passa, e <c>//host</c> é recusado porque o browser o trata como absoluto.
    /// </remarks>
    public static string? SafeReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return null;
        }

        string target = returnUrl.Trim();

        return target.StartsWith('/') && !target.StartsWith("//", StringComparison.Ordinal)
            ? target
            : null;
    }
}
