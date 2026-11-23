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

    /// <summary>Cria as tabelas (se não existirem) e os dados iniciais.</summary>
    public static async Task InicializarBaseDeDadosAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
        await DbSeeder.SeedAsync(db, app.Configuration.GetValue("SeedUtilizadoresTeste", false));
    }
}
