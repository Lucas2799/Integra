import 'dart:async';

import 'package:signalr_netcore/signalr_client.dart';

import '../configuracao.dart';

/// Evento recebido em tempo real (ex.: "rotaAtualizada" depois de um recálculo).
class EventoTempoReal {
  EventoTempoReal(this.nome, this.dados);

  final String nome;
  final Map<String, dynamic> dados;
}

/// Conexão SignalR com o hub /tempo-real. Reconecta sozinha quando a internet volta.
class ConexaoTempoReal {
  ConexaoTempoReal(this._obterToken);

  static const eventos = ['rotaAtualizada', 'progressoImportacao', 'posicaoEntregador'];

  final Future<String?> Function() _obterToken;
  final _controlador = StreamController<EventoTempoReal>.broadcast();
  HubConnection? _hub;

  Stream<EventoTempoReal> get fluxo => _controlador.stream;

  Future<void> conectar() async {
    if (_hub != null) return;
    final hub = HubConnectionBuilder()
        .withUrl('${Configuracao.urlDaApi}/tempo-real',
            options: HttpConnectionOptions(accessTokenFactory: () async => await _obterToken() ?? ''))
        .withAutomaticReconnect()
        .build();
    for (final nome in eventos) {
      hub.on(nome, (argumentos) {
        final dados = argumentos?.firstOrNull;
        if (dados is Map) _controlador.add(EventoTempoReal(nome, Map<String, dynamic>.from(dados)));
      });
    }
    _hub = hub;
    try {
      await hub.start();
    } catch (_) {
      // Sem conexão agora: o app funciona sem tempo real e tenta de novo depois.
      _hub = null;
    }
  }

  Future<void> desconectar() async {
    await _hub?.stop();
    _hub = null;
  }
}
