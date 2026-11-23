namespace Avarias.Api.Models;

/// <summary>
/// Só leitura: tabela auth.users do Supabase Auth (email e se a conta está bloqueada).
/// As contas criam-se e alteram-se pela API de administração do Supabase, nunca por aqui.
/// </summary>
public class ContaAuth
{
    public Guid Id { get; set; }
    public string? Email { get; set; }
    public DateTime? BannedUntil { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}
