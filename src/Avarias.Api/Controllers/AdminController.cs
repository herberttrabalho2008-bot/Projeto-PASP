using System.Security.Claims;
using Avarias.Api.Data;
using Avarias.Api.Dtos;
using Avarias.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Avarias.Api.Controllers;

/// <summary>
/// Backoffice: contas, locais, equipamentos e tipos de avaria. Só o técnico tem acesso.
/// Para listar locais, equipamentos e tipos usa-se GET /api/catalogos.
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize(Roles = nameof(Perfil.Tecnico))]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _db;
    public AdminController(AppDbContext db) => _db = db;

    private int UserId => int.Parse(User.FindFirstValue("sub")!);

    private static ObjectResult Conflito(string msg) => new(new { message = msg }) { StatusCode = 409 };
    private NotFoundObjectResult NaoEncontrado(string msg) => NotFound(new { message = msg });

    private static UtilizadorAdminDto ParaDto(Utilizador u) => new(u.Id, u.Nome, u.Email, u.Perfil, u.Ativo, u.CriadoEm);

    // ------------------------------------------------------------------ contas
    [HttpGet("utilizadores")]
    public async Task<List<UtilizadorAdminDto>> ListarUtilizadores() =>
        (await _db.Utilizadores.AsNoTracking().OrderBy(u => u.Nome).ToListAsync()).Select(ParaDto).ToList();

    [HttpPost("utilizadores")]
    public async Task<IActionResult> CriarUtilizador(GuardarUtilizadorRequest req)
    {
        if (string.IsNullOrEmpty(req.Password))
            return BadRequest(new { message = "Indique a palavra-passe inicial." });

        var email = req.Email.Trim().ToLowerInvariant();
        if (await _db.Utilizadores.AnyAsync(u => u.Email == email))
            return Conflito("Já existe uma conta com este email.");

        var u = new Utilizador
        {
            Nome = req.Nome.Trim(), Email = email, Perfil = req.Perfil, Ativo = req.Ativo,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password, 11),
        };
        _db.Utilizadores.Add(u);
        await _db.SaveChangesAsync();
        return StatusCode(201, ParaDto(u));
    }

    [HttpPut("utilizadores/{id:int}")]
    public async Task<IActionResult> EditarUtilizador(int id, GuardarUtilizadorRequest req)
    {
        var u = await _db.Utilizadores.FirstOrDefaultAsync(x => x.Id == id);
        if (u is null) return NaoEncontrado("Conta não encontrada.");

        // Impede que o técnico se tranque fora do backoffice.
        if (id == UserId && (!req.Ativo || req.Perfil != Perfil.Tecnico))
            return BadRequest(new { message = "Não pode desativar a sua própria conta nem tirar-lhe o perfil de técnico." });

        var email = req.Email.Trim().ToLowerInvariant();
        if (email != u.Email && await _db.Utilizadores.AnyAsync(x => x.Email == email))
            return Conflito("Já existe uma conta com este email.");

        u.Nome = req.Nome.Trim();
        u.Email = email;
        u.Perfil = req.Perfil;
        u.Ativo = req.Ativo;
        await _db.SaveChangesAsync();
        return Ok(ParaDto(u));
    }

    [HttpPost("utilizadores/{id:int}/password")]
    public async Task<IActionResult> DefinirPassword(int id, DefinirPasswordRequest req)
    {
        var u = await _db.Utilizadores.FirstOrDefaultAsync(x => x.Id == id);
        if (u is null) return NaoEncontrado("Conta não encontrada.");

        u.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NovaPassword, 11);
        // Links de recuperação pendentes deixam de servir.
        await _db.TokensRecuperacao.Where(t => t.UtilizadorId == id && !t.Usado).ForEachAsync(t => t.Usado = true);
        await _db.SaveChangesAsync();
        return Ok(new { message = "Palavra-passe definida. Comunique-a à pessoa por um canal seguro." });
    }

    // ------------------------------------------------------------------ locais
    [HttpPost("locais")]
    public async Task<IActionResult> CriarLocal(LocalRequest req)
    {
        var nome = req.Nome.Trim();
        if (await _db.Locais.AnyAsync(l => l.Nome == nome)) return Conflito("Já existe um local com este nome.");
        var l = new Local { Nome = nome, EBiblioteca = req.EBiblioteca };
        _db.Locais.Add(l);
        await _db.SaveChangesAsync();
        return StatusCode(201, new { id = l.Id });
    }

    [HttpPut("locais/{id:int}")]
    public async Task<IActionResult> EditarLocal(int id, LocalRequest req)
    {
        var l = await _db.Locais.FirstOrDefaultAsync(x => x.Id == id);
        if (l is null) return NaoEncontrado("Local não encontrado.");
        var nome = req.Nome.Trim();
        if (nome != l.Nome && await _db.Locais.AnyAsync(x => x.Nome == nome)) return Conflito("Já existe um local com este nome.");
        l.Nome = nome;
        l.EBiblioteca = req.EBiblioteca;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("locais/{id:int}")]
    public async Task<IActionResult> ApagarLocal(int id)
    {
        var l = await _db.Locais.Include(x => x.Equipamentos).FirstOrDefaultAsync(x => x.Id == id);
        if (l is null) return NaoEncontrado("Local não encontrado.");
        if (await _db.Reportes.AnyAsync(r => r.LocalId == id))
            return Conflito("Este local tem reportes associados e não pode ser apagado. Pode mudar-lhe o nome.");
        _db.Equipamentos.RemoveRange(l.Equipamentos);
        _db.Locais.Remove(l);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ------------------------------------------------------------------ equipamentos
    [HttpPost("locais/{localId:int}/equipamentos")]
    public async Task<IActionResult> CriarEquipamento(int localId, NomeRequest req)
    {
        if (!await _db.Locais.AnyAsync(l => l.Id == localId)) return NaoEncontrado("Local não encontrado.");
        var e = new Equipamento { Nome = req.Nome.Trim(), LocalId = localId };
        _db.Equipamentos.Add(e);
        await _db.SaveChangesAsync();
        return StatusCode(201, new { id = e.Id });
    }

    [HttpPut("equipamentos/{id:int}")]
    public async Task<IActionResult> EditarEquipamento(int id, NomeRequest req)
    {
        var e = await _db.Equipamentos.FirstOrDefaultAsync(x => x.Id == id);
        if (e is null) return NaoEncontrado("Equipamento não encontrado.");
        e.Nome = req.Nome.Trim();
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("equipamentos/{id:int}")]
    public async Task<IActionResult> ApagarEquipamento(int id)
    {
        var e = await _db.Equipamentos.FirstOrDefaultAsync(x => x.Id == id);
        if (e is null) return NaoEncontrado("Equipamento não encontrado.");
        if (await _db.Reportes.AnyAsync(r => r.EquipamentoId == id))
            return Conflito("Este equipamento tem reportes associados e não pode ser apagado. Pode mudar-lhe o nome.");
        _db.Equipamentos.Remove(e);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ------------------------------------------------------------------ tipos de avaria
    [HttpPost("tipos")]
    public async Task<IActionResult> CriarTipo(NomeRequest req)
    {
        var nome = req.Nome.Trim();
        if (await _db.TiposAvaria.AnyAsync(t => t.Nome == nome)) return Conflito("Já existe um tipo com este nome.");
        var t = new TipoAvaria { Nome = nome };
        _db.TiposAvaria.Add(t);
        await _db.SaveChangesAsync();
        return StatusCode(201, new { id = t.Id });
    }

    [HttpPut("tipos/{id:int}")]
    public async Task<IActionResult> EditarTipo(int id, NomeRequest req)
    {
        var t = await _db.TiposAvaria.FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return NaoEncontrado("Tipo não encontrado.");
        var nome = req.Nome.Trim();
        if (nome != t.Nome && await _db.TiposAvaria.AnyAsync(x => x.Nome == nome)) return Conflito("Já existe um tipo com este nome.");
        t.Nome = nome;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("tipos/{id:int}")]
    public async Task<IActionResult> ApagarTipo(int id)
    {
        var t = await _db.TiposAvaria.FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return NaoEncontrado("Tipo não encontrado.");
        if (await _db.Reportes.AnyAsync(r => r.TipoId == id))
            return Conflito("Este tipo tem reportes associados e não pode ser apagado. Pode mudar-lhe o nome.");
        _db.TiposAvaria.Remove(t);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
