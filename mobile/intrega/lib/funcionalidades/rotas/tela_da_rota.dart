import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:image_picker/image_picker.dart';
import 'package:latlong2/latlong.dart';

import '../../app/tema.dart';
import '../../nucleo/api/erro_da_api.dart';
import '../../nucleo/componentes/avisos.dart';
import '../../nucleo/componentes/mapa_base.dart';
import '../../nucleo/formatacao/formatos.dart';
import '../../nucleo/geo/polilinha.dart';
import '../conta/provedores_da_conta.dart';
import '../navegacao/navegacao_externa.dart';
import '../paradas/dialogos_de_entrega.dart';
import '../paradas/modelos_de_paradas.dart';
import '../rastreamento/rastreador_de_jornada.dart';
import 'modelos_de_rotas.dart';
import 'provedores_de_rotas.dart';
import 'repositorio_de_rotas.dart';

/// Tela principal do trabalho: mapa com a rota, lista na ordem otimizada e ações de entrega.
class TelaDaRota extends ConsumerStatefulWidget {
  const TelaDaRota({super.key, required this.rotaId});

  final String rotaId;

  @override
  ConsumerState<TelaDaRota> createState() => _TelaDaRotaState();
}

class _TelaDaRotaState extends ConsumerState<TelaDaRota> {
  final _mapa = MapController();

  RepositorioDeRotas get _repositorio => ref.read(repositorioDeRotasProvider);
  ControleDaRota get _controle => ref.read(detalhesDaRotaProvider(widget.rotaId).notifier);

  Future<LatLng?> _posicao() => ref.read(servicoDeLocalizacaoProvider).posicaoAtual();

  Future<void> _otimizar() async {
    final posicao = await _posicao();
    if (!mounted) return;
    final rota = await executarComCarregamento(context, () => _repositorio.otimizar(widget.rotaId, posicaoAtual: posicao),
        mensagem: 'Calculando a melhor rota...');
    if (rota == null) return;
    _controle.atualizar(rota);
    ref.invalidate(contaProvider);
    _enquadrar(rota);
  }

  Future<void> _recalcular() async {
    final posicao = await _posicao();
    if (!mounted) return;
    final rota = await executarComCarregamento(context, () => _repositorio.recalcular(widget.rotaId, posicaoAtual: posicao),
        mensagem: 'Recalculando a partir de onde você está...');
    if (rota != null) _controle.atualizar(rota);
  }

  Future<void> _entregar(Parada parada) async {
    final podeFoto = ref.read(contaProvider).value?.limites.comprovanteDeEntrega ?? false;
    final dados = await perguntarDadosDaEntrega(context, podeTirarFoto: podeFoto);
    if (dados == null || !mounted) return;
    XFile? foto;
    if (dados.tirarFoto) foto = await ImagePicker().pickImage(source: ImageSource.camera, imageQuality: 60, maxWidth: 1600);
    final posicao = await _posicao();
    await ref.read(rastreadorDeJornadaProvider).iniciar(widget.rotaId);
    try {
      final online = await _repositorio.entregar(parada.id, recebidoPor: dados.recebidoPor, local: posicao);
      if (foto != null && online) await _repositorio.enviarComprovante(parada.id, foto.path);
      if (!mounted) return;
      mostrarMensagem(context, online ? 'Entrega registrada!' : 'Sem internet: a entrega será enviada quando a conexão voltar.');
      await _controle.recarregar();
    } catch (e) {
      if (mounted) await mostrarErro(context, e);
    }
  }

  Future<void> _naoEntregue(Parada parada) async {
    final dados = await perguntarMotivoDaFalha(context);
    if (dados == null || !mounted) return;
    final posicao = await _posicao();
    try {
      final online = await _repositorio.naoEntregue(parada.id, dados.motivo, dados.estrategia, minutos: dados.minutos, local: posicao);
      if (!mounted) return;
      mostrarMensagem(context, online ? 'Registrado. A rota foi recalculada.' : 'Sem internet: será enviado quando a conexão voltar.');
      await _controle.recarregar();
    } catch (e) {
      if (mounted) await mostrarErro(context, e);
    }
  }

  Future<void> _navegar(DetalhesDaRota rota, Parada parada) async {
    await ref.read(rastreadorDeJornadaProvider).iniciar(widget.rotaId);
    if (!mounted || parada.local == null) return;
    final navegacaoNoApp = ref.read(contaProvider).value?.limites.navegacaoNoApp ?? false;
    if (navegacaoNoApp) {
      context.push('/rotas/${widget.rotaId}/navegacao?paradaId=${parada.id}');
    } else {
      await NavegacaoExterna.escolherEAbrir(context, parada.local!);
    }
  }

