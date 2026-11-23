using System.Security.Claims;
using Avarias.Api.Data;
using Avarias.Api.Dtos;
using Avarias.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Avarias.Api.Controllers;

/// <summary>
/// Reportes de avarias.
///
/// REGRAS DE ACESSO (aplicadas aqui, no servidor):
///  - Professor:  cria reportes; vê apenas os seus; comenta.
///  - Biblioteca: regista avarias e consulta pendentes e histórico (só leitura); comenta.
///                Por omissão vê todos os reportes (fluxo do README). Com
///                "Reportes:BibliotecaVeTudo": false fica limitada aos locais da biblioteca.
///  - Técnico:    vê tudo e é o único que altera estado, prioridade e resolução.
/// Um reporte fora do âmbito do utilizador responde 404 (não revela que existe).
/// </summary>
[ApiController]
[Route("api/reportes")]
[Authorize]
public class ReportesController : ControllerBase
{
    private const long TamanhoMaxFoto = 5 * 1024 * 1024;

    private readonly AppDbContext _db;
    private readonly IConfiguration _cfg;
    private readonly IWebHostEnvironment _env;

    public ReportesController(AppDbContext db, IConfiguration cfg, IWebHostEnvironment env)
    {
        _db = db; _cfg = cfg; _env = env;
    }

    private int UserId => int.Parse(User.FindFirstValue("sub")!);
    private bool BibliotecaVeTudo => _cfg.GetValue("Reportes:BibliotecaVeTudo", true);
    private string PastaUploads => Path.Combine(_env.ContentRootPath, _cfg["Uploads:Pasta"] ?? "uploads");

    /// <summary>Base de TODAS as consultas: só devolve os reportes que o utilizador pode ver.</summary>
    private IQueryable<Reporte> Visiveis()
    {
        var q = _db.Reportes.AsQueryable();

        if (User.IsInRole(nameof(Perfil.Professor)))
            return q.Where(r => r.AutorId == UserId);

        if (User.IsInRole(nameof(Perfil.Biblioteca)) && !BibliotecaVeTudo)
            return q.Where(r => r.Local.EBiblioteca);

        return q; // Técnico (e Biblioteca com acesso total)
    }

    // ------------------------------------------------------------------ listagem
    [HttpGet]
    public async Task<List<ReporteResumo>> Listar(
        [FromQuery] string? q, [FromQuery] Estado? estado, [FromQuery] int? localId,
        [FromQuery] int? tipoId, [FromQuery] Urgencia? urgencia, [FromQuery] Prioridade? prioridade)
    {
        var query = Visiveis().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var termo = "%" + q.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            query = query.Where(r => EF.Functions.ILike(r.Titulo, termo) || EF.Functions.ILike(r.Descricao, termo));
        }
        if (estado is not null) query = query.Where(r => r.Estado == estado);
        if (localId is not null) query = query.Where(r => r.LocalId == localId);
        if (tipoId is not null) query = query.Where(r => r.TipoId == tipoId);
        if (urgencia is not null) query = query.Where(r => r.Urgencia == urgencia);
        if (prioridade is not null) query = query.Where(r => r.Prioridade == prioridade);

