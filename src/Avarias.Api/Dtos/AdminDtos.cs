using System.ComponentModel.DataAnnotations;
using Avarias.Api.Models;

namespace Avarias.Api.Dtos;

// Backoffice (só técnico)
public record UtilizadorAdminDto(Guid Id, string Nome, string? Email, Perfil? Perfil, bool Ativo, DateTime? CriadoEm);

public class GuardarUtilizadorRequest
{
    [Required(ErrorMessage = "Indique o nome."),
     StringLength(100, MinimumLength = 2, ErrorMessage = "O nome deve ter entre 2 e 100 caracteres.")]
    public string Nome { get; set; } = "";

    [Required(ErrorMessage = "Indique o email."), EmailAddress(ErrorMessage = "O email não é válido.")]
    public string Email { get; set; } = "";

    public Perfil Perfil { get; set; }
    public bool Ativo { get; set; } = true;

    /// <summary>Obrigatória ao criar; ignorada ao editar (há um endpoint próprio para a palavra-passe).</summary>
    [MinLength(8, ErrorMessage = "A palavra-passe deve ter pelo menos 8 caracteres.")]
    public string? Password { get; set; }
}

public class DefinirPasswordRequest
{
    [Required(ErrorMessage = "Indique a nova palavra-passe."),
     MinLength(8, ErrorMessage = "A palavra-passe deve ter pelo menos 8 caracteres.")]
    public string NovaPassword { get; set; } = "";
}

public class CriarSalaRequest
{
    [Required(ErrorMessage = "Indique o número ou nome da sala."),
     StringLength(50, MinimumLength = 1, ErrorMessage = "O número da sala pode ter até 50 caracteres.")]
    public string NrSala { get; set; } = "";

    [Required(ErrorMessage = "Indique o tipo de sala."),
     StringLength(100, MinimumLength = 2, ErrorMessage = "O tipo de sala deve ter entre 2 e 100 caracteres.")]
    public string TipoSala { get; set; } = "";
}

public class EditarSalaRequest
{
    [Required(ErrorMessage = "Indique o tipo de sala."),
     StringLength(100, MinimumLength = 2, ErrorMessage = "O tipo de sala deve ter entre 2 e 100 caracteres.")]
    public string TipoSala { get; set; } = "";
}

public class DescricaoRequest
{
    [Required(ErrorMessage = "Indique a descrição."),
     StringLength(100, MinimumLength = 2, ErrorMessage = "A descrição deve ter entre 2 e 100 caracteres.")]
    public string Descricao { get; set; } = "";
}

public class EquipamentoRequest
{
    [Required(ErrorMessage = "Escolha a sala.")]
    public string NrSala { get; set; } = "";

    public Guid? IdTipo { get; set; }

    [StringLength(100, ErrorMessage = "O número de série pode ter até 100 caracteres.")]
    public string? NrSerie { get; set; }

    [Required(ErrorMessage = "Indique a descrição do equipamento."),
     StringLength(200, MinimumLength = 2, ErrorMessage = "A descrição deve ter entre 2 e 200 caracteres.")]
    public string Descricao { get; set; } = "";
}
