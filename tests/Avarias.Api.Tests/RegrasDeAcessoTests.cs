using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Avarias.Api.Data;
using Avarias.Api.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Avarias.Api.Tests;

/// <summary>
/// API real em memória (base de dados EF InMemory em vez do PostgreSQL).
/// Arranca em Development, por isso o DbSeeder cria os três utilizadores de teste.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    public ApiFactory()
    {
        // Lida pelo Program.cs antes de os serviços de teste entrarem; as variáveis de ambiente ganham aos user-secrets.
        Environment.SetEnvironmentVariable("Jwt__Key", "chave-so-para-testes-com-mais-de-32-caracteres");
        Environment.SetEnvironmentVariable("Smtp__Host", "");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var nomeBd = "testes-" + Guid.NewGuid();
        builder.ConfigureTestServices(s =>
        {
            s.RemoveAll<DbContextOptions<AppDbContext>>();
            s.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(nomeBd));
        });
    }
}

internal static class ServiceCollectionExtras
{
    public static void RemoveAll<T>(this IServiceCollection s)
    {
        foreach (var d in s.Where(d => d.ServiceType == typeof(T)).ToList()) s.Remove(d);
    }
}

public class RegrasDeAcessoTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _f;
    public RegrasDeAcessoTests(ApiFactory f) => _f = f;

    private record LoginResp(string Token);
    private record Criado(int Id);

    private async Task<HttpClient> Entrar(string email, string password)
    {
        var c = _f.CreateClient();
        var r = await c.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var token = (await r.Content.ReadFromJsonAsync<LoginResp>())!.Token;
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return c;
    }

    private async Task CriarUtilizador(string email, string password, Perfil perfil)
    {
        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (await db.Utilizadores.AnyAsync(u => u.Email == email)) return;
        db.Utilizadores.Add(new Utilizador
        {
            Nome = email, Email = email, Perfil = perfil,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, 4),
        });
        await db.SaveChangesAsync();
    }

    private async Task<int> CriarReporte(HttpClient c)
    {
        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var local = await db.Locais.FirstAsync();
        var tipo = await db.TiposAvaria.FirstAsync();

        var form = new MultipartFormDataContent
        {
            { new StringContent("Projetor não liga"), "titulo" },
            { new StringContent("O projetor da sala não acende desde ontem."), "descricao" },
            { new StringContent(local.Id.ToString()), "localId" },
            { new StringContent(tipo.Id.ToString()), "tipoId" },
            { new StringContent("Media"), "urgencia" },
        };
        var r = await c.PostAsync("/api/reportes", form);
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        return (await r.Content.ReadFromJsonAsync<Criado>())!.Id;
    }

    [Fact]
    public async Task Professor_nao_ve_nem_altera_reportes_de_outros()
    {
        await CriarUtilizador("outro.professor@escola.test", "Outro123!", Perfil.Professor);
        var autor = await Entrar("professor@escola.test", "Professor123!");
        var outro = await Entrar("outro.professor@escola.test", "Outro123!");
        var tecnico = await Entrar("tecnico@escola.test", "Tecnico123!");

        var id = await CriarReporte(autor);

        Assert.Equal(HttpStatusCode.OK, (await autor.GetAsync($"/api/reportes/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outro.GetAsync($"/api/reportes/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await outro.PostAsJsonAsync($"/api/reportes/{id}/comentarios", new { texto = "olá" })).StatusCode);
        Assert.DoesNotContain($"\"id\":{id},", await outro.GetStringAsync("/api/reportes"));

        // Só o técnico altera o estado.
        Assert.Equal(HttpStatusCode.Forbidden,
            (await autor.PatchAsJsonAsync($"/api/reportes/{id}", new { estado = "EmAnalise" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await tecnico.GetAsync($"/api/reportes/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await tecnico.PatchAsJsonAsync($"/api/reportes/{id}", new { estado = "EmAnalise" })).StatusCode);
    }

    [Fact]
    public async Task Backoffice_so_para_tecnico_e_sem_se_trancar_fora()
    {
        var professor = await Entrar("professor@escola.test", "Professor123!");
        var biblioteca = await Entrar("biblioteca@escola.test", "Biblioteca123!");
        var tecnico = await Entrar("tecnico@escola.test", "Tecnico123!");

        Assert.Equal(HttpStatusCode.Forbidden, (await professor.GetAsync("/api/admin/utilizadores")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await biblioteca.PostAsJsonAsync("/api/admin/tipos", new { nome = "Tipo novo" })).StatusCode);

        // Criar uma conta e entrar com ela; o email fica em minúsculas.
        var criar = await tecnico.PostAsJsonAsync("/api/admin/utilizadores",
            new { nome = "Nova Pessoa", email = "Nova.Pessoa@Escola.test", perfil = "Professor", ativo = true, password = "Inicial123!" });
        Assert.Equal(HttpStatusCode.Created, criar.StatusCode);
        await Entrar("nova.pessoa@escola.test", "Inicial123!");
        Assert.Equal(HttpStatusCode.Conflict, (await tecnico.PostAsJsonAsync("/api/admin/utilizadores",
            new { nome = "Repetida", email = "nova.pessoa@escola.test", perfil = "Professor", password = "Inicial123!" })).StatusCode);

        // O técnico não se pode desativar a si próprio.
        var eu = (await tecnico.GetFromJsonAsync<List<Conta>>("/api/admin/utilizadores"))!.First(u => u.Email == "tecnico@escola.test");
        Assert.Equal(HttpStatusCode.BadRequest, (await tecnico.PutAsJsonAsync($"/api/admin/utilizadores/{eu.Id}",
            new { nome = eu.Nome, email = eu.Email, perfil = "Tecnico", ativo = false })).StatusCode);
    }

    private record Conta(int Id, string Nome, string Email);

    [Fact]
    public async Task Nao_apaga_local_ou_tipo_com_reportes()
    {
        var autor = await Entrar("professor@escola.test", "Professor123!");
        var tecnico = await Entrar("tecnico@escola.test", "Tecnico123!");
        await CriarReporte(autor); // usa o primeiro local e o primeiro tipo

        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var local = await db.Locais.FirstAsync();
        var tipo = await db.TiposAvaria.FirstAsync();

        Assert.Equal(HttpStatusCode.Conflict, (await tecnico.DeleteAsync($"/api/admin/locais/{local.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await tecnico.DeleteAsync($"/api/admin/tipos/{tipo.Id}")).StatusCode);

        var novo = await tecnico.PostAsJsonAsync("/api/admin/tipos", new { nome = "Tipo sem reportes" });
        var idNovo = (await novo.Content.ReadFromJsonAsync<Criado>())!.Id;
        Assert.Equal(HttpStatusCode.NoContent, (await tecnico.DeleteAsync($"/api/admin/tipos/{idNovo}")).StatusCode);
    }

    [Fact]
    public async Task Conta_desativada_perde_o_acesso_logo()
    {
        await CriarUtilizador("a.desativar@escola.test", "Desativar123!", Perfil.Tecnico);
        var c = await Entrar("a.desativar@escola.test", "Desativar123!");
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/reportes")).StatusCode);

        using (var scope = _f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var u = await db.Utilizadores.FirstAsync(x => x.Email == "a.desativar@escola.test");
            u.Ativo = false;
            await db.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/reportes")).StatusCode);
    }
}
