import 'package:intl/intl.dart';

/// Formatação padrão brasileira usada em todo o app.
class Formatos {
  Formatos._();

  static final _hora = DateFormat.Hm('pt_BR');
  static final _data = DateFormat('dd/MM/yyyy', 'pt_BR');
  static final _diaDaSemana = DateFormat("EEE, dd 'de' MMM", 'pt_BR');
  static final _moeda = NumberFormat.currency(locale: 'pt_BR', symbol: r'R$');

  static String hora(DateTime? d) => d == null ? '--:--' : _hora.format(d.toLocal());
  static String data(DateTime d) => _data.format(d);
  static String diaDaSemana(DateTime d) => _diaDaSemana.format(d);
  static String moeda(num? valor) => valor == null ? '—' : _moeda.format(valor);

  static String distancia(num? metros) {
    if (metros == null) return '—';
    return metros < 1000 ? '${metros.round()} m' : '${(metros / 1000).toStringAsFixed(1).replaceAll('.', ',')} km';
  }

  static String duracao(num? segundos) {
    if (segundos == null) return '—';
    final minutos = (segundos / 60).round();
    if (minutos < 60) return '$minutos min';
    return '${minutos ~/ 60} h ${(minutos % 60).toString().padLeft(2, '0')} min';
  }

  /// Data no formato da API (yyyy-MM-dd).
  static String dataDaApi(DateTime d) => DateFormat('yyyy-MM-dd').format(d);
}
