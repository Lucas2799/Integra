import 'dart:async';

import 'package:dio/dio.dart';

import '../configuracao.dart';
import '../sessao/armazenamento_da_sessao.dart';

/// Cliente HTTP do Intrega: coloca o token em cada chamada e renova a sessão
/// automaticamente quando o token de acesso expira (401).
class ClienteDaApi {
  ClienteDaApi(this._sessao, {required this.aoPerderSessao, String? urlBase})
      : dio = Dio(BaseOptions(
          baseUrl: '${urlBase ?? Configuracao.urlDaApi}/api',
          connectTimeout: const Duration(seconds: 15),
          receiveTimeout: const Duration(seconds: 60),
        )) {
    dio.interceptors.add(QueuedInterceptorsWrapper(
      onRequest: (opcoes, continuar) async {
        final sessao = await _sessao.ler();
        if (sessao != null) opcoes.headers['Authorization'] = 'Bearer ${sessao.tokenDeAcesso}';
        continuar.next(opcoes);
      },
      onError: (erro, continuar) async {
        final ehRenovacao = erro.requestOptions.path.contains('/autenticacao/');
        if (erro.response?.statusCode != 401 || ehRenovacao) return continuar.next(erro);

        final renovou = await _renovarSessao();
        if (!renovou) {
          aoPerderSessao();
          return continuar.next(erro);
        }
        final sessao = await _sessao.ler();
        final opcoes = erro.requestOptions..headers['Authorization'] = 'Bearer ${sessao!.tokenDeAcesso}';
        try {
          continuar.resolve(await dio.fetch(opcoes));
        } on DioException catch (e) {
          continuar.next(e);
        }
      },
    ));
  }

  final Dio dio;
  final ArmazenamentoDaSessao _sessao;

  /// Chamado quando a sessão expirou de vez (o app volta para a tela de entrada).
  final void Function() aoPerderSessao;

  Future<String?> tokenAtual() async => (await _sessao.ler())?.tokenDeAcesso;

  Future<bool> _renovarSessao() async {
    final atual = await _sessao.ler();
    if (atual == null) return false;
    try {
      final resposta = await Dio(BaseOptions(baseUrl: dio.options.baseUrl))
          .post('/autenticacao/renovar', data: {'tokenDeAtualizacao': atual.tokenDeAtualizacao});
      await _sessao.salvar(Sessao.deJson(resposta.data as Map<String, dynamic>));
      return true;
    } on DioException {
      await _sessao.apagar();
      return false;
    }
  }
}
