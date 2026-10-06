import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'api/cliente_da_api.dart';
import 'offline/banco_local.dart';
import 'offline/sincronizador.dart';
import 'sessao/armazenamento_da_sessao.dart';
import 'tempo_real/conexao_tempo_real.dart';

/// Providers de infraestrutura compartilhados por todas as funcionalidades.

final armazenamentoDaSessaoProvider = Provider((ref) => ArmazenamentoDaSessao());

/// Aberto no main() antes de iniciar o app e injetado com overrideWithValue.
final bancoLocalProvider = Provider<BancoLocal>((ref) => throw UnimplementedError('BancoLocal não inicializado'));

final estadoDaSessaoProvider = NotifierProvider<ControleDaSessao, EstadoDaSessao>(ControleDaSessao.new);

final clienteDaApiProvider = Provider((ref) => ClienteDaApi(
      ref.watch(armazenamentoDaSessaoProvider),
      aoPerderSessao: () => ref.read(estadoDaSessaoProvider.notifier).sessaoPerdida(),
    ));

final sincronizadorProvider = Provider((ref) {
  final sincronizador = Sincronizador(ref.watch(clienteDaApiProvider), ref.watch(bancoLocalProvider))..iniciar();
  ref.onDispose(sincronizador.parar);
  return sincronizador;
});

final conexaoTempoRealProvider = Provider((ref) {
  final conexao = ConexaoTempoReal(ref.watch(clienteDaApiProvider).tokenAtual);
  ref.onDispose(conexao.desconectar);
  return conexao;
});

final eventosTempoRealProvider = StreamProvider<EventoTempoReal>((ref) {
  final conexao = ref.watch(conexaoTempoRealProvider);
  conexao.conectar();
  return conexao.fluxo;
});

enum EstadoDaSessao { verificando, autenticado, desconectado }

/// Sabe se existe sessão salva; a navegação (go_router) reage a este estado.
class ControleDaSessao extends Notifier<EstadoDaSessao> {
  @override
  EstadoDaSessao build() {
    _verificar();
    return EstadoDaSessao.verificando;
  }

  Future<void> _verificar() async {
    final sessao = await ref.read(armazenamentoDaSessaoProvider).ler();
    state = sessao == null ? EstadoDaSessao.desconectado : EstadoDaSessao.autenticado;
  }

  Future<void> entrou(Sessao sessao) async {
    await ref.read(armazenamentoDaSessaoProvider).salvar(sessao);
    state = EstadoDaSessao.autenticado;
  }

  Future<void> sair() async {
    await ref.read(armazenamentoDaSessaoProvider).apagar();
    await ref.read(conexaoTempoRealProvider).desconectar();
    state = EstadoDaSessao.desconectado;
  }

  void sessaoPerdida() => state = EstadoDaSessao.desconectado;
}
