namespace Avarias.Api.Models;

/// <summary>
/// Tabela historico_avarias: cópia do estado da avaria em cada momento (criação e cada alteração),
/// com os dados da altura (sala, equipamento, estado), para não mudarem se o catálogo mudar.
/// </summary>
public class HistoricoAvaria
{
    public Guid Id { get; set; }
    public Guid? AvariaId { get; set; }
    public string? DescGeral { get; set; }
    /// <summary>O que aconteceu neste registo (ex.: "Estado: Pendente → Em análise").</summary>
    public string? DescDetalhes { get; set; }
    public Guid? IdUtilizador { get; set; }
    public Utilizador? Utilizador { get; set; }
    public string? NrSerieEquipamento { get; set; }
    public string? DescTipoEquipamento { get; set; }
    public string? NrSala { get; set; }
    public DateTime? DataAvaria { get; set; }
    public string? FotoAvaria { get; set; }
    public DateTime? DataResolucao { get; set; }
    public string? DescResolucao { get; set; }
    public string? FotoResolucao { get; set; }
    public string? DescEstado { get; set; }
    public DateTime? DataRegisto { get; set; }
}
