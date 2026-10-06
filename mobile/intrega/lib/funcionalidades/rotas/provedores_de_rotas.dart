import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../nucleo/provedores.dart';
import 'modelos_de_rotas.dart';
import 'repositorio_de_rotas.dart';

final listaDeRotasProvider = FutureProvider.autoDispose<List<ResumoDaRota>>((ref) => ref.watch(repositorioDeRotasProvider).listar());

/// Detalhes da rota aberta. Recarrega sozinho quando o servidor avisa que a rota mudou (recálculo etc.).
final detalhesDaRotaProvider =
    AsyncNotifierProvider.autoDispose.family<ControleDaRota, DetalhesDaRota, String>(ControleDaRota.new);

class ControleDaRota extends AsyncNotifier<DetalhesDaRota> {
  ControleDaRota(this.rotaId);

  final String rotaId;

  @override
  Future<DetalhesDaRota> build() async {
    ref.listen(eventosTempoRealProvider, (_, evento) {
      final e = evento.value;
      if (e != null && e.nome == 'rotaAtualizada' && e.dados['rotaId'] == rotaId) recarregar();
    });
    return ref.read(repositorioDeRotasProvider).obter(rotaId);
  }

  Future<void> recarregar() async {
    state = await AsyncValue.guard(() => ref.read(repositorioDeRotasProvider).obter(rotaId));
  }

  /// Substitui a rota exibida pela devolvida pela API (após otimizar, reordenar...).
  void atualizar(DetalhesDaRota detalhes) => state = AsyncData(detalhes);
}
