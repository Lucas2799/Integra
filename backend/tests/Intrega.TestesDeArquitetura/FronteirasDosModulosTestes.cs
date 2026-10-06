using System.Reflection;

namespace Intrega.TestesDeArquitetura;

/// <summary>
/// Garante as regras do monólito modular. Se um destes testes falhar, algum módulo
/// passou a depender da implementação interna de outro: use o projeto .Contratos.
/// </summary>
public class FronteirasDosModulosTestes
{
    private static readonly string[] Modulos =
        ["Identidade", "Geocodificacao", "Paradas", "Roteirizacao", "Importacao", "Etiquetas", "Rastreamento"];

    private static IEnumerable<string> ReferenciasIntrega(string assembly) =>
        Assembly.Load(assembly).GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(n => n.StartsWith("Intrega.Modulos.", StringComparison.Ordinal));

    public static TheoryData<string> TodosOsModulos => new(Modulos);

    [Theory]
    [MemberData(nameof(TodosOsModulos))]
    public void Modulo_so_depende_dos_contratos_de_outros_modulos(string modulo)
    {
        var proibidas = ReferenciasIntrega($"Intrega.Modulos.{modulo}")
            .Where(r => !r.EndsWith(".Contratos", StringComparison.Ordinal))
            .ToList();

        Assert.True(proibidas.Count == 0, $"{modulo} referencia implementações de outros módulos: {string.Join(", ", proibidas)}");
    }

    [Theory]
    [MemberData(nameof(TodosOsModulos))]
    public void Contratos_nao_dependem_de_implementacoes(string modulo)
    {
        var nome = $"Intrega.Modulos.{modulo}.Contratos";
        if (!ExisteAssembly(nome)) return; // nem todo módulo expõe contratos

        var proibidas = ReferenciasIntrega(nome).Where(r => !r.EndsWith(".Contratos", StringComparison.Ordinal)).ToList();

        Assert.Empty(proibidas);
    }

    [Theory]
    [MemberData(nameof(TodosOsModulos))]
    public void Tipos_internos_nao_vazam_para_fora_do_modulo(string modulo)
    {
        // Entidades de domínio e DbContexts devem ser internal: só o próprio módulo os manipula.
        var publicos = Assembly.Load($"Intrega.Modulos.{modulo}").GetExportedTypes()
            .Where(t => t.Namespace?.EndsWith(".Dominio", StringComparison.Ordinal) == true && t.IsClass)
            .Where(t => !t.IsEnum)
            .Select(t => t.FullName)
            .ToList();

        Assert.Empty(publicos);
    }

    private static bool ExisteAssembly(string nome)
    {
        try
        {
            Assembly.Load(nome);
            return true;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
    }
}
