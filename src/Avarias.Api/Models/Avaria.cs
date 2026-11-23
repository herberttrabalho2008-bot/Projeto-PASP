namespace Avarias.Api.Models;

// Mapeamento para as tabelas do Supabase em Data/AppDbContext.cs (ver database/esquema-supabase.csv).

/// <summary>Tabela avarias. A sala obtém-se através do equipamento.</summary>
public class Avaria
{
    public Guid Id { get; set; }
    public string DescGeral { get; set; } = "";
    public Guid? IdUtilizador { get; set; }
    public Utilizador? Utilizador { get; set; }
    public Guid? IdEquipamento { get; set; }
    public Equipamento? Equipamento { get; set; }
    public DateTime? DataAvaria { get; set; }
    /// <summary>Nome do ficheiro na pasta de uploads (nunca um caminho).</summary>
    public string? FotoAvaria { get; set; }
    public Guid? EstadoId { get; set; }
    public EstadoAvaria? Estado { get; set; }
    public DateTime? DataResolucao { get; set; }
    public string? DescResolucao { get; set; }
    /// <summary>Nome do ficheiro na pasta de uploads (nunca um caminho).</summary>
    public string? FotoResolucao { get; set; }
    public DateTime? CreatedAt { get; set; }

    public List<HistoricoAvaria> Historico { get; set; } = new();
}
