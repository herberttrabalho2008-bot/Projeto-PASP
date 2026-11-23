using Avarias.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Avarias.Api.Data;

/// <summary>
/// Contexto EF Core mapeado para as tabelas que já existem no Supabase.
/// A API NÃO cria nem altera o esquema: as mudanças fazem-se no Supabase
/// e depois atualiza-se este mapeamento (ver docs/base-de-dados.md).
/// Os nomes das colunas em snake_case vêm de UseSnakeCaseNamingConvention;
/// os que não coincidem com o nome da propriedade estão indicados abaixo.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Utilizador> Utilizadores => Set<Utilizador>();
    public DbSet<TipoUtilizador> TiposUtilizador => Set<TipoUtilizador>();
    public DbSet<Sala> Salas => Set<Sala>();
    public DbSet<TipoEquipamento> TiposEquipamento => Set<TipoEquipamento>();
    public DbSet<Equipamento> Equipamentos => Set<Equipamento>();
    public DbSet<EstadoAvaria> EstadosAvarias => Set<EstadoAvaria>();
    public DbSet<Avaria> Avarias => Set<Avaria>();
    public DbSet<HistoricoAvaria> HistoricoAvarias => Set<HistoricoAvaria>();
    /// <summary>Só leitura (auth.users).</summary>
    public DbSet<ContaAuth> ContasAuth => Set<ContaAuth>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Utilizador>(e =>
        {
            e.ToTable("utilizadores");
            e.HasOne(x => x.Permissao).WithMany().HasForeignKey(x => x.PermissaoId);
        });

        b.Entity<TipoUtilizador>(e =>
        {
            e.ToTable("tipos_utilizador");
            e.Property(x => x.Descricao).HasColumnName("tipo_utilizador");
        });

        b.Entity<Sala>(e =>
        {
            e.ToTable("salas");
            e.HasKey(x => x.NrSala);
        });

        b.Entity<TipoEquipamento>().ToTable("tipos_equipamento");

        b.Entity<Equipamento>(e =>
        {
            e.ToTable("equipamentos");
            e.HasOne(x => x.Sala).WithMany(s => s.Equipamentos).HasForeignKey(x => x.NrSala);
            e.HasOne(x => x.Tipo).WithMany().HasForeignKey(x => x.IdTipo);
        });

        b.Entity<EstadoAvaria>().ToTable("estados_avarias");

        b.Entity<Avaria>(e =>
        {
            e.ToTable("avarias");
            e.HasOne(x => x.Utilizador).WithMany().HasForeignKey(x => x.IdUtilizador);
            e.HasOne(x => x.Equipamento).WithMany().HasForeignKey(x => x.IdEquipamento);
            e.HasOne(x => x.Estado).WithMany().HasForeignKey(x => x.EstadoId);
        });

        b.Entity<HistoricoAvaria>(e =>
        {
            e.ToTable("historico_avarias");
            e.HasOne<Avaria>().WithMany(a => a.Historico).HasForeignKey(x => x.AvariaId);
            e.HasOne(x => x.Utilizador).WithMany().HasForeignKey(x => x.IdUtilizador);
        });

        b.Entity<ContaAuth>(e =>
        {
            e.ToTable("users", "auth", t => t.ExcludeFromMigrations());
        });
    }
}
