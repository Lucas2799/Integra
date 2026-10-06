import 'package:flutter/material.dart';

import 'modelos_de_paradas.dart';

class DadosDaEntrega {
  DadosDaEntrega(this.recebidoPor, this.tirarFoto);

  final String? recebidoPor;
  final bool tirarFoto;
}

/// Confirmação de entrega: quem recebeu e, no Pro, foto de comprovante.
Future<DadosDaEntrega?> perguntarDadosDaEntrega(BuildContext context, {required bool podeTirarFoto}) {
  final recebidoPor = TextEditingController();
  var tirarFoto = false;
  return showModalBottomSheet<DadosDaEntrega>(
    context: context,
    isScrollControlled: true,
    builder: (contexto) => StatefulBuilder(
      builder: (contexto, setState) => Padding(
        padding: EdgeInsets.fromLTRB(16, 16, 16, MediaQuery.of(contexto).viewInsets.bottom + 16),
        child: Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.stretch, children: [
          Text('Confirmar entrega', style: Theme.of(contexto).textTheme.titleLarge),
          const SizedBox(height: 12),
          TextField(
            controller: recebidoPor,
            decoration: const InputDecoration(labelText: 'Quem recebeu (opcional)', prefixIcon: Icon(Icons.person)),
            textCapitalization: TextCapitalization.words,
          ),
          CheckboxListTile(
            contentPadding: EdgeInsets.zero,
            value: tirarFoto,
            onChanged: podeTirarFoto ? (v) => setState(() => tirarFoto = v ?? false) : null,
            title: const Text('Tirar foto de comprovante'),
            subtitle: podeTirarFoto ? null : const Text('Disponível no plano Pro'),
          ),
          const SizedBox(height: 8),
          FilledButton.icon(
            onPressed: () => Navigator.pop(contexto, DadosDaEntrega(recebidoPor.text.trim().isEmpty ? null : recebidoPor.text.trim(), tirarFoto)),
            icon: const Icon(Icons.check_circle),
            label: const Text('Entregue'),
          ),
        ]),
      ),
    ),
  );
}

class DadosDaFalha {
  DadosDaFalha(this.motivo, this.estrategia, this.minutos);

  final MotivoDaFalha motivo;
  final EstrategiaDeNovaTentativa estrategia;
  final int minutos;
}

/// "Não entregue": motivo e o que fazer. A rota é recalculada automaticamente no servidor.
Future<DadosDaFalha?> perguntarMotivoDaFalha(BuildContext context) {
  var motivo = MotivoDaFalha.destinatarioAusente;
  var estrategia = EstrategiaDeNovaTentativa.fimDaRota;
  var minutos = 30;
  return showModalBottomSheet<DadosDaFalha>(
    context: context,
    isScrollControlled: true,
    builder: (contexto) => StatefulBuilder(
      builder: (contexto, setState) => SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(16),
          child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
            Text('Não foi possível entregar', style: Theme.of(contexto).textTheme.titleLarge),
            const SizedBox(height: 8),
            Wrap(spacing: 8, runSpacing: 4, children: [
              for (final m in MotivoDaFalha.values)
                ChoiceChip(label: Text(m.rotulo), selected: motivo == m, onSelected: (_) => setState(() => motivo = m)),
            ]),
            const Divider(height: 24),
            Text('O que fazer?', style: Theme.of(contexto).textTheme.titleSmall),
            RadioGroup<EstrategiaDeNovaTentativa>(
              groupValue: estrategia,
              onChanged: (v) => setState(() => estrategia = v!),
              child: Column(children: [
                for (final e in EstrategiaDeNovaTentativa.values)
                  RadioListTile<EstrategiaDeNovaTentativa>(contentPadding: EdgeInsets.zero, value: e, title: Text(e.rotulo)),
              ]),
            ),
            if (estrategia == EstrategiaDeNovaTentativa.aposMinutos)
              Row(children: [
                const Text('Daqui a'),
                Expanded(
                  child: Slider(
                    value: minutos.toDouble(),
                    min: 10,
                    max: 180,
                    divisions: 17,
                    label: '$minutos min',
                    onChanged: (v) => setState(() => minutos = v.round()),
                  ),
                ),
                Text('$minutos min'),
              ]),
            const SizedBox(height: 8),
            FilledButton.icon(
              style: FilledButton.styleFrom(backgroundColor: Theme.of(contexto).colorScheme.error),
              onPressed: () => Navigator.pop(contexto, DadosDaFalha(motivo, estrategia, minutos)),
              icon: const Icon(Icons.replay),
              label: const Text('Registrar e recalcular rota'),
            ),
          ]),
        ),
      ),
    ),
  );
}
