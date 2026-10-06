import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_tts/flutter_tts.dart';
import 'package:geolocator/geolocator.dart';
import 'package:latlong2/latlong.dart';

import '../../nucleo/componentes/avisos.dart';
import '../../nucleo/componentes/mapa_base.dart';
import '../../nucleo/formatacao/formatos.dart';
import '../../nucleo/geo/localizacao.dart';
import '../../nucleo/geo/polilinha.dart';
import '../rastreamento/rastreador_de_jornada.dart';
import '../rotas/modelos_de_rotas.dart';
import '../rotas/repositorio_de_rotas.dart';

/// Navegação curva a curva dentro do app (plano Pro), com voz em português.
/// O trajeto vem do OSRM via API; o celular só acompanha o GPS e avança os passos.
class TelaDeNavegacao extends ConsumerStatefulWidget {
  const TelaDeNavegacao({super.key, required this.rotaId, this.paradaId});

  final String rotaId;
  final String? paradaId;

  @override
  ConsumerState<TelaDeNavegacao> createState() => _TelaDeNavegacaoState();
}

class _TelaDeNavegacaoState extends ConsumerState<TelaDeNavegacao> {
  static const _distanciaParaAvancarPasso = 25.0;
  static const _distanciaParaChegada = 40.0;
  static const _distanciaForaDaRota = 80.0;

  final _mapa = MapController();
  final _voz = FlutterTts();
  StreamSubscription<Position>? _gps;
  Navegacao? _navegacao;
  List<LatLng> _tracado = const [];
  LatLng? _posicao;
  int _passoAtual = 0;
  int _leiturasForaDaRota = 0;
  bool _recalculando = false;
  bool _chegou = false;

  @override
  void initState() {
    super.initState();
    _voz
      ..setLanguage('pt-BR')
      ..setSpeechRate(0.5);
    _iniciar();
  }

  @override
  void dispose() {
    _gps?.cancel();
    _voz.stop();
    super.dispose();
  }

  Future<void> _iniciar() async {
    final localizacao = ref.read(servicoDeLocalizacaoProvider);
    final inicio = await localizacao.posicaoAtual();
    if (inicio == null) {
      if (mounted) mostrarMensagem(context, 'Ative o GPS para navegar.');
      return;
    }
    _posicao = inicio;
    await _buscarTrajeto(inicio);
    _gps = localizacao.acompanhar(distanciaMinimaMetros: 5).listen((p) => _aoMover(LatLng(p.latitude, p.longitude)));
  }

  Future<void> _buscarTrajeto(LatLng de) async {
    try {
      final navegacao = await ref.read(repositorioDeRotasProvider).navegacao(widget.rotaId, de, paradaId: widget.paradaId);
      if (!mounted) return;
      setState(() {
        _navegacao = navegacao;
        _tracado = decodificarPolilinha(navegacao.geometria);
        _passoAtual = 0;
        _leiturasForaDaRota = 0;
      });
      _falar(navegacao.passos.firstOrNull?.instrucao);
    } catch (e) {
      if (mounted) await mostrarErro(context, e);
    }
  }

  void _aoMover(LatLng posicao) {
    final navegacao = _navegacao;
    if (navegacao == null || _chegou) return;
    setState(() => _posicao = posicao);
    _mapa.move(posicao, _mapa.camera.zoom < 16 ? 17 : _mapa.camera.zoom);

    if (ServicoDeLocalizacao.distanciaEmMetros(posicao, _tracado.isEmpty ? posicao : _tracado.last) < _distanciaParaChegada) {
      setState(() => _chegou = true);
      _falar('Você chegou ao destino');
      return;
    }

    // Avança para o próximo passo quando passa pelo ponto da manobra.
    final proximo = _passoAtual + 1;
    if (proximo < navegacao.passos.length &&
        ServicoDeLocalizacao.distanciaEmMetros(posicao, navegacao.passos[proximo].local) < _distanciaParaAvancarPasso) {
      setState(() => _passoAtual = proximo);
      _falar(navegacao.passos[proximo].instrucao);
    }

    // Saiu do caminho por 3 leituras seguidas: recalcula.
    final distanciaAoTracado = _tracado.isEmpty
        ? 0.0
        : _tracado.map((p) => ServicoDeLocalizacao.distanciaEmMetros(posicao, p)).reduce((a, b) => a < b ? a : b);
    _leiturasForaDaRota = distanciaAoTracado > _distanciaForaDaRota ? _leiturasForaDaRota + 1 : 0;
    if (_leiturasForaDaRota >= 3 && !_recalculando) {
      _recalculando = true;
      _falar('Recalculando');
      _buscarTrajeto(posicao).whenComplete(() => _recalculando = false);
    }
  }

