using Avarias.Api.Data;
using Avarias.Api.Dtos;
using Avarias.Api.Extensions;
using Avarias.Api.Models;
using Avarias.Api.Services.Supabase;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Avarias.Api.Controllers;

/// <summary>
/// Sessão com o Supabase Auth. O browser só fala com a API; a API fala com o Supabase.
/// O perfil (Professor, Biblioteca, Técnico) vem da tabela utilizadores.
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ISupabaseAuth _auth;
    private readonly IConfiguration _cfg;

    public AuthController(AppDbContext db, ISupabaseAuth auth, IConfiguration cfg)
    {
        _db = db; _auth = auth; _cfg = cfg;
    }

    [HttpPost("login")]
    [EnableRateLimiting(ServiceCollectionExtensions.PoliticaAuth)]
    public async Task<IActionResult> Login(LoginRequest req)
    {
        SessaoSupabase s;
        try
        {
            s = await _auth.EntrarAsync(req.Email.Trim().ToLowerInvariant(), req.Password);
        }
        catch (SupabaseAuthException ex)
        {
            return Erro(ex);
        }

        var u = await _db.Utilizadores.AsNoTracking().Include(x => x.Permissao).FirstOrDefaultAsync(x => x.Id == s.UserId);
        var perfil = Textos.PerfilDe(u?.Permissao?.Descricao);
        if (u is null || perfil is null)
        {
            await _auth.TerminarSessaoAsync(s.AccessToken);
            return StatusCode(403, new { message = "Esta conta ainda não tem perfil atribuído. Contacte o técnico da escola." });
        }

        return Ok(new LoginResponse(s.AccessToken, s.RefreshToken, s.ExpiraEm,
            new UserInfo(u.Id, u.NomeUtilizador, s.Email, perfil.Value)));
    }

    /// <summary>Troca o refresh token por uma sessão nova (o access token do Supabase dura ~1 hora).</summary>
    [HttpPost("refresh")]
    [EnableRateLimiting(ServiceCollectionExtensions.PoliticaAuth)]
    public async Task<IActionResult> Renovar(RefreshRequest req)
    {
        try
        {
            var s = await _auth.RenovarAsync(req.RefreshToken);
            return Ok(new SessaoResponse(s.AccessToken, s.RefreshToken, s.ExpiraEm));
        }
        catch (SupabaseAuthException ex)
        {
            return Erro(ex);
        }
    }

    [HttpGet("me")]
    [Authorize]
    public IActionResult Me() =>
        Ok(new UserInfo(User.UserId(), User.Identity!.Name!, User.Email(), User.PerfilApp()));

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var token = Request.Headers.Authorization.ToString().Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase);
        await _auth.TerminarSessaoAsync(token);
        return NoContent();
    }

    /// <summary>
    /// O Supabase envia o email com o link de recuperação. A resposta é sempre a mesma,
    /// para não revelar que emails existem.
    /// </summary>
    [HttpPost("forgot-password")]
    [EnableRateLimiting(ServiceCollectionExtensions.PoliticaAuth)]
    public async Task<IActionResult> EsqueciPassword(ForgotPasswordRequest req)
    {
        var baseUrl = (_cfg["Frontend:BaseUrl"] ?? $"{Request.Scheme}://{Request.Host}").TrimEnd('/');
        try
        {
            await _auth.PedirRecuperacaoAsync(req.Email.Trim().ToLowerInvariant(), $"{baseUrl}/repor-password.html");
        }
        catch (SupabaseAuthException ex) when (ex.Status == 429)
        {
            return Erro(ex);
        }
        catch (SupabaseAuthException) { /* não revela nada */ }

        return Ok(new { message = "Se o email existir, enviámos um link para repor a palavra-passe." });
    }

    [HttpPost("reset-password")]
    [EnableRateLimiting(ServiceCollectionExtensions.PoliticaAuth)]
    public async Task<IActionResult> ReporPassword(ResetPasswordRequest req)
    {
        try
        {
            await _auth.AlterarPasswordAsync(req.AccessToken, req.NovaPassword);
            await _auth.TerminarSessaoAsync(req.AccessToken);
            return Ok(new { message = "Palavra-passe alterada com sucesso. Já pode iniciar sessão." });
        }
        catch (SupabaseAuthException ex)
        {
            return Erro(ex.Status == 401
                ? new SupabaseAuthException(400, "O link de recuperação é inválido ou já expirou. Peça um novo.")
                : ex);
        }
    }

    private ObjectResult Erro(SupabaseAuthException ex) => StatusCode(ex.Status, new { message = ex.Message });
}