  Future<void> _excluirRota() async {
    final confirmou = await showDialog<bool>(
      context: context,
      builder: (c) => AlertDialog(
        title: const Text('Excluir rota?'),
        content: const Text('As paradas pendentes voltam para a lista de paradas sem rota.'),
        actions: [
          TextButton(onPressed: () => Navigator.pop(c, false), child: const Text('Cancelar')),
          FilledButton(onPressed: () => Navigator.pop(c, true), child: const Text('Excluir')),
        ],
      ),
    );
    if (confirmou != true || !mounted) return;
    final ok = await executarComCarregamento(context, () async {
      await _repositorio.excluir(widget.rotaId);
      return true;
    });
    if (ok != true || !mounted) return;
    ref.invalidate(listaDeRotasProvider);
    context.pop();
  }

  void _enquadrar(DetalhesDaRota rota) {
    final pontos = [rota.saida, ...rota.paradas.map((p) => p.parada.local).whereType<LatLng>()];
    if (pontos.length < 2) return;
    _mapa.fitCamera(CameraFit.coordinates(coordinates: pontos, padding: const EdgeInsets.all(40)));
  }

  void _adicionar() => showModalBottomSheet<void>(
        context: context,
        builder: (contexto) => SafeArea(
          child: Column(mainAxisSize: MainAxisSize.min, children: [
            ListTile(
              leading: const Icon(Icons.edit_location_alt),
              title: const Text('Digitar endereço'),
              onTap: () {
                Navigator.pop(contexto);
                context.push('/rotas/${widget.rotaId}/paradas/nova').then((_) => _controle.recarregar());
              },
            ),
            ListTile(
              leading: const Icon(Icons.qr_code_scanner),
              title: const Text('Ler etiqueta do pacote'),
              subtitle: const Text('Foto da etiqueta: lê QR/código de barras e o endereço'),
              onTap: () {
                Navigator.pop(contexto);
                context.push('/rotas/${widget.rotaId}/etiqueta').then((_) => _controle.recarregar());
              },
            ),
            ListTile(
              leading: const Icon(Icons.table_chart),
              title: const Text('Importar planilha'),
              subtitle: const Text('Arquivo .xlsx ou .csv (Pro)'),
              onTap: () {
                Navigator.pop(contexto);
                context.push('/rotas/${widget.rotaId}/importar').then((_) => _controle.recarregar());
              },
            ),
          ]),
        ),
      );

  @override
  Widget build(BuildContext context) {
    final estado = ref.watch(detalhesDaRotaProvider(widget.rotaId));
    return estado.when(
      loading: () => Scaffold(appBar: AppBar(), body: const Center(child: CircularProgressIndicator())),
      error: (e, _) => Scaffold(appBar: AppBar(), body: Center(child: Text(ErroDaApi.de(e).mensagem))),
      data: (rota) => Scaffold(
        appBar: AppBar(
          title: Text(rota.resumo.nome),
          actions: [
            IconButton(
              tooltip: 'Ordem de carregamento',
              icon: const Icon(Icons.inventory_2),
              onPressed: () => context.push('/rotas/${widget.rotaId}/carregamento'),
            ),
            PopupMenuButton<String>(
              onSelected: (opcao) => opcao == 'recalcular' ? _recalcular() : _excluirRota(),
              itemBuilder: (_) => const [
                PopupMenuItem(value: 'recalcular', child: Text('Recalcular a partir daqui')),
                PopupMenuItem(value: 'excluir', child: Text('Excluir rota')),
              ],
            ),
          ],
        ),
        floatingActionButton: FloatingActionButton(onPressed: _adicionar, tooltip: 'Adicionar parada', child: const Icon(Icons.add_location_alt)),
        body: Column(children: [
          SizedBox(height: MediaQuery.of(context).size.height * 0.35, child: _MapaDaRota(rota: rota, controlador: _mapa)),
          _Resumo(rota: rota, aoOtimizar: _otimizar),
          Expanded(child: _ListaDeParadas(rota: rota, estado: this)),
        ]),
        bottomNavigationBar: rota.proxima == null ? null : _BarraDaProxima(item: rota.proxima!, estado: this, rota: rota),
      ),
    );
  }
}

class _MapaDaRota extends StatelessWidget {
  const _MapaDaRota({required this.rota, required this.controlador});

  final DetalhesDaRota rota;
  final MapController controlador;

  @override
  Widget build(BuildContext context) {
    final tracado = decodificarPolilinha(rota.geometria);
    return MapaBase(
      centro: rota.proxima?.parada.local ?? rota.saida,
      zoom: 13,
      controlador: controlador,
      camadas: [
        if (tracado.length > 1)
          PolylineLayer(polylines: [Polyline(points: tracado, strokeWidth: 5, color: TemaIntrega.corPendente.withValues(alpha: 0.8))]),
        MarkerLayer(markers: [
          Marker(point: rota.saida, width: 34, height: 34, child: const MarcadorDeParada(texto: 'S', cor: Colors.black87)),
          for (final item in rota.paradas.where((p) => p.parada.local != null))
            Marker(
              point: item.parada.local!,
              width: 34,
              height: 34,
              child: MarcadorDeParada(
                texto: item.parada.pendente ? '${item.ordem ?? '?'}' : '✓',
                cor: _corDaParada(item.parada),
                destaque: item.parada.id == rota.proximaParadaId,
              ),
            ),
        ]),
      ],
    );
  }
}

