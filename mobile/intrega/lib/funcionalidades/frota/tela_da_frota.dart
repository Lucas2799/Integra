import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:latlong2/latlong.dart';

import '../../nucleo/api/erro_da_api.dart';
import '../../nucleo/componentes/avisos.dart';
import '../../nucleo/componentes/mapa_base.dart';
import '../../nucleo/formatacao/formatos.dart';
import '../../nucleo/provedores.dart';
import '../../nucleo/sessao/armazenamento_da_sessao.dart';
import '../conta/provedores_da_conta.dart';
import '../rastreamento/rastreador_de_jornada.dart';

final _organizacaoProvider = FutureProvider.autoDispose<Map<String, dynamic>?>((ref) async {
  try {
    return (await ref.watch(clienteDaApiProvider).dio.get('/organizacoes/minha')).data as Map<String, dynamic>;
  } on DioException catch (e) {
    if (e.response?.statusCode == 404) return null;
    throw ErroDaApi.de(e);
  }
});

/// B2B: criar/entrar numa empresa; o gestor convida entregadores, distribui as paradas e acompanha ao vivo.
class TelaDaFrota extends ConsumerWidget {
  const TelaDaFrota({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) => Scaffold(
        appBar: AppBar(title: const Text('Empresa / frota')),
        body: ref.watch(_organizacaoProvider).when(
              loading: () => const Center(child: CircularProgressIndicator()),
              error: (e, _) => Center(child: Text(ErroDaApi.de(e).mensagem)),
              data: (org) => org == null ? const _SemOrganizacao() : _PainelDaOrganizacao(org: org),
            ),
      );
}

class _SemOrganizacao extends ConsumerWidget {
  const _SemOrganizacao();

  Future<void> _enviar(BuildContext context, WidgetRef ref, String caminho, Map<String, dynamic> corpo) async {
    final sessao = await executarComCarregamento(context, () async {
      final r = await ref.read(clienteDaApiProvider).dio.post(caminho, data: corpo);
      return Sessao.deJson(r.data as Map<String, dynamic>);
    });
    if (sessao == null) return;
    // Entrar/criar organização muda as permissões do token: grava a nova sessão.
    await ref.read(estadoDaSessaoProvider.notifier).entrou(sessao);
    ref.invalidate(_organizacaoProvider);
    ref.invalidate(contaProvider);
  }

