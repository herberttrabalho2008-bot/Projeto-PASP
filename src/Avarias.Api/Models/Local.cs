namespace Avarias.Api.Models;

public class Local
{
    public int Id { get; set; }
    public string Nome { get; set; } = "";
    /// <summary>Marca o espaço da biblioteca (usado quando Reportes:BibliotecaVeTudo = false).</summary>
    public bool EBiblioteca { get; set; }
    public List<Equipamento> Equipamentos { get; set; } = new();
}
