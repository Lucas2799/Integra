import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../nucleo/api/erro_da_api.dart';
import 'modelos_de_rotas.dart';
import 'repositorio_de_rotas.dart';

final _carregamentoProvider = FutureProvider.autoDispose.family<List<ItemDeCarregamento>, String>(
    (ref, rotaId) => ref.watch(repositorioDeRotasProvider).ordemDeCarregamento(rotaId));

/// Como arrumar os pacotes no veículo: a última entrega vai no fundo, a primeira fica à mão.
class TelaOrdemDeCarregamento extends ConsumerWidget {
  const TelaOrdemDeCarregamento({super.key, required this.rotaId});

  final String rotaId;

  @override
  Widget build(BuildContext context, WidgetRef ref) => Scaffold(
        appBar: AppBar(title: const Text('Ordem de carregamento')),
        body: ref.watch(_carregamentoProvider(rotaId)).when(
              loading: () => const Center(child: CircularProgressIndicator()),
              error: (e, _) => Center(child: Text(ErroDaApi.de(e).mensagem)),
              data: (itens) => itens.isEmpty
                  ? const Center(child: Text('Otimize a rota para ver a ordem de carregamento.'))
                  : ListView(children: [
                      const Padding(
                        padding: EdgeInsets.all(16),
                        child: Text('Carregue de cima para baixo: o primeiro da lista vai no FUNDO do veículo.'),
                      ),
                      for (final item in itens)
                        ListTile(
                          leading: CircleAvatar(child: Text('${item.posicaoNoCarregamento}')),
                          title: Text(item.descricao),
                          subtitle: Text([
                            'Entrega nº ${item.ordemDeEntrega}',
                            if (item.nomeDoDestinatario != null) item.nomeDoDestinatario!,
                            if (item.codigosDePacote.isNotEmpty) item.codigosDePacote.join(', '),
                          ].join(' • ')),
                          trailing: item.quantidadeDePacotes > 1 ? Chip(label: Text('${item.quantidadeDePacotes} pct')) : null,
                        ),
                    ]),
            ),
      );
}
