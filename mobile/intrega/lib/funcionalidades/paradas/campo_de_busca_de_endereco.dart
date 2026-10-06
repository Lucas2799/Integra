import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:latlong2/latlong.dart';

import '../rotas/repositorio_de_rotas.dart';
import 'modelos_de_paradas.dart';

/// Busca de endereço com sugestões enquanto digita (aceita CEP). Espera o usuário
/// parar de digitar antes de consultar, para respeitar os limites dos serviços gratuitos.
class CampoDeBuscaDeEndereco extends ConsumerStatefulWidget {
  const CampoDeBuscaDeEndereco({super.key, required this.aoEscolher, this.proximoDe, this.rotulo});

  final void Function(SugestaoDeEndereco sugestao) aoEscolher;
  final LatLng? proximoDe;
  final String? rotulo;

  @override
  ConsumerState<CampoDeBuscaDeEndereco> createState() => _CampoDeBuscaDeEnderecoState();
}

class _CampoDeBuscaDeEnderecoState extends ConsumerState<CampoDeBuscaDeEndereco> {
  final _texto = TextEditingController();
  Timer? _espera;
  List<SugestaoDeEndereco> _sugestoes = const [];
  bool _buscando = false;

  @override
  void dispose() {
    _espera?.cancel();
    _texto.dispose();
    super.dispose();
  }

  void _aoDigitar(String valor) {
    _espera?.cancel();
    if (valor.trim().length < 4) {
      setState(() => _sugestoes = const []);
      return;
    }
    _espera = Timer(const Duration(milliseconds: 600), () async {
      setState(() => _buscando = true);
      try {
        final resultado = await ref.read(repositorioDeRotasProvider).buscarEndereco(valor, proximoDe: widget.proximoDe);
        if (mounted && _texto.text == valor) setState(() => _sugestoes = resultado);
      } catch (_) {
        if (mounted) setState(() => _sugestoes = const []);
      } finally {
        if (mounted) setState(() => _buscando = false);
      }
    });
  }

  @override
  Widget build(BuildContext context) => Column(children: [
        TextField(
          controller: _texto,
          onChanged: _aoDigitar,
          decoration: InputDecoration(
            labelText: widget.rotulo ?? 'Buscar endereço ou CEP',
            prefixIcon: const Icon(Icons.search),
            suffixIcon: _buscando
                ? const Padding(padding: EdgeInsets.all(12), child: SizedBox.square(dimension: 18, child: CircularProgressIndicator(strokeWidth: 2)))
                : null,
          ),
        ),
        for (final s in _sugestoes)
          ListTile(
            dense: true,
            leading: const Icon(Icons.place_outlined),
            title: Text(s.descricao),
            onTap: () {
              widget.aoEscolher(s);
              _texto.text = s.descricao;
              setState(() => _sugestoes = const []);
              FocusScope.of(context).unfocus();
            },
          ),
      ]);
}
