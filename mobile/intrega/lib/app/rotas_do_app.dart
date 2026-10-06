import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:latlong2/latlong.dart';

import '../funcionalidades/assinatura/tela_de_planos.dart';
import '../funcionalidades/autenticacao/tela_de_entrada.dart';
import '../funcionalidades/etiquetas/tela_ler_etiqueta.dart';
import '../funcionalidades/frota/tela_da_frota.dart';
import '../funcionalidades/importacao/tela_importar_planilha.dart';
import '../funcionalidades/navegacao/tela_de_navegacao.dart';
import '../funcionalidades/paradas/tela_confirmar_local.dart';
import '../funcionalidades/paradas/tela_nova_parada.dart';
import '../funcionalidades/rastreamento/tela_de_resultados.dart';
import '../funcionalidades/rotas/tela_da_rota.dart';
import '../funcionalidades/rotas/tela_inicial.dart';
import '../funcionalidades/rotas/tela_nova_rota.dart';
import '../funcionalidades/rotas/tela_ordem_de_carregamento.dart';
import '../nucleo/provedores.dart';

/// Mapa de telas do app. Sem sessão, qualquer endereço leva para /entrar.
final rotasDoAppProvider = Provider<GoRouter>((ref) {
  final aviso = ValueNotifier(ref.read(estadoDaSessaoProvider));
  ref.listen(estadoDaSessaoProvider, (_, novo) => aviso.value = novo);
  ref.onDispose(aviso.dispose);

  return GoRouter(
    initialLocation: '/',
    refreshListenable: aviso,
    redirect: (_, estado) {
      final sessao = aviso.value;
      final naEntrada = estado.matchedLocation == '/entrar';
      if (sessao == EstadoDaSessao.verificando) return null;
      if (sessao == EstadoDaSessao.desconectado) return naEntrada ? null : '/entrar';
      return naEntrada ? '/' : null;
    },
    routes: [
      GoRoute(path: '/entrar', builder: (_, _) => const TelaDeEntrada()),
      GoRoute(path: '/', builder: (_, _) => const TelaInicial()),
      GoRoute(path: '/rotas/nova', builder: (_, _) => const TelaNovaRota()),
      GoRoute(
        path: '/rotas/:id',
        builder: (_, e) => TelaDaRota(rotaId: e.pathParameters['id']!),
        routes: [
          GoRoute(path: 'paradas/nova', builder: (_, e) => TelaNovaParada(rotaId: e.pathParameters['id']!)),
          GoRoute(path: 'etiqueta', builder: (_, e) => TelaLerEtiqueta(rotaId: e.pathParameters['id']!)),
          GoRoute(path: 'importar', builder: (_, e) => TelaImportarPlanilha(rotaId: e.pathParameters['id']!)),
          GoRoute(path: 'carregamento', builder: (_, e) => TelaOrdemDeCarregamento(rotaId: e.pathParameters['id']!)),
          GoRoute(
            path: 'navegacao',
            builder: (_, e) => TelaDeNavegacao(rotaId: e.pathParameters['id']!, paradaId: e.uri.queryParameters['paradaId']),
          ),
        ],
      ),
      GoRoute(
        path: '/paradas/:id/local',
        builder: (_, e) => TelaConfirmarLocal(paradaId: e.pathParameters['id']!, localInicial: e.extra as LatLng?),
      ),
      GoRoute(path: '/resumo', builder: (_, _) => const TelaDeResultados()),
      GoRoute(path: '/planos', builder: (_, _) => const TelaDePlanos()),
      GoRoute(path: '/frota', builder: (_, _) => const TelaDaFrota()),
    ],
    errorBuilder: (_, _) => const Scaffold(body: Center(child: Text('Página não encontrada'))),
  );
});
