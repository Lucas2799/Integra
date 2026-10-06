import 'package:flutter_test/flutter_test.dart';
import 'package:intrega/nucleo/geo/polilinha.dart';

void main() {
  test('decodifica o exemplo oficial do Google (mesmo formato da API)', () {
    final pontos = decodificarPolilinha('_p~iF~ps|U_ulLnnqC_mqNvxq`@');

    expect(pontos, hasLength(3));
    expect(pontos[0].latitude, closeTo(38.5, 1e-5));
    expect(pontos[0].longitude, closeTo(-120.2, 1e-5));
    expect(pontos[2].latitude, closeTo(43.252, 1e-5));
    expect(pontos[2].longitude, closeTo(-126.453, 1e-5));
  });

  test('traçado vazio ou nulo não quebra', () {
    expect(decodificarPolilinha(null), isEmpty);
    expect(decodificarPolilinha(''), isEmpty);
  });
}
