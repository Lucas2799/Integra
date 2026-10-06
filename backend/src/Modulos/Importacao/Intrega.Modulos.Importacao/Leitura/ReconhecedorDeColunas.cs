using System.Globalization;
using System.Text;

namespace Intrega.Modulos.Importacao.Leitura;

/// <summary>Descobre o significado de cada coluna pelo cabeçalho (português ou inglês, com ou sem acento).</summary>
internal static class ReconhecedorDeColunas
{
    private static readonly (CampoDaPlanilha Campo, string[] Nomes)[] Sinonimos =
    [
        (CampoDaPlanilha.EnderecoCompleto, ["endereco completo", "endereco entrega", "endereco de entrega", "full address", "address", "destino"]),
        (CampoDaPlanilha.Logradouro, ["logradouro", "rua", "street", "endereco", "end", "avenida"]),
        (CampoDaPlanilha.Numero, ["numero", "num", "nro", "no", "n", "number", "nº"]),
        (CampoDaPlanilha.Complemento, ["complemento", "compl", "apto", "apartamento", "bloco", "complement"]),
        (CampoDaPlanilha.Bairro, ["bairro", "neighborhood", "district", "distrito"]),
        (CampoDaPlanilha.Cidade, ["cidade", "municipio", "city", "localidade"]),
        (CampoDaPlanilha.Uf, ["uf", "estado", "state"]),
        (CampoDaPlanilha.Cep, ["cep", "zip", "codigo postal", "postal code", "zipcode"]),
        (CampoDaPlanilha.NomeDoDestinatario, ["destinatario", "nome", "cliente", "recebedor", "recipient", "name", "nome destinatario", "comprador"]),
        (CampoDaPlanilha.Telefone, ["telefone", "celular", "fone", "whatsapp", "phone", "contato", "tel"]),
        (CampoDaPlanilha.Observacoes, ["observacao", "observacoes", "obs", "notas", "referencia", "ponto de referencia", "notes", "instrucoes"]),
        (CampoDaPlanilha.CodigoDoPacote, ["codigo", "codigo rastreio", "codigo de rastreio", "rastreio", "rastreamento", "tracking", "pedido", "id pacote", "pacote", "order", "order id", "id envio", "envio", "etiqueta"]),
        (CampoDaPlanilha.Marketplace, ["marketplace", "loja", "plataforma", "origem", "canal"]),
        (CampoDaPlanilha.JanelaInicio, ["janela inicio", "inicio janela", "horario inicio", "hora inicio", "de", "a partir de"]),
        (CampoDaPlanilha.JanelaFim, ["janela fim", "fim janela", "horario fim", "hora fim", "ate", "limite"]),
        (CampoDaPlanilha.Latitude, ["latitude", "lat"]),
        (CampoDaPlanilha.Longitude, ["longitude", "lng", "lon", "long"])
    ];

    public static string Normalizar(string cabecalho)
    {
        var decomposto = cabecalho.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in decomposto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.IsLetterOrDigit(c) || c == 'º' ? c : ' ');
        }
        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>Associa cada coluna a no máximo um campo; cada campo é usado uma vez (a primeira coluna vence).</summary>
    public static Dictionary<int, CampoDaPlanilha> Reconhecer(IReadOnlyList<string> cabecalhos)
    {
        var resultado = new Dictionary<int, CampoDaPlanilha>();
        var usados = new HashSet<CampoDaPlanilha>();
        var normalizados = cabecalhos.Select(Normalizar).ToList();

        // 1ª passada: nome exato. 2ª passada: o cabeçalho começa com o sinônimo ("cep destino").
        foreach (var exato in new[] { true, false })
        {
            for (var i = 0; i < normalizados.Count; i++)
            {
                if (resultado.ContainsKey(i) || normalizados[i].Length == 0) continue;
                foreach (var (campo, nomes) in Sinonimos)
                {
                    if (usados.Contains(campo)) continue;
                    var bate = exato
                        ? nomes.Contains(normalizados[i])
                        : nomes.Any(n => n.Length >= 3 && normalizados[i].StartsWith(n + " ", StringComparison.Ordinal));
                    if (!bate) continue;
                    resultado[i] = campo;
                    usados.Add(campo);
                    break;
                }
            }
        }

        // "Endereço" sem coluna de número costuma ser o endereço completo numa célula só.
        if (usados.Contains(CampoDaPlanilha.Logradouro) && !usados.Contains(CampoDaPlanilha.Numero) &&
            !usados.Contains(CampoDaPlanilha.EnderecoCompleto))
        {
            var coluna = resultado.Single(kv => kv.Value == CampoDaPlanilha.Logradouro).Key;
            resultado[coluna] = CampoDaPlanilha.EnderecoCompleto;
        }

        return resultado;
    }
}
