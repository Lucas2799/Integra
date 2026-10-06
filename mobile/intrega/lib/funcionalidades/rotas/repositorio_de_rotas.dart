import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:latlong2/latlong.dart';

import '../../nucleo/api/erro_da_api.dart';
import '../../nucleo/formatacao/formatos.dart';
import '../../nucleo/offline/banco_local.dart';
import '../../nucleo/offline/sincronizador.dart';
import '../../nucleo/provedores.dart';
import '../paradas/modelos_de_paradas.dart';
import 'modelos_de_rotas.dart';

final repositorioDeRotasProvider = Provider((ref) => RepositorioDeRotas(
      ref.watch(clienteDaApiProvider).dio,
      ref.watch(bancoLocalProvider),
      ref.watch(sincronizadorProvider),
    ));

/// Acesso à API de rotas e paradas, com cache local para funcionar sem internet.
class RepositorioDeRotas {
  RepositorioDeRotas(this._dio, this._banco, this._sincronizador);

  final Dio _dio;
  final BancoLocal _banco;
  final Sincronizador _sincronizador;

  Future<T> _chamar<T>(Future<T> Function() chamada) async {
    try {
      return await chamada();
    } catch (e) {
      throw ErroDaApi.de(e);
    }
  }

  Map<String, dynamic>? _posicao(LatLng? p) => p == null ? null : {'latitude': p.latitude, 'longitude': p.longitude};

  Future<List<ResumoDaRota>> listar() => _chamar(() async {
        final r = await _dio.get('/rotas');
        return (r.data as List).map((j) => ResumoDaRota.deJson(j as Map<String, dynamic>)).toList();
      });

  Future<DetalhesDaRota> criar({
    required String nome,
    required DateTime data,
    required LatLng saida,
    String? descricaoDaSaida,
    required bool voltarAoInicio,
    required PerfilDeVeiculo veiculo,
  }) =>
      _chamar(() async {
        final r = await _dio.post('/rotas', data: {
          'nome': nome,
          'data': Formatos.dataDaApi(data),
          'saida': {..._posicao(saida)!, 'descricao': descricaoDaSaida},
          'voltarAoInicio': voltarAoInicio,
          'veiculo': veiculo.valor,
        });
        return _guardar(r.data as Map<String, dynamic>);
      });

  /// Busca a rota no servidor; sem internet, devolve a última versão guardada no aparelho.
  Future<DetalhesDaRota> obter(String id) async {
    try {
      final r = await _dio.get('/rotas/$id');
      return await _guardar(r.data as Map<String, dynamic>);
    } catch (e) {
      final erro = ErroDaApi.de(e);
      final emCache = erro.semConexao ? await _banco.lerRota(id) : null;
      if (emCache != null) return DetalhesDaRota.deJson(emCache);
      throw erro;
    }
  }

  Future<DetalhesDaRota> otimizar(String id, {LatLng? posicaoAtual}) => _chamar(() async {
        final r = await _dio.post('/rotas/$id/otimizar', data: _posicao(posicaoAtual) ?? {});
        return _guardar((r.data as Map<String, dynamic>)['rota'] as Map<String, dynamic>);
      });

  Future<DetalhesDaRota> recalcular(String id, {LatLng? posicaoAtual}) => _chamar(() async {
        final r = await _dio.post('/rotas/$id/recalcular', data: _posicao(posicaoAtual) ?? {});
        return _guardar((r.data as Map<String, dynamic>)['rota'] as Map<String, dynamic>);
      });

  Future<DetalhesDaRota> reordenar(String id, List<String> paradaIds) => _chamar(() async {
        final r = await _dio.put('/rotas/$id/sequencia', data: {'paradaIds': paradaIds});
        return _guardar(r.data as Map<String, dynamic>);
      });

  Future<void> excluir(String id) => _chamar(() => _dio.delete('/rotas/$id'));

  Future<Navegacao> navegacao(String id, LatLng atual, {String? paradaId}) => _chamar(() async {
        final r = await _dio.get('/rotas/$id/navegacao', queryParameters: {
          'lat': atual.latitude,
          'lng': atual.longitude,
          'paradaId': ?paradaId,
        });
        return Navegacao.deJson(r.data as Map<String, dynamic>);
      });

  Future<List<ItemDeCarregamento>> ordemDeCarregamento(String id) => _chamar(() async {
        final r = await _dio.get('/rotas/$id/ordem-de-carregamento');
        return (r.data as List).map((j) => ItemDeCarregamento.deJson(j as Map<String, dynamic>)).toList();
      });

  // --- Paradas ----------------------------------------------------------------

  Future<Parada> incluirParada(String rotaId, Map<String, dynamic> dados) => _chamar(() async {
        final r = await _dio.post('/paradas', data: {'rotaId': rotaId, 'parada': dados});
        return Parada.deJson(r.data as Map<String, dynamic>);
      });

  Future<Parada> confirmarLocal(String paradaId, LatLng local) => _chamar(() async {
        final r = await _dio.put('/paradas/$paradaId/local', data: _posicao(local));
        return Parada.deJson(r.data as Map<String, dynamic>);
      });

  Future<void> excluirParada(String paradaId) => _chamar(() => _dio.delete('/paradas/$paradaId'));

  /// Marca como entregue. Sem internet, a ação fica na fila e é enviada depois (com o horário real).
  Future<bool> entregar(String paradaId, {String? recebidoPor, LatLng? local}) => _chamar(() =>
      _sincronizador.executarOuEnfileirar('POST', '/paradas/$paradaId/entregar', {
        'recebidoPor': recebidoPor,
        ...?_posicao(local),
        'entregueEm': DateTime.now().toUtc().toIso8601String(),
      }));

  Future<bool> naoEntregue(String paradaId, MotivoDaFalha motivo, EstrategiaDeNovaTentativa estrategia,
          {int? minutos, LatLng? local}) =>
      _chamar(() => _sincronizador.executarOuEnfileirar('POST', '/paradas/$paradaId/nao-entregue', {
            'motivo': motivo.valor,
            'estrategia': estrategia.valor,
            'minutosParaNovaTentativa': minutos,
            ...?_posicao(local),
          }));

  Future<void> reabrir(String paradaId) => _chamar(() => _dio.post('/paradas/$paradaId/reabrir'));

  Future<void> enviarComprovante(String paradaId, String caminhoDaFoto) => _chamar(() => _dio.put(
        '/paradas/$paradaId/comprovante',
        data: FormData.fromMap({'foto': MultipartFile.fromFileSync(caminhoDaFoto, contentType: DioMediaType('image', 'jpeg'))}),
      ));

  Future<List<SugestaoDeEndereco>> buscarEndereco(String texto, {LatLng? proximoDe}) => _chamar(() async {
        final r = await _dio.get('/geocodificacao/buscar', queryParameters: {
          'texto': texto,
          if (proximoDe != null) 'lat': proximoDe.latitude,
          if (proximoDe != null) 'lng': proximoDe.longitude,
        });
        return (r.data as List).map((j) => SugestaoDeEndereco.deJson(j as Map<String, dynamic>)).toList();
      });

  Future<DetalhesDaRota> _guardar(Map<String, dynamic> json) async {
    final detalhes = DetalhesDaRota.deJson(json);
    await _banco.salvarRota(detalhes.resumo.id, json);
    return detalhes;
  }
}
