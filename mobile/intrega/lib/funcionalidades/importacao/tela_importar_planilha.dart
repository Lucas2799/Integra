import 'dart:async';

import 'package:dio/dio.dart';
import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:url_launcher/url_launcher.dart';

import '../../nucleo/componentes/avisos.dart';
import '../../nucleo/configuracao.dart';
import '../../nucleo/provedores.dart';

/// Importa uma planilha de endereços (.xlsx/.csv). Mostra antes como as colunas foram entendidas.
class TelaImportarPlanilha extends ConsumerStatefulWidget {
  const TelaImportarPlanilha({super.key, required this.rotaId});

  final String rotaId;

  @override
  ConsumerState<TelaImportarPlanilha> createState() => _TelaImportarPlanilhaState();
}

class _TelaImportarPlanilhaState extends ConsumerState<TelaImportarPlanilha> {
  String? _caminho;
  String? _nome;
  Map<String, dynamic>? _previa;
  Map<String, dynamic>? _importacao;
  Timer? _acompanhamento;

  Dio get _dio => ref.read(clienteDaApiProvider).dio;

  @override
  void dispose() {
    _acompanhamento?.cancel();
    super.dispose();
  }

  Future<void> _escolherArquivo() async {
    final arquivos = await FilePicker.pickFiles(type: FileType.custom, allowedExtensions: ['xlsx', 'csv']);
    final arquivo = arquivos.firstOrNull;
    if (arquivo?.path == null || !mounted) return;
    final previa = await executarComCarregamento(context, () async {
      final r = await _dio.post('/importacoes/pre-visualizar',
          data: FormData.fromMap({'arquivo': await MultipartFile.fromFile(arquivo!.path!, filename: arquivo.name)}));
      return r.data as Map<String, dynamic>;
    }, mensagem: 'Lendo planilha...');
    if (previa == null) return;
    setState(() {
      _caminho = arquivo!.path;
      _nome = arquivo.name;
      _previa = previa;
    });
  }

  Future<void> _importar() async {
    final importacao = await executarComCarregamento(context, () async {
      final r = await _dio.post('/importacoes',
          queryParameters: {'rotaId': widget.rotaId},
          data: FormData.fromMap({'arquivo': await MultipartFile.fromFile(_caminho!, filename: _nome)}));
      return r.data as Map<String, dynamic>;
    });
    if (importacao == null) return;
    setState(() => _importacao = importacao);
    // Os endereços são localizados no servidor (1 por segundo no serviço gratuito): acompanha o andamento.
    _acompanhamento = Timer.periodic(const Duration(seconds: 2), (_) async {
      try {
        final r = await _dio.get('/importacoes/${importacao['id']}');
        if (!mounted) return;
        setState(() => _importacao = r.data as Map<String, dynamic>);
        if (_importacao!['status'] == 'Concluida' || _importacao!['status'] == 'Falhou') _acompanhamento?.cancel();
      } catch (_) {}
    });
  }

  @override
  Widget build(BuildContext context) {
    final importacao = _importacao;
    return Scaffold(
      appBar: AppBar(title: const Text('Importar planilha')),
      body: ListView(padding: const EdgeInsets.all(16), children: [
        if (importacao != null) ..._progresso(importacao)
        else ...[
          const Text('A planilha precisa ter uma coluna de endereço (ou rua + número) ou de CEP. '
              'Nome, telefone, código do pacote e observação são opcionais.'),
          TextButton.icon(
            onPressed: () => launchUrl(Uri.parse('${Configuracao.urlDaApi}/api/importacoes/modelo'), mode: LaunchMode.externalApplication),
            icon: const Icon(Icons.download),
            label: const Text('Baixar planilha modelo'),
          ),
          const SizedBox(height: 8),
          OutlinedButton.icon(onPressed: _escolherArquivo, icon: const Icon(Icons.attach_file), label: Text(_nome ?? 'Escolher arquivo')),
          if (_previa != null) ..._resumoDaPrevia(_previa!),
        ],
      ]),
    );
  }

  List<Widget> _resumoDaPrevia(Map<String, dynamic> previa) {
    final colunas = (previa['colunasReconhecidas'] as Map).cast<String, String>();
    final ignoradas = (previa['colunasIgnoradas'] as List).cast<String>();
    final avisos = (previa['avisos'] as List).cast<String>();
    return [
      const SizedBox(height: 16),
      Text('${previa['totalDeLinhas']} linhas encontradas', style: Theme.of(context).textTheme.titleMedium),
      const SizedBox(height: 8),
      for (final e in colunas.entries) Text('✓ "${e.key}" → ${e.value}'),
      for (final c in ignoradas) Text('– "$c" será ignorada', style: TextStyle(color: Theme.of(context).disabledColor)),
      for (final a in avisos) Text('⚠ $a', style: const TextStyle(color: Colors.orange)),
      const SizedBox(height: 16),
      FilledButton.icon(onPressed: _importar, icon: const Icon(Icons.upload), label: const Text('Importar para a rota')),
    ];
  }

  List<Widget> _progresso(Map<String, dynamic> i) {
    final total = (i['totalDeLinhas'] as int).clamp(1, 1 << 30);
    final feitas = i['linhasProcessadas'] as int;
    final concluida = i['status'] == 'Concluida';
    final erros = (i['erros'] as List).cast<Map<String, dynamic>>();
    return [
      Text(concluida ? 'Importação concluída' : 'Localizando endereços... ($feitas de $total)',
          style: Theme.of(context).textTheme.titleMedium),
      const SizedBox(height: 12),
      LinearProgressIndicator(value: feitas / total),
      const SizedBox(height: 12),
      Text('${i['paradasCriadas']} paradas criadas • ${i['precisamConfirmacao']} para conferir no mapa • ${i['semLocalizacao']} sem localização'),
      for (final e in erros) Text('Linha ${e['linha']}: ${e['mensagem']}', style: const TextStyle(color: Colors.red)),
      if (concluida) ...[
        const SizedBox(height: 16),
        FilledButton(onPressed: () => Navigator.pop(context), child: const Text('Voltar para a rota e otimizar')),
      ],
    ];
  }
}
