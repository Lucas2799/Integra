import 'dart:async';

import 'package:connectivity_plus/connectivity_plus.dart';
import 'package:dio/dio.dart';

import '../api/cliente_da_api.dart';
import 'banco_local.dart';

/// Envia ao servidor, em ordem, as ações feitas sem internet (entregas, ausências)
/// e as posições GPS acumuladas. Roda ao recuperar a conexão e periodicamente.
class Sincronizador {
  Sincronizador(this._api, this._banco);

  final ClienteDaApi _api;
  final BancoLocal _banco;
  StreamSubscription<List<ConnectivityResult>>? _conexao;
  Timer? _temporizador;
  bool _sincronizando = false;

  void iniciar() {
    _conexao = Connectivity().onConnectivityChanged.listen((estado) {
      if (!estado.contains(ConnectivityResult.none)) sincronizar();
    });
    _temporizador = Timer.periodic(const Duration(minutes: 1), (_) => sincronizar());
  }

  void parar() {
    _conexao?.cancel();
    _temporizador?.cancel();
  }

  /// Executa uma ação agora; se estiver sem internet, guarda na fila e devolve false.
  Future<bool> executarOuEnfileirar(String metodo, String caminho, Map<String, dynamic>? corpo) async {
    try {
      await _api.dio.request(caminho, data: corpo, options: Options(method: metodo));
      return true;
    } on DioException catch (e) {
      if (e.response != null) rethrow; // erro de negócio: não adianta repetir
      await _banco.enfileirar(metodo, caminho, corpo);
      return false;
    }
  }

  Future<void> sincronizar() async {
    if (_sincronizando) return;
    _sincronizando = true;
    try {
      for (final acao in await _banco.acoesPendentes()) {
        try {
          await _api.dio.request(acao.caminho, data: acao.corpo, options: Options(method: acao.metodo));
          await _banco.removerAcao(acao.id);
        } on DioException catch (e) {
          if (e.response == null) break; // continua sem internet: tenta depois, mantendo a ordem
          // Erro de negócio (ex.: parada já entregue em outro aparelho): descarta para não travar a fila.
          if (acao.tentativas >= 2) {
            await _banco.removerAcao(acao.id);
          } else {
            await _banco.registrarTentativa(acao.id);
          }
        }
      }
      await _enviarPosicoes();
    } finally {
      _sincronizando = false;
    }
  }

  Future<void> _enviarPosicoes() async {
    final pendentes = await _banco.posicoesPendentes();
    if (pendentes.isEmpty) return;
    final porRota = <String?, List<Map<String, dynamic>>>{};
    for (final (_, rotaId, posicao) in pendentes) {
      porRota.putIfAbsent(rotaId, () => []).add(posicao);
    }
    try {
      for (final entrada in porRota.entries) {
        await _api.dio.post('/rastreamento/posicoes', data: {'rotaId': entrada.key, 'posicoes': entrada.value});
      }
      await _banco.removerPosicoesAte(pendentes.last.$1);
    } on DioException {
      // Sem conexão: as posições continuam guardadas.
    }
  }
}
