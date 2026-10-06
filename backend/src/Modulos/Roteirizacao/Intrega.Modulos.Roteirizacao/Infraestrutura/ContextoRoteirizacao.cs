using Intrega.Infraestrutura.Persistencia;
using Intrega.Modulos.Roteirizacao.Dominio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Intrega.Modulos.Roteirizacao.Infraestrutura;

internal sealed class ContextoRoteirizacao(DbContextOptions<ContextoRoteirizacao> opcoes) : ContextoDoModulo(opcoes)
{
    public const string NomeDoSchema = "roteirizacao";
    protected override string Schema => NomeDoSchema;

    public DbSet<Rota> Rotas => Set<Rota>();
}

internal sealed class ConfiguracaoRota : IEntityTypeConfiguration<Rota>
{
    public void Configure(EntityTypeBuilder<Rota> b)
    {
        b.ToTable("rotas");
        b.HasKey(x => x.Id);
        b.Property(x => x.Nome).HasMaxLength(120).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Veiculo).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.DescricaoDaSaida).HasMaxLength(300);
        b.Property(x => x.DescricaoDaChegada).HasMaxLength(300);
        b.Property(x => x.AlgoritmoUsado).HasMaxLength(30);
        b.Property(x => x.MotorUsado).HasMaxLength(30);
        // Controle de concorrência pelo xmin do PostgreSQL.
        b.Property(x => x.Versao).IsRowVersion();
        b.HasIndex(x => new { x.EntregadorId, x.Data });
        b.HasIndex(x => new { x.DonoId, x.Data });
        b.HasIndex(x => new { x.OrganizacaoId, x.Data });
        b.Ignore(x => x.Saida);
        b.Ignore(x => x.Chegada);

        // A sequência é gravada como JSON: sempre lida e salva junto com a rota.
        b.OwnsMany(x => x.Sequencia, s => s.ToJson("sequencia"));
    }
}
