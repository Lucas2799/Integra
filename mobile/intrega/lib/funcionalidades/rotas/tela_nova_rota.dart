import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:latlong2/latlong.dart';

import '../../nucleo/componentes/avisos.dart';
import '../../nucleo/formatacao/formatos.dart';
import '../paradas/campo_de_busca_de_endereco.dart';
import '../rastreamento/rastreador_de_jornada.dart';
import 'modelos_de_rotas.dart';
import 'provedores_de_rotas.dart';
import 'repositorio_de_rotas.dart';

/// Cria a rota do dia: ponto de saída (posição atual ou endereço), veículo e se volta ao início.
class TelaNovaRota extends ConsumerStatefulWidget {
  const TelaNovaRota({super.key});

  @override
  ConsumerState<TelaNovaRota> createState() => _TelaNovaRotaState();
}

class _TelaNovaRotaState extends ConsumerState<TelaNovaRota> {
  final _nome = TextEditingController();
  DateTime _data = DateTime.now();
  PerfilDeVeiculo _veiculo = PerfilDeVeiculo.moto;
  bool _voltarAoInicio = false;
  LatLng? _saida;
  String? _descricaoDaSaida;

  @override
  void initState() {
    super.initState();
    _nome.text = 'Rota ${Formatos.data(_data).substring(0, 5)}';
    _usarPosicaoAtual();
  }

  @override
  void dispose() {
    _nome.dispose();
    super.dispose();
  }

  Future<void> _usarPosicaoAtual() async {
    final posicao = await ref.read(servicoDeLocalizacaoProvider).posicaoAtual();
    if (posicao != null && mounted) {
      setState(() {
        _saida = posicao;
        _descricaoDaSaida = 'Minha localização';
      });
    }
  }

  Future<void> _criar() async {
    if (_saida == null) return mostrarMensagem(context, 'Defina o ponto de saída.');
    final rota = await executarComCarregamento(
      context,
      () => ref.read(repositorioDeRotasProvider).criar(
            nome: _nome.text.trim(),
            data: _data,
            saida: _saida!,
            descricaoDaSaida: _descricaoDaSaida,
            voltarAoInicio: _voltarAoInicio,
            veiculo: _veiculo,
          ),
    );
    if (rota == null || !mounted) return;
    ref.invalidate(listaDeRotasProvider);
    context.pushReplacement('/rotas/${rota.resumo.id}');
  }

  @override
  Widget build(BuildContext context) => Scaffold(
        appBar: AppBar(title: const Text('Nova rota')),
        body: ListView(padding: const EdgeInsets.all(16), children: [
          TextField(controller: _nome, decoration: const InputDecoration(labelText: 'Nome da rota')),
          const SizedBox(height: 12),
          ListTile(
            contentPadding: EdgeInsets.zero,
            leading: const Icon(Icons.calendar_today),
            title: Text(Formatos.diaDaSemana(_data)),
            trailing: const Text('Alterar'),
            onTap: () async {
              final escolhida = await showDatePicker(
                context: context,
                initialDate: _data,
                firstDate: DateTime.now().subtract(const Duration(days: 1)),
                lastDate: DateTime.now().add(const Duration(days: 30)),
              );
              if (escolhida != null) setState(() => _data = escolhida);
            },
          ),
          const SizedBox(height: 8),
          Text('Ponto de saída', style: Theme.of(context).textTheme.titleSmall),
          const SizedBox(height: 8),
          if (_saida != null)
            Card(
              margin: EdgeInsets.zero,
              child: ListTile(leading: const Icon(Icons.my_location), title: Text(_descricaoDaSaida ?? 'Ponto escolhido')),
            ),
          const SizedBox(height: 8),
          CampoDeBuscaDeEndereco(
            rotulo: 'Ou busque o endereço de saída (ex.: centro de distribuição)',
            proximoDe: _saida,
            aoEscolher: (s) => setState(() {
              _saida = s.local;
              _descricaoDaSaida = s.descricao;
            }),
          ),
          TextButton.icon(onPressed: _usarPosicaoAtual, icon: const Icon(Icons.gps_fixed), label: const Text('Usar minha localização')),
          const SizedBox(height: 12),
          Text('Veículo', style: Theme.of(context).textTheme.titleSmall),
          const SizedBox(height: 8),
          SegmentedButton<PerfilDeVeiculo>(
            segments: [for (final v in PerfilDeVeiculo.values) ButtonSegment(value: v, label: Text(v.rotulo))],
            selected: {_veiculo},
            onSelectionChanged: (s) => setState(() => _veiculo = s.first),
          ),
          SwitchListTile(
            contentPadding: EdgeInsets.zero,
            title: const Text('Voltar ao ponto de saída no fim'),
            value: _voltarAoInicio,
            onChanged: (v) => setState(() => _voltarAoInicio = v),
          ),
          const SizedBox(height: 16),
          FilledButton.icon(onPressed: _criar, icon: const Icon(Icons.check), label: const Text('Criar rota')),
        ]),
      );
}
