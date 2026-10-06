import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:latlong2/latlong.dart';

import '../../nucleo/componentes/avisos.dart';
import '../../nucleo/componentes/mapa_base.dart';
import '../rastreamento/rastreador_de_jornada.dart';
import '../rotas/repositorio_de_rotas.dart';

/// O entregador move o mapa até o pino ficar na porta certa. A correção vale para as próximas entregas.
class TelaConfirmarLocal extends ConsumerStatefulWidget {
  const TelaConfirmarLocal({super.key, required this.paradaId, this.localInicial});

  final String paradaId;
  final LatLng? localInicial;

  @override
  ConsumerState<TelaConfirmarLocal> createState() => _TelaConfirmarLocalState();
}

class _TelaConfirmarLocalState extends ConsumerState<TelaConfirmarLocal> {
  final _mapa = MapController();
  late LatLng _centro = widget.localInicial ?? const LatLng(-23.5505, -46.6333);

  Future<void> _confirmar() async {
    final parada = await executarComCarregamento(context, () => ref.read(repositorioDeRotasProvider).confirmarLocal(widget.paradaId, _centro));
    if (parada != null && mounted) context.pop();
  }

  @override
  Widget build(BuildContext context) => Scaffold(
        appBar: AppBar(title: const Text('Posição da entrega')),
        body: Stack(children: [
          MapaBase(centro: _centro, zoom: 17, controlador: _mapa, aoMover: (camera, _) => _centro = camera.center),
          const IgnorePointer(
            child: Center(child: Padding(padding: EdgeInsets.only(bottom: 40), child: Icon(Icons.location_pin, size: 48, color: Colors.red))),
          ),
          Positioned(
            right: 16,
            bottom: 100,
            child: FloatingActionButton.small(
              heroTag: 'minha_posicao',
              onPressed: () async {
                final aqui = await ref.read(servicoDeLocalizacaoProvider).posicaoAtual();
                if (aqui != null) _mapa.move(aqui, 18);
              },
              child: const Icon(Icons.my_location),
            ),
          ),
          Positioned(
            left: 16,
            right: 16,
            bottom: 24,
            child: FilledButton.icon(onPressed: _confirmar, icon: const Icon(Icons.check), label: const Text('Confirmar posição')),
          ),
          const Positioned(
            top: 12,
            left: 12,
            right: 12,
            child: Card(child: Padding(padding: EdgeInsets.all(12), child: Text('Arraste o mapa até o pino ficar na porta do destinatário.'))),
          ),
        ]),
      );
}
