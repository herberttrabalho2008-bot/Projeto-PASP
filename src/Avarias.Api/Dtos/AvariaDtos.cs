using System.ComponentModel.DataAnnotations;

namespace Avarias.Api.Dtos;

// Avarias
public class CriarAvariaForm
{
    [Required(ErrorMessage = "Descreva a avaria."),
     StringLength(2000, MinimumLength = 10, ErrorMessage = "A descrição deve ter entre 10 e 2000 caracteres.")]
    public string Descricao { get; set; } = "";

    [Required(ErrorMessage = "Escolha o equipamento.")]
    public Guid? EquipamentoId { get; set; }

    public IFormFile? Foto { get; set; }
}

/// <summary>Só técnico. Campos vazios não mudam nada.</summary>
public class AtualizarAvariaForm
{
    public Guid? EstadoId { get; set; }

    [StringLength(2000, ErrorMessage = "A resolução não pode ter mais de 2000 caracteres.")]
    public string? Resolucao { get; set; }

    public IFormFile? FotoResolucao { get; set; }
}

public record AvariaResumo(
    Guid Id, string Descricao, string? Sala, string? Equipamento, string? NrSerie, string? TipoEquipamento,
    EstadoDto? Estado, string? Autor, DateTime? DataAvaria, bool TemFoto);

public record HistoricoDto(Guid Id, string? Detalhes, string? Estado, string EstadoChave, string? Utilizador, DateTime? Data);

public record AvariaDetalhe(
    Guid Id, string Descricao, string? Sala, string? TipoSala, Guid? EquipamentoId, string? Equipamento, string? NrSerie,
    string? TipoEquipamento, EstadoDto? Estado, string? Autor, DateTime? DataAvaria, bool TemFoto,
    DateTime? DataResolucao, string? Resolucao, bool TemFotoResolucao, List<HistoricoDto> Historico);

public record ContagemDto(EstadoDto Estado, int Total);
