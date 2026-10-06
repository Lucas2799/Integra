import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../nucleo/api/erro_da_api.dart';
import '../../nucleo/componentes/avisos.dart';
import '../../nucleo/formatacao/formatos.dart';
import '../../nucleo/provedores.dart';

final _periodoProvider = NotifierProvider<_Periodo, int>(_Periodo.new);

class _Periodo extends Notifier<int> {
  @override
  int build() => 0;

  void escolher(int dias) => state = dias;
}

final _resultadosProvider = FutureProvider.autoDispose<Map<String, dynamic>>((ref) async {
  final dias = ref.watch(_periodoProvider);
  final hoje = DateTime.now();
  try {
    final r = await ref.watch(clienteDaApiProvider).dio.get('/rastreamento/resumo', queryParameters: {
      'de': Formatos.dataDaApi(hoje.subtract(Duration(days: dias))),
      'ate': Formatos.dataDaApi(hoje),
    });
    return r.data as Map<String, dynamic>;
  } catch (e) {
    throw ErroDaApi.de(e);
  }
});

/// Produtividade e ganhos: entregas, km rodados, horas trabalhadas, combustível e lucro estimado.
class TelaDeResultados extends ConsumerWidget {
  const TelaDeResultados({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final dias = ref.watch(_periodoProvider);
    return Scaffold(
      appBar: AppBar(
        title: const Text('Meus resultados'),
        actions: [IconButton(icon: const Icon(Icons.settings), tooltip: 'Valores e custos', onPressed: () => _configurar(context, ref))],
      ),
      body: ListView(padding: const EdgeInsets.all(12), children: [
        SegmentedButton<int>(
          segments: const [
            ButtonSegment(value: 0, label: Text('Hoje')),
            ButtonSegment(value: 6, label: Text('7 dias')),
            ButtonSegment(value: 29, label: Text('30 dias')),
          ],
          selected: {dias},
          onSelectionChanged: (s) => ref.read(_periodoProvider.notifier).escolher(s.first),
        ),
        const SizedBox(height: 12),
        ref.watch(_resultadosProvider).when(
              loading: () => const Padding(padding: EdgeInsets.all(48), child: Center(child: CircularProgressIndicator())),
              error: (e, _) {
                final erro = ErroDaApi.de(e);
                return Column(children: [
                  Padding(padding: const EdgeInsets.all(24), child: Text(erro.mensagem, textAlign: TextAlign.center)),
                  if (erro.limiteDoPlano) FilledButton(onPressed: () => mostrarErro(context, erro), child: const Text('Ver planos')),
                ]);
              },
              data: (r) => _Painel(dados: r),
            ),
      ]),
    );
  }

  Future<void> _configurar(BuildContext context, WidgetRef ref) async {
    final dio = ref.read(clienteDaApiProvider).dio;
    final atual = (await dio.get('/rastreamento/configuracao-financeira')).data as Map<String, dynamic>;
    if (!context.mounted) return;
    final campos = {
      'valorPorEntrega': ('Quanto recebo por entrega (R\$)', TextEditingController(text: '${atual['valorPorEntrega'] ?? ''}')),
      'valorPorPacote': ('Quanto recebo por pacote (R\$)', TextEditingController(text: '${atual['valorPorPacote'] ?? ''}')),
      'precoDoCombustivelPorLitro': ('Preço do combustível (R\$/L)', TextEditingController(text: '${atual['precoDoCombustivelPorLitro'] ?? ''}')),
      'kmPorLitro': ('Consumo do veículo (km/L)', TextEditingController(text: '${atual['kmPorLitro'] ?? ''}')),
      'custoFixoPorDia': ('Outros custos por dia (R\$)', TextEditingController(text: '${atual['custoFixoPorDia'] ?? ''}')),
    };
    final salvar = await showDialog<bool>(
      context: context,
      builder: (c) => AlertDialog(
        title: const Text('Valores e custos'),
        content: SingleChildScrollView(
          child: Column(mainAxisSize: MainAxisSize.min, children: [
            for (final e in campos.values)
              Padding(
                padding: const EdgeInsets.only(bottom: 8),
                child: TextField(
                  controller: e.$2,
                  decoration: InputDecoration(labelText: e.$1),
                  keyboardType: const TextInputType.numberWithOptions(decimal: true),
                ),
              ),
          ]),
        ),
        actions: [
          TextButton(onPressed: () => Navigator.pop(c, false), child: const Text('Cancelar')),
          FilledButton(onPressed: () => Navigator.pop(c, true), child: const Text('Salvar')),
        ],
      ),
    );
    if (salvar != true || !context.mounted) return;
    await executarComCarregamento(context, () => dio.put('/rastreamento/configuracao-financeira', data: {
          for (final e in campos.entries) e.key: double.tryParse(e.value.$2.text.replaceAll(',', '.')),
        }));
    ref.invalidate(_resultadosProvider);
  }
}

class _Painel extends StatelessWidget {
  const _Painel({required this.dados});

