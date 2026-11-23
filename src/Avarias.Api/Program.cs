using Avarias.Api.Extensions;

// Arranque da API. A configuração de cada parte está em Extensions/.
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddBaseDeDados(builder.Configuration)          // PostgreSQL no Supabase (tabelas já existentes)
    .AddSupabase(builder.Configuration)             // Supabase Auth: login, sessões, contas
    .AddControllersComErrosSimples()                // erros sempre como { "message": "..." }
    .AddAutenticacaoSupabase(builder.Configuration) // valida os tokens do Supabase + perfil da BD
    .AddLimiteDeTentativas();                       // login / recuperação

var app = builder.Build();

app.UsarPipelineAvarias();
await app.InicializarBaseDeDadosAsync();

app.Run();

// Permite aos testes (WebApplicationFactory) arrancar a API.
public partial class Program { }
