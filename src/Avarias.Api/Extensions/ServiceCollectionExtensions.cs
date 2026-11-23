using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Avarias.Api.Data;
using Avarias.Api.Services;
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

    /// <summary>Tokens e envio de email. Sem "Smtp:Host" os emails só aparecem no log (desenvolvimento).</summary>
    public static IServiceCollection AddServicosAplicacao(this IServiceCollection services, IConfiguration cfg)
    {
        services.AddSingleton<TokenService>();
        if (string.IsNullOrWhiteSpace(cfg["Smtp:Host"]))
            services.AddSingleton<IEmailSender, ConsoleEmailSender>();
        else
            services.AddSingleton<IEmailSender, SmtpEmailSender>();
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

    /// <summary>Autenticação JWT. A API recusa arrancar sem "Jwt:Key" com 32+ caracteres.</summary>
    public static IServiceCollection AddAutenticacaoJwt(this IServiceCollection services, IConfiguration cfg)
    {
        var jwtKey = cfg["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
            throw new InvalidOperationException("Configure Jwt:Key com pelo menos 32 caracteres (ver README).");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.MapInboundClaims = false; // mantém os nomes curtos: sub, name, role
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    NameClaimType = "name",
                    RoleClaimType = "role",
                    ValidateIssuer = true,
                    ValidIssuer = cfg["Jwt:Issuer"],
                    ValidateAudience = true,
                    ValidAudience = cfg["Jwt:Audience"],
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                };
                o.Events = new JwtBearerEvents
                {
                    // Uma conta desativada perde o acesso logo, sem esperar que o token expire.
                    OnTokenValidated = async ctx =>
                    {
                        var db = ctx.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                        var ativo = int.TryParse(ctx.Principal?.FindFirst("sub")?.Value, out var id)
                                    && await db.Utilizadores.AnyAsync(u => u.Id == id && u.Ativo);
                        if (!ativo) ctx.Fail("Conta desativada ou inexistente.");
                    },
                };
            });
        services.AddAuthorization();
        return services;
    }

    /// <summary>
    /// Limite de tentativas no login e na recuperação, por IP. Numa escola todos saem pelo mesmo IP,
    /// por isso o limite é largo; o BCrypt já torna cada tentativa lenta.
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
