using Avarias.Api.Data;
using Avarias.Api.Dtos;
using Avarias.Api.Extensions;
using Avarias.Api.Models;
using Avarias.Api.Services.Fotos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Avarias.Api.Controllers;

/// <summary>
/// Avarias (tabela avarias) e o seu histórico (historico_avarias).
///
/// REGRAS DE ACESSO (aplicadas aqui, no servidor):
///  - Professor:  regista avarias; vê apenas as suas.
///  - Biblioteca: regista avarias e consulta pendentes e histórico (só leitura).
///                Com "Avarias:BibliotecaVeTudo": false fica limitada às salas do tipo "Biblioteca".
///  - Técnico:    vê tudo e é o único que muda o estado e regista a resolução.
/// Uma avaria fora do âmbito do utilizador responde 404 (não revela que existe).
/// Cada criação e alteração grava uma cópia em historico_avarias.
/// </summary>
[ApiController]
[Route("api/avarias")]
[Authorize]
public class AvariasController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _cfg;
    private readonly ArmazenamentoFotos _fotos;

    public AvariasController(AppDbContext db, IConfiguration cfg, ArmazenamentoFotos fotos)
    {
        _db = db; _cfg = cfg; _fotos = fotos;
    }

    private bool BibliotecaVeTudo => _cfg.GetValue("Avarias:BibliotecaVeTudo", true);

    /// <summary>Base de TODAS as consultas: só devolve as avarias que o utilizador pode ver.</summary>
    private IQueryable<Avaria> Visiveis()
    {
        var q = _db.Avarias.AsQueryable();
        var eu = User.UserId();
        return User.PerfilApp() switch
        {
            Perfil.Professor => q.Where(a => a.IdUtilizador == eu),
            Perfil.Biblioteca when !BibliotecaVeTudo =>
                q.Where(a => a.Equipamento != null && a.Equipamento.Sala != null && a.Equipamento.Sala.TipoSala.ToLower() == "biblioteca"),
            _ => q,
        };
    }

    // ------------------------------------------------------------------ listagem
    [HttpGet]
    public async Task<List<AvariaResumo>> Listar(
        [FromQuery] string? q, [FromQuery] Guid? estadoId, [FromQuery] string? nrSala, [FromQuery] Guid? tipoId)
    {
        var query = Visiveis().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var termo = q.Trim().ToLower();
            query = query.Where(a => a.DescGeral.ToLower().Contains(termo)
                || (a.Equipamento != null && a.Equipamento.DescEquipamento.ToLower().Contains(termo))
                || (a.Equipamento != null && a.Equipamento.NrSerie != null && a.Equipamento.NrSerie.ToLower().Contains(termo)));
        }
        if (estadoId is not null) query = query.Where(a => a.EstadoId == estadoId);
        if (!string.IsNullOrWhiteSpace(nrSala)) query = query.Where(a => a.Equipamento != null && a.Equipamento.NrSala == nrSala);
        if (tipoId is not null) query = query.Where(a => a.Equipamento != null && a.Equipamento.IdTipo == tipoId);

        var lista = await query
            .OrderByDescending(a => a.DataAvaria)
            .Take(500)
            .Select(a => new
            {
                a.Id, a.DescGeral, a.DataAvaria, TemFoto = a.FotoAvaria != null,
                Sala = a.Equipamento != null ? a.Equipamento.NrSala : null,
                Equipamento = a.Equipamento != null ? a.Equipamento.DescEquipamento : null,
                NrSerie = a.Equipamento != null ? a.Equipamento.NrSerie : null,
                Tipo = a.Equipamento != null && a.Equipamento.Tipo != null ? a.Equipamento.Tipo.DescTipo : null,
                a.Estado,
                Autor = a.Utilizador != null ? a.Utilizador.NomeUtilizador : null,
            })
            .ToListAsync();

        return lista.Select(a => new AvariaResumo(a.Id, a.DescGeral, a.Sala, a.Equipamento, a.NrSerie, a.Tipo,
            a.Estado is null ? null : CatalogosController.ParaDto(a.Estado), a.Autor, a.DataAvaria, a.TemFoto)).ToList();
    }

    /// <summary>Número de avarias por estado, dentro do âmbito do utilizador (cartões do painel).</summary>
    [HttpGet("contagens")]
    public async Task<List<ContagemDto>> Contagens()
    {
        var porEstado = await Visiveis().AsNoTracking()
            .GroupBy(a => a.EstadoId)
            .Select(g => new { g.Key, N = g.Count() })
            .ToListAsync();
        var estados = await _db.EstadosAvarias.AsNoTracking().ToListAsync();

        return CatalogosController.OrdenarEstados(estados)
            .Select(e => new ContagemDto(CatalogosController.ParaDto(e), porEstado.FirstOrDefault(p => p.Key == e.Id)?.N ?? 0))
            .ToList();
    }

    // ------------------------------------------------------------------ detalhe e fotografias
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AvariaDetalhe>> Obter(Guid id)
    {
        var a = await CarregarAsync(id);
        return a is null ? NaoEncontrada() : ParaDetalhe(a);
    }

    [HttpGet("{id:guid}/foto")]
    public Task<IActionResult> Foto(Guid id) => EnviarFotoAsync(id, resolucao: false);

    [HttpGet("{id:guid}/foto-resolucao")]
    public Task<IActionResult> FotoResolucao(Guid id) => EnviarFotoAsync(id, resolucao: true);

    private async Task<IActionResult> EnviarFotoAsync(Guid id, bool resolucao)
    {
        var ficheiro = await Visiveis().AsNoTracking().Where(a => a.Id == id)
            .Select(a => resolucao ? a.FotoResolucao : a.FotoAvaria).FirstOrDefaultAsync();
        var caminho = _fotos.Caminho(ficheiro);
        if (caminho is null) return NotFound(new { message = "A fotografia não está disponível." });
        return PhysicalFile(caminho, ArmazenamentoFotos.TipoMime(caminho));
    }

    // ------------------------------------------------------------------ registar
    [HttpPost]
    [Authorize(Roles = "Professor,Biblioteca")]
    [RequestSizeLimit(8_000_000)]
    public async Task<IActionResult> Criar([FromForm] CriarAvariaForm f)
    {
        var equipamento = await _db.Equipamentos.AsNoTracking().Include(e => e.Sala).Include(e => e.Tipo)
            .FirstOrDefaultAsync(e => e.Id == f.EquipamentoId);
        if (equipamento is null) return BadRequest(new { message = "O equipamento escolhido não existe." });

        if (User.PerfilApp() == Perfil.Biblioteca && !BibliotecaVeTudo && !Textos.EBiblioteca(equipamento.Sala?.TipoSala))
            return StatusCode(403, new { message = "Só pode registar avarias de equipamentos da biblioteca." });

        var estados = await _db.EstadosAvarias.AsNoTracking().ToListAsync();
        var pendente = estados.FirstOrDefault(e => Textos.Chave(e.DescEstado) == Textos.EstadoInicial);
        if (pendente is null)
            return StatusCode(500, new { message = "Falta o estado \"Pendente\" na tabela estados_avarias." });

        string? foto = null;
        if (f.Foto is { Length: > 0 })
        {
            var (ficheiro, erro) = await _fotos.GuardarAsync(f.Foto);
            if (erro is not null) return BadRequest(new { message = erro });
            foto = ficheiro;
        }

        var agora = DateTime.UtcNow;
        var avaria = new Avaria
        {
            Id = Guid.NewGuid(),
            DescGeral = f.Descricao.Trim(),
            IdUtilizador = User.UserId(),
            IdEquipamento = equipamento.Id,
            DataAvaria = agora,
            FotoAvaria = foto,
            EstadoId = pendente.Id,
            CreatedAt = agora,
        };
        _db.Avarias.Add(avaria);
        _db.HistoricoAvarias.Add(Copia(avaria, equipamento, pendente, "Avaria registada", agora));
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(Obter), new { id = avaria.Id }, new { id = avaria.Id });
    }

    // ------------------------------------------------------------------ atualizar (só técnico)
    [HttpPatch("{id:guid}")]
    [Authorize(Roles = nameof(Perfil.Tecnico))]
    [RequestSizeLimit(8_000_000)]
    public async Task<ActionResult<AvariaDetalhe>> Atualizar(Guid id, [FromForm] AtualizarAvariaForm f)
    {
        var a = await CarregarAsync(id, seguir: true);
        if (a is null) return NaoEncontrada();

        var agora = DateTime.UtcNow;
        var mudancas = new List<string>();

        if (f.EstadoId is { } novoId && novoId != a.EstadoId)
        {
            var novo = await _db.EstadosAvarias.FirstOrDefaultAsync(e => e.Id == novoId);
            if (novo is null) return BadRequest(new { message = "O estado escolhido não existe." });
            mudancas.Add($"Estado: {a.Estado?.DescEstado ?? "—"} → {novo.DescEstado}");
            a.EstadoId = novo.Id;
            a.Estado = novo;
            a.DataResolucao = Textos.EResolvido(novo.DescEstado) ? agora : null;
        }

        if (f.Resolucao is not null)
        {
            var nova = f.Resolucao.Trim();
            if (nova != (a.DescResolucao ?? ""))
            {
                mudancas.Add(string.IsNullOrEmpty(a.DescResolucao) ? "Resolução registada" : "Resolução atualizada");
                a.DescResolucao = nova.Length == 0 ? null : nova;
            }
        }

        if (f.FotoResolucao is { Length: > 0 })
        {
            var (ficheiro, erro) = await _fotos.GuardarAsync(f.FotoResolucao);
            if (erro is not null) return BadRequest(new { message = erro });
            a.FotoResolucao = ficheiro;
            mudancas.Add("Fotografia da resolução adicionada");
        }

        if (Textos.EResolvido(a.Estado?.DescEstado) && string.IsNullOrWhiteSpace(a.DescResolucao))
            return BadRequest(new { message = "Para marcar como resolvida, registe a resolução (o que foi feito)." });

        if (mudancas.Count > 0)
        {
            _db.HistoricoAvarias.Add(Copia(a, a.Equipamento, a.Estado, string.Join("; ", mudancas), agora));
            await _db.SaveChangesAsync();
        }

        return ParaDetalhe((await CarregarAsync(id))!);
    }

    // ------------------------------------------------------------------ auxiliares
    private Task<Avaria?> CarregarAsync(Guid id, bool seguir = false)
    {
        var q = Visiveis()
            .Include(a => a.Equipamento).ThenInclude(e => e!.Sala)
            .Include(a => a.Equipamento).ThenInclude(e => e!.Tipo)
            .Include(a => a.Estado)
            .Include(a => a.Utilizador)
            .Include(a => a.Historico).ThenInclude(h => h.Utilizador)
            .AsSplitQuery();
        return (seguir ? q : q.AsNoTracking()).FirstOrDefaultAsync(a => a.Id == id);
    }

    /// <summary>Cópia da avaria para historico_avarias, com os textos da altura.</summary>
    private HistoricoAvaria Copia(Avaria a, Equipamento? e, EstadoAvaria? estado, string detalhes, DateTime agora) => new()
    {
        Id = Guid.NewGuid(),
        AvariaId = a.Id,
        DescGeral = a.DescGeral,
        DescDetalhes = detalhes,
        IdUtilizador = User.UserId(),
        NrSerieEquipamento = e?.NrSerie,
        DescTipoEquipamento = e?.Tipo?.DescTipo,
        NrSala = e?.NrSala,
        DataAvaria = a.DataAvaria,
        FotoAvaria = a.FotoAvaria,
        DataResolucao = a.DataResolucao,
        DescResolucao = a.DescResolucao,
        FotoResolucao = a.FotoResolucao,
        DescEstado = estado?.DescEstado,
        DataRegisto = agora,
    };

    private NotFoundObjectResult NaoEncontrada() => NotFound(new { message = "Avaria não encontrada." });

    private static AvariaDetalhe ParaDetalhe(Avaria a) => new(
        a.Id, a.DescGeral, a.Equipamento?.NrSala, a.Equipamento?.Sala?.TipoSala, a.IdEquipamento,
        a.Equipamento?.DescEquipamento, a.Equipamento?.NrSerie, a.Equipamento?.Tipo?.DescTipo,
        a.Estado is null ? null : CatalogosController.ParaDto(a.Estado),
        a.Utilizador?.NomeUtilizador, a.DataAvaria, a.FotoAvaria is not null,
        a.DataResolucao, a.DescResolucao, a.FotoResolucao is not null,
        a.Historico.OrderBy(h => h.DataRegisto).ThenBy(h => h.Id)
            .Select(h => new HistoricoDto(h.Id, h.DescDetalhes, h.DescEstado, Textos.Chave(h.DescEstado),
                h.Utilizador?.NomeUtilizador, h.DataRegisto))
            .ToList());
}