Color _corDaParada(Parada p) => switch (p.status) {
      StatusDaParada.entregue => TemaIntrega.corEntregue,
      StatusDaParada.devolvida || StatusDaParada.adiada => TemaIntrega.corFalha,
      _ => p.novaTentativa ? TemaIntrega.corAlerta : TemaIntrega.corPendente,
    };

class _Resumo extends StatelessWidget {
  const _Resumo({required this.rota, required this.aoOtimizar});

  final DetalhesDaRota rota;
  final VoidCallback aoOtimizar;

  @override
  Widget build(BuildContext context) {
    final r = rota.resumo;
    return Material(
      color: Theme.of(context).colorScheme.surfaceContainerHigh,
      child: Padding(
        padding: const EdgeInsets.fromLTRB(16, 8, 8, 8),
        child: Row(children: [
          Expanded(
            child: Text(
              '${r.entregues}/${r.totalDeParadas} entregues • ${Formatos.distancia(r.distanciaTotalMetros)} • ${Formatos.duracao(r.duracaoTotalSegundos)}',
            ),
          ),
          if (rota.precisaOtimizar || r.otimizadaEm == null)
            FilledButton.icon(onPressed: aoOtimizar, icon: const Icon(Icons.auto_fix_high), label: const Text('Otimizar'))
          else
            TextButton.icon(onPressed: aoOtimizar, icon: const Icon(Icons.auto_fix_high), label: const Text('Otimizar')),
        ]),
      ),
    );
  }
}

class _ListaDeParadas extends ConsumerWidget {
  const _ListaDeParadas({required this.rota, required this.estado});

  final DetalhesDaRota rota;
  final _TelaDaRotaState estado;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final pendentes = rota.pendentes;
    final finalizadas = rota.paradas.where((p) => !p.parada.pendente).toList();
    if (rota.paradas.isEmpty) {
      return const Center(child: Padding(padding: EdgeInsets.all(24), child: Text('Toque em + para adicionar as entregas.')));
    }
    return CustomScrollView(slivers: [
      SliverReorderableList(
        itemCount: pendentes.length,
        onReorderItem: (de, para) async {
          final ids = pendentes.map((p) => p.parada.id).toList();
          ids.insert(para, ids.removeAt(de));
          final nova = await executarComCarregamento(context, () => ref.read(repositorioDeRotasProvider).reordenar(rota.resumo.id, ids));
          if (nova != null) estado._controle.atualizar(nova);
        },
        itemBuilder: (_, i) => ReorderableDelayedDragStartListener(
          key: ValueKey(pendentes[i].parada.id),
          index: i,
          child: _ItemDaParada(item: pendentes[i], estado: estado, rota: rota),
        ),
      ),
      if (finalizadas.isNotEmpty)
        SliverToBoxAdapter(
          child: ExpansionTile(
            title: Text('Finalizadas (${finalizadas.length})'),
            children: [for (final item in finalizadas) _ItemDaParada(item: item, estado: estado, rota: rota)],
          ),
        ),
      const SliverToBoxAdapter(child: SizedBox(height: 96)),
    ]);
  }
}

class _ItemDaParada extends StatelessWidget {
  const _ItemDaParada({required this.item, required this.estado, required this.rota});

  final ParadaNaRota item;
  final _TelaDaRotaState estado;
  final DetalhesDaRota rota;

  @override
  Widget build(BuildContext context) {
    final p = item.parada;
    final avisos = [
      if (p.precisaConfirmarLocal) 'confira o pino',
      if (p.novaTentativa) 'nova tentativa (${p.tentativas}ª)',
      if (item.atrasadaParaJanela) 'fora do horário',
      if (p.quantidadeDePacotes > 1) '${p.quantidadeDePacotes} pacotes',
    ];
    return ListTile(
      leading: CircleAvatar(
        backgroundColor: _corDaParada(p),
        foregroundColor: Colors.white,
        child: Text(p.pendente ? '${item.ordem ?? '•'}' : '✓'),
      ),
      title: Text(p.endereco.descricao, maxLines: 2, overflow: TextOverflow.ellipsis),
      subtitle: Text([
        if (p.nomeDoDestinatario != null) p.nomeDoDestinatario!,
        if (p.pendente && item.previsaoDeChegada != null) 'chega ${Formatos.hora(item.previsaoDeChegada)}',
        if (p.entregueEm != null) 'entregue ${Formatos.hora(p.entregueEm)}',
        ...avisos,
      ].join(' • ')),
      trailing: p.pendente ? const Icon(Icons.drag_handle) : null,
      onTap: () => _abrirDetalhes(context),
    );
  }

