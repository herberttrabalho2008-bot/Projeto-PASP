namespace Avarias.Api.Services;

/// <summary>Envio de emails (recuperação de palavra-passe). Escolhido em Program.cs conforme "Smtp:Host".</summary>
public interface IEmailSender
{
    Task EnviarAsync(string para, string assunto, string corpo);
}
