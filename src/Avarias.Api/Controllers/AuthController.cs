using System.Security.Claims;
using Avarias.Api.Data;
using Avarias.Api.Dtos;
using Avarias.Api.Extensions;
using Avarias.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Avarias.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    // Hash falso, usado para que o tempo de resposta seja semelhante quando o email não existe.
    private static readonly string HashFalso = BCrypt.Net.BCrypt.HashPassword("nao-existe", 11);

    private readonly AppDbContext _db;
    private readonly TokenService _tokens;
    private readonly IEmailSender _email;
    private readonly IConfiguration _cfg;

    public AuthController(AppDbContext db, TokenService tokens, IEmailSender email, IConfiguration cfg)
    {
        _db = db; _tokens = tokens; _email = email; _cfg = cfg;
    }

    /// <summary>Login com email e password. A mensagem de erro é igual para email inexistente e password errada.</summary>
    [HttpPost("login")]
    [EnableRateLimiting(ServiceCollectionExtensions.PoliticaAuth)]
    public async Task<IActionResult> Login(LoginRequest req)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        var u = await _db.Utilizadores.FirstOrDefaultAsync(x => x.Email == email);

        var ok = BCrypt.Net.BCrypt.Verify(req.Password, u?.PasswordHash ?? HashFalso);
        if (u is null || !ok)
            return Unauthorized(new { message = "Email ou palavra-passe incorretos." });

        if (!u.Ativo)
            return StatusCode(403, new { message = "Esta conta está desativada. Contacte os serviços da escola." });

        return Ok(new LoginResponse(_tokens.CriarJwt(u), new UserInfo(u.Id, u.Nome, u.Email, u.Perfil)));
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var id = int.Parse(User.FindFirstValue("sub")!);
        var u = await _db.Utilizadores.FirstOrDefaultAsync(x => x.Id == id && x.Ativo);
        if (u is null) return Unauthorized(new { message = "A sessão já não é válida." });
        return Ok(new UserInfo(u.Id, u.Nome, u.Email, u.Perfil));
    }

    /// <summary>
    /// Pede a recuperação de palavra-passe. A resposta é sempre a mesma,
    /// para não revelar que emails existem no sistema.
    /// </summary>
    [HttpPost("forgot-password")]
    [EnableRateLimiting(ServiceCollectionExtensions.PoliticaAuth)]
    public async Task<IActionResult> EsqueciPassword(ForgotPasswordRequest req)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        var u = await _db.Utilizadores.FirstOrDefaultAsync(x => x.Email == email && x.Ativo);

        if (u is not null)
        {
            var (token, hash) = TokenService.NovoTokenRecuperacao();
            _db.TokensRecuperacao.Add(new Models.TokenRecuperacao
            {
                UtilizadorId = u.Id,
                TokenHash = hash,
                ExpiraEm = DateTime.UtcNow.AddHours(1),
            });
            await _db.SaveChangesAsync();

            var link = $"{_cfg["Frontend:BaseUrl"]}/repor-password.html?token={token}";
            await _email.EnviarAsync(u.Email, "Recuperação de palavra-passe",
                $"Olá {u.Nome},\n\nPara definir uma nova palavra-passe abra o link (válido durante 1 hora):\n{link}\n\n" +
                "Se não fez este pedido, ignore esta mensagem.");
        }

        return Ok(new { message = "Se o email existir, enviámos um link para repor a palavra-passe." });
    }

    [HttpPost("reset-password")]
    [EnableRateLimiting(ServiceCollectionExtensions.PoliticaAuth)]
    public async Task<IActionResult> ReporPassword(ResetPasswordRequest req)
    {
        var hash = TokenService.Hash(req.Token);
        var t = await _db.TokensRecuperacao
            .FirstOrDefaultAsync(x => x.TokenHash == hash && !x.Usado && x.ExpiraEm > DateTime.UtcNow);

        if (t is null)
            return BadRequest(new { message = "O link de recuperação é inválido ou já expirou. Peça um novo." });

        var u = await _db.Utilizadores.FirstAsync(x => x.Id == t.UtilizadorId);
        u.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NovaPassword, 11);
        t.Usado = true;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Palavra-passe alterada com sucesso. Já pode iniciar sessão." });
    }
}
