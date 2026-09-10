using Microsoft.AspNetCore.Authorization;

namespace MilkyMoo.Services;

/// <summary>
/// Quem pode administrar o painel. Os papéis vêm de <c>Auth:AdminRoles</c>, lista separada por vírgula.
/// </summary>
/// <remarks>
/// <para>
/// Hoje o valor é <c>Feijuca.ApiWriter</c>, papel que vem em
/// <c>resource_access.feijuca-auth-api</c> e que o usuário comum não tem — com ele
/// <c>GET /api/v1/users</c> responde 200, sem ele 403. É configurável, e não uma constante, porque esse
/// papel é da API de auth, não do produto: quando o realm ganhar um papel de negócio próprio, troca-se o
/// valor sem tocar em código. O provedor de estado já achata <c>realm_access</c> e <c>resource_access</c>
/// em <c>ClaimTypes.Role</c>, então qualquer um dos dois serve.
/// </para>
/// <para>
/// <b>Lista vazia libera todo autenticado.</b> É a válvula de escape para o caso de o papel sumir ou mudar
/// de nome no realm: esvaziar a chave devolve o acesso a todo mundo em vez de trancar a função para
/// todos, inclusive para quem administra.
/// </para>
/// </remarks>
public static class AdminPolicy
{
    /// <summary>Nome da política. É <c>const</c> porque vai dentro de <c>[Authorize(Policy = …)]</c>.</summary>
    public const string Name = "Admin";

    /// <summary>Monta a política a partir do valor configurado. Nulo ou vazio libera todo autenticado.</summary>
    public static Action<AuthorizationPolicyBuilder> Build(string? configuredRoles) => policy =>
    {
        policy.RequireAuthenticatedUser();

        string[] roles = (configuredRoles ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (roles.Length > 0)
        {
            // RequireRole com vários nomes é "qualquer um deles", não "todos".
            policy.RequireRole(roles);
        }
    };
}
