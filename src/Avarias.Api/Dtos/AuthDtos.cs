using System.ComponentModel.DataAnnotations;
using Avarias.Api.Models;

namespace Avarias.Api.Dtos;

// Autenticação
public class LoginRequest
{
    [Required(ErrorMessage = "Indique o email."), EmailAddress(ErrorMessage = "O email não é válido.")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Indique a palavra-passe.")]
    public string Password { get; set; } = "";
}

public record UserInfo(int Id, string Nome, string Email, Perfil Perfil);
public record LoginResponse(string Token, UserInfo User);

public class ForgotPasswordRequest
{
    [Required(ErrorMessage = "Indique o email."), EmailAddress(ErrorMessage = "O email não é válido.")]
    public string Email { get; set; } = "";
}

public class ResetPasswordRequest
{
    [Required(ErrorMessage = "O link de recuperação é inválido.")]
    public string Token { get; set; } = "";

    [Required(ErrorMessage = "Indique a nova palavra-passe."),
     MinLength(8, ErrorMessage = "A palavra-passe deve ter pelo menos 8 caracteres.")]
    public string NovaPassword { get; set; } = "";
}
