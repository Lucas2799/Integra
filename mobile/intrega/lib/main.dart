import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/date_symbol_data_local.dart';

import 'app/aplicativo.dart';
import 'nucleo/offline/banco_local.dart';
import 'nucleo/provedores.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  await initializeDateFormatting('pt_BR');
  final banco = await BancoLocal.abrir();
  runApp(ProviderScope(
    overrides: [bancoLocalProvider.overrideWithValue(banco)],
    child: const AplicativoIntrega(),
  ));
}
