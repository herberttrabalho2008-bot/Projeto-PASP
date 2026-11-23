using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Avarias.Api.Data;
using Avarias.Api.Models;
using Avarias.Api.Services.Supabase;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Avarias.Api.Tests;

/// <summary>
/// API real em memória: base de dados EF InMemory em vez do Supabase e um Supabase Auth falso.
/// Os tokens são assinados com um segredo HS256 de teste (Supabase:JwtSecret), como nos projetos antigos do Supabase.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    // Endereço que recusa ligações logo: a leitura das chaves públicas falha depressa e usa-se o segredo.
    public const string SupabaseUrl = "http://127.0.0.1:9";
    public const string Segredo = "segredo-so-para-testes-com-mais-de-32-caracteres";

    public ApiFactory()
    {
        // Lidas pelo Program.cs antes de os serviços de teste entrarem; as variáveis de ambiente ganham aos user-secrets.
        Environment.SetEnvironmentVariable("Supabase__Url", SupabaseUrl);
        Environment.SetEnvironmentVariable("Supabase__AnonKey", "chave-publica-de-teste");
        Environment.SetEnvironmentVariable("Supabase__JwtSecret", Segredo);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testes");
        var nomeBd = "testes-" + Guid.NewGuid();
        builder.ConfigureTestServices(s =>
        {
            foreach (var d in s.Where(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>)).ToList()) s.Remove(d);
            s.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(nomeBd));

            foreach (var d in s.Where(d => d.ServiceType == typeof(ISupabaseAuth)).ToList()) s.Remove(d);
            s.AddScoped<ISupabaseAuth, SupabaseAuthFalso>();
        });
    }

    /// <summary>Token como o do Supabase Auth: iss, aud "authenticated", sub = id da conta.</summary>
    public static string CriarToken(Guid id, string email)
    {
        var chave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Segredo));
        var token = new JwtSecurityToken(
            issuer: SupabaseUrl + "/auth/v1",
            audience: "authenticated",
            claims: new[] { new Claim("sub", id.ToString()), new Claim("email", email), new Claim("role", "authenticated") },
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(chave, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

/// <summary>
/// Supabase Auth em memória. Guarda as palavras-passe num dicionário e escreve as contas em auth.users
/// (ContasAuth) na base de dados InMemory, como o Supabase faria.
/// </summary>
public class SupabaseAuthFalso : ISupabaseAuth
{
    private static readonly Dictionary<string, (Guid Id, string Password)> Contas = new();
    private readonly AppDbContext _db;
    public SupabaseAuthFalso(AppDbContext db) => _db = db;

    public static void Registar(Guid id, string email, string password)
    {
        lock (Contas) Contas[email] = (id, password);
    }

    public async Task<SessaoSupabase> EntrarAsync(string email, string password)
    {
        (Guid Id, string Password) c;
        lock (Contas)
        {
            if (!Contas.TryGetValue(email, out c) || c.Password != password)
                throw new SupabaseAuthException(401, "Email ou palavra-passe incorretos.");
        }
        if (await _db.ContasAuth.AnyAsync(x => x.Id == c.Id && x.BannedUntil > DateTime.UtcNow))
            throw new SupabaseAuthException(403, "Esta conta está desativada. Contacte os serviços da escola.");
        return new SessaoSupabase(ApiFactory.CriarToken(c.Id, email), "refresh-" + c.Id, DateTime.UtcNow.AddHours(1), c.Id, email);
    }

    public Task<SessaoSupabase> RenovarAsync(string refreshToken) =>
        throw new SupabaseAuthException(401, "A sua sessão terminou. Inicie sessão novamente.");

    public Task PedirRecuperacaoAsync(string email, string redirectTo) => Task.CompletedTask;
    public Task AlterarPasswordAsync(string accessToken, string novaPassword) => Task.CompletedTask;
    public Task TerminarSessaoAsync(string accessToken) => Task.CompletedTask;

    public async Task<Guid> CriarContaAsync(string email, string password, string nome)
    {
        lock (Contas)
        {
            if (Contas.ContainsKey(email)) throw new SupabaseAuthException(409, "Já existe uma conta com este email.");
        }
        var id = Guid.NewGuid();
        Registar(id, email, password);
        _db.ContasAuth.Add(new ContaAuth { Id = id, Email = email, CreatedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();
        return id;
    }

    public async Task AtualizarContaAsync(Guid id, string? email = null, string? password = null, bool? ativa = null)
    {
        var c = await _db.ContasAuth.FirstAsync(x => x.Id == id);
        if (email is not null) c.Email = email;
        if (ativa is not null) c.BannedUntil = ativa.Value ? null : DateTime.UtcNow.AddYears(100);
        await _db.SaveChangesAsync();
    }

    public Task ApagarContaAsync(Guid id) => Task.CompletedTask;
}
