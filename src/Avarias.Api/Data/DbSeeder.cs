using Avarias.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Avarias.Api.Data;

/// <summary>
/// Garante os valores de que a aplicação depende, sem tocar no resto dos dados:
///  - os três perfis em tipos_utilizador (só acrescenta os que faltam);
///  - os estados em estados_avarias (só se a tabela estiver vazia).
/// Salas, equipamentos e contas gerem-se no Backoffice.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, ILogger log)
    {
        var perfisExistentes = (await db.TiposUtilizador.Select(t => t.Descricao).ToListAsync())
            .Select(Textos.PerfilDe).ToHashSet();
        foreach (var p in Enum.GetValues<Perfil>().Where(p => !perfisExistentes.Contains(p)))
        {
            db.TiposUtilizador.Add(new TipoUtilizador { Id = Guid.NewGuid(), Descricao = Textos.TextoPerfil(p) });
            log.LogInformation("Criado o perfil {Perfil} em tipos_utilizador.", Textos.TextoPerfil(p));
        }

        var estados = await db.EstadosAvarias.Select(e => e.DescEstado).ToListAsync();
        if (estados.Count == 0)
        {
            db.EstadosAvarias.AddRange(Textos.EstadosBase.Select(e => new EstadoAvaria { Id = Guid.NewGuid(), DescEstado = e }));
            log.LogInformation("Criados os estados base em estados_avarias.");
        }
        else
        {
            var chaves = estados.Select(Textos.Chave).ToHashSet();
            if (!chaves.Contains(Textos.EstadoInicial) || !chaves.Contains(Textos.EstadoResolvido))
                log.LogWarning("A tabela estados_avarias deve ter os estados \"Pendente\" e \"Resolvido\". Existentes: {Estados}",
                    string.Join(", ", estados));
        }

        await db.SaveChangesAsync();
    }
}