        return await query
            .OrderByDescending(r => r.CriadoEm)
            .Take(500)
            .Select(r => new ReporteResumo(
                r.Id, r.Titulo, r.Local.Nome, r.Equipamento != null ? r.Equipamento.Nome : null, r.Tipo.Nome,
                r.Urgencia, r.Prioridade, r.Estado, r.Autor.Nome, r.CriadoEm, r.FotoFicheiro != null))
            .ToListAsync();
    }

    /// <summary>Número de reportes por estado, dentro do âmbito do utilizador (cartões do painel).</summary>
    [HttpGet("contagens")]
    public async Task<Dictionary<string, int>> Contagens()
    {
        var dados = await Visiveis().AsNoTracking()
            .GroupBy(r => r.Estado)
            .Select(g => new { g.Key, N = g.Count() })
            .ToListAsync();

        return Enum.GetValues<Estado>().ToDictionary(e => e.ToString(), e => dados.FirstOrDefault(d => d.Key == e)?.N ?? 0);
    }

    // ------------------------------------------------------------------ detalhe
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ReporteDetalhe>> Obter(int id)
    {
        var r = await CarregarAsync(id);
        return r is null ? NaoEncontrado() : ParaDetalhe(r);
    }

    [HttpGet("{id:int}/foto")]
    public async Task<IActionResult> Foto(int id)
    {
        var ficheiro = await Visiveis().AsNoTracking()
            .Where(r => r.Id == id).Select(r => r.FotoFicheiro).FirstOrDefaultAsync();

        if (ficheiro is null) return NotFound(new { message = "Este reporte não tem fotografia." });

        ficheiro = Path.GetFileName(ficheiro); // defesa contra caminhos
        var caminho = Path.Combine(PastaUploads, ficheiro);
        if (!System.IO.File.Exists(caminho)) return NotFound(new { message = "A fotografia já não está disponível." });

        var tipo = Path.GetExtension(ficheiro).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "image/jpeg",
        };
        return PhysicalFile(caminho, tipo);
    }

    // ------------------------------------------------------------------ criar
    [HttpPost]
    [Authorize(Roles = "Professor,Biblioteca")]
    [RequestSizeLimit(8_000_000)]
    public async Task<IActionResult> Criar([FromForm] CriarReporteForm f)
    {
        var local = await _db.Locais.AsNoTracking().FirstOrDefaultAsync(l => l.Id == f.LocalId);
        if (local is null) return BadRequest(new { message = "O local escolhido não existe." });

        if (User.IsInRole(nameof(Perfil.Biblioteca)) && !BibliotecaVeTudo && !local.EBiblioteca)
            return StatusCode(403, new { message = "Só pode reportar avarias de locais da biblioteca." });

        if (f.EquipamentoId is not null &&
            !await _db.Equipamentos.AnyAsync(e => e.Id == f.EquipamentoId && e.LocalId == f.LocalId))
            return BadRequest(new { message = "O equipamento não pertence ao local escolhido." });

        if (!await _db.TiposAvaria.AnyAsync(t => t.Id == f.TipoId))
            return BadRequest(new { message = "O tipo de avaria escolhido não existe." });

        // Foto opcional: validar tamanho e tipo real (pelos primeiros bytes, não pelo nome).
        string? ficheiro = null;
        if (f.Foto is { Length: > 0 })
        {
            if (f.Foto.Length > TamanhoMaxFoto)
                return BadRequest(new { message = "A fotografia é demasiado grande (máximo 5 MB)." });

            var ext = await DetetarExtensaoImagemAsync(f.Foto);
            if (ext is null)
                return BadRequest(new { message = "A fotografia deve ser uma imagem JPG, PNG ou WebP." });

            Directory.CreateDirectory(PastaUploads);
            ficheiro = Guid.NewGuid().ToString("N") + ext;
            await using var fs = System.IO.File.Create(Path.Combine(PastaUploads, ficheiro));
            await f.Foto.CopyToAsync(fs);
        }

        var agora = DateTime.UtcNow;
        var reporte = new Reporte
        {
            Titulo = f.Titulo.Trim(),
            Descricao = f.Descricao.Trim(),
            LocalId = f.LocalId,
            EquipamentoId = f.EquipamentoId,
            TipoId = f.TipoId,
            Urgencia = f.Urgencia,
            Estado = Estado.Pendente,
            FotoFicheiro = ficheiro,
            AutorId = UserId,
            CriadoEm = agora,
            AtualizadoEm = agora,
        };
        reporte.Historico.Add(new HistoricoReporte
        {
            UtilizadorId = UserId, Campo = "Criacao", ValorNovo = nameof(Estado.Pendente), Data = agora,
        });

        _db.Reportes.Add(reporte);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(Obter), new { id = reporte.Id }, new { id = reporte.Id });
    }

    // ------------------------------------------------------------------ atualizar (só técnico)
    [HttpPatch("{id:int}")]
    [Authorize(Roles = nameof(Perfil.Tecnico))]
    public async Task<ActionResult<ReporteDetalhe>> Atualizar(int id, AtualizarReporteRequest req)
    {
        var r = await CarregarAsync(id);
        if (r is null) return NaoEncontrado();

        var agora = DateTime.UtcNow;

        if (req.Estado is { } novoEstado && novoEstado != r.Estado)
        {
            r.Historico.Add(Registo("Estado", r.Estado.ToString(), novoEstado.ToString(), agora));
            r.Estado = novoEstado;
            r.ResolvidoEm = novoEstado == Estado.Resolvido ? agora : null;
        }

        if (req.Prioridade is { } novaPrioridade && novaPrioridade != r.Prioridade)
        {
            r.Historico.Add(Registo("Prioridade", r.Prioridade?.ToString(), novaPrioridade.ToString(), agora));
            r.Prioridade = novaPrioridade;
        }

        if (req.Resolucao is not null)
        {
            var nova = req.Resolucao.Trim();
            if (nova != (r.Resolucao ?? ""))
            {
                r.Historico.Add(Registo("Resolucao", Resumir(r.Resolucao), Resumir(nova), agora));
                r.Resolucao = nova.Length == 0 ? null : nova;
            }
        }

        if (r.Estado == Estado.Resolvido && string.IsNullOrWhiteSpace(r.Resolucao))
            return BadRequest(new { message = "Para marcar como resolvido, registe a resolução (o que foi feito)." });

        r.AtualizadoEm = agora;
        await _db.SaveChangesAsync();

        return ParaDetalhe((await CarregarAsync(id))!);
    }

    // ------------------------------------------------------------------ comentários
    [HttpPost("{id:int}/comentarios")]
    public async Task<ActionResult<ComentarioDto>> Comentar(int id, NovoComentarioRequest req)
    {
        // Só quem pode ver o reporte pode comentar.
        if (!await Visiveis().AnyAsync(r => r.Id == id)) return NaoEncontrado();

        var c = new Comentario { ReporteId = id, AutorId = UserId, Texto = req.Texto.Trim() };
        _db.Comentarios.Add(c);
        await _db.SaveChangesAsync();

        var autor = User.FindFirstValue("name") ?? "";
        return StatusCode(201, new ComentarioDto(c.Id, autor, c.Texto, c.CriadoEm));
    }

    // ------------------------------------------------------------------ auxiliares
    private Task<Reporte?> CarregarAsync(int id) =>
        Visiveis()
            .Include(r => r.Local).Include(r => r.Equipamento).Include(r => r.Tipo).Include(r => r.Autor)
            .Include(r => r.Historico).ThenInclude(h => h.Utilizador)
            .Include(r => r.Comentarios).ThenInclude(c => c.Autor)
            .AsSplitQuery()
            .FirstOrDefaultAsync(r => r.Id == id);

    private HistoricoReporte Registo(string campo, string? antes, string? depois, DateTime data) => new()
    {
        UtilizadorId = UserId, Campo = campo, ValorAnterior = antes, ValorNovo = depois, Data = data,
    };

    private static string? Resumir(string? t) =>
        string.IsNullOrEmpty(t) ? null : (t.Length <= 200 ? t : t[..200] + "…");

    private NotFoundObjectResult NaoEncontrado() => NotFound(new { message = "Reporte não encontrado." });

    private static ReporteDetalhe ParaDetalhe(Reporte r) => new(
        r.Id, r.Titulo, r.Descricao, r.Local.Nome, r.Equipamento?.Nome, r.Tipo.Nome,
        r.Urgencia, r.Prioridade, r.Estado, r.Autor.Nome,
        r.CriadoEm, r.AtualizadoEm, r.ResolvidoEm, r.Resolucao, r.FotoFicheiro is not null,
        r.Historico.OrderBy(h => h.Data).ThenBy(h => h.Id)
            .Select(h => new HistoricoDto(h.Id, h.Campo, h.ValorAnterior, h.ValorNovo, h.Utilizador.Nome, h.Data)).ToList(),
        r.Comentarios.OrderBy(c => c.CriadoEm)
            .Select(c => new ComentarioDto(c.Id, c.Autor.Nome, c.Texto, c.CriadoEm)).ToList());

    /// <summary>Identifica JPG, PNG e WebP pelos primeiros bytes do ficheiro.</summary>
    private static async Task<string?> DetetarExtensaoImagemAsync(IFormFile file)
    {
        var b = new byte[12];
        await using var s = file.OpenReadStream();
        var lidos = await s.ReadAsync(b, 0, b.Length);
        if (lidos < 12) return null;

        if (b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return ".jpg";
        if (b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47) return ".png";
        if (b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F' &&
            b[8] == 'W' && b[9] == 'E' && b[10] == 'B' && b[11] == 'P') return ".webp";
        return null;
    }
}
