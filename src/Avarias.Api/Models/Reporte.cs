namespace Avarias.Api.Models;

public class Reporte
{
    public int Id { get; set; }
    public string Titulo { get; set; } = "";
    public string Descricao { get; set; } = "";

    public int LocalId { get; set; }
    public Local Local { get; set; } = null!;
    public int? EquipamentoId { get; set; }
    public Equipamento? Equipamento { get; set; }
    public int TipoId { get; set; }
    public TipoAvaria Tipo { get; set; } = null!;

    /// <summary>Indicada por quem reporta.</summary>
    public Urgencia Urgencia { get; set; }
    /// <summary>Definida pelo técnico (nula até ser avaliada).</summary>
    public Prioridade? Prioridade { get; set; }
    public Estado Estado { get; set; } = Estado.Pendente;

    /// <summary>Nome do ficheiro da foto na pasta de uploads (nunca um caminho).</summary>
    public string? FotoFicheiro { get; set; }

    public int AutorId { get; set; }
    public Utilizador Autor { get; set; } = null!;

    public string? Resolucao { get; set; }
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    public DateTime AtualizadoEm { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvidoEm { get; set; }

    public List<HistoricoReporte> Historico { get; set; } = new();
    public List<Comentario> Comentarios { get; set; } = new();
}
