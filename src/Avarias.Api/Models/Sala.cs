namespace Avarias.Api.Models;

/// <summary>Tabela salas. A chave é o número da sala (texto).</summary>
public class Sala
{
    public string NrSala { get; set; } = "";
    public string TipoSala { get; set; } = "";
    public List<Equipamento> Equipamentos { get; set; } = new();
}
