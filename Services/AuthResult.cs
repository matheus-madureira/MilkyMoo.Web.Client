namespace MilkyMoo.Services;

/// <summary>
/// Resultado de uma operação de autenticação. A camada de serviço nunca lança para a UI: erro de rede,
/// credencial recusada e falha da API chegam aqui como <see cref="Error"/> já em pt-BR.
/// </summary>
public sealed record AuthResult(bool Succeeded, string? Error)
{
    public static AuthResult Success() => new(true, null);

    public static AuthResult Failure(string error) => new(false, error);
}
