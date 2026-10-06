import 'package:dio/dio.dart';

/// Erro devolvido pela API no formato ProblemDetails, já com a mensagem em português.
class ErroDaApi implements Exception {
  ErroDaApi({required this.mensagem, this.codigo, this.status, this.errosDeCampo = const {}});

  final String mensagem;
  final String? codigo;
  final int? status;
  final Map<String, List<String>> errosDeCampo;

  /// O recurso exige um plano melhor: o app deve abrir a tela de planos.
  bool get limiteDoPlano => status == 402;
  bool get naoAutorizado => status == 401;
  bool get semConexao => status == null;

  factory ErroDaApi.de(Object erro) {
    if (erro is ErroDaApi) return erro;
    if (erro is DioException) {
      final resposta = erro.response;
      if (resposta == null) {
        return ErroDaApi(mensagem: 'Sem conexão com o servidor. Verifique sua internet.');
      }
      final dados = resposta.data;
      if (dados is Map) {
        final campos = <String, List<String>>{};
        final errors = dados['errors'];
        if (errors is Map) {
          errors.forEach((k, v) => campos['$k'] = (v as List).map((e) => '$e').toList());
        }
        return ErroDaApi(
          mensagem: (dados['detail'] ?? campos.values.expand((e) => e).firstOrNull ?? 'Erro inesperado.').toString(),
          codigo: (dados['codigo'] ?? dados['title'])?.toString(),
          status: resposta.statusCode,
          errosDeCampo: campos,
        );
      }
      return ErroDaApi(mensagem: 'Erro ${resposta.statusCode} no servidor.', status: resposta.statusCode);
    }
    return ErroDaApi(mensagem: erro.toString());
  }

  @override
  String toString() => mensagem;
}