  void _abrirDetalhes(BuildContext context) {
    final p = item.parada;
    showModalBottomSheet<void>(
      context: context,
      showDragHandle: true,
      builder: (contexto) => SafeArea(
        child: Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.stretch, children: [
          ListTile(
            title: Text(p.endereco.descricao),
            subtitle: Text([
              if (p.nomeDoDestinatario != null) p.nomeDoDestinatario!,
              if (p.observacoes != null) 'Obs.: ${p.observacoes}',
              if (p.codigosDePacote.isNotEmpty) 'Pacotes: ${p.codigosDePacote.join(', ')}',
            ].join('\n')),
          ),
          if (p.telefoneDoDestinatario != null)
            ListTile(
              leading: const Icon(Icons.phone),
              title: Text('Ligar para ${p.telefoneDoDestinatario}'),
              onTap: () => NavegacaoExterna.ligar(p.telefoneDoDestinatario!),
            ),
          if (p.pendente) ...[
            ListTile(
              leading: const Icon(Icons.navigation),
              title: const Text('Navegar até aqui'),
              onTap: () {
                Navigator.pop(contexto);
                estado._navegar(rota, p);
              },
            ),
            ListTile(
              leading: const Icon(Icons.push_pin),
              title: Text(p.precisaConfirmarLocal ? 'Confirmar posição no mapa (recomendado)' : 'Ajustar posição no mapa'),
              onTap: () {
                Navigator.pop(contexto);
                context.push('/paradas/${p.id}/local', extra: p.local ?? rota.saida).then((_) => estado._controle.recarregar());
              },
            ),
            ListTile(
              leading: const Icon(Icons.check_circle, color: TemaIntrega.corEntregue),
              title: const Text('Entregue'),
              onTap: () {
                Navigator.pop(contexto);
                estado._entregar(p);
              },
            ),
            ListTile(
              leading: const Icon(Icons.cancel, color: TemaIntrega.corFalha),
              title: const Text('Não entregue'),
              onTap: () {
                Navigator.pop(contexto);
                estado._naoEntregue(p);
              },
            ),
            ListTile(
              leading: const Icon(Icons.delete_outline),
              title: const Text('Remover parada'),
              onTap: () async {
                Navigator.pop(contexto);
                await executarComCarregamento(context, () => estado._repositorio.excluirParada(p.id));
                await estado._controle.recarregar();
              },
            ),
          ] else
            ListTile(
              leading: const Icon(Icons.undo),
              title: const Text('Desfazer (voltar para pendente)'),
              onTap: () async {
                Navigator.pop(contexto);
                await executarComCarregamento(context, () => estado._repositorio.reabrir(p.id));
                await estado._controle.recarregar();
              },
            ),
        ]),
      ),
    );
  }
}

/// Barra fixa com a próxima entrega: navegar, entregue e não entregue a um toque.
class _BarraDaProxima extends StatelessWidget {
  const _BarraDaProxima({required this.item, required this.estado, required this.rota});

  final ParadaNaRota item;
  final _TelaDaRotaState estado;
  final DetalhesDaRota rota;

  @override
  Widget build(BuildContext context) => SafeArea(
        child: Material(
          elevation: 8,
          child: Padding(
            padding: const EdgeInsets.all(12),
            child: Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.stretch, children: [
              Text('Próxima: ${item.parada.endereco.descricao}', maxLines: 1, overflow: TextOverflow.ellipsis,
                  style: Theme.of(context).textTheme.titleSmall),
              const SizedBox(height: 8),
              Row(children: [
                Expanded(
                  child: FilledButton.icon(
                    onPressed: () => estado._navegar(rota, item.parada),
                    icon: const Icon(Icons.navigation),
                    label: const Text('Ir'),
                  ),
                ),
                const SizedBox(width: 8),
                IconButton.filled(
                  style: IconButton.styleFrom(backgroundColor: TemaIntrega.corEntregue, minimumSize: const Size(52, 52)),
                  tooltip: 'Entregue',
                  onPressed: () => estado._entregar(item.parada),
                  icon: const Icon(Icons.check),
                ),
                const SizedBox(width: 8),
                IconButton.filled(
                  style: IconButton.styleFrom(backgroundColor: TemaIntrega.corFalha, minimumSize: const Size(52, 52)),
                  tooltip: 'Não entregue',
                  onPressed: () => estado._naoEntregue(item.parada),
                  icon: const Icon(Icons.close),
                ),
              ]),
            ]),
          ),
        ),
      );
}
