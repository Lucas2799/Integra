using Intrega.Infraestrutura.Armazenamento;
using Intrega.Modulos.Geocodificacao.Contratos;
using Intrega.Modulos.Identidade.Contratos;
using Intrega.Modulos.Paradas.Contratos;
using Intrega.Modulos.Paradas.Dominio;
using Intrega.Modulos.Paradas.Infraestrutura;
using Intrega.Modulos.Roteirizacao.Contratos;
using Intrega.Nucleo.Autenticacao;
using Intrega.Nucleo.Eventos;
using Intrega.Nucleo.Geo;
using Intrega.Nucleo.Resultados;
using Microsoft.EntityFrameworkCore;

namespace Intrega.Modulos.Paradas.Aplicacao;

/// <summary>Casos de uso dos endpoints de paradas: inclusão de destinos, entrega, ausência e comprovante.</summary>
internal sealed class ServicoDeParadas(
    ContextoParadas bd,
    ApiDoModuloParadas api,
    IModuloGeocodificacao geocodificacao,
    IModuloIdentidade identidade,
    IModuloRoteirizacao roteirizacao,
    IUsuarioAtual usuario,
    IBarramentoDeEventos barramento,
    IArmazenamentoDeArquivos armazenamento,
    TimeProvider relogio)
{
    public const long TamanhoMaximoDoComprovante = 5 * 1024 * 1024;

    private static readonly Erro ParadaNaoEncontrada = Erro.NaoEncontrado("parada.nao_encontrada", "Parada não encontrada.");
    private static readonly Erro RotaNaoEncontrada = Erro.NaoEncontrado("rota.nao_encontrada", "Rota não encontrada.");
    private static readonly Erro JanelaSoNoPro =
        Erro.LimiteDoPlano("plano.janelas_de_horario", "Janelas de horário estão disponíveis no plano Pro.");

    public async Task<Resultado<ParadaDto>> CriarAsync(CriarParadaRequisicao req, CancellationToken ct)
    {
        var dados = req.Parada.ParaNovaParada();
        var limites = (await identidade.ObterDireitosAsync(usuario.Id, ct)).Limites;
        if (!limites.JanelasDeHorario && (dados.JanelaInicio is not null || dados.JanelaFim is not null)) return JanelaSoNoPro;

        var responsavel = new ResponsavelPelaParada(usuario.Id, usuario.OrganizacaoId);
        if (req.RotaId is { } rotaId)
        {
            var rota = await roteirizacao.ObterAcessoAsync(rotaId, ct);
            if (rota is null || !rota.PodeSerAcessadaPor(usuario.Id, usuario.OrganizacaoId, usuario.GestorDaOrganizacao))
                return RotaNaoEncontrada;
            responsavel = responsavel with { OrganizacaoId = rota.OrganizacaoId };
        }

        var resultado = await api.IncluirAsync(responsavel, req.RotaId, [dados], OrigemDaParada.Manual, ct: ct);
        if (resultado.Falha) return resultado.Erro!;
        if (resultado.Valor.Erros.Count > 0)
            return Erro.LimiteDoPlano("plano.paradas_por_rota", resultado.Valor.Erros[0].Mensagem);

        // Endereço repetido: a parada foi agrupada com uma já existente; devolve a existente.
        if (resultado.Valor.IdsCriados.Count == 0)
        {
            var agrupada = await bd.Paradas.AsNoTracking()
                .Where(p => p.RotaId == req.RotaId && p.Status == StatusDaParada.Pendente)
                .OrderByDescending(p => p.AtualizadoEm).FirstAsync(ct);
            return agrupada.ParaDto();
        }

        var criada = await bd.Paradas.AsNoTracking().FirstAsync(p => p.Id == resultado.Valor.IdsCriados[0], ct);
        return criada.ParaDto();
    }

    /// <param name="semRota">Lista as paradas sem rota (adiadas ou aguardando despacho do gestor).</param>
    public async Task<IReadOnlyList<ParadaDto>> ListarAsync(Guid? rotaId, bool semRota, CancellationToken ct)
    {
        IQueryable<Parada> consulta = bd.Paradas.AsNoTracking();
        if (rotaId is { } id)
        {
            var rota = await roteirizacao.ObterAcessoAsync(id, ct);
            if (rota is null || !rota.PodeSerAcessadaPor(usuario.Id, usuario.OrganizacaoId, usuario.GestorDaOrganizacao))
                return [];
            consulta = consulta.Where(p => p.RotaId == id);
        }
        else if (semRota)
        {
            consulta = consulta.Where(p => p.RotaId == null &&
                                           (p.Status == StatusDaParada.Pendente || p.Status == StatusDaParada.Adiada));
            consulta = usuario.GestorDaOrganizacao && usuario.OrganizacaoId is { } organizacaoId
                ? consulta.Where(p => p.OrganizacaoId == organizacaoId)
                : consulta.Where(p => p.ResponsavelId == usuario.Id);
        }
        else
        {
            consulta = consulta.Where(p => p.ResponsavelId == usuario.Id);
        }

        return (await consulta.OrderBy(p => p.CriadoEm).Take(1000).ToListAsync(ct)).Select(p => p.ParaDto()).ToList();
    }

    public async Task<Resultado<ParadaDto>> ObterAsync(Guid id, CancellationToken ct)
    {
        var parada = await BuscarComPermissaoAsync(id, ct);
        return parada is null ? ParadaNaoEncontrada : parada.ParaDto();
    }

    public async Task<Resultado<ParadaDto>> AtualizarAsync(Guid id, DadosDaParadaRequisicao req, CancellationToken ct)
    {
        var parada = await BuscarComPermissaoAsync(id, ct);
        if (parada is null) return ParadaNaoEncontrada;

        var dados = req.ParaNovaParada();
        var limites = (await identidade.ObterDireitosAsync(usuario.Id, ct)).Limites;
        if (!limites.JanelasDeHorario && (dados.JanelaInicio is not null || dados.JanelaFim is not null)) return JanelaSoNoPro;

        var enderecoMudou = !MesmoEndereco(parada.Endereco.ParaInformado(), dados.Endereco) || dados.Local is not null;
        parada.AtualizarDados(dados);
        if (enderecoMudou)
        {
            var regiao = parada.RotaId is { } rotaId ? (await roteirizacao.ObterAcessoAsync(rotaId, ct))?.Saida : null;
            await api.LocalizarAsync(parada, dados, regiao, ct);
        }
        await bd.SaveChangesAsync(ct);
        return parada.ParaDto();
    }

    public async Task<Resultado> ExcluirAsync(Guid id, CancellationToken ct)
    {
        var parada = await BuscarComPermissaoAsync(id, ct);
        if (parada is null) return ParadaNaoEncontrada;
        bd.Paradas.Remove(parada);
        await bd.SaveChangesAsync(ct);
        if (parada.CaminhoDoComprovante is { } caminho) await armazenamento.ExcluirAsync(caminho, ct);
        return Resultado.Ok();
    }

    /// <summary>O entregador ajusta o pino no mapa. A correção é reaproveitada nas próximas entregas no mesmo endereço.</summary>
    public async Task<Resultado<ParadaDto>> ConfirmarLocalAsync(Guid id, ConfirmarLocalRequisicao req, CancellationToken ct)
    {
        var parada = await BuscarComPermissaoAsync(id, ct);
        if (parada is null) return ParadaNaoEncontrada;
        var ponto = new PontoGeo(req.Latitude, req.Longitude);
        parada.ConfirmarLocal(ponto);
        await bd.SaveChangesAsync(ct);
        await geocodificacao.SalvarCorrecaoAsync(parada.Endereco.ParaInformado(), ponto, ct);
        return parada.ParaDto();
    }

    public async Task<Resultado<ParadaDto>> EntregarAsync(Guid id, EntregarRequisicao req, CancellationToken ct)
    {
        var parada = await BuscarComPermissaoAsync(id, ct);
        if (parada is null) return ParadaNaoEncontrada;
        if (parada.Status == StatusDaParada.Entregue) return parada.ParaDto();

        var agora = relogio.GetUtcNow();
        var entregueEm = req.EntregueEm is { } informado && informado <= agora ? informado : agora;
        var local = PontoOuNulo(req.Latitude, req.Longitude);
        parada.MarcarEntregue(entregueEm, local, req.RecebidoPor, req.Observacoes);
        await bd.SaveChangesAsync(ct);

        await barramento.PublicarAsync(new ParadaEntregue(parada.Id, parada.RotaId, usuario.Id, entregueEm, local,
            parada.Marketplace, parada.QuantidadeDePacotes), ct);
        return parada.ParaDto();
    }

    /// <summary>Destinatário ausente ou recusou: registra a tentativa e dispara o recálculo da rota.</summary>
    public async Task<Resultado<ParadaDto>> RegistrarFalhaAsync(Guid id, RegistrarFalhaRequisicao req, CancellationToken ct)
    {
        var parada = await BuscarComPermissaoAsync(id, ct);
        if (parada is null) return ParadaNaoEncontrada;
        if (parada.Status != StatusDaParada.Pendente)
            return Erro.Conflito("parada.nao_pendente", "A parada não está pendente.");

        var rotaId = parada.RotaId; // guardado antes: "próximo dia" tira a parada da rota
        parada.RegistrarFalha(req.Motivo, req.Estrategia, relogio.GetUtcNow(), req.MinutosParaNovaTentativa ?? 30);
        await bd.SaveChangesAsync(ct);

        await barramento.PublicarAsync(new EntregaNaoRealizada(parada.Id, rotaId, usuario.Id, req.Motivo, req.Estrategia,
            parada.NovaTentativaApos, PontoOuNulo(req.Latitude, req.Longitude)), ct);
        return parada.ParaDto();
    }

    public async Task<Resultado<ParadaDto>> ReabrirAsync(Guid id, CancellationToken ct)
    {
        var parada = await BuscarComPermissaoAsync(id, ct);
        if (parada is null) return ParadaNaoEncontrada;
        parada.Reabrir();
        await bd.SaveChangesAsync(ct);
        return parada.ParaDto();
    }

    public async Task<Resultado<ParadaDto>> MoverAsync(Guid id, MoverParadaRequisicao req, CancellationToken ct)
    {
        var parada = await BuscarComPermissaoAsync(id, ct);
        if (parada is null) return ParadaNaoEncontrada;
        if (req.RotaId is { } rotaId)
        {
            var rota = await roteirizacao.ObterAcessoAsync(rotaId, ct);
            if (rota is null || !rota.PodeSerAcessadaPor(usuario.Id, usuario.OrganizacaoId, usuario.GestorDaOrganizacao))
                return RotaNaoEncontrada;
        }
        parada.AtribuirARota(req.RotaId);
        if (parada.Status == StatusDaParada.Adiada) parada.Reabrir();
        await bd.SaveChangesAsync(ct);
        return parada.ParaDto();
    }

    public async Task<Resultado> EnviarComprovanteAsync(Guid id, Stream foto, long tamanho, string tipoDeConteudo, CancellationToken ct)
    {
        var limites = (await identidade.ObterDireitosAsync(usuario.Id, ct)).Limites;
        if (!limites.ComprovanteDeEntrega)
            return Erro.LimiteDoPlano("plano.comprovante", "Comprovante com foto está disponível no plano Pro.");
        if (tamanho is <= 0 or > TamanhoMaximoDoComprovante)
            return Erro.Validacao("parada.comprovante_tamanho", "A foto deve ter até 5 MB.");
        if (tipoDeConteudo is not ("image/jpeg" or "image/png" or "image/webp"))
            return Erro.Validacao("parada.comprovante_formato", "Envie uma imagem JPEG, PNG ou WebP.");

        var parada = await BuscarComPermissaoAsync(id, ct);
        if (parada is null) return ParadaNaoEncontrada;

        var caminho = $"comprovantes/{parada.ResponsavelId}/{parada.Id}";
        await armazenamento.SalvarAsync(caminho, foto, ct);
        parada.DefinirComprovante(caminho);
        await bd.SaveChangesAsync(ct);
        return Resultado.Ok();
    }

    public async Task<Stream?> AbrirComprovanteAsync(Guid id, CancellationToken ct)
    {
        var parada = await BuscarComPermissaoAsync(id, ct);
        return parada?.CaminhoDoComprovante is { } caminho ? await armazenamento.AbrirLeituraAsync(caminho, ct) : null;
    }

    /// <summary>Acesso: responsável pela parada, gestor da mesma organização ou quem tem acesso à rota dela.</summary>
    private async Task<Parada?> BuscarComPermissaoAsync(Guid id, CancellationToken ct)
    {
        var parada = await bd.Paradas.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (parada is null) return null;
        if (parada.ResponsavelId == usuario.Id) return parada;
        if (usuario.GestorDaOrganizacao && parada.OrganizacaoId is not null && parada.OrganizacaoId == usuario.OrganizacaoId)
            return parada;
        if (parada.RotaId is { } rotaId)
        {
            var rota = await roteirizacao.ObterAcessoAsync(rotaId, ct);
            if (rota?.PodeSerAcessadaPor(usuario.Id, usuario.OrganizacaoId, usuario.GestorDaOrganizacao) == true) return parada;
        }
        return null;
    }

    private static PontoGeo? PontoOuNulo(double? latitude, double? longitude) =>
        latitude is { } lat && longitude is { } lng ? new PontoGeo(lat, lng) : null;

    private static bool MesmoEndereco(EnderecoInformado a, EnderecoInformado b)
    {
        static string Texto(string? s) => s?.Trim().ToUpperInvariant() ?? "";
        static string Digitos(string? s) => new((s ?? "").Where(char.IsDigit).ToArray());
        return Texto(a.Logradouro) == Texto(b.Logradouro) && Texto(a.Numero) == Texto(b.Numero) &&
               Texto(a.Cidade) == Texto(b.Cidade) && Digitos(a.Cep) == Digitos(b.Cep);
    }
}
