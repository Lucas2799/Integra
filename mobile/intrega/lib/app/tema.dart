import 'package:flutter/material.dart';

/// Identidade visual do Intrega: botões grandes e alto contraste para usar no trânsito, com uma mão.
class TemaIntrega {
  TemaIntrega._();

  static const corPrincipal = Color(0xFF0B6E4F);
  static const corEntregue = Color(0xFF2E7D32);
  static const corPendente = Color(0xFF1565C0);
  static const corAlerta = Color(0xFFE65100);
  static const corFalha = Color(0xFFC62828);

  static ThemeData claro() => _base(Brightness.light);
  static ThemeData escuro() => _base(Brightness.dark);

  static ThemeData _base(Brightness brilho) {
    final cores = ColorScheme.fromSeed(seedColor: corPrincipal, brightness: brilho);
    return ThemeData(
      colorScheme: cores,
      useMaterial3: true,
      visualDensity: VisualDensity.standard,
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(minimumSize: const Size(64, 52), textStyle: const TextStyle(fontSize: 16)),
      ),
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(minimumSize: const Size(64, 52)),
      ),
      inputDecorationTheme: const InputDecorationTheme(border: OutlineInputBorder()),
      cardTheme: const CardThemeData(margin: EdgeInsets.symmetric(horizontal: 12, vertical: 6)),
    );
  }
}
