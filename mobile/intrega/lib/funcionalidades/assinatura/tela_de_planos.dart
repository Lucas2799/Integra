import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../nucleo/componentes/avisos.dart';
import '../../nucleo/formatacao/formatos.dart';
import '../../nucleo/provedores.dart';
import '../conta/provedores_da_conta.dart';

final _planosProvider = FutureProvider.autoDispose<Map<String, dynamic>>(
    (ref) async => (await ref.watch(clienteDaApiProvider).dio.get('/assinatura/planos')).data as Map<String, dynamic>);

/// Comparação dos planos (freemium) e assinatura.
///
/// A compra real pelas lojas (Google Play Billing / App Store) entra via `in_app_purchase` ou RevenueCat:
/// o app recebe o comprovante da loja e envia para /assinatura/validar-compra. Em modo de
/// desenvolvimento usamos um comprovante de teste ("dev-...") aceito só quando a API permite.
class TelaDePlanos extends ConsumerWidget {
  const TelaDePlanos({super.key});

  static const _recursos = [
    ('Paradas por rota', 'maximoParadasPorRota'),
    ('Otimizações por dia', 'maximoOtimizacoesPorDia'),
    ('Leituras de etiqueta por dia', 'maximoLeiturasPorDia'),
    ('Recálculos por rota', 'maximoRecalculosPorRota'),
    ('Importar planilha', 'importarPlanilha'),
    ('Janelas de horário', 'janelasDeHorario'),
    ('Navegação dentro do app', 'navegacaoNoApp'),
    ('Foto de comprovante', 'comprovanteDeEntrega'),
    ('Relatórios de 30 dias', 'relatoriosAvancados'),
    ('Gestão de frota', 'gestaoDeFrota'),
  ];

  Future<void> _assinar(BuildContext context, WidgetRef ref, String produtoId) async {
    if (!kDebugMode) {
      mostrarMensagem(context, 'A assinatura pela loja será liberada na publicação do app.');
      return;
    }
    final ok = await executarComCarregamento(context, () async {
      await ref.read(clienteDaApiProvider).dio.post('/assinatura/validar-compra', data: {
        'plataforma': defaultTargetPlatform == TargetPlatform.iOS ? 'ios' : 'android',
        'produtoId': produtoId,
        'comprovante': 'dev-${DateTime.now().microsecondsSinceEpoch}',
      });
      return true;
    });
    if (ok == true && context.mounted) {
      ref.invalidate(contaProvider);
      mostrarMensagem(context, 'Plano ativado (modo de teste).');
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final conta = ref.watch(contaProvider).value;
    return Scaffold(
      appBar: AppBar(title: const Text('Planos')),
      body: ref.watch(_planosProvider).when(
            loading: () => const Center(child: CircularProgressIndicator()),
            error: (e, _) => Center(child: Text('$e')),
            data: (dados) {
              final planos = (dados['planos'] as List).cast<Map<String, dynamic>>();
              final produtos = (dados['produtos'] as List).cast<Map<String, dynamic>>();
              return ListView(padding: const EdgeInsets.all(12), children: [
                if (conta != null)
                  Card(
                    child: ListTile(
                      leading: const Icon(Icons.workspace_premium),
                      title: Text('Seu plano: ${conta.nomeDoPlano}${conta.emPeriodoDeTeste ? ' (teste grátis)' : ''}'),
                      subtitle: conta.planoExpiraEm == null ? null : Text('Válido até ${Formatos.data(conta.planoExpiraEm!)}'),
                    ),
                  ),
                SingleChildScrollView(
                  scrollDirection: Axis.horizontal,
                  child: DataTable(
                    columns: [
                      const DataColumn(label: Text('')),
                      for (final p in planos) DataColumn(label: Text('${p['plano']}', style: const TextStyle(fontWeight: FontWeight.bold))),
                    ],
                    rows: [
                      for (final (rotulo, chave) in _recursos)
                        DataRow(cells: [
                          DataCell(Text(rotulo)),
                          for (final p in planos) DataCell(_valor((p['limites'] as Map)[chave])),
                        ]),
                    ],
                  ),
                ),
                const SizedBox(height: 16),
                for (final produto in produtos)
                  Card(
                    child: ListTile(
                      title: Text('${produto['descricao']}'),
                      subtitle: Text(Formatos.moeda(produto['precoEmReais'] as num)),
                      trailing: FilledButton(onPressed: () => _assinar(context, ref, produto['id'] as String), child: const Text('Assinar')),
                    ),
                  ),
              ]);
            },
          ),
    );
  }

  Widget _valor(Object? v) => switch (v) {
        true => const Icon(Icons.check, color: Colors.green),
        false => const Icon(Icons.close, color: Colors.grey),
        null => const Text('Ilimitado'),
        _ => Text('$v'),
      };
}
