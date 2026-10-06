using Intrega.Infraestrutura.Persistencia;
using Intrega.Modulos.Geocodificacao.Dominio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Intrega.Modulos.Geocodificacao.Infraestrutura;

internal sealed class ContextoGeocodificacao(DbContextOptions<ContextoGeocodificacao> opcoes) : ContextoDoModulo(opcoes)
{
    public const string NomeDoSchema = "geocodificacao";
    protected override string Schema => NomeDoSchema;

    public DbSet<EnderecoEmCache> Enderecos => Set<EnderecoEmCache>();
    public DbSet<CepEmCache> Ceps => Set<CepEmCache>();
}

internal sealed class ConfiguracaoEnderecoEmCache : IEntityTypeConfiguration<EnderecoEmCache>
{
    public void Configure(EntityTypeBuilder<EnderecoEmCache> b)
    {
        b.ToTable("enderecos");
        b.HasKey(x => x.Chave);
        b.Property(x => x.Chave).HasMaxLength(500);
        b.Property(x => x.Provedor).HasMaxLength(30);
        b.Property(x => x.Descricao).HasMaxLength(500);
        b.Property(x => x.Uf).HasMaxLength(2);
        b.Property(x => x.Cep).HasMaxLength(8);
    }
}

internal sealed class ConfiguracaoCepEmCache : IEntityTypeConfiguration<CepEmCache>
{
    public void Configure(EntityTypeBuilder<CepEmCache> b)
    {
        b.ToTable("ceps");
        b.HasKey(x => x.Cep);
        b.Property(x => x.Cep).HasMaxLength(8);
        b.Property(x => x.Provedor).HasMaxLength(30);
        b.Property(x => x.Uf).HasMaxLength(2);
    }
}
