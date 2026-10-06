using Intrega.Infraestrutura.Persistencia;
using Intrega.Modulos.Identidade.Dominio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Intrega.Modulos.Identidade.Infraestrutura;

internal sealed class ContextoIdentidade(DbContextOptions<ContextoIdentidade> opcoes) : ContextoDoModulo(opcoes)
{
    public const string NomeDoSchema = "identidade";
    protected override string Schema => NomeDoSchema;

    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Organizacao> Organizacoes => Set<Organizacao>();
    public DbSet<TokenDeAtualizacao> TokensDeAtualizacao => Set<TokenDeAtualizacao>();
    public DbSet<RegistroDeUso> RegistrosDeUso => Set<RegistroDeUso>();
    public DbSet<Compra> Compras => Set<Compra>();
}

internal sealed class ConfiguracaoUsuario : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> b)
    {
        b.ToTable("usuarios");
        b.HasKey(x => x.Id);
        b.Property(x => x.Nome).HasMaxLength(120).IsRequired();
        b.Property(x => x.Email).HasMaxLength(254).IsRequired();
        b.HasIndex(x => x.Email).IsUnique();
        b.Property(x => x.Telefone).HasMaxLength(30);
        b.Property(x => x.HashDaSenha).IsRequired();
        b.Property(x => x.Plano).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.PapelNaOrganizacao).HasMaxLength(20);
        b.HasIndex(x => x.OrganizacaoId);
    }
}

internal sealed class ConfiguracaoOrganizacao : IEntityTypeConfiguration<Organizacao>
{
    public void Configure(EntityTypeBuilder<Organizacao> b)
    {
        b.ToTable("organizacoes");
        b.HasKey(x => x.Id);
        b.Property(x => x.Nome).HasMaxLength(120).IsRequired();
        b.Property(x => x.CodigoDeConvite).HasMaxLength(16).IsRequired();
        b.HasIndex(x => x.CodigoDeConvite).IsUnique();
    }
}

internal sealed class ConfiguracaoTokenDeAtualizacao : IEntityTypeConfiguration<TokenDeAtualizacao>
{
    public void Configure(EntityTypeBuilder<TokenDeAtualizacao> b)
    {
        b.ToTable("tokens_de_atualizacao");
        b.HasKey(x => x.Id);
        b.Property(x => x.HashDoToken).HasMaxLength(64).IsRequired();
        b.HasIndex(x => x.HashDoToken).IsUnique();
        b.HasIndex(x => x.UsuarioId);
    }
}

internal sealed class ConfiguracaoRegistroDeUso : IEntityTypeConfiguration<RegistroDeUso>
{
    public void Configure(EntityTypeBuilder<RegistroDeUso> b)
    {
        b.ToTable("registros_de_uso");
        b.HasKey(x => new { x.UsuarioId, x.Recurso, x.Dia });
        b.Property(x => x.Recurso).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Quantidade).IsConcurrencyToken();
    }
}

internal sealed class ConfiguracaoCompra : IEntityTypeConfiguration<Compra>
{
    public void Configure(EntityTypeBuilder<Compra> b)
    {
        b.ToTable("compras");
        b.HasKey(x => x.Id);
        b.Property(x => x.Plataforma).HasMaxLength(20);
        b.Property(x => x.ProdutoId).HasMaxLength(100);
        b.Property(x => x.HashDoComprovante).HasMaxLength(64);
        b.HasIndex(x => x.HashDoComprovante).IsUnique();
        b.Property(x => x.Plano).HasConversion<string>().HasMaxLength(20);
    }
}
