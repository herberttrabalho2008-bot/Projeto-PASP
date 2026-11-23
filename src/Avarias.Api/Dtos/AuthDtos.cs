using System.ComponentModel.DataAnnotations;
using Avarias.Api.Models;

namespace Avarias.Api.Dtos;

// Autenticação (Supabase Auth através da API)
public class LoginRequest
{
    [Required(ErrorMessage = "Indique o email."), EmailAddress(ErrorMessage = "O email não é válido.")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Indique a palavra-passe.")]
    public string Password { get; set; } = "";
}

public class RefreshRequest
{
    [Required(ErrorMessage = "A sessão terminou. Inicie sessão novamente.")]
    public string RefreshToken { get; set; } = "";
}

public record UserInfo(Guid Id, string Nome, string Email, Perfil Perfil);

/// <summary>Token = access token do Supabase (expira em ~1 h); RefreshToken renova-o sem pedir a palavra-passe.</summary>
public record LoginResponse(string Token, string RefreshToken, DateTime ExpiraEm, UserInfo User);
public record SessaoResponse(string Token, string RefreshToken, DateTime ExpiraEm);

public class ForgotPasswordRequest
{
    [Required(ErrorMessage = "Indique o email."), EmailAddress(ErrorMessage = "O email não é válido.")]
    public string Email { get; set; } = "";
}

public class ResetPasswordRequest
{
    /// <summary>Access token que o Supabase põe no link de recuperação (#access_token=...).</summary>
    [Required(ErrorMessage = "O link de recuperação é inválido.")]
    public string AccessToken { get; set; } = "";

    [Required(ErrorMessage = "Indique a nova palavra-passe."),
     MinLength(8, ErrorMessage = "A palavra-passe deve ter pelo menos 8 caracteres.")]
    public string NovaPassword { get; set; } = "";
}
