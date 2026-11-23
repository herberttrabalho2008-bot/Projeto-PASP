using Avarias.Api.Extensions;

// Arranque da API. A configuração de cada parte está em Extensions/.
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddBaseDeDados(builder.Configuration)          // PostgreSQL / Supabase
    .AddServicosAplicacao(builder.Configuration)    // TokenService, IEmailSender
    .AddControllersComErrosSimples()                // erros sempre como { "message": "..." }
    .AddAutenticacaoJwt(builder.Configuration)      // JWT + conta ativa em cada pedido
    .AddLimiteDeTentativas();                       // login / recuperação

var app = builder.Build();

app.UsarPipelineAvarias();
await app.InicializarBaseDeDadosAsync();

app.Run();

// Permite aos testes (WebApplicationFactory) arrancar a API.
public partial class Program { }
