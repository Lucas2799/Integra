import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../nucleo/provedores.dart';
import 'rotas_do_app.dart';
import 'tema.dart';

class AplicativoIntrega extends ConsumerWidget {
  const AplicativoIntrega({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    // Mantém a sincronização offline e o tempo real ativos enquanto houver sessão.
    if (ref.watch(estadoDaSessaoProvider) == EstadoDaSessao.autenticado) {
      ref.watch(sincronizadorProvider);
      ref.watch(eventosTempoRealProvider);
    }
    return MaterialApp.router(
      title: 'Intrega',
      debugShowCheckedModeBanner: false,
      theme: TemaIntrega.claro(),
      darkTheme: TemaIntrega.escuro(),
      routerConfig: ref.watch(rotasDoAppProvider),
      locale: const Locale('pt', 'BR'),
      supportedLocales: const [Locale('pt', 'BR')],
      localizationsDelegates: GlobalMaterialLocalizations.delegates,
    );
  }
}
