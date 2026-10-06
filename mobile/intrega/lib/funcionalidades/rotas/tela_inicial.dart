import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/tema.dart';
import '../../nucleo/api/erro_da_api.dart';
import '../../nucleo/formatacao/formatos.dart';
import '../../nucleo/provedores.dart';
import '../conta/provedores_da_conta.dart';
import 'modelos_de_rotas.dart';
import 'provedores_de_rotas.dart';

/// Tela inicial: rotas do entregador e atalhos para as outras áreas.
class TelaInicial extends ConsumerWidget {
  const TelaInicial({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final rotas = ref.watch(listaDeRotasProvider);
    final conta = ref.watch(contaProvider).value;

    return Scaffold(
      appBar: AppBar(
        title: const Text('Minhas rotas'),
        actions: [
          if (conta != null)
            Padding(
              padding: const EdgeInsets.only(right: 8),
              child: ActionChip(
                avatar: const Icon(Icons.workspace_premium, size: 18),
                label: Text(conta.emPeriodoDeTeste ? 'Pro (teste)' : conta.nomeDoPlano),
                onPressed: () => context.push('/planos'),
              ),
            ),
        ],
      ),
      drawer: _Menu(nome: conta?.nome, gestor: conta?.organizacao?.gestor ?? false),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: () => context.push('/rotas/nova'),
        icon: const Icon(Icons.add_road),
        label: const Text('Nova rota'),
      ),
      body: RefreshIndicator(
        onRefresh: () async {
          ref.invalidate(listaDeRotasProvider);
          ref.invalidate(contaProvider);
          await ref.read(listaDeRotasProvider.future);
        },
        child: rotas.when(
          loading: () => const Center(child: CircularProgressIndicator()),
          error: (e, _) => ListView(children: [
            const SizedBox(height: 120),
            Icon(Icons.cloud_off, size: 48, color: Theme.of(context).disabledColor),
            Padding(padding: const EdgeInsets.all(16), child: Text(ErroDaApi.de(e).mensagem, textAlign: TextAlign.center)),
          ]),
          data: (lista) => lista.isEmpty
              ? ListView(children: const [
                  SizedBox(height: 120),
                  Icon(Icons.route, size: 64),
                  Padding(
                    padding: EdgeInsets.all(24),
                    child: Text('Crie sua primeira rota e adicione os endereços das entregas.', textAlign: TextAlign.center),
                  ),
                ])
              : ListView.builder(
                  padding: const EdgeInsets.only(bottom: 96),
                  itemCount: lista.length,
                  itemBuilder: (_, i) => _CartaoDaRota(rota: lista[i]),
                ),
        ),
      ),
    );
  }
}

class _CartaoDaRota extends StatelessWidget {
  const _CartaoDaRota({required this.rota});

  final ResumoDaRota rota;

  @override
  Widget build(BuildContext context) {
    final progresso = rota.totalDeParadas == 0 ? 0.0 : rota.entregues / rota.totalDeParadas;
    return Card(
      child: InkWell(
        onTap: () => context.push('/rotas/${rota.id}'),
        child: Padding(
          padding: const EdgeInsets.all(14),
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Row(children: [
              Expanded(child: Text(rota.nome, style: Theme.of(context).textTheme.titleMedium)),
              Chip(label: Text(rota.statusLegivel), visualDensity: VisualDensity.compact),
            ]),
            Text('${Formatos.diaDaSemana(rota.data)} • ${rota.veiculo.rotulo}'),
            const SizedBox(height: 8),
            LinearProgressIndicator(value: progresso, color: TemaIntrega.corEntregue),
            const SizedBox(height: 6),
            Text('${rota.entregues}/${rota.totalDeParadas} entregues • '
                '${Formatos.distancia(rota.distanciaTotalMetros)} • ${Formatos.duracao(rota.duracaoTotalSegundos)}'),
          ]),
        ),
      ),
    );
  }
}

class _Menu extends ConsumerWidget {
  const _Menu({this.nome, required this.gestor});

  final String? nome;
  final bool gestor;

  @override
  Widget build(BuildContext context, WidgetRef ref) => NavigationDrawer(
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(28, 24, 16, 16),
            child: Text(nome ?? 'Intrega', style: Theme.of(context).textTheme.titleLarge),
          ),
          ListTile(leading: const Icon(Icons.insights), title: const Text('Meus resultados'), onTap: () => context.push('/resumo')),
          ListTile(leading: const Icon(Icons.workspace_premium), title: const Text('Planos'), onTap: () => context.push('/planos')),
          ListTile(
            leading: const Icon(Icons.groups),
            title: Text(gestor ? 'Minha frota' : 'Empresa / frota'),
            onTap: () => context.push('/frota'),
          ),
          const Divider(),
          ListTile(
            leading: const Icon(Icons.logout),
            title: const Text('Sair'),
            onTap: () => ref.read(estadoDaSessaoProvider.notifier).sair(),
          ),
        ],
      );
}
