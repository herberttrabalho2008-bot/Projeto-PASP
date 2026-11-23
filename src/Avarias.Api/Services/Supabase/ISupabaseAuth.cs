namespace Avarias.Api.Services.Supabase;

/// <summary>Sessão devolvida pelo Supabase Auth (access token JWT + refresh token).</summary>
public record SessaoSupabase(string AccessToken, string RefreshToken, DateTime ExpiraEm, Guid UserId, string Email);

/// <summary>Erro do Supabase Auth já traduzido para uma mensagem em português.</summary>
public class SupabaseAuthException : Exception
{
    public int Status { get; }
    public SupabaseAuthException(int status, string message) : base(message) => Status = status;
}

/// <summary>
/// Operações no Supabase Auth. A API fala com o Supabase em nome do browser,
/// por isso o frontend nunca vê as chaves do Supabase.
/// </summary>
public interface ISupabaseAuth
{
    // ---- com a chave pública (anon / publishable)
    Task<SessaoSupabase> EntrarAsync(string email, string password);
    Task<SessaoSupabase> RenovarAsync(string refreshToken);
    /// <summary>Envia o email de recuperação; o link abre redirectTo com o token no endereço.</summary>
    Task PedirRecuperacaoAsync(string email, string redirectTo);
    /// <summary>Muda a palavra-passe com o access token que vem no link de recuperação.</summary>
    Task AlterarPasswordAsync(string accessToken, string novaPassword);
    Task TerminarSessaoAsync(string accessToken);

    // ---- administração (chave secreta / service_role; só no servidor)
    Task<Guid> CriarContaAsync(string email, string password, string nome);
    Task AtualizarContaAsync(Guid id, string? email = null, string? password = null, bool? ativa = null);
    Task ApagarContaAsync(Guid id);
}
