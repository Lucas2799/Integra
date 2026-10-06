import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../app/tema.dart';
import '../../nucleo/componentes/avisos.dart';
import '../../nucleo/configuracao.dart';
import '../../nucleo/sessao/armazenamento_da_sessao.dart';
import '../../nucleo/provedores.dart';

/// Entrar ou criar conta. Novos usuários ganham alguns dias de Pro para testar.
class TelaDeEntrada extends ConsumerStatefulWidget {
  const TelaDeEntrada({super.key});

  @override
  ConsumerState<TelaDeEntrada> createState() => _TelaDeEntradaState();
}

class _TelaDeEntradaState extends ConsumerState<TelaDeEntrada> {
  final _formulario = GlobalKey<FormState>();
  final _nome = TextEditingController();
  final _email = TextEditingController();
  final _senha = TextEditingController();
  bool _cadastrando = false;
  bool _enviando = false;

  @override
  void dispose() {
    _nome.dispose();
    _email.dispose();
    _senha.dispose();
    super.dispose();
  }

  Future<void> _enviar() async {
    if (!_formulario.currentState!.validate()) return;
    setState(() => _enviando = true);
    try {
      final dio = Dio(BaseOptions(baseUrl: '${Configuracao.urlDaApi}/api'));
      final resposta = _cadastrando
          ? await dio.post('/autenticacao/cadastrar',
              data: {'nome': _nome.text.trim(), 'email': _email.text.trim(), 'senha': _senha.text})
          : await dio.post('/autenticacao/entrar', data: {'email': _email.text.trim(), 'senha': _senha.text});
      await ref.read(estadoDaSessaoProvider.notifier).entrou(Sessao.deJson(resposta.data as Map<String, dynamic>));
    } catch (e) {
      if (mounted) await mostrarErro(context, e);
    } finally {
      if (mounted) setState(() => _enviando = false);
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
        body: SafeArea(
          child: Center(
            child: SingleChildScrollView(
              padding: const EdgeInsets.all(24),
              child: Form(
                key: _formulario,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    const Icon(Icons.route, size: 72, color: TemaIntrega.corPrincipal),
                    const SizedBox(height: 8),
                    Text('Intrega', textAlign: TextAlign.center, style: Theme.of(context).textTheme.headlineLarge),
                    const Text('Suas entregas na melhor rota', textAlign: TextAlign.center),
                    const SizedBox(height: 32),
                    if (_cadastrando) ...[
                      TextFormField(
                        controller: _nome,
                        decoration: const InputDecoration(labelText: 'Seu nome', prefixIcon: Icon(Icons.person)),
                        textCapitalization: TextCapitalization.words,
                        validator: (v) => (v ?? '').trim().length < 2 ? 'Informe seu nome' : null,
                      ),
                      const SizedBox(height: 12),
                    ],
                    TextFormField(
                      controller: _email,
                      decoration: const InputDecoration(labelText: 'E-mail', prefixIcon: Icon(Icons.email)),
                      keyboardType: TextInputType.emailAddress,
                      autofillHints: const [AutofillHints.email],
                      validator: (v) => (v ?? '').contains('@') ? null : 'E-mail inválido',
                    ),
                    const SizedBox(height: 12),
                    TextFormField(
                      controller: _senha,
                      decoration: const InputDecoration(labelText: 'Senha', prefixIcon: Icon(Icons.lock)),
                      obscureText: true,
                      validator: (v) => (v ?? '').length < 8 ? 'Mínimo de 8 caracteres' : null,
                      onFieldSubmitted: (_) => _enviar(),
                    ),
                    const SizedBox(height: 24),
                    FilledButton(
                      onPressed: _enviando ? null : _enviar,
                      child: _enviando
                          ? const SizedBox.square(dimension: 22, child: CircularProgressIndicator(strokeWidth: 2))
                          : Text(_cadastrando ? 'Criar conta grátis' : 'Entrar'),
                    ),
                    TextButton(
                      onPressed: () => setState(() => _cadastrando = !_cadastrando),
                      child: Text(_cadastrando ? 'Já tenho conta' : 'Criar conta (7 dias de Pro grátis)'),
                    ),
                  ],
                ),
              ),
            ),
          ),
        ),
      );
}