  final Map<String, dynamic> dados;

  @override
  Widget build(BuildContext context) {
    final taxa = ((dados['taxaDeSucesso'] as num) * 100).round();
    final porMarketplace = (dados['porMarketplace'] as List).cast<Map<String, dynamic>>();
    return Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
      Wrap(spacing: 8, runSpacing: 8, children: [
        _Indicador('Entregas', '${dados['entregas']}', Icons.check_circle),
        _Indicador('Pacotes', '${dados['pacotes']}', Icons.inventory_2),
        _Indicador('Sucesso', '$taxa%', Icons.percent),
        _Indicador('Rodados', Formatos.distancia((dados['distanciaKm'] as num) * 1000), Icons.route),
        _Indicador('Trabalhadas', Formatos.duracao((dados['horasAtivas'] as num) * 3600), Icons.schedule),
        _Indicador('Por entrega', dados['minutosPorEntrega'] == null ? '—' : '${dados['minutosPorEntrega']} min', Icons.timer),
      ]),
      const SizedBox(height: 16),
      Card(
        margin: EdgeInsets.zero,
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Text('Financeiro (estimado)', style: Theme.of(context).textTheme.titleMedium),
            const SizedBox(height: 8),
            _Linha('Ganhos', Formatos.moeda(dados['ganhoEstimado'] as num?)),
            _Linha('Combustível', Formatos.moeda(dados['custoDeCombustivel'] as num?)),
            _Linha('Outros custos', Formatos.moeda(dados['custoFixo'] as num?)),
            const Divider(),
            _Linha('Lucro', Formatos.moeda(dados['lucroEstimado'] as num?), destaque: true),
            if (dados['ganhoEstimado'] == null)
              const Text('Toque na engrenagem e informe quanto você recebe para ver seus ganhos.'),
          ]),
        ),
      ),
      if (porMarketplace.isNotEmpty) ...[
        const SizedBox(height: 16),
        Text('Por marketplace', style: Theme.of(context).textTheme.titleMedium),
        for (final m in porMarketplace)
          ListTile(dense: true, title: Text('${m['marketplace']}'), trailing: Text('${m['entregas']} entregas')),
      ],
    ]);
  }
}

class _Indicador extends StatelessWidget {
  const _Indicador(this.rotulo, this.valor, this.icone);

  final String rotulo;
  final String valor;
  final IconData icone;

  @override
  Widget build(BuildContext context) => SizedBox(
        width: (MediaQuery.of(context).size.width - 40) / 3,
        child: Card(
          margin: EdgeInsets.zero,
          child: Padding(
            padding: const EdgeInsets.all(10),
            child: Column(children: [
              Icon(icone, size: 20),
              Text(valor, style: Theme.of(context).textTheme.titleMedium),
              Text(rotulo, style: Theme.of(context).textTheme.bodySmall),
            ]),
          ),
        ),
      );
}

class _Linha extends StatelessWidget {
  const _Linha(this.rotulo, this.valor, {this.destaque = false});

  final String rotulo;
  final String valor;
  final bool destaque;

  @override
  Widget build(BuildContext context) {
    final estilo = destaque ? Theme.of(context).textTheme.titleMedium : null;
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 2),
      child: Row(children: [Expanded(child: Text(rotulo, style: estilo)), Text(valor, style: estilo)]),
    );
  }
}
