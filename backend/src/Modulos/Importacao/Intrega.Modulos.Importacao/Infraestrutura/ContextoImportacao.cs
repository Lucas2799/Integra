using System.Text.Json;
using Intrega.Infraestrutura.Persistencia;
using Intrega.Modulos.Importacao.Dominio;
using Intrega.Modulos.Importacao.Leitura;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Intrega.Modulos.Importacao.Infraestrutura;

internal sealed class ContextoImportacao(DbContextOptions<ContextoImportacao> opcoes) : ContextoDoModulo(opcoes)
{
    public const string NomeDoSchema = "importacao";
    protected override string Schema => NomeDoSchema;

    public DbSet<TrabalhoDeImportacao> Trabalhos => Set<TrabalhoDeImportacao>();
}

internal sealed class ConfiguracaoTrabalhoDeImportacao : IEntityTypeConfiguration<TrabalhoDeImportacao>
{
    public void Configure(EntityTypeBuilder<TrabalhoDeImportacao> b)
    {
        b.ToTable("trabalhos");
        b.HasKey(x => x.Id);
        b.Property(x => x.NomeDoArquivo).HasMaxLength(255);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.HasIndex(x => new { x.UsuarioId, x.CriadoEm });
        b.HasIndex(x => x.Status);
        b.OwnsMany(x => x.Erros, e => e.ToJson("erros"));

        // Linhas pendentes como JSON simples (são descartadas ao concluir).
        b.Property(x => x.LinhasPendentes)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<LinhaImportada>>(v, (JsonSerializerOptions?)null) ?? new List<LinhaImportada>(),
                new ValueComparer<List<LinhaImportada>>(
                    (a, c) => a!.Count == c!.Count && a.SequenceEqual(c),
                    v => v.Count,
                    v => v.ToList()));
    }
}
