using Intrega.Modulos.Geocodificacao.Contratos;
using Intrega.Modulos.Geocodificacao.Dominio;
using Intrega.Modulos.Geocodificacao.Infraestrutura;
using Intrega.Modulos.Geocodificacao.Infraestrutura.Provedores;
using Intrega.Nucleo.Geo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Intrega.Modulos.Geocodificacao.Aplicacao;

/// <summary>
/// Orquestra a geocodificação, nesta ordem: cache, consulta do CEP, busca por campos,
/// busca por texto e, por último, o centro do CEP.
/// </summary>
internal sealed class ServicoDeGeocodificacao(
    ContextoGeocodificacao bd,
    IEnumerable<IProvedorDeCep> provedoresDeCep,
    IGeocodificador geocodificador,
    IGeocodificadorReverso reverso,
    IProvedorDeBusca busca,
    IOptions<OpcoesDeGeocodificacao> opcoes,
    TimeProvider relogio,
    ILogger<ServicoDeGeocodificacao> log) : IModuloGeocodificacao
{
    /// <summary>Resultado mais longe que isso do centro do CEP é considerado suspeito.</summary>
    private const double DistanciaMaximaDoCepEmMetros = 30_000;

    public async Task<ResultadoDeGeocodificacao?> GeocodificarAsync(EnderecoInformado endereco, PontoGeo? proximoDe = null,
        CancellationToken ct = default)
    {
        var infoCep = await ConsultarCepAsync(endereco.Cep ?? string.Empty, ct);
        var completo = CompletarComCep(endereco, infoCep);
        var chave = NormalizadorDeEndereco.MontarChave(completo);

        var emCache = await bd.Enderecos.FirstOrDefaultAsync(a => a.Chave == chave, ct);
        if (emCache is not null &&
            (emCache.CorrecaoManual || emCache.AtualizadoEm > relogio.GetUtcNow().AddDays(-opcoes.Value.DiasDeCache)))
        {
            emCache.RegistrarAcesso();
            await bd.SaveChangesAsync(ct);
            return DoCache(emCache, completo);
        }

        var candidato = string.IsNullOrWhiteSpace(completo.Logradouro)
            ? null
            : await ChamarComSegurancaAsync(() => geocodificador.GeocodificarPorCamposAsync(completo, proximoDe, ct));

        if (candidato is null || candidato.Precisao < PrecisaoDoResultado.Rua)
        {
            var texto = completo.TextoLivre ?? NormalizadorDeEndereco.Normalizar(completo).Descricao;
            if (!string.IsNullOrWhiteSpace(texto))
            {
                var porTexto = await ChamarComSegurancaAsync(() => geocodificador.GeocodificarTextoAsync(texto, proximoDe, ct));
                if (porTexto is not null && (candidato is null || porTexto.Precisao > candidato.Precisao))
                    candidato = porTexto;
            }
        }

        ResultadoDeGeocodificacao? resultado;
        if (candidato is not null)
        {
            if (infoCep?.Local is { } centroDoCep && candidato.Local.DistanciaAte(centroDoCep) > DistanciaMaximaDoCepEmMetros)
            {
                log.LogWarning("Geocodificação de {Chave} ficou longe do CEP; usando o centro do CEP", chave);
                resultado = new ResultadoDeGeocodificacao(centroDoCep, ConfiancaDe(PrecisaoDoResultado.Cep), "cep", Mesclar(completo, null));
            }
            else
            {
                var confianca = ConfiancaDe(candidato.Precisao);
                if (Suspeito(completo, candidato, proximoDe))
                {
                    log.LogWarning("Geocodificação de {Chave} caiu em {Cidade}; pedindo confirmação", chave, candidato.Endereco.Cidade);
                    confianca = Math.Min(confianca, ConfiancaDe(PrecisaoDoResultado.Bairro));
                }
                resultado = new ResultadoDeGeocodificacao(candidato.Local, confianca, candidato.Provedor,
                    Mesclar(completo, candidato.Endereco));
            }
        }
        else if (infoCep?.Local is { } centroDoCep)
        {
            resultado = new ResultadoDeGeocodificacao(centroDoCep, ConfiancaDe(PrecisaoDoResultado.Cep), "cep", Mesclar(completo, null));
        }
        else
        {
            return null;
        }

        await GravarNoCacheAsync(chave, resultado, correcaoManual: false, ct);
        return resultado;
    }

    public async Task<InformacoesDoCep?> ConsultarCepAsync(string cep, CancellationToken ct = default)
    {
        var cepNormalizado = NormalizadorDeEndereco.NormalizarCep(cep);
        if (cepNormalizado is null) return null;

        var emCache = await bd.Ceps.AsNoTracking().FirstOrDefaultAsync(p => p.Cep == cepNormalizado, ct);
        if (emCache is not null)
        {
            return new InformacoesDoCep(cepNormalizado, emCache.Logradouro, emCache.Bairro, emCache.Cidade, emCache.Uf,
                emCache is { Latitude: { } lat, Longitude: { } lng } ? new PontoGeo(lat, lng) : null);
        }

        foreach (var provedor in provedoresDeCep)
        {
            var info = await ChamarComSegurancaAsync(() => provedor.ConsultarAsync(cepNormalizado, ct));
            if (info is null) continue;

            bd.Ceps.Add(CepEmCache.Criar(cepNormalizado, info.Logradouro, info.Bairro, info.Cidade, info.Uf,
                info.Local?.Latitude, info.Local?.Longitude, provedor.Nome));
            await bd.SaveChangesAsync(ct);
            return info;
        }
        return null;
    }

    public async Task<IReadOnlyList<SugestaoDeEndereco>> BuscarAsync(string texto, PontoGeo? proximoDe, CancellationToken ct = default)
    {
        // Se o texto for um CEP, devolve o próprio CEP como sugestão.
        if (texto.Count(char.IsDigit) == 8 && texto.Length <= 10 && NormalizadorDeEndereco.NormalizarCep(texto) is { } cep)
        {
            var info = await ConsultarCepAsync(cep, ct);
            if (info?.Local is not { } local) return [];
            var descricao = NormalizadorDeEndereco.MontarDescricao(info.Logradouro, null, null, info.Bairro, info.Cidade, info.Uf);
            return [new SugestaoDeEndereco(descricao,
                new EnderecoNormalizado(info.Logradouro, null, null, info.Bairro, info.Cidade, info.Uf, cep, descricao), local)];
        }
        return await ChamarComSegurancaAsync(() => busca.BuscarAsync(texto, proximoDe, ct)) ?? [];
    }

    public async Task<EnderecoNormalizado?> GeocodificacaoReversaAsync(PontoGeo ponto, CancellationToken ct = default) =>
        await ChamarComSegurancaAsync(() => reverso.ReversoAsync(ponto, ct));

    public async Task SalvarCorrecaoAsync(EnderecoInformado endereco, PontoGeo local, CancellationToken ct = default)
    {
        var chave = NormalizadorDeEndereco.MontarChave(endereco);
        var resultado = new ResultadoDeGeocodificacao(local, 1.0, "manual", NormalizadorDeEndereco.Normalizar(endereco));
        await GravarNoCacheAsync(chave, resultado, correcaoManual: true, ct);
    }

    /// <summary>Distância acima disso da região esperada indica homônimo em outra cidade.</summary>
    private const double DistanciaMaximaDaRegiaoEmMetros = 80_000;

    /// <summary>
    /// Resultado provavelmente errado: caiu em outra cidade (ex.: "Rua Augusta, São Paulo" achada em Sumaré)
    /// ou longe demais da região da rota. Nesses casos o app pede para o entregador conferir o pino.
    /// </summary>
    internal static bool Suspeito(EnderecoInformado informado, CandidatoDeGeocodificacao candidato, PontoGeo? proximoDe)
    {
        if (proximoDe is { } regiao && candidato.Local.DistanciaAte(regiao) > DistanciaMaximaDaRegiaoEmMetros) return true;

        var cidadeEncontrada = NormalizadorDeEndereco.Simplificar(candidato.Endereco.Cidade);
        if (cidadeEncontrada.Length == 0) return false;
        if (!string.IsNullOrWhiteSpace(informado.Cidade))
            return NormalizadorDeEndereco.Simplificar(informado.Cidade) != cidadeEncontrada;
        // Texto livre: a cidade citada no texto, se houver, tem que ser a encontrada.
        return CidadeCitada(informado.TextoLivre) is { } citada &&
               NormalizadorDeEndereco.Simplificar(citada) != cidadeEncontrada;
    }

    /// <summary>
    /// Em "Rua Augusta, 900, São Paulo - SP" a cidade costuma ser o último trecho sem números
    /// (ignorando a UF). Precisa haver pelo menos rua + cidade para valer.
    /// </summary>
    internal static string? CidadeCitada(string? texto)
    {
        var trechos = texto?.Split([',', '-', '/'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
        var semNumeros = trechos.Where(t => !t.Any(char.IsDigit) && !UfsBrasileiras.EhUf(t)).ToList();
        return semNumeros.Count >= 2 ? semNumeros[^1] : null;
    }

    internal static double ConfiancaDe(PrecisaoDoResultado precisao) => precisao switch
    {
        PrecisaoDoResultado.Numero => 0.9,
        PrecisaoDoResultado.Rua => 0.7,
        PrecisaoDoResultado.Bairro => 0.5,
        _ => 0.4
    };

    /// <summary>Preenche rua/bairro/cidade/UF que faltaram usando os dados do CEP.</summary>
    private static EnderecoInformado CompletarComCep(EnderecoInformado e, InformacoesDoCep? cep) =>
        cep is null
            ? e
            : e with
            {
                Logradouro = string.IsNullOrWhiteSpace(e.Logradouro) && e.TextoLivre is null ? cep.Logradouro : e.Logradouro,
                Bairro = string.IsNullOrWhiteSpace(e.Bairro) ? cep.Bairro : e.Bairro,
                Cidade = string.IsNullOrWhiteSpace(e.Cidade) ? cep.Cidade : e.Cidade,
                Uf = string.IsNullOrWhiteSpace(e.Uf) ? cep.Uf : e.Uf,
                Cep = cep.Cep
            };

    /// <summary>O que o usuário digitou prevalece; o provedor completa o que faltar.</summary>
    private static EnderecoNormalizado Mesclar(EnderecoInformado informado, EnderecoNormalizado? doProvedor)
    {
        var meu = NormalizadorDeEndereco.Normalizar(informado);
        if (doProvedor is null) return meu;
        var logradouro = meu.Logradouro ?? doProvedor.Logradouro;
        // Texto livre ("Rua Augusta, 900"): o provedor pode achar só a rua; o número digitado não pode se perder.
        var numero = meu.Numero ?? doProvedor.Numero ?? NormalizadorDeEndereco.NumeroNoTexto(informado.TextoLivre);
        var bairro = meu.Bairro ?? doProvedor.Bairro;
        var cidade = meu.Cidade ?? doProvedor.Cidade;
        var uf = meu.Uf ?? doProvedor.Uf;
        var descricao = NormalizadorDeEndereco.MontarDescricao(logradouro, numero, meu.Complemento, bairro, cidade, uf);
        return new EnderecoNormalizado(logradouro, numero, meu.Complemento, bairro, cidade, uf,
            meu.Cep ?? doProvedor.Cep, string.IsNullOrEmpty(descricao) ? doProvedor.Descricao : descricao);
    }

    private static ResultadoDeGeocodificacao DoCache(EnderecoEmCache c, EnderecoInformado informado)
    {
        var descricao = NormalizadorDeEndereco.MontarDescricao(c.Logradouro, c.Numero, informado.Complemento, c.Bairro, c.Cidade, c.Uf);
        var endereco = new EnderecoNormalizado(c.Logradouro, c.Numero, informado.Complemento?.Trim(), c.Bairro, c.Cidade,
            c.Uf, c.Cep, string.IsNullOrEmpty(descricao) ? c.Descricao : descricao);
        return new ResultadoDeGeocodificacao(new PontoGeo(c.Latitude, c.Longitude), c.Confianca, c.Provedor, endereco);
    }

    private async Task GravarNoCacheAsync(string chave, ResultadoDeGeocodificacao r, bool correcaoManual, CancellationToken ct)
    {
        var registro = await bd.Enderecos.FirstOrDefaultAsync(a => a.Chave == chave, ct);
        // Resultado automático nunca sobrescreve uma correção feita pelo entregador.
        if (registro is { CorrecaoManual: true } && !correcaoManual) return;
        if (registro is null)
        {
            registro = EnderecoEmCache.Criar(chave);
            bd.Enderecos.Add(registro);
        }
        var e = r.Endereco;
        registro.Atualizar(r.Local.Latitude, r.Local.Longitude, r.Confianca, r.Provedor, e.Logradouro, e.Numero,
            e.Bairro, e.Cidade, e.Uf, e.Cep, e.Descricao, correcaoManual);
        try
        {
            await bd.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            // Outra requisição gravou a mesma chave ao mesmo tempo; o cache é "melhor esforço".
            log.LogDebug(ex, "Conflito ao gravar o cache de geocodificação {Chave}", chave);
            bd.ChangeTracker.Clear();
        }
    }

    /// <summary>Provedor fora do ar não pode derrubar o cadastro de paradas: registra e segue.</summary>
    private async Task<T?> ChamarComSegurancaAsync<T>(Func<Task<T?>> chamada)
    {
        try
        {
            return await chamada();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException
                                       or Polly.Timeout.TimeoutRejectedException or Polly.CircuitBreaker.BrokenCircuitException)
        {
            log.LogWarning(ex, "Provedor de geocodificação indisponível");
            return default;
        }
    }
}
