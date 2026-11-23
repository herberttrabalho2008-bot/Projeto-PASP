using Avarias.Api.Data;

namespace Avarias.Api.Extensions;

/// <summary>Pipeline de pedidos e arranque, chamado a partir de Program.cs.</summary>
public static class WebApplicationExtensions
{
    /// <summary>Erros, frontend estático (wwwroot), limite de tentativas, autenticação e controllers.</summary>
    public static WebApplication UsarPipelineAvarias(this WebApplication app)
    {
        // Erros inesperados: resposta clara, sem detalhes internos.
        app.UseExceptionHandler(a => a.Run(async ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await ctx.Response.WriteAsJsonAsync(new { message = "Ocorreu um erro no servidor. Tente novamente dentro de instantes." });
        }));

        // Frontend (HTML, CSS e JavaScript) servido da pasta wwwroot, na mesma origem da API.
        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        return app;
    }

    /// <summary>
    /// Confirma a ligação à base de dados e garante os perfis e estados de que a aplicação depende.
    /// As tabelas são do Supabase: aqui nada é criado nem alterado além desses valores.
    /// </summary>
    public static async Task InicializarBaseDeDadosAsync(this WebApplication app)
    {
        if (!app.Configuration.GetValue("BaseDeDados:GarantirDadosBase", true)) return;

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var log = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Arranque");
        try
        {
            await DbSeeder.SeedAsync(db, log);
        }
        catch (Exception ex)
        {
            log.LogCritical(ex, "Não foi possível usar a base de dados. Confirme ConnectionStrings:Default e se as tabelas existem no Supabase.");
            throw;
        }
    }
}
