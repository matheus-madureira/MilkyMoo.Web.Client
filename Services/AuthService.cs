using System.Net.Http.Json;
using System.Text.Json;

namespace MilkyMoo.Services;

/// <summary>
/// Fachada de autenticação usada pelas telas: entrar, criar usuário, sair e restaurar a sessão no boot.
/// Nunca lança — toda falha volta como <see cref="AuthResult"/> com mensagem em pt-BR.
/// </summary>
public sealed class AuthService(AuthSession session, ApiHttpClient api)
{
    /// <summary>Autentica com e-mail e senha.</summary>
    public Task<AuthResult> LoginAsync(string email, string password) =>
        session.SignInAsync(email.Trim(), password);

    /// <summary>
    /// Cria um usuário no realm.
    /// </summary>
    /// <remarks>
    /// <b>Não</b> entra com ele: quem chama é um admin dentro do painel, e trocar a sessão pela do usuário
    /// recém-criado o expulsaria da própria tela. O usuário nasce habilitado e com e-mail confirmado, então
    /// ele consegue entrar sozinho logo em seguida.
    /// </remarks>
    public async Task<AuthResult> CreateUserAsync(
        string firstName,
        string lastName,
        string email,
        string password)
    {
        string username = email.Trim();

        try
        {
            using HttpResponseMessage response = await api.Client.PostAsJsonAsync(
                "api/v1/users",
                new
                {
                    username,
                    password,
                    email = username,
                    firstName = firstName.Trim(),
                    lastName = lastName.Trim()
                });

            return response.IsSuccessStatusCode
                ? AuthResult.Success()
                : AuthResult.Failure(await ApiErrorReader.ReadAsync(response));
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            return AuthResult.Failure(ApiErrorReader.NetworkError);
        }
    }

    /// <summary>
    /// Encerra a sessão no servidor e localmente.
    /// </summary>
    /// <remarks>
    /// A chamada à API é a que invalida a sessão no Keycloak, mas falhar nela não pode prender ninguém
    /// dentro do app: a sessão local é descartada de qualquer forma.
    /// </remarks>
    public async Task LogoutAsync()
    {
        // A renovação vem primeiro, e só depois o refresh token é lido: a chamada de logout passa pelo
        // AuthTokenHandler, que renovaria o par no meio do caminho — e o Keycloak rotaciona o refresh token,
        // então o valor lido antes já estaria invalidado quando chegasse ao servidor.
        await session.GetAccessTokenAsync();

        if (session.Tokens?.RefreshToken is string refreshToken)
        {
            try
            {
                using HttpResponseMessage response = await api.Client.PostAsJsonAsync(
                    "api/v1/users/logout",
                    new { refreshToken });
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
            {
                // Sem rede o token continua válido no servidor até expirar; nada a fazer daqui.
            }
        }

        await session.SignOutLocalAsync();
    }

    /// <summary>Recupera a sessão salva no armazenamento local. Chamado uma vez, no start do app.</summary>
    public Task RestoreSessionAsync() => session.RestoreAsync();
}
