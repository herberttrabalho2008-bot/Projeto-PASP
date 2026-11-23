using Avarias.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Avarias.Api.Data;

/// <summary>
/// Contexto EF Core. Os nomes das tabelas vêm dos nomes das propriedades DbSet
/// convertidos para snake_case (ver UseSnakeCaseNamingConvention em Program.cs).
/// Este modelo é a única definição do esquema: a API cria as tabelas no arranque.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Utilizador> Utilizadores => Set<Utilizador>();
    public DbSet<Local> Locais => Set<Local>();
    public DbSet<Equipamento> Equipamentos => Set<Equipamento>();
    public DbSet<TipoAvaria> TiposAvaria => Set<TipoAvaria>();
    public DbSet<Reporte> Reportes => Set<Reporte>();
    public DbSet<HistoricoReporte> HistoricoReportes => Set<HistoricoReporte>();
    public DbSet<Comentario> Comentarios => Set<Comentario>();
    public DbSet<TokenRecuperacao> TokensRecuperacao => Set<TokenRecuperacao>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Utilizador>(e =>
        {
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Perfil).HasConversion<string>();
            e.ToTable(t =>
            {
                // O login procura o email em minúsculas: uma conta com maiúsculas nunca conseguiria entrar.
                t.HasCheckConstraint("ck_utilizadores_email_minusculas", "email = lower(email)");
                t.HasCheckConstraint("ck_utilizadores_perfil", "perfil in ('Professor', 'Biblioteca', 'Tecnico')");
            });
        });

        b.Entity<Local>().HasIndex(x => x.Nome).IsUnique();
        b.Entity<TipoAvaria>().HasIndex(x => x.Nome).IsUnique();

        b.Entity<Reporte>(e =>
        {
            e.Property(x => x.Urgencia).HasConversion<string>();
            e.Property(x => x.Prioridade).HasConversion<string>();
            e.Property(x => x.Estado).HasConversion<string>();
            e.HasOne(x => x.Autor).WithMany().HasForeignKey(x => x.AutorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Local).WithMany().HasForeignKey(x => x.LocalId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Tipo).WithMany().HasForeignKey(x => x.TipoId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Equipamento).WithMany().HasForeignKey(x => x.EquipamentoId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.AutorId);
            e.HasIndex(x => x.Estado);
            e.HasIndex(x => x.LocalId);
            e.HasIndex(x => x.TipoId);
        });

        b.Entity<HistoricoReporte>(e =>
        {
            e.HasOne<Reporte>().WithMany(r => r.Historico).HasForeignKey(x => x.ReporteId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Utilizador).WithMany().HasForeignKey(x => x.UtilizadorId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Comentario>(e =>
        {
            e.HasOne<Reporte>().WithMany(r => r.Comentarios).HasForeignKey(x => x.ReporteId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Autor).WithMany().HasForeignKey(x => x.AutorId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<TokenRecuperacao>(e =>
        {
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasOne<Utilizador>().WithMany().HasForeignKey(x => x.UtilizadorId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
