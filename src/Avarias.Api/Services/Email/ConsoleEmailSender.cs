namespace Avarias.Api.Services;

/// <summary>
/// Implementação de desenvolvimento: escreve o email na consola da API.
/// É usada quando "Smtp:Host" não está configurado.
/// </summary>
public class ConsoleEmailSender : IEmailSender
{
    private readonly ILogger<ConsoleEmailSender> _log;
    public ConsoleEmailSender(ILogger<ConsoleEmailSender> log) => _log = log;

    public Task EnviarAsync(string para, string assunto, string corpo)
    {
        _log.LogWarning("EMAIL (simulado) para {Para} | {Assunto}\n{Corpo}", para, assunto, corpo);
        return Task.CompletedTask;
    }
}
