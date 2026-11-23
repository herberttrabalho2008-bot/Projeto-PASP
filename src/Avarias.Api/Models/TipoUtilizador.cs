namespace Avarias.Api.Models;

/// <summary>Tabela tipos_utilizador: os perfis (Professor, Biblioteca, Técnico).</summary>
public class TipoUtilizador
{
    public Guid Id { get; set; }
    public string Descricao { get; set; } = ""; // coluna tipo_utilizador
}
