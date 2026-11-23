using Avarias.Api.Data;
using Avarias.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Avarias.Api.Controllers;

/// <summary>Listas usadas nos formulários e filtros: locais (com equipamentos) e tipos de avaria.</summary>
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
        var locais = await _db.Locais.AsNoTracking()
            .Include(l => l.Equipamentos)
            .OrderBy(l => l.Nome)
            .ToListAsync();

        var tipos = await _db.TiposAvaria.AsNoTracking().OrderBy(t => t.Nome).ToListAsync();

        return new CatalogosDto(
            locais.Select(l => new LocalDto(l.Id, l.Nome, l.EBiblioteca,
                l.Equipamentos.OrderBy(e => e.Nome).Select(e => new EquipamentoDto(e.Id, e.Nome)).ToList())).ToList(),
            tipos.Select(t => new TipoDto(t.Id, t.Nome)).ToList());
    }
}