  Future<String?> _pedirTexto(BuildContext context, String titulo, String rotulo) {
    final texto = TextEditingController();
    return showDialog<String>(
      context: context,
      builder: (c) => AlertDialog(
        title: Text(titulo),
        content: TextField(controller: texto, decoration: InputDecoration(labelText: rotulo), autofocus: true),
        actions: [
          TextButton(onPressed: () => Navigator.pop(c), child: const Text('Cancelar')),
          FilledButton(onPressed: () => Navigator.pop(c, texto.text.trim()), child: const Text('Continuar')),
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) => ListView(padding: const EdgeInsets.all(24), children: [
        const Icon(Icons.groups, size: 64),
        const SizedBox(height: 16),
        const Text('Trabalha para uma transportadora? Entre com o código de convite do seu gestor.\n\n'
            'É gestor? Crie a empresa (14 dias grátis do plano Frota) e convide seus entregadores.', textAlign: TextAlign.center),
        const SizedBox(height: 24),
        FilledButton.icon(
          icon: const Icon(Icons.login),
          label: const Text('Tenho um código de convite'),
          onPressed: () async {
            final codigo = await _pedirTexto(context, 'Entrar na empresa', 'Código de convite');
            if (codigo != null && codigo.isNotEmpty && context.mounted) {
              await _enviar(context, ref, '/organizacoes/entrar', {'codigoDeConvite': codigo});
            }
          },
        ),
        const SizedBox(height: 12),
        OutlinedButton.icon(
          icon: const Icon(Icons.add_business),
          label: const Text('Criar minha empresa'),
          onPressed: () async {
            final nome = await _pedirTexto(context, 'Nova empresa', 'Nome da empresa');
            if (nome != null && nome.isNotEmpty && context.mounted) await _enviar(context, ref, '/organizacoes', {'nome': nome});
          },
        ),
      ]);
}

class _PainelDaOrganizacao extends ConsumerStatefulWidget {
  const _PainelDaOrganizacao({required this.org});

  final Map<String, dynamic> org;

  @override
  ConsumerState<_PainelDaOrganizacao> createState() => _PainelDaOrganizacaoState();
}

class _PainelDaOrganizacaoState extends ConsumerState<_PainelDaOrganizacao> {
  List<Map<String, dynamic>> _rotas = const [];
  List<Map<String, dynamic>> _aoVivo = const [];

  bool get _gestor => widget.org['codigoDeConvite'] != null;
  Dio get _dio => ref.read(clienteDaApiProvider).dio;

  @override
  void initState() {
    super.initState();
    if (_gestor) _carregar();
  }

  Future<void> _carregar() async {
    try {
      final rotas = await _dio.get('/frota/rotas');
      final aoVivo = await _dio.get('/rastreamento/frota/ao-vivo');
      if (!mounted) return;
      setState(() {
        _rotas = (rotas.data as List).cast<Map<String, dynamic>>();
        _aoVivo = (aoVivo.data as List).cast<Map<String, dynamic>>();
      });
    } catch (e) {
      if (mounted) await mostrarErro(context, e);
    }
  }

  /// Distribui todas as paradas sem rota entre os entregadores escolhidos, saindo da posição atual (depósito).
  Future<void> _despachar(List<Map<String, dynamic>> membros) async {
    final paradas = ((await _dio.get('/paradas', queryParameters: {'semRota': true})).data as List).cast<Map<String, dynamic>>();
    if (!mounted) return;
    if (paradas.isEmpty) {
      return mostrarMensagem(context, 'Não há paradas sem rota. Importe uma planilha ou inclua paradas antes.');
    }
    final escolhidos = <String>{};
    final confirmar = await showDialog<bool>(
      context: context,
      builder: (c) => StatefulBuilder(
        builder: (c, setState) => AlertDialog(
          title: Text('Distribuir ${paradas.length} paradas'),
          content: SingleChildScrollView(
            child: Column(mainAxisSize: MainAxisSize.min, children: [
              for (final m in membros)
                CheckboxListTile(
                  value: escolhidos.contains(m['usuarioId']),
                  title: Text('${m['nome']}'),
                  onChanged: (v) => setState(() => v == true ? escolhidos.add(m['usuarioId'] as String) : escolhidos.remove(m['usuarioId'])),
                ),
            ]),
          ),
          actions: [
            TextButton(onPressed: () => Navigator.pop(c, false), child: const Text('Cancelar')),
            FilledButton(onPressed: escolhidos.isEmpty ? null : () => Navigator.pop(c, true), child: const Text('Distribuir')),
          ],
        ),
      ),
    );
    if (confirmar != true || !mounted) return;
    final deposito = await ref.read(servicoDeLocalizacaoProvider).posicaoAtual();
    if (!mounted) return;
    if (deposito == null) return mostrarMensagem(context, 'Ative o GPS: a saída será a sua posição atual.');
    final resposta = await executarComCarregamento(context, () async {
      final r = await _dio.post('/frota/despachar', data: {
        'deposito': {'latitude': deposito.latitude, 'longitude': deposito.longitude, 'descricao': 'Depósito'},
        'entregadorIds': escolhidos.toList(),
        'paradaIds': paradas.map((p) => p['id']).toList(),
      });
      return r.data as Map<String, dynamic>;
    }, mensagem: 'Dividindo as entregas entre a equipe...');
    if (resposta != null && mounted) {
      mostrarMensagem(context, '${(resposta['rotas'] as List).length} rotas criadas.');
      await _carregar();
    }
  }

  @override
  Widget build(BuildContext context) {
    final org = widget.org;
    final membros = (org['membros'] as List).cast<Map<String, dynamic>>();
    return RefreshIndicator(
      onRefresh: _carregar,
      child: ListView(padding: const EdgeInsets.all(12), children: [
        Text('${org['nome']}', style: Theme.of(context).textTheme.headlineSmall),
        if (org['planoExpiraEm'] != null) Text('Plano Frota até ${Formatos.data(DateTime.parse(org['planoExpiraEm'] as String))}'),
        if (_gestor) ...[
          const SizedBox(height: 12),
          Card(
            margin: EdgeInsets.zero,
            child: ListTile(
              leading: const Icon(Icons.key),
              title: Text('Código de convite: ${org['codigoDeConvite']}'),
              subtitle: const Text('Envie para seus entregadores'),
              trailing: IconButton(
                icon: const Icon(Icons.copy),
                onPressed: () {
                  Clipboard.setData(ClipboardData(text: '${org['codigoDeConvite']}'));
                  mostrarMensagem(context, 'Código copiado.');
                },
              ),
            ),
          ),
          const SizedBox(height: 12),
          FilledButton.icon(onPressed: () => _despachar(membros), icon: const Icon(Icons.call_split), label: const Text('Distribuir paradas entre a equipe')),
          if (_aoVivo.isNotEmpty) ...[
            const SizedBox(height: 12),
            SizedBox(
              height: 220,
              child: MapaBase(
                centro: LatLng((_aoVivo.first['latitude'] as num).toDouble(), (_aoVivo.first['longitude'] as num).toDouble()),
                zoom: 12,
                camadas: [
                  MarkerLayer(markers: [
                    for (final p in _aoVivo)
                      Marker(
                        point: LatLng((p['latitude'] as num).toDouble(), (p['longitude'] as num).toDouble()),
                        width: 120,
                        height: 50,
                        child: Column(children: [
                          const Icon(Icons.delivery_dining, color: Colors.deepPurple, size: 28),
                          Text('${p['nome']}', style: const TextStyle(fontSize: 11, fontWeight: FontWeight.bold)),
                        ]),
                      ),
                  ]),
                ],
              ),
            ),
          ],
          const SizedBox(height: 12),
          Text('Rotas de hoje', style: Theme.of(context).textTheme.titleMedium),
          for (final r in _rotas)
            ListTile(
              title: Text('${r['nomeDoEntregador']}'),
              subtitle: Text('${(r['rota'] as Map)['entregues']}/${(r['rota'] as Map)['totalDeParadas']} entregues'),
              trailing: const Icon(Icons.chevron_right),
              onTap: () => context.push('/rotas/${(r['rota'] as Map)['id']}'),
            ),
        ],
        const SizedBox(height: 12),
        Text('Equipe (${membros.length})', style: Theme.of(context).textTheme.titleMedium),
        for (final m in membros)
          ListTile(
            leading: Icon(m['papel'] == 'gestor' ? Icons.manage_accounts : Icons.person),
            title: Text('${m['nome']}'),
            subtitle: Text('${m['email']}'),
          ),
      ]),
    );
  }
}
