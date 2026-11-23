using System.Net;
using System.Net.Mail;

namespace Avarias.Api.Services;

/// <summary>Envia emails por SMTP (secção "Smtp" da configuração; a password vai em user-secrets).</summary>
public class SmtpEmailSender : IEmailSender
{
    private readonly IConfigurationSection _smtp;
    private readonly ILogger<SmtpEmailSender> _log;

    public SmtpEmailSender(IConfiguration cfg, ILogger<SmtpEmailSender> log)
    {
        _smtp = cfg.GetSection("Smtp");
        _log = log;
    }

    public async Task EnviarAsync(string para, string assunto, string corpo)
    {
        using var cliente = new SmtpClient(_smtp["Host"], _smtp.GetValue("Port", 587))
        {
            EnableSsl = _smtp.GetValue("Ssl", true),
            Credentials = new NetworkCredential(_smtp["Utilizador"], _smtp["Password"]),
        };
        using var msg = new MailMessage(_smtp["De"]!, para, assunto, corpo);
        try
        {
            await cliente.SendMailAsync(msg);
        }
        catch (SmtpException ex)
        {
            // Não propaga: um erro 500 só para emails existentes revelaria que a conta existe.
            _log.LogError(ex, "Falha ao enviar email para {Para}", para);
        }
    }
}
