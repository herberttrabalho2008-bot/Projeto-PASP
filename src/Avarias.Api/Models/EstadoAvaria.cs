namespace Avarias.Api.Models;

/// <summary>Tabela estados_avarias (Pendente, Em análise, Em resolução, Resolvido).</summary>
public class EstadoAvaria
{
    public Guid Id { get; set; }
    public string DescEstado { get; set; } = "";
}
