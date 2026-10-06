import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:latlong2/latlong.dart';

import '../../nucleo/componentes/avisos.dart';
import '../rotas/provedores_de_rotas.dart';
import '../rotas/repositorio_de_rotas.dart';
import 'campo_de_busca_de_endereco.dart';
import 'modelos_de_paradas.dart';

/// Inclusão de destino: busca com sugestões (ou CEP + número) e dados do destinatário.
class TelaNovaParada extends ConsumerStatefulWidget {
  const TelaNovaParada({super.key, required this.rotaId});

  final String rotaId;

  @override
  ConsumerState<TelaNovaParada> createState() => _TelaNovaParadaState();
}

class _TelaNovaParadaState extends ConsumerState<TelaNovaParada> {
  final _cep = TextEditingController();
  final _logradouro = TextEditingController();
  final _numero = TextEditingController();
  final _complemento = TextEditingController();
  final _cidade = TextEditingController();
  final _destinatario = TextEditingController();
  final _telefone = TextEditingController();
  final _observacoes = TextEditingController();
  final _codigo = TextEditingController();
  LatLng? _localEscolhido;
  Endereco? _enderecoEscolhido;
  int _adicionadas = 0;

  @override
  void dispose() {
    for (final c in [_cep, _logradouro, _numero, _complemento, _cidade, _destinatario, _telefone, _observacoes, _codigo]) {
      c.dispose();
    }
    super.dispose();
  }

  String? _texto(TextEditingController c) => c.text.trim().isEmpty ? null : c.text.trim();

  Future<void> _salvar({required bool continuar}) async {
    if (_enderecoEscolhido == null && _texto(_logradouro) == null && _texto(_cep) == null) {
      return mostrarMensagem(context, 'Informe o endereço ou o CEP.');
    }
    // Se escolheu uma sugestão e não mudou a rua, usa a coordenada dela (mais precisa).
    final usarSugestao = _enderecoEscolhido != null && _texto(_logradouro) == _enderecoEscolhido!.logradouro;
    final dados = {
      'endereco': {
        ...?_enderecoEscolhido?.paraInformado(),
        'logradouro': _texto(_logradouro),
        'numero': _texto(_numero),
        'complemento': _texto(_complemento),
        'cidade': _texto(_cidade) ?? _enderecoEscolhido?.cidade,
        'cep': _texto(_cep) ?? _enderecoEscolhido?.cep,
      },
      if (usarSugestao && _numero.text.trim() == (_enderecoEscolhido!.numero ?? '')) ...{
        'latitude': _localEscolhido!.latitude,
        'longitude': _localEscolhido!.longitude,
      },
      'nomeDoDestinatario': _texto(_destinatario),
      'telefoneDoDestinatario': _texto(_telefone),
      'observacoes': _texto(_observacoes),
      if (_texto(_codigo) != null) 'codigosDePacote': [_texto(_codigo)],
    };
    final parada = await executarComCarregamento(context, () => ref.read(repositorioDeRotasProvider).incluirParada(widget.rotaId, dados),
        mensagem: 'Localizando endereço...');
    if (parada == null || !mounted) return;
    ref.invalidate(detalhesDaRotaProvider(widget.rotaId));

    if (parada.precisaConfirmarLocal) {
      mostrarMensagem(context, 'Não achamos o número exato. Confira o pino no mapa.');
      await context.push('/paradas/${parada.id}/local', extra: parada.local);
      if (!mounted) return;
    }
    if (!continuar) return context.pop();
    setState(() {
      _adicionadas++;
      for (final c in [_cep, _logradouro, _numero, _complemento, _destinatario, _telefone, _observacoes, _codigo]) {
        c.clear();
      }
      _enderecoEscolhido = null;
      _localEscolhido = null;
    });
    mostrarMensagem(context, 'Parada adicionada ($_adicionadas). Pode incluir a próxima.');
  }

  @override
  Widget build(BuildContext context) {
    final saida = ref.watch(detalhesDaRotaProvider(widget.rotaId)).value?.saida;
    return Scaffold(
      appBar: AppBar(title: const Text('Nova parada')),
      body: ListView(padding: const EdgeInsets.all(16), children: [
        CampoDeBuscaDeEndereco(
          proximoDe: saida,
          aoEscolher: (s) => setState(() {
            _enderecoEscolhido = s.endereco;
            _localEscolhido = s.local;
            _logradouro.text = s.endereco.logradouro ?? '';
            _numero.text = s.endereco.numero ?? '';
            _cidade.text = s.endereco.cidade ?? '';
            _cep.text = s.endereco.cep ?? '';
          }),
        ),
        const SizedBox(height: 16),
        Row(children: [
          Expanded(child: TextField(controller: _cep, decoration: const InputDecoration(labelText: 'CEP'), keyboardType: TextInputType.number)),
          const SizedBox(width: 8),
          Expanded(child: TextField(controller: _numero, decoration: const InputDecoration(labelText: 'Número'))),
        ]),
        const SizedBox(height: 8),
        TextField(controller: _logradouro, decoration: const InputDecoration(labelText: 'Rua / Avenida')),
        const SizedBox(height: 8),
        Row(children: [
          Expanded(child: TextField(controller: _complemento, decoration: const InputDecoration(labelText: 'Complemento'))),
          const SizedBox(width: 8),
          Expanded(child: TextField(controller: _cidade, decoration: const InputDecoration(labelText: 'Cidade'))),
        ]),
        const Divider(height: 32),
        TextField(controller: _destinatario, decoration: const InputDecoration(labelText: 'Destinatário', prefixIcon: Icon(Icons.person)),
            textCapitalization: TextCapitalization.words),
        const SizedBox(height: 8),
        TextField(controller: _telefone, decoration: const InputDecoration(labelText: 'Telefone', prefixIcon: Icon(Icons.phone)),
            keyboardType: TextInputType.phone),
        const SizedBox(height: 8),
        TextField(controller: _codigo, decoration: const InputDecoration(labelText: 'Código do pacote', prefixIcon: Icon(Icons.qr_code))),
        const SizedBox(height: 8),
        TextField(controller: _observacoes, decoration: const InputDecoration(labelText: 'Observações (ex.: portão azul)'), maxLines: 2),
        const SizedBox(height: 20),
        FilledButton.icon(onPressed: () => _salvar(continuar: true), icon: const Icon(Icons.add), label: const Text('Salvar e adicionar outra')),
        const SizedBox(height: 8),
        OutlinedButton(onPressed: () => _salvar(continuar: false), child: const Text('Salvar e voltar')),
      ]),
    );
  }
}
