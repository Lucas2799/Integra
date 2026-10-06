import 'package:latlong2/latlong.dart';

/// Decodifica o traçado no formato "Encoded Polyline" (o mesmo que a API e o OSRM usam).
List<LatLng> decodificarPolilinha(String? codificada, {int precisao = 5}) {
  if (codificada == null || codificada.isEmpty) return const [];
  final fator = _potencia(precisao);
  final pontos = <LatLng>[];
  var indice = 0;
  var lat = 0;
  var lng = 0;

  int proximoValor() {
    var resultado = 0;
    var deslocamento = 0;
    int b;
    do {
      b = codificada.codeUnitAt(indice++) - 63;
      resultado |= (b & 0x1f) << deslocamento;
      deslocamento += 5;
    } while (b >= 0x20);
    return (resultado & 1) != 0 ? ~(resultado >> 1) : resultado >> 1;
  }

  while (indice < codificada.length) {
    lat += proximoValor();
    lng += proximoValor();
    pontos.add(LatLng(lat / fator, lng / fator));
  }
  return pontos;
}

double _potencia(int n) {
  var v = 1.0;
  for (var i = 0; i < n; i++) {
    v *= 10;
  }
  return v;
}