  void _falar(String? texto) {
    if (texto != null) _voz.speak(texto);
  }

  @override
  Widget build(BuildContext context) {
    final navegacao = _navegacao;
    final passo = navegacao == null || navegacao.passos.isEmpty ? null : navegacao.passos[_passoAtual];
    final proximoPasso = navegacao != null && _passoAtual + 1 < navegacao.passos.length ? navegacao.passos[_passoAtual + 1] : null;
    final distanciaAoProximo = proximoPasso != null && _posicao != null
        ? ServicoDeLocalizacao.distanciaEmMetros(_posicao!, proximoPasso.local)
        : null;

    return Scaffold(
      body: Stack(children: [
        if (_posicao != null)
          MapaBase(
            centro: _posicao!,
            zoom: 17,
            controlador: _mapa,
            camadas: [
              if (_tracado.length > 1) PolylineLayer(polylines: [Polyline(points: _tracado, strokeWidth: 7, color: Colors.blue)]),
              MarkerLayer(markers: [
                Marker(point: _posicao!, width: 28, height: 28, child: const Icon(Icons.navigation, color: Colors.blue, size: 28)),
                if (_tracado.isNotEmpty) Marker(point: _tracado.last, width: 40, height: 40, child: const Icon(Icons.flag, color: Colors.red, size: 36)),
              ]),
            ],
          )
        else
          const Center(child: CircularProgressIndicator()),
        SafeArea(
          child: Card(
            color: Theme.of(context).colorScheme.primaryContainer,
            child: ListTile(
              leading: Icon(_iconeDaManobra(proximoPasso ?? passo), size: 40),
              title: Text(_chegou ? 'Você chegou!' : (proximoPasso ?? passo)?.instrucao ?? 'Calculando trajeto...',
                  style: Theme.of(context).textTheme.titleLarge),
              subtitle: distanciaAoProximo == null || _chegou ? null : Text('em ${Formatos.distancia(distanciaAoProximo)}'),
            ),
          ),
        ),
        Positioned(
          left: 12,
          right: 12,
          bottom: 24,
          child: SafeArea(
            child: Card(
              child: Padding(
                padding: const EdgeInsets.all(12),
                child: Row(children: [
                  Expanded(
                    child: Text(
                      navegacao == null
                          ? ''
                          : '${navegacao.descricaoDaParada}\n${Formatos.distancia(navegacao.distanciaMetros)} • ${Formatos.duracao(navegacao.duracaoSegundos)}',
                    ),
                  ),
                  FilledButton(onPressed: () => Navigator.pop(context), child: Text(_chegou ? 'Entregar' : 'Sair')),
                ]),
              ),
            ),
          ),
        ),
      ]),
    );
  }

  IconData _iconeDaManobra(PassoDeNavegacao? passo) {
    final m = passo?.modificador ?? '';
    if (passo?.manobra == 'arrive') return Icons.flag;
    if (passo?.manobra.contains('roundabout') ?? false) return Icons.roundabout_right;
    if (m.contains('uturn')) return Icons.u_turn_left;
    if (m.contains('left')) return Icons.turn_left;
    if (m.contains('right')) return Icons.turn_right;
    return Icons.straight;
  }
}
