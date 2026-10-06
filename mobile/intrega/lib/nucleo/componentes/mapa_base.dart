import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:latlong2/latlong.dart';

import '../configuracao.dart';

/// Mapa padrão do app (OpenStreetMap). Trocar de provedor de mapa = mudar só aqui e a URL dos tiles.
class MapaBase extends StatelessWidget {
  const MapaBase({
    super.key,
    required this.centro,
    this.zoom = 14,
    this.controlador,
    this.camadas = const [],
    this.aoMover,
  });

  final LatLng centro;
  final double zoom;
  final MapController? controlador;
  final List<Widget> camadas;
  final void Function(MapCamera camera, bool porGesto)? aoMover;

  @override
  Widget build(BuildContext context) => FlutterMap(
        mapController: controlador,
        options: MapOptions(
          initialCenter: centro,
          initialZoom: zoom,
          onPositionChanged: aoMover,
          interactionOptions: const InteractionOptions(flags: InteractiveFlag.all & ~InteractiveFlag.rotate),
        ),
        children: [
          TileLayer(urlTemplate: Configuracao.urlDosTiles, userAgentPackageName: Configuracao.identificacaoDoApp),
          ...camadas,
          const RichAttributionWidget(attributions: [TextSourceAttribution('© colaboradores do OpenStreetMap')]),
        ],
      );
}

/// Marcador numerado com a ordem de entrega.
class MarcadorDeParada extends StatelessWidget {
  const MarcadorDeParada({super.key, required this.texto, required this.cor, this.destaque = false});

  final String texto;
  final Color cor;
  final bool destaque;

  @override
  Widget build(BuildContext context) => Container(
        decoration: BoxDecoration(
          color: cor,
          shape: BoxShape.circle,
          border: Border.all(color: Colors.white, width: destaque ? 4 : 2),
          boxShadow: const [BoxShadow(blurRadius: 4, color: Colors.black38)],
        ),
        alignment: Alignment.center,
        child: Text(texto, style: const TextStyle(color: Colors.white, fontWeight: FontWeight.bold, fontSize: 13)),
      );
}
