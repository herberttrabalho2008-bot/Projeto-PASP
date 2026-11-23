namespace Avarias.Api.Models;

public class Comentario
{
    public int Id { get; set; }
    public int ReporteId { get; set; }
    public int AutorId { get; set; }
    public Utilizador Autor { get; set; } = null!;
    public string Texto { get; set; } = "";
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
}
