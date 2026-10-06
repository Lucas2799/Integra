import 'package:flutter_secure_storage/flutter_secure_storage.dart';

/// Tokens da sessão guardados no armazenamento seguro do aparelho (Keystore/Keychain).
class Sessao {
  const Sessao({required this.tokenDeAcesso, required this.tokenDeAtualizacao});

  final String tokenDeAcesso;
  final String tokenDeAtualizacao;

  factory Sessao.deJson(Map<String, dynamic> json) => Sessao(
        tokenDeAcesso: json['tokenDeAcesso'] as String,
        tokenDeAtualizacao: json['tokenDeAtualizacao'] as String,
      );
}

class ArmazenamentoDaSessao {
  ArmazenamentoDaSessao([FlutterSecureStorage? armazenamento])
      : _armazenamento = armazenamento ?? const FlutterSecureStorage();

  static const _chaveAcesso = 'token_de_acesso';
  static const _chaveAtualizacao = 'token_de_atualizacao';

  final FlutterSecureStorage _armazenamento;

  Future<Sessao?> ler() async {
    final acesso = await _armazenamento.read(key: _chaveAcesso);
    final atualizacao = await _armazenamento.read(key: _chaveAtualizacao);
    if (acesso == null || atualizacao == null) return null;
    return Sessao(tokenDeAcesso: acesso, tokenDeAtualizacao: atualizacao);
  }

  Future<void> salvar(Sessao sessao) async {
    await _armazenamento.write(key: _chaveAcesso, value: sessao.tokenDeAcesso);
    await _armazenamento.write(key: _chaveAtualizacao, value: sessao.tokenDeAtualizacao);
  }

  Future<void> apagar() async {
    await _armazenamento.delete(key: _chaveAcesso);
    await _armazenamento.delete(key: _chaveAtualizacao);
  }
}
