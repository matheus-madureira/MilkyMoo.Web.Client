using System.Security.Claims;

namespace MilkyMoo.Services;

/// <summary>
/// Como o usuário autenticado é apresentado no header, a partir das claims do token.
/// </summary>
public static class UserDisplay
{
    /// <summary>Nome exibido: nome completo, caindo para o usuário e, por fim, para um genérico.</summary>
    public static string Name(ClaimsPrincipal user)
    {
        string? given = user.FindFirst(ClaimTypes.GivenName)?.Value;
        string? surname = user.FindFirst(ClaimTypes.Surname)?.Value;

        string full = string.Join(' ', new[] { given, surname }.Where(part => !string.IsNullOrWhiteSpace(part)));

        if (!string.IsNullOrWhiteSpace(full))
        {
            return full;
        }

        return user.FindFirst("name")?.Value
            ?? user.Identity?.Name
            ?? "Usuário";
    }

    /// <summary>
    /// Linha secundária do header.
    /// </summary>
    /// <remarks>
    /// Onde antes havia um cargo fixo ("Gestor") vai o e-mail: o realm ainda não tem papel de negócio, só os
    /// técnicos do Keycloak (<c>offline_access</c>, <c>default-roles-conttei</c>), que não dizem nada a quem
    /// está olhando a tela.
    /// </remarks>
    public static string Subtitle(ClaimsPrincipal user) =>
        user.FindFirst(ClaimTypes.Email)?.Value
        ?? user.Identity?.Name
        ?? string.Empty;
}
