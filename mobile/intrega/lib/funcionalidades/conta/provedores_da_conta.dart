import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../nucleo/api/erro_da_api.dart';
import '../../nucleo/provedores.dart';
import 'modelos_da_conta.dart';

/// Conta do usuário logado (plano, limites e uso do dia). Recarregue com ref.invalidate(contaProvider).
final contaProvider = FutureProvider<Conta>((ref) async {
  ref.watch(estadoDaSessaoProvider);
  try {
    final resposta = await ref.watch(clienteDaApiProvider).dio.get('/conta');
    return Conta.deJson(resposta.data as Map<String, dynamic>);
  } catch (e) {
    throw ErroDaApi.de(e);
  }
});
