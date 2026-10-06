using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Intrega.Infraestrutura.Persistencia;

public static class ExtensoesDePersistencia
{
    public const string NomeDaConexao = "Intrega";

    public static IServiceCollection AdicionarContextoDoModulo<TContexto>(
        this IServiceCollection servicos, IConfiguration configuracao, string schema)
        where TContexto : DbContext
    {
        var conexao = configuracao.GetConnectionString(NomeDaConexao)
            ?? throw new InvalidOperationException($"ConnectionStrings:{NomeDaConexao} não configurada.");

        servicos.AddDbContext<TContexto>(opcoes => opcoes
            .UseNpgsql(conexao, npgsql => npgsql
                .MigrationsHistoryTable("__historico_migrations", schema)
                .EnableRetryOnFailure(3)));
        return servicos;
    }

    /// <summary>Aplica as migrations do módulo se BancoDeDados:MigrarNaInicializacao = true.</summary>
    public static async Task MigrarSeHabilitadoAsync<TContexto>(this IServiceProvider servicos, CancellationToken ct)
        where TContexto : DbContext
    {
        var configuracao = servicos.GetRequiredService<IConfiguration>();
        if (!configuracao.GetValue("BancoDeDados:MigrarNaInicializacao", true)) return;
        await servicos.GetRequiredService<TContexto>().Database.MigrateAsync(ct);
    }
}
