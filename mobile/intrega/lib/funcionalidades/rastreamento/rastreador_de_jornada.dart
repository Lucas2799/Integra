import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:geolocator/geolocator.dart';

import '../../nucleo/geo/localizacao.dart';
import '../../nucleo/offline/banco_local.dart';
import '../../nucleo/offline/sincronizador.dart';
import '../../nucleo/provedores.dart';

final servicoDeLocalizacaoProvider = Provider((ref) => ServicoDeLocalizacao());

final rastreadorDeJornadaProvider = Provider((ref) {
  final rastreador = RastreadorDeJornada(
    ref.watch(servicoDeLocalizacaoProvider),
    ref.watch(bancoLocalProvider),
    ref.watch(sincronizadorProvider),
  );
  ref.onDispose(rastreador.parar);
  return rastreador;
});

/// Registra o trajeto enquanto a rota está em andamento. As posições ficam no aparelho e
/// são enviadas em lote (funciona sem internet). Base dos km rodados e do tempo trabalhado.
class RastreadorDeJornada {
  RastreadorDeJornada(this._localizacao, this._banco, this._sincronizador);

  final ServicoDeLocalizacao _localizacao;
  final BancoLocal _banco;
  final Sincronizador _sincronizador;
  StreamSubscription<Position>? _assinatura;
  String? _rotaId;
  int _desdeOUltimoEnvio = 0;

  bool get ativo => _assinatura != null;

  Future<void> iniciar(String rotaId) async {
    if (_rotaId == rotaId && ativo) return;
    await parar();
    if (!await _localizacao.garantirPermissao()) return;
    _rotaId = rotaId;
    _assinatura = _localizacao.acompanhar(distanciaMinimaMetros: 30).listen((p) async {
      await _banco.guardarPosicao(_rotaId, {
        'latitude': p.latitude,
        'longitude': p.longitude,
        'registradaEm': p.timestamp.toUtc().toIso8601String(),
        'velocidadeMetrosPorSegundo': p.speed,
        'direcao': p.heading,
        'precisaoMetros': p.accuracy,
      });
      // Envia a cada ~20 posições para economizar dados e bateria.
      if (++_desdeOUltimoEnvio >= 20) {
        _desdeOUltimoEnvio = 0;
        unawaited(_sincronizador.sincronizar());
      }
    });
  }

  Future<void> parar() async {
    await _assinatura?.cancel();
    _assinatura = null;
    _rotaId = null;
    await _sincronizador.sincronizar();
  }
}
