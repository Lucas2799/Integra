using System.ComponentModel.DataAnnotations;
using Intrega.Modulos.Etiquetas.Interpretacao;
using Intrega.Modulos.Geocodificacao.Contratos;
using Intrega.Modulos.Identidade.Contratos;
using Intrega.Modulos.Paradas.Contratos;
using Intrega.Modulos.Roteirizacao.Contratos;
using Intrega.Nucleo.Autenticacao;
using Intrega.Nucleo.Resultados;

namespace Intrega.Modulos.Etiquetas.Aplicacao;

public sealed record LeituraDeEtiquetaRequisicao(
    [property: StringLength(8000)] string? TextoLido,
    List<CodigoLido>? Codigos);

public sealed record CriarParadaDaEtiquetaRequisicao(
    Guid? RotaId,
    [property: StringLength(8000)] string? TextoLido,
    List<CodigoLido>? Codigos,
    /// <summary>Endereço conferido/corrigido pelo entregador na tela de confirmação.</summary>
    EnderecoInformado? EnderecoCorrigido = null,
    [property: StringLength(120)] string? NomeDoDestinatario = null,
    double? Latitude = null,
    double? Longitude = null);

public sealed record ParadaDaEtiquetaResposta(bool JaExistia, ParadaDto Parada, EtiquetaInterpretada Etiqueta);

/// <summary>Leitura da etiqueta do pacote: interpreta o texto/códigos e cria a parada sem digitar nada.</summary>
internal sealed class ServicoDeEtiquetas(
    IModuloGeocodificacao geocodificacao,
    IModuloParadas paradas,
    IModuloIdentidade identidade,
    IModuloRoteirizacao roteirizacao,
    IUsuarioAtual usuario)
{
    public async Task<EtiquetaInterpretada> InterpretarAsync(LeituraDeEtiquetaRequisicao req, CancellationToken ct)
    {
        var etiqueta = InterpretadorDeEtiquetas.Interpretar(req.TextoLido, req.Codigos);
        return await CompletarComCepAsync(etiqueta, ct);
    }

    public async Task<Resultado<ParadaDaEtiquetaResposta>> CriarParadaAsync(CriarParadaDaEtiquetaRequisicao req, CancellationToken ct)
    {
        var responsavel = new ResponsavelPelaParada(usuario.Id, usuario.OrganizacaoId);
        if (req.RotaId is { } rotaId)
        {
            var rota = await roteirizacao.ObterAcessoAsync(rotaId, ct);
            if (rota is null || !rota.PodeSerAcessadaPor(usuario.Id, usuario.OrganizacaoId, usuario.GestorDaOrganizacao))
                return Erro.NaoEncontrado("rota.nao_encontrada", "Rota não encontrada.");
            responsavel = responsavel with { OrganizacaoId = rota.OrganizacaoId };
        }

        var etiqueta = await CompletarComCepAsync(InterpretadorDeEtiquetas.Interpretar(req.TextoLido, req.Codigos), ct);

        // Pacote já lido antes (mesmo código de rastreio): não duplica a parada.
        if (etiqueta.CodigoDeRastreio is { } codigo &&
            await paradas.BuscarPorCodigoDePacoteAsync(usuario.Id, codigo, ct) is { } existente &&
            existente.RotaId == req.RotaId && existente.Status == StatusDaParada.Pendente)
        {
            return new ParadaDaEtiquetaResposta(true, existente, etiqueta);
        }

        var endereco = req.EnderecoCorrigido ?? etiqueta.Endereco;
        if (string.IsNullOrWhiteSpace(endereco.Logradouro) && string.IsNullOrWhiteSpace(endereco.Cep) &&
            string.IsNullOrWhiteSpace(endereco.TextoLivre) && req.Latitude is null)
            return Erro.Validacao("etiqueta.sem_endereco", "Não foi possível ler o endereço. Tire outra foto ou digite o endereço.");

        var uso = await identidade.ConsumirUsoAsync(usuario.Id, RecursoMedido.LeituraDeEtiqueta, ct);
        if (uso.Falha) return uso.Erro!;

        var nova = new NovaParada(
            endereco,
            req.NomeDoDestinatario ?? etiqueta.NomeDoDestinatario,
            etiqueta.Telefone,
            CodigosDePacote: etiqueta.CodigoDeRastreio is { } c ? [c] : null,
            Marketplace: etiqueta.Marketplace,
            Local: req.Latitude is { } lat && req.Longitude is { } lng ? new Intrega.Nucleo.Geo.PontoGeo(lat, lng) : null);

        var resultado = await paradas.IncluirAsync(responsavel, req.RotaId, [nova], OrigemDaParada.Etiqueta, ct: ct);
        if (resultado.Falha) return resultado.Erro!;
        if (resultado.Valor.Erros.Count > 0)
            return Erro.LimiteDoPlano("plano.paradas_por_rota", resultado.Valor.Erros[0].Mensagem);

        // Se foi agrupada com outra parada do mesmo endereço, busca pelo código; senão pelo id criado.
        var parada = resultado.Valor.IdsCriados.Count > 0
            ? (await paradas.ListarPorIdsAsync(resultado.Valor.IdsCriados, ct))[0]
            : etiqueta.CodigoDeRastreio is { } cod ? await paradas.BuscarPorCodigoDePacoteAsync(usuario.Id, cod, ct) : null;
        if (parada is null) return Erro.Validacao("etiqueta.agrupada", "O pacote foi agrupado com uma parada existente.");
        return new ParadaDaEtiquetaResposta(false, parada, etiqueta);
    }

    /// <summary>Com o CEP lido, completa rua/bairro/cidade que o OCR não pegou e confere a cidade.</summary>
    private async Task<EtiquetaInterpretada> CompletarComCepAsync(EtiquetaInterpretada etiqueta, CancellationToken ct)
    {
        if (etiqueta.Endereco.Cep is not { } cep) return etiqueta;
        var info = await geocodificacao.ConsultarCepAsync(cep, ct);
        if (info is null)
            return etiqueta with { Avisos = [.. etiqueta.Avisos, $"O CEP {cep} não foi encontrado; pode ter sido lido errado."] };

        var e = etiqueta.Endereco;
        var avisos = etiqueta.Avisos.ToList();
        if (e.Cidade is not null && !string.Equals(InterpretadorDeEtiquetas.SemAcentos(e.Cidade),
                InterpretadorDeEtiquetas.SemAcentos(info.Cidade), StringComparison.OrdinalIgnoreCase))
            avisos.Add($"A cidade lida ({e.Cidade}) é diferente da cidade do CEP ({info.Cidade}).");

        var completo = e with
        {
            Logradouro = e.Logradouro ?? info.Logradouro,
            Bairro = e.Bairro ?? info.Bairro,
            Cidade = info.Cidade,
            Uf = info.Uf
        };
        var bonus = e.Logradouro is null && info.Logradouro is not null ? 0.1 : 0.05;
        return etiqueta with { Endereco = completo, Avisos = avisos, Confianca = Math.Min(1, Math.Round(etiqueta.Confianca + bonus, 2)) };
    }
}
