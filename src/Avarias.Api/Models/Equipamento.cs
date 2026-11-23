namespace Avarias.Api.Models;

/// <summary>Tabela equipamentos. Cada equipamento está numa sala.</summary>
public class Equipamento
{
    public Guid Id { get; set; }
    public string? NrSerie { get; set; }
    public string? NrSala { get; set; }
    public Sala? Sala { get; set; }
    public Guid? IdTipo { get; set; }
    public TipoEquipamento? Tipo { get; set; }
    public string DescEquipamento { get; set; } = "";
    public DateTime? CreatedAt { get; set; }
}
