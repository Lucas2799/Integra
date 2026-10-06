/// Configurações do app definidas na compilação:
///   flutter run --dart-define=URL_API=http://192.168.0.10:5080
class Configuracao {
  Configuracao._();

  /// 10.0.2.2 é o "localhost" do computador visto de dentro do emulador Android.
  static const urlDaApi = String.fromEnvironment('URL_API', defaultValue: 'http://10.0.2.2:5080');

  /// Tiles do OpenStreetMap: grátis para desenvolvimento. Em produção troque por um provedor
  /// próprio ou pago (MapTiler, Stadia, servidor próprio) — a política do OSM proíbe uso intenso.
  static const urlDosTiles = String.fromEnvironment(
    'URL_TILES',
    defaultValue: 'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
  );

  static const identificacaoDoApp = 'app.intrega';
}
