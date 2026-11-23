namespace Avarias.Api.Models;

public class HistoricoReporte
{
    public int Id { get; set; }
    public int ReporteId { get; set; }
    public int UtilizadorId { get; set; }
    public Utilizador Utilizador { get; set; } = null!;
    public string Campo { get; set; } = "";
    public string? ValorAnterior { get; set; }
    public string? ValorNovo { get; set; }
    public DateTime Data { get; set; } = DateTime.UtcNow;
}
