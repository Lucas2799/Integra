import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../api/erro_da_api.dart';

/// Mostra um erro da API. Se for limite do plano gratuito, oferece a tela de planos.
Future<void> mostrarErro(BuildContext context, Object erro) async {
  if (!context.mounted) return;
  final e = ErroDaApi.de(erro);
  if (e.limiteDoPlano) {
    final verPlanos = await showDialog<bool>(
      context: context,
      builder: (contexto) => AlertDialog(
        icon: const Icon(Icons.workspace_premium, size: 40),
        title: const Text('Recurso do plano Pro'),
        content: Text(e.mensagem),
        actions: [
          TextButton(onPressed: () => Navigator.pop(contexto, false), child: const Text('Agora não')),
          FilledButton(onPressed: () => Navigator.pop(contexto, true), child: const Text('Ver planos')),
        ],
      ),
    );
    if (verPlanos == true && context.mounted) context.push('/planos');
    return;
  }
  ScaffoldMessenger.of(context)
    ..hideCurrentSnackBar()
    ..showSnackBar(SnackBar(content: Text(e.mensagem), behavior: SnackBarBehavior.floating));
}

void mostrarMensagem(BuildContext context, String mensagem) {
  if (!context.mounted) return;
  ScaffoldMessenger.of(context)
    ..hideCurrentSnackBar()
    ..showSnackBar(SnackBar(content: Text(mensagem), behavior: SnackBarBehavior.floating));
}

/// Executa uma ação mostrando "carregando" e tratando o erro. Devolve o resultado ou null.
Future<T?> executarComCarregamento<T>(BuildContext context, Future<T> Function() acao, {String? mensagem}) async {
  showDialog<void>(
    context: context,
    barrierDismissible: false,
    builder: (_) => PopScope(
      canPop: false,
      child: AlertDialog(
        content: Row(children: [
          const CircularProgressIndicator(),
          const SizedBox(width: 20),
          Expanded(child: Text(mensagem ?? 'Aguarde...')),
        ]),
      ),
    ),
  );
  try {
    final resultado = await acao();
    if (context.mounted) Navigator.of(context, rootNavigator: true).pop();
    return resultado;
  } catch (e) {
    if (context.mounted) {
      Navigator.of(context, rootNavigator: true).pop();
      await mostrarErro(context, e);
    }
    return null;
  }
}
