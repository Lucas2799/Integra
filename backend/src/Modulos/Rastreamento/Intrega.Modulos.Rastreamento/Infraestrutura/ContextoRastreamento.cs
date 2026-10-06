using Intrega.Infraestrutura.Persistencia;
using Intrega.Modulos.Rastreamento.Dominio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Intrega.Modulos.Rastreamento.Infraestrutura;

internal sealed class ContextoRastreamento(DbContextOptions<ContextoRastreamento> opcoes) : ContextoDoModulo(opcoes)
{
    public const string NomeDoSchema = "rastreamento";
    protected override string Schema => NomeDoSchema;

    public DbSet<AmostraDePosicao> Posicoes => Set<AmostraDePosicao>();
    public DbSet<RegistroDeEntrega> RegistrosDeEntrega => Set<RegistroDeEntrega>();
    public DbSet<ConfiguracaoFinanceira> ConfiguracoesFinanceiras => Set<ConfiguracaoFinanceira>();
}

internal sealed class ConfiguracaoAmostraDePosicao : IEntityTypeConfiguration<AmostraDePosicao>
{
    public void Configure(EntityTypeBuilder<AmostraDePosicao> b)
    {
        b.ToTable("posicoes");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).UseIdentityAlwaysColumn();
        b.HasIndex(x => new { x.UsuarioId, x.RegistradaEm });
        b.HasIndex(x => x.RegistradaEm);
    }
}

internal sealed class ConfiguracaoRegistroDeEntrega : IEntityTypeConfiguration<RegistroDeEntrega>
{
    public void Configure(EntityTypeBuilder<RegistroDeEntrega> b)
    {
        b.ToTable("registros_de_entrega");
        b.HasKey(x => x.Id);
        b.Property(x => x.Resultado).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Motivo).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Marketplace).HasConversion<string>().HasMaxLength(20);
        b.HasIndex(x => new { x.UsuarioId, x.OcorridoEm });
    }
}

internal sealed class ConfiguracaoConfiguracaoFinanceira : IEntityTypeConfiguration<ConfiguracaoFinanceira>
{
    public void Configure(EntityTypeBuilder<ConfiguracaoFinanceira> b)
    {
        b.ToTable("configuracoes_financeiras");
        b.HasKey(x => x.UsuarioId);
        foreach (var p in new[] { "ValorPorEntrega", "ValorPorPacote", "PrecoDoCombustivelPorLitro", "KmPorLitro", "CustoFixoPorDia" })
            b.Property(p).HasPrecision(10, 2);
    }
}

/// <summary>Seção "Rastreamento" do appsettings.</summary>
public sealed class OpcoesDeRastreamento
{
    public const string Secao = "Rastreamento";

    /// <summary>LGPD: posições GPS são apagadas depois deste prazo.</summary>
    public int DiasDeRetencaoDasPosicoes { get; set; } = 30;
}
