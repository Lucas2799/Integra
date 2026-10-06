import 'package:flutter/material.dart';
import 'package:latlong2/latlong.dart';
import 'package:url_launcher/url_launcher.dart';

/// Abre o destino no Waze ou no Google Maps (plano gratuito). Sem custo de API para o Intrega.
class NavegacaoExterna {
  NavegacaoExterna._();

  static Future<void> escolherEAbrir(BuildContext context, LatLng destino) async {
    final app = await showModalBottomSheet<String>(
      context: context,
      builder: (contexto) => SafeArea(
        child: Column(mainAxisSize: MainAxisSize.min, children: [
          ListTile(leading: const Icon(Icons.navigation), title: const Text('Waze'), onTap: () => Navigator.pop(contexto, 'waze')),
          ListTile(leading: const Icon(Icons.map), title: const Text('Google Maps'), onTap: () => Navigator.pop(contexto, 'google')),
        ]),
      ),
    );
    if (app == 'waze') await abrirNoWaze(destino);
    if (app == 'google') await abrirNoGoogleMaps(destino);
  }

  static Future<bool> abrirNoWaze(LatLng d) => _abrir(
        Uri.parse('waze://?ll=${d.latitude},${d.longitude}&navigate=yes'),
        Uri.parse('https://waze.com/ul?ll=${d.latitude},${d.longitude}&navigate=yes'),
      );

  static Future<bool> abrirNoGoogleMaps(LatLng d) => _abrir(
        Uri.parse('google.navigation:q=${d.latitude},${d.longitude}'),
        Uri.parse('https://www.google.com/maps/dir/?api=1&destination=${d.latitude},${d.longitude}&travelmode=driving'),
      );

  /// Tenta o app instalado; se não houver, abre a versão web.
  static Future<bool> _abrir(Uri doApp, Uri web) async {
    if (await canLaunchUrl(doApp) && await launchUrl(doApp)) return true;
    return launchUrl(web, mode: LaunchMode.externalApplication);
  }

  static Future<void> ligar(String telefone) => launchUrl(Uri(scheme: 'tel', path: telefone));
}
