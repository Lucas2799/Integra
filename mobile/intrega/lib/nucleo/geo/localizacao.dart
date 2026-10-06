import 'dart:async';

import 'package:geolocator/geolocator.dart';
import 'package:latlong2/latlong.dart';

/// Acesso ao GPS com o pedido de permissão tratado num lugar só.
class ServicoDeLocalizacao {
  Future<bool> garantirPermissao() async {
    if (!await Geolocator.isLocationServiceEnabled()) return false;
    var permissao = await Geolocator.checkPermission();
    if (permissao == LocationPermission.denied) permissao = await Geolocator.requestPermission();
    return permissao == LocationPermission.always || permissao == LocationPermission.whileInUse;
  }

  /// Posição atual, ou null se o GPS estiver desligado/negado (o app segue sem ela).
  Future<LatLng?> posicaoAtual() async {
    if (!await garantirPermissao()) return null;
    try {
      final p = await Geolocator.getCurrentPosition(
        locationSettings: const LocationSettings(accuracy: LocationAccuracy.high, timeLimit: Duration(seconds: 10)),
      );
      return LatLng(p.latitude, p.longitude);
    } on TimeoutException {
      final ultima = await Geolocator.getLastKnownPosition();
      return ultima == null ? null : LatLng(ultima.latitude, ultima.longitude);
    }
  }

  /// Posições contínuas durante a rota/navegação. distanciaMinima evita gastar bateria parado.
  Stream<Position> acompanhar({int distanciaMinimaMetros = 15}) => Geolocator.getPositionStream(
        locationSettings: LocationSettings(accuracy: LocationAccuracy.high, distanceFilter: distanciaMinimaMetros),
      );

  static double distanciaEmMetros(LatLng a, LatLng b) =>
      Geolocator.distanceBetween(a.latitude, a.longitude, b.latitude, b.longitude);
}
