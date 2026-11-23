using Avarias.Api.Data;
using Avarias.Api.Dtos;
using Avarias.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Avarias.Api.Controllers;

/// <summary>Listas usadas nos formulários e filtros: salas com equipamentos, tipos de equipamento e estados.</summary>
[ApiController]
[Route("api/catalogos")]
[Authorize]
public class CatalogosController : ControllerBase
{
    private readonly AppDbContext _db;
    public CatalogosController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<CatalogosDto> Obter()
    {
        var salas = await _db.Salas.AsNoTracking()
            .Include(s => s.Equipamentos).ThenInclude(e => e.Tipo)
            .OrderBy(s => s.NrSala)
            .ToListAsync();
        var tipos = await _db.TiposEquipamento.AsNoTracking().OrderBy(t => t.DescTipo).ToListAsync();
        var estados = await _db.EstadosAvarias.AsNoTracking().ToListAsync();

        return new CatalogosDto(
            salas.Select(s => new SalaDto(s.NrSala, s.TipoSala, Textos.EBiblioteca(s.TipoSala),
                s.Equipamentos.OrderBy(e => e.DescEquipamento).Select(ParaDto).ToList())).ToList(),
            tipos.Select(t => new TipoEquipamentoDto(t.Id, t.DescTipo)).ToList(),
            OrdenarEstados(estados).Select(ParaDto).ToList());
    }

    public static EquipamentoDto ParaDto(Equipamento e) =>
        new(e.Id, e.DescEquipamento, e.NrSerie, e.IdTipo, e.Tipo?.DescTipo);

    public static EstadoDto ParaDto(EstadoAvaria e) =>
        new(e.Id, e.DescEstado, Textos.Chave(e.DescEstado), Textos.EResolvido(e.DescEstado));

    /// <summary>Pela ordem de Textos.OrdemEstados; outros estados depois, por nome.</summary>
    public static IEnumerable<EstadoAvaria> OrdenarEstados(IEnumerable<EstadoAvaria> estados)
    {
        var ordem = Textos.OrdemEstados.ToList();
        return estados
            .OrderBy(e => ordem.IndexOf(Textos.Chave(e.DescEstado)) is var i && i >= 0 ? i : int.MaxValue)
            .ThenBy(e => e.DescEstado);
    }
}
