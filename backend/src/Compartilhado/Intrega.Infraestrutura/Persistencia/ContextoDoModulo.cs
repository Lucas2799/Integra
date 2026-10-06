using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Intrega.Infraestrutura.Persistencia;

/// <summary>
/// Cada módulo tem seu próprio DbContext e schema no mesmo PostgreSQL.
/// Nenhum módulo lê tabelas de outro: a comunicação passa pelos projetos .Contratos.
/// </summary>
public abstract class ContextoDoModulo(DbContextOptions opcoes) : DbContext(opcoes)
{
    protected abstract string Schema { get; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);
        base.OnModelCreating(modelBuilder);
    }

    /// <summary>
    /// O PostgreSQL só aceita datas em UTC. Horários de Brasília (-03:00) vindos do app ou
    /// calculados no servidor são convertidos automaticamente ao gravar e ao filtrar.
    /// </summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder configuracao)
    {
        configuracao.Properties<DateTimeOffset>().HaveConversion<ConversorParaUtc>();
        configuracao.Properties<DateTimeOffset?>().HaveConversion<ConversorParaUtc>();
    }

    private sealed class ConversorParaUtc() : ValueConverter<DateTimeOffset, DateTimeOffset>(
        valor => valor.ToUniversalTime(),
        valor => valor);
}
