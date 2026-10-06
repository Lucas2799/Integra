import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:image_picker/image_picker.dart';

import '../../nucleo/api/erro_da_api.dart';
import '../../nucleo/componentes/avisos.dart';
import '../../nucleo/provedores.dart';
import '../conta/provedores_da_conta.dart';
import 'leitor_de_etiquetas.dart';

/// Foto da etiqueta → código do pacote + endereço do destinatário → parada criada.
/// Sempre mostra os dados para o entregador conferir, porque o OCR pode errar.
class TelaLerEtiqueta extends ConsumerStatefulWidget {
  const TelaLerEtiqueta({super.key, required this.rotaId});

  final String rotaId;

  @override
  ConsumerState<TelaLerEtiqueta> createState() => _TelaLerEtiquetaState();
}

class _TelaLerEtiquetaState extends ConsumerState<TelaLerEtiqueta> {
  final _leitor = LeitorDeEtiquetas();
  final _campos = {
    for (final c in ['nomeDoDestinatario', 'logradouro', 'numero', 'complemento', 'bairro', 'cidade', 'uf', 'cep'])
      c: TextEditingController(),
  };
  LeituraDaEtiqueta? _leitura;
  Map<String, dynamic>? _interpretada;
  bool _processando = false;
  int _lidasNestaSessao = 0;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => _fotografar());
  }

  @override
  void dispose() {
    _leitor.fechar();
    for (final c in _campos.values) {
      c.dispose();
    }
    super.dispose();
  }

  Future<void> _fotografar() async {
    final foto = await ImagePicker().pickImage(source: ImageSource.camera, imageQuality: 90, maxWidth: 2000);
    if (foto == null) return;
    setState(() => _processando = true);
    try {
      final leitura = await _leitor.ler(foto.path);
      if (leitura.vazia) {
        if (mounted) mostrarMensagem(context, 'Não consegui ler nada. Tente com mais luz e a etiqueta inteira na foto.');
        return;
      }
      final resposta = await ref.read(clienteDaApiProvider).dio.post('/etiquetas/interpretar', data: _corpo(leitura));
      final dados = resposta.data as Map<String, dynamic>;
      final endereco = dados['endereco'] as Map<String, dynamic>;
      setState(() {
        _leitura = leitura;
        _interpretada = dados;
        _campos['nomeDoDestinatario']!.text = dados['nomeDoDestinatario'] as String? ?? '';
        for (final c in ['logradouro', 'numero', 'complemento', 'bairro', 'cidade', 'uf', 'cep']) {
          _campos[c]!.text = endereco[c] as String? ?? '';
        }
      });
    } catch (e) {
      if (mounted) await mostrarErro(context, ErroDaApi.de(e));
    } finally {
      if (mounted) setState(() => _processando = false);
    }
  }

  Map<String, dynamic> _corpo(LeituraDaEtiqueta l) => {
        'textoLido': l.texto,
        'codigos': [for (final c in l.codigos) {'formato': c.formato, 'valor': c.valor}],
      };

  String? _valor(String campo) => _campos[campo]!.text.trim().isEmpty ? null : _campos[campo]!.text.trim();

  Future<void> _criarParada() async {
    final resposta = await executarComCarregamento(context, () async {
      final r = await ref.read(clienteDaApiProvider).dio.post('/etiquetas/paradas', data: {
        'rotaId': widget.rotaId,
        ..._corpo(_leitura!),
        'nomeDoDestinatario': _valor('nomeDoDestinatario'),
        'enderecoCorrigido': {
          for (final c in ['logradouro', 'numero', 'complemento', 'bairro', 'cidade', 'uf', 'cep']) c: _valor(c),
        },
      });
      return r.data as Map<String, dynamic>;
    }, mensagem: 'Criando parada...');
    if (resposta == null || !mounted) return;
    ref.invalidate(contaProvider);
    final jaExistia = resposta['jaExistia'] as bool;
    mostrarMensagem(context, jaExistia ? 'Este pacote já estava na rota.' : 'Parada criada! Leia a próxima etiqueta.');
    setState(() {
      if (!jaExistia) _lidasNestaSessao++;
      _leitura = null;
      _interpretada = null;
    });
    await _fotografar();
  }

  @override
  Widget build(BuildContext context) {
    final interpretada = _interpretada;
    final confianca = (interpretada?['confianca'] as num?)?.toDouble() ?? 0;
    final avisos = (interpretada?['avisos'] as List?)?.cast<String>() ?? const [];
    return Scaffold(
      appBar: AppBar(title: Text('Ler etiqueta${_lidasNestaSessao > 0 ? ' ($_lidasNestaSessao lidas)' : ''}')),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: _processando ? null : _fotografar,
        icon: const Icon(Icons.photo_camera),
        label: const Text('Fotografar'),
      ),
      body: _processando
          ? const Center(child: Column(mainAxisSize: MainAxisSize.min, children: [CircularProgressIndicator(), SizedBox(height: 12), Text('Lendo etiqueta...')]))
          : interpretada == null
              ? const Center(
                  child: Padding(
                    padding: EdgeInsets.all(32),
                    child: Text('Fotografe a etiqueta inteira, com boa luz. O app lê o código e o endereço do destinatário.',
                        textAlign: TextAlign.center),
                  ),
                )
              : ListView(padding: const EdgeInsets.fromLTRB(16, 16, 16, 96), children: [
                  Card(
                    margin: EdgeInsets.zero,
                    child: ListTile(
                      leading: Icon(confianca >= 0.75 ? Icons.verified : Icons.warning_amber,
                          color: confianca >= 0.75 ? Colors.green : Colors.orange),
                      title: Text('${interpretada['marketplace']} • ${interpretada['codigoDeRastreio'] ?? 'sem código'}'),
                      subtitle: Text(confianca >= 0.75 ? 'Leitura boa. Confira e confirme.' : 'Leitura incerta: confira os campos.'),
                    ),
                  ),
                  for (final aviso in avisos)
                    Padding(padding: const EdgeInsets.only(top: 6), child: Text('⚠ $aviso', style: const TextStyle(color: Colors.orange))),
                  const SizedBox(height: 12),
                  for (final (campo, rotulo) in const [
                    ('nomeDoDestinatario', 'Destinatário'),
                    ('logradouro', 'Rua / Avenida'),
                    ('numero', 'Número'),
                    ('complemento', 'Complemento'),
                    ('bairro', 'Bairro'),
                    ('cidade', 'Cidade'),
                    ('uf', 'UF'),
                    ('cep', 'CEP'),
                  ])
                    Padding(
                      padding: const EdgeInsets.only(bottom: 8),
                      child: TextField(controller: _campos[campo], decoration: InputDecoration(labelText: rotulo)),
                    ),
                  const SizedBox(height: 8),
                  FilledButton.icon(onPressed: _criarParada, icon: const Icon(Icons.add_location_alt), label: const Text('Confirmar e criar parada')),
                ]),
    );
  }
}
