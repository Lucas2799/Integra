using Intrega.Infraestrutura.Persistencia;
using Intrega.Modulos.Paradas.Dominio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Intrega.Modulos.Paradas.Infraestrutura;

internal sealed class ContextoParadas(DbContextOptions<ContextoParadas> opcoes) : ContextoDoModulo(opcoes)
{
    public const string NomeDoSchema = "paradas";
    protected override string Schema => NomeDoSchema;

    public DbSet<Parada> Paradas => Set<Parada>();
}

internal sealed class ConfiguracaoParada : IEntityTypeConfiguration<Parada>
{
    public void Configure(EntityTypeBuilder<Parada> b)
    {
        b.ToTable("paradas");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.RotaId);
        b.HasIndex(x => new { x.ResponsavelId, x.Status });
        b.HasIndex(x => new { x.OrganizacaoId, x.Status });
        // Índice GIN para achar rápido uma parada pelo código do pacote (leitura de etiqueta).
        b.HasIndex(x => x.CodigosDePacote).HasMethod("gin");

        b.OwnsOne(x => x.Endereco, e =>
        {
            e.Property(p => p.Logradouro).HasColumnName("logradouro").HasMaxLength(200);
            e.Property(p => p.Numero).HasColumnName("numero").HasMaxLength(20);
            e.Property(p => p.Complemento).HasColumnName("complemento").HasMaxLength(120);
            e.Property(p => p.Bairro).HasColumnName("bairro").HasMaxLength(120);
            e.Property(p => p.Cidade).HasColumnName("cidade").HasMaxLength(120);
            e.Property(p => p.Uf).HasColumnName("uf").HasMaxLength(2);
            e.Property(p => p.Cep).HasColumnName("cep").HasMaxLength(8);
            e.Property(p => p.Descricao).HasColumnName("descricao").HasMaxLength(500);
        });

        b.Property(x => x.ProvedorDaLocalizacao).HasMaxLength(30);
        b.Property(x => x.Marketplace).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Origem).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.UltimoMotivoDeFalha).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.EstrategiaDeNovaTentativa).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.CaminhoDoComprovante).HasMaxLength(300);
        b.Ignore(x => x.Local);
        b.Ignore(x => x.PrecisaConfirmarLocal);
    }
}
