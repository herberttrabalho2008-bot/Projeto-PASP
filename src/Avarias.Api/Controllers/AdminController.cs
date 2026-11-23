using Avarias.Api.Data;
using Avarias.Api.Dtos;
using Avarias.Api.Extensions;
using Avarias.Api.Models;
using Avarias.Api.Services.Supabase;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Avarias.Api.Controllers;

/// <summary>
/// Backoffice: contas, salas, equipamentos e tipos de equipamento. Só o técnico tem acesso.
/// As contas vivem no Supabase Auth (email, palavra-passe, bloqueio) e em utilizadores (nome, perfil).
/// Para listar salas, equipamentos e tipos usa-se GET /api/catalogos.
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize(Roles = nameof(Perfil.Tecnico))]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ISupabaseAuth _auth;

    public AdminController(AppDbContext db, ISupabaseAuth auth)
    {
        _db = db; _auth = auth;
    }

    private static ObjectResult Conflito(string msg) => new(new { message = msg }) { StatusCode = 409 };
    private NotFoundObjectResult NaoEncontrado(string msg) => NotFound(new { message = msg });
    private ObjectResult Erro(SupabaseAuthException ex) => StatusCode(ex.Status, new { message = ex.Message });

    // ------------------------------------------------------------------ contas
    [HttpGet("utilizadores")]
    public async Task<List<UtilizadorAdminDto>> ListarUtilizadores()
    {
        var agora = DateTime.UtcNow;
        var lista = await _db.Utilizadores.AsNoTracking()
            .OrderBy(u => u.NomeUtilizador)
            .Select(u => new
            {
                u.Id, u.NomeUtilizador, u.CreatedAt,
                Perfil = u.Permissao != null ? u.Permissao.Descricao : null,
                Conta = _db.ContasAuth.Where(c => c.Id == u.Id).Select(c => new { c.Email, c.BannedUntil, c.DeletedAt }).FirstOrDefault(),
            })
            .ToListAsync();

        return lista.Select(u => new UtilizadorAdminDto(u.Id, u.NomeUtilizador, u.Conta?.Email, Textos.PerfilDe(u.Perfil),
            u.Conta is not null && u.Conta.DeletedAt is null && !(u.Conta.BannedUntil > agora), u.CreatedAt)).ToList();
    }

    [HttpPost("utilizadores")]
    public async Task<IActionResult> CriarUtilizador(GuardarUtilizadorRequest req)
    {
        if (string.IsNullOrEmpty(req.Password))
            return BadRequest(new { message = "Indique a palavra-passe inicial." });

        var tipo = await TipoDoPerfilAsync(req.Perfil);
        if (tipo is null) return StatusCode(500, new { message = $"Falta o perfil {req.Perfil} na tabela tipos_utilizador." });

        Guid id;
        try
        {
            id = await _auth.CriarContaAsync(req.Email.Trim().ToLowerInvariant(), req.Password, req.Nome.Trim());
            if (!req.Ativo) await _auth.AtualizarContaAsync(id, ativa: false);
        }
        catch (SupabaseAuthException ex)
        {
            return Erro(ex);
        }

        var u = new Utilizador { Id = id, NomeUtilizador = req.Nome.Trim(), PermissaoId = tipo.Id, CreatedAt = DateTime.UtcNow };
        _db.Utilizadores.Add(u);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            await _auth.ApagarContaAsync(id); // não deixa uma conta sem perfil
            throw;
        }

        return StatusCode(201, new UtilizadorAdminDto(u.Id, u.NomeUtilizador, req.Email.Trim().ToLowerInvariant(), req.Perfil, req.Ativo, u.CreatedAt));
    }

    [HttpPut("utilizadores/{id:guid}")]
    public async Task<IActionResult> EditarUtilizador(Guid id, GuardarUtilizadorRequest req)
    {
        var u = await _db.Utilizadores.FirstOrDefaultAsync(x => x.Id == id);
        if (u is null) return NaoEncontrado("Conta não encontrada.");

        // Impede que o técnico se tranque fora do backoffice.
        if (id == User.UserId() && (!req.Ativo || req.Perfil != Perfil.Tecnico))
            return BadRequest(new { message = "Não pode desativar a sua própria conta nem tirar-lhe o perfil de técnico." });

        var tipo = await TipoDoPerfilAsync(req.Perfil);
        if (tipo is null) return StatusCode(500, new { message = $"Falta o perfil {req.Perfil} na tabela tipos_utilizador." });

        var email = req.Email.Trim().ToLowerInvariant();
        var emailAtual = await _db.ContasAuth.Where(c => c.Id == id).Select(c => c.Email).FirstOrDefaultAsync();
        try
        {
            await _auth.AtualizarContaAsync(id, email: email != emailAtual ? email : null, ativa: req.Ativo);
        }
        catch (SupabaseAuthException ex)
        {
            return Erro(ex);
        }

        u.NomeUtilizador = req.Nome.Trim();
        u.PermissaoId = tipo.Id;
        await _db.SaveChangesAsync();
        return Ok(new UtilizadorAdminDto(u.Id, u.NomeUtilizador, email, req.Perfil, req.Ativo, u.CreatedAt));
    }

    [HttpPost("utilizadores/{id:guid}/password")]
    public async Task<IActionResult> DefinirPassword(Guid id, DefinirPasswordRequest req)
    {
        if (!await _db.Utilizadores.AnyAsync(x => x.Id == id)) return NaoEncontrado("Conta não encontrada.");
        try
        {
            await _auth.AtualizarContaAsync(id, password: req.NovaPassword);
        }
        catch (SupabaseAuthException ex)
        {
            return Erro(ex);
        }
        return Ok(new { message = "Palavra-passe definida. Comunique-a à pessoa por um canal seguro." });
    }

    private async Task<TipoUtilizador?> TipoDoPerfilAsync(Perfil p) =>
        (await _db.TiposUtilizador.ToListAsync()).FirstOrDefault(t => Textos.PerfilDe(t.Descricao) == p);

    // ------------------------------------------------------------------ salas
    [HttpPost("salas")]
    public async Task<IActionResult> CriarSala(CriarSalaRequest req)
    {
        var nr = req.NrSala.Trim();
        if (await _db.Salas.AnyAsync(s => s.NrSala == nr)) return Conflito("Já existe uma sala com este número.");
        _db.Salas.Add(new Sala { NrSala = nr, TipoSala = req.TipoSala.Trim() });
        await _db.SaveChangesAsync();
        return StatusCode(201, new { nrSala = nr });
    }

    /// <summary>Muda o tipo da sala. O número é a chave e não se altera (crie uma sala nova).</summary>
    [HttpPut("salas/{nrSala}")]
    public async Task<IActionResult> EditarSala(string nrSala, EditarSalaRequest req)
    {
        var s = await _db.Salas.FirstOrDefaultAsync(x => x.NrSala == nrSala);
        if (s is null) return NaoEncontrado("Sala não encontrada.");
        s.TipoSala = req.TipoSala.Trim();
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("salas/{nrSala}")]
    public async Task<IActionResult> ApagarSala(string nrSala)
    {
        var s = await _db.Salas.FirstOrDefaultAsync(x => x.NrSala == nrSala);
        if (s is null) return NaoEncontrado("Sala não encontrada.");
        if (await _db.Equipamentos.AnyAsync(e => e.NrSala == nrSala))
            return Conflito("Esta sala tem equipamentos. Apague-os ou mude-os de sala primeiro.");
        _db.Salas.Remove(s);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ------------------------------------------------------------------ equipamentos
    [HttpPost("equipamentos")]
    public async Task<IActionResult> CriarEquipamento(EquipamentoRequest req)
    {
        var erro = await ValidarEquipamentoAsync(req, null);
        if (erro is not null) return erro;
        var e = new Equipamento { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow };
        Preencher(e, req);
        _db.Equipamentos.Add(e);
        await _db.SaveChangesAsync();
        return StatusCode(201, new { id = e.Id });
    }

    [HttpPut("equipamentos/{id:guid}")]
    public async Task<IActionResult> EditarEquipamento(Guid id, EquipamentoRequest req)
    {
        var e = await _db.Equipamentos.FirstOrDefaultAsync(x => x.Id == id);
        if (e is null) return NaoEncontrado("Equipamento não encontrado.");
        var erro = await ValidarEquipamentoAsync(req, id);
        if (erro is not null) return erro;
        Preencher(e, req);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("equipamentos/{id:guid}")]
    public async Task<IActionResult> ApagarEquipamento(Guid id)
    {
        var e = await _db.Equipamentos.FirstOrDefaultAsync(x => x.Id == id);
        if (e is null) return NaoEncontrado("Equipamento não encontrado.");
        if (await _db.Avarias.AnyAsync(a => a.IdEquipamento == id))
            return Conflito("Este equipamento tem avarias registadas e não pode ser apagado. Pode mudar-lhe a descrição.");
        _db.Equipamentos.Remove(e);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<IActionResult?> ValidarEquipamentoAsync(EquipamentoRequest req, Guid? id)
    {
        if (!await _db.Salas.AnyAsync(s => s.NrSala == req.NrSala)) return BadRequest(new { message = "A sala escolhida não existe." });
        if (req.IdTipo is not null && !await _db.TiposEquipamento.AnyAsync(t => t.Id == req.IdTipo))
            return BadRequest(new { message = "O tipo de equipamento escolhido não existe." });
        var serie = string.IsNullOrWhiteSpace(req.NrSerie) ? null : req.NrSerie.Trim();
        if (serie is not null && await _db.Equipamentos.AnyAsync(e => e.NrSerie == serie && e.Id != id))
            return Conflito("Já existe um equipamento com este número de série.");
        return null;
    }

    private static void Preencher(Equipamento e, EquipamentoRequest req)
    {
        e.NrSala = req.NrSala;
        e.IdTipo = req.IdTipo;
        e.NrSerie = string.IsNullOrWhiteSpace(req.NrSerie) ? null : req.NrSerie.Trim();
        e.DescEquipamento = req.Descricao.Trim();
    }

    // ------------------------------------------------------------------ tipos de equipamento
    [HttpPost("tipos-equipamento")]
    public async Task<IActionResult> CriarTipo(DescricaoRequest req)
    {
        var desc = req.Descricao.Trim();
        if (await _db.TiposEquipamento.AnyAsync(t => t.DescTipo == desc)) return Conflito("Já existe um tipo com esta descrição.");
        var t = new TipoEquipamento { Id = Guid.NewGuid(), DescTipo = desc };
        _db.TiposEquipamento.Add(t);
        await _db.SaveChangesAsync();
        return StatusCode(201, new { id = t.Id });
    }

    [HttpPut("tipos-equipamento/{id:guid}")]
    public async Task<IActionResult> EditarTipo(Guid id, DescricaoRequest req)
    {
        var t = await _db.TiposEquipamento.FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return NaoEncontrado("Tipo não encontrado.");
        var desc = req.Descricao.Trim();
        if (desc != t.DescTipo && await _db.TiposEquipamento.AnyAsync(x => x.DescTipo == desc))
            return Conflito("Já existe um tipo com esta descrição.");
        t.DescTipo = desc;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("tipos-equipamento/{id:guid}")]
    public async Task<IActionResult> ApagarTipo(Guid id)
    {
        var t = await _db.TiposEquipamento.FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return NaoEncontrado("Tipo não encontrado.");
        if (await _db.Equipamentos.AnyAsync(e => e.IdTipo == id))
            return Conflito("Há equipamentos deste tipo, por isso não pode ser apagado. Pode mudar-lhe a descrição.");
        _db.TiposEquipamento.Remove(t);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
