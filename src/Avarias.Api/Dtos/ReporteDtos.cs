using System.ComponentModel.DataAnnotations;
using Avarias.Api.Models;

namespace Avarias.Api.Dtos;

// Reportes
public class CriarReporteForm
{
    [Required(ErrorMessage = "Indique o título."),
     StringLength(120, MinimumLength = 3, ErrorMessage = "O título deve ter entre 3 e 120 caracteres.")]
    public string Titulo { get; set; } = "";

    [Required(ErrorMessage = "Indique a descrição."),
     StringLength(2000, MinimumLength = 10, ErrorMessage = "A descrição deve ter entre 10 e 2000 caracteres.")]
    public string Descricao { get; set; } = "";

    [Range(1, int.MaxValue, ErrorMessage = "Escolha o local.")]
    public int LocalId { get; set; }

    public int? EquipamentoId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Escolha o tipo de avaria.")]
    public int TipoId { get; set; }

    public Urgencia Urgencia { get; set; } = Urgencia.Media;

    public IFormFile? Foto { get; set; }
}

public class AtualizarReporteRequest
{
    public Estado? Estado { get; set; }
    public Prioridade? Prioridade { get; set; }

    [StringLength(2000, ErrorMessage = "A resolução não pode ter mais de 2000 caracteres.")]
    public string? Resolucao { get; set; }
}

public class NovoComentarioRequest
{
    [Required(ErrorMessage = "Escreva o comentário."),
     StringLength(1000, ErrorMessage = "O comentário não pode ter mais de 1000 caracteres.")]
    public string Texto { get; set; } = "";
}

public record ReporteResumo(
    int Id, string Titulo, string Local, string? Equipamento, string Tipo,
    Urgencia Urgencia, Prioridade? Prioridade, Estado Estado,
    string Autor, DateTime CriadoEm, bool TemFoto);

public record HistoricoDto(int Id, string Campo, string? ValorAnterior, string? ValorNovo, string Utilizador, DateTime Data);
public record ComentarioDto(int Id, string Autor, string Texto, DateTime CriadoEm);

public record ReporteDetalhe(
    int Id, string Titulo, string Descricao, string Local, string? Equipamento, string Tipo,
    Urgencia Urgencia, Prioridade? Prioridade, Estado Estado, string Autor,
    DateTime CriadoEm, DateTime AtualizadoEm, DateTime? ResolvidoEm, string? Resolucao, bool TemFoto,
    List<HistoricoDto> Historico, List<ComentarioDto> Comentarios);
