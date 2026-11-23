using Avarias.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Avarias.Api.Data;

/// <summary>
/// Cria os dados iniciais se a base de dados estiver vazia:
/// tipos de avaria, locais com equipamentos e um utilizador de teste por perfil.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, bool criarUtilizadoresTeste)
    {
        if (!await db.TiposAvaria.AnyAsync())
        {
            db.TiposAvaria.AddRange(
                new TipoAvaria { Nome = "Computador / informática" },
                new TipoAvaria { Nome = "Projetor / quadro interativo" },
                new TipoAvaria { Nome = "Rede / internet" },
                new TipoAvaria { Nome = "Eletricidade / iluminação" },
                new TipoAvaria { Nome = "Mobiliário" },
                new TipoAvaria { Nome = "Climatização" },
                new TipoAvaria { Nome = "Outro" });
        }

        if (!await db.Locais.AnyAsync())
        {
            var nomesEquip = new[] { "Computador", "Projetor", "Quadro interativo", "Colunas de som" };
            var salas = new[] { "Sala 101", "Sala 102", "Sala 201", "Laboratório de Informática", "Sala de Professores" };

            foreach (var s in salas)
            {
                var local = new Local { Nome = s };
                local.Equipamentos.AddRange(nomesEquip.Select(n => new Equipamento { Nome = n }));
                db.Locais.Add(local);
            }

            var biblioteca = new Local { Nome = "Biblioteca", EBiblioteca = true };
            biblioteca.Equipamentos.AddRange(new[]
            {
                new Equipamento { Nome = "Computador de utilização livre" },
                new Equipamento { Nome = "Impressora" },
                new Equipamento { Nome = "Digitalizador" },
                new Equipamento { Nome = "Projetor" },
            });
            db.Locais.Add(biblioteca);
        }

        if (criarUtilizadoresTeste && !await db.Utilizadores.AnyAsync())
        {
            // Apenas para desenvolvimento. Desligar com "SeedUtilizadoresTeste": false.
            db.Utilizadores.AddRange(
                Novo("Prof. Ana Silva", "professor@escola.test", "Professor123!", Perfil.Professor),
                Novo("Biblioteca Escolar", "biblioteca@escola.test", "Biblioteca123!", Perfil.Biblioteca),
                Novo("Técnico Rui Costa", "tecnico@escola.test", "Tecnico123!", Perfil.Tecnico));
        }

        await db.SaveChangesAsync();
    }

    private static Utilizador Novo(string nome, string email, string password, Perfil perfil) => new()
    {
        Nome = nome,
        Email = email,
        Perfil = perfil,
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 11),
    };
}
