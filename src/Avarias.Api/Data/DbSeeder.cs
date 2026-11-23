using Avarias.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Avarias.Api.Data;

/// <summary>
/// Verifica no arranque os valores de que a aplicação depende:
///  - avisa (não escreve) se faltar algum perfil em tipos_utilizador;
///  - cria os estados base só se estados_avarias estiver vazia (sem estados nada funciona).
/// Salas, equipamentos e contas gerem-se no Backoffice.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, ILogger log)
    {
        var perfis = await db.TiposUtilizador.Select(t => t.Descricao).ToListAsync();
        var faltam = Enum.GetValues<Perfil>().Except(perfis.Select(Textos.PerfilDe).OfType<Perfil>()).ToList();
        if (faltam.Count > 0)
            log.LogWarning("Faltam perfis em tipos_utilizador: {Faltam}. Existentes: {Perfis}",
                string.Join(", ", faltam), string.Join(", ", perfis));

        var estados = await db.EstadosAvarias.Select(e => e.DescEstado).ToListAsync();
        if (estados.Count == 0)
        {
            db.EstadosAvarias.AddRange(Textos.EstadosBase.Select(e => new EstadoAvaria { Id = Guid.NewGuid(), DescEstado = e }));
            log.LogInformation("Criados os estados base em estados_avarias.");
        }
        else
        {
            var chaves = estados.Select(Textos.Chave).ToHashSet();
            if (!chaves.Contains(Textos.EstadoInicial) || !estados.Any(Textos.EResolvido))
                log.LogWarning("A tabela estados_avarias deve ter os estados \"Pendente\" e \"Resolvida\". Existentes: {Estados}",
                    string.Join(", ", estados));
        }

        await db.SaveChangesAsync();
    }
}
