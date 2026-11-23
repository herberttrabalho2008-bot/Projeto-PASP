using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Avarias.Api.Data;
using Avarias.Api.Models;
using Avarias.Api.Services.Supabase;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Avarias.Api.Extensions;

/// <summary>Registo dos serviços da API, chamado a partir de Program.cs.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Nome da política de limite de tentativas usada em AuthController.</summary>
    public const string PoliticaAuth = "auth";

    /// <summary>Base de dados PostgreSQL (Supabase), com nomes em snake_case.</summary>
    public static IServiceCollection AddBaseDeDados(this IServiceCollection services, IConfiguration cfg)
    {
        services.AddDbContext<AppDbContext>(o =>
            o.UseNpgsql(cfg.GetConnectionString("Default"))
             .UseSnakeCaseNamingConvention());
        return services;
    }

    /// <summary>Cliente do Supabase Auth (login, sessões, recuperação e gestão de contas).</summary>
    public static IServiceCollection AddSupabase(this IServiceCollection services, IConfiguration cfg)
    {
        SupabaseConfig.Url(cfg); // falha logo no arranque se faltar a configuração
        if (string.IsNullOrWhiteSpace(cfg["Supabase:AnonKey"]))
            throw new InvalidOperationException("Configure Supabase:AnonKey (chave pública do projeto). Ver README.");

        services.AddSingleton<Services.Fotos.ArmazenamentoFotos>();
        services.AddHttpClient<ISupabaseAuth, SupabaseAuthClient>(h => h.Timeout = TimeSpan.FromSeconds(15));
        // As chaves de validação ficam em cache: um único SupabaseJwks para toda a aplicação.
        services.AddHttpClient(nameof(SupabaseJwks), h => h.Timeout = TimeSpan.FromSeconds(10));
        services.AddSingleton(sp => ActivatorUtilities.CreateInstance<SupabaseJwks>(sp,
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(SupabaseJwks))));
        return services;
    }

    /// <summary>Controllers com enums em texto e erros de validação no formato único { "message": "..." }.</summary>
    public static IServiceCollection AddControllersComErrosSimples(this IServiceCollection services)
    {
        services.AddControllers()
            .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
            .ConfigureApiBehaviorOptions(o =>
            {
                o.InvalidModelStateResponseFactory = ctx =>
                {
                    var msg = ctx.ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .FirstOrDefault(m => !string.IsNullOrWhiteSpace(m)) ?? "Os dados enviados não são válidos.";
                    return new BadRequestObjectResult(new { message = msg });
                };
            });
        return services;
    }

    /// <summary>
    /// Valida os tokens do Supabase Auth e acrescenta o perfil da aplicação (tabela utilizadores → tipos_utilizador).
    /// Em cada pedido confirma que a conta existe em utilizadores e não está bloqueada em auth.users.
    /// </summary>
    public static IServiceCollection AddAutenticacaoSupabase(this IServiceCollection services, IConfiguration cfg)
    {
        var issuer = SupabaseConfig.Issuer(cfg);

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<SupabaseJwks>((o, jwks) =>
            {
                o.MapInboundClaims = false; // mantém os nomes curtos: sub, email…
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    NameClaimType = Claims.Nome,
                    RoleClaimType = Claims.Perfil, // o "role" do Supabase é sempre "authenticated"; o perfil vem da BD
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = SupabaseConfig.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKeyResolver = (_, _, kid, _) => jwks.Chaves(kid),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                };
                o.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async ctx =>
                    {
                        if (!Guid.TryParse(ctx.Principal?.FindFirst("sub")?.Value, out var id))
                        {
                            ctx.Fail("Token sem utilizador.");
                            return;
                        }
                        var db = ctx.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                        var agora = DateTime.UtcNow;
                        var u = await db.Utilizadores.AsNoTracking()
                            .Where(x => x.Id == id)
                            .Select(x => new
                            {
                                x.NomeUtilizador,
                                Perfil = x.Permissao != null ? x.Permissao.Descricao : null,
                                Bloqueada = db.ContasAuth.Any(c => c.Id == id && (c.BannedUntil > agora || c.DeletedAt != null)),
                            })
                            .FirstOrDefaultAsync();

                        var perfil = Textos.PerfilDe(u?.Perfil);
                        if (u is null || perfil is null || u.Bloqueada)
                        {
                            ctx.Fail("Conta desativada, sem perfil ou inexistente.");
                            return;
                        }
                        ((ClaimsIdentity)ctx.Principal!.Identity!).AddClaims(new[]
                        {
                            new Claim(Claims.Nome, u.NomeUtilizador),
                            new Claim(Claims.Perfil, perfil.Value.ToString()),
                        });
                    },
                };
            });
        services.AddAuthorization();
        return services;
    }

    /// <summary>
    /// Limite de tentativas no login e na recuperação, por IP. Numa escola todos saem pelo mesmo IP,
    /// por isso o limite é largo; o Supabase tem também os seus próprios limites.
    /// </summary>
    public static IServiceCollection AddLimiteDeTentativas(this IServiceCollection services)
    {
        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.AddPolicy(PoliticaAuth, ctx => RateLimitPartition.GetFixedWindowLimiter(
                ctx.Connection.RemoteIpAddress?.ToString() ?? "anon",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 100, Window = TimeSpan.FromMinutes(1) }));
        });
        return services;
    }
}
