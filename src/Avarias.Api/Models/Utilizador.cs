namespace Avarias.Api.Models;

/// <summary>Tabela utilizadores. O Id é o mesmo da conta em auth.users (Supabase Auth).</summary>
public class Utilizador
{
    public Guid Id { get; set; }
    public string NomeUtilizador { get; set; } = "";
    public Guid? PermissaoId { get; set; }
    public TipoUtilizador? Permissao { get; set; }
    public DateTime? CreatedAt { get; set; }
}
