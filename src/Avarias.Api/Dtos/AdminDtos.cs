using System.ComponentModel.DataAnnotations;
using Avarias.Api.Models;

namespace Avarias.Api.Dtos;

// Backoffice (só técnico)
public record UtilizadorAdminDto(int Id, string Nome, string Email, Perfil Perfil, bool Ativo, DateTime CriadoEm);

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

public class NomeRequest
{
    [Required(ErrorMessage = "Indique o nome."),
     StringLength(100, MinimumLength = 2, ErrorMessage = "O nome deve ter entre 2 e 100 caracteres.")]
    public string Nome { get; set; } = "";
}

public class LocalRequest : NomeRequest
{
    public bool EBiblioteca { get; set; }
}
