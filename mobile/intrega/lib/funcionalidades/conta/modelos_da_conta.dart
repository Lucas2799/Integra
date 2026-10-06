/// Limites do plano (null = ilimitado). Espelha LimitesDoPlano da API.
class LimitesDoPlano {
  LimitesDoPlano.deJson(Map<String, dynamic> j)
      : maximoParadasPorRota = j['maximoParadasPorRota'] as int?,
        maximoOtimizacoesPorDia = j['maximoOtimizacoesPorDia'] as int?,
        maximoLeiturasPorDia = j['maximoLeiturasPorDia'] as int?,
        maximoRecalculosPorRota = j['maximoRecalculosPorRota'] as int?,
        importarPlanilha = j['importarPlanilha'] as bool,
        janelasDeHorario = j['janelasDeHorario'] as bool,
        navegacaoNoApp = j['navegacaoNoApp'] as bool,
        comprovanteDeEntrega = j['comprovanteDeEntrega'] as bool,
        relatoriosAvancados = j['relatoriosAvancados'] as bool,
        gestaoDeFrota = j['gestaoDeFrota'] as bool;

  final int? maximoParadasPorRota;
  final int? maximoOtimizacoesPorDia;
  final int? maximoLeiturasPorDia;
  final int? maximoRecalculosPorRota;
  final bool importarPlanilha;
  final bool janelasDeHorario;
  final bool navegacaoNoApp;
  final bool comprovanteDeEntrega;
  final bool relatoriosAvancados;
  final bool gestaoDeFrota;
}

class OrganizacaoResumo {
  OrganizacaoResumo.deJson(Map<String, dynamic> j)
      : id = j['id'] as String,
        nome = j['nome'] as String,
        papel = j['papel'] as String;

  final String id;
  final String nome;
  final String papel;

  bool get gestor => papel == 'gestor';
}

class Conta {
  Conta.deJson(Map<String, dynamic> j)
      : id = j['id'] as String,
        nome = j['nome'] as String,
        email = j['email'] as String,
        telefone = j['telefone'] as String?,
        plano = j['plano'] as String,
        planoExpiraEm = j['planoExpiraEm'] == null ? null : DateTime.parse(j['planoExpiraEm'] as String),
        emPeriodoDeTeste = j['emPeriodoDeTeste'] as bool,
        limites = LimitesDoPlano.deJson(j['limites'] as Map<String, dynamic>),
        otimizacoesHoje = (j['usoDeHoje'] as Map)['otimizacoes'] as int,
        leiturasHoje = (j['usoDeHoje'] as Map)['leiturasDeEtiqueta'] as int,
        organizacao = j['organizacao'] == null ? null : OrganizacaoResumo.deJson(j['organizacao'] as Map<String, dynamic>);

  final String id;
  final String nome;
  final String email;
  final String? telefone;
  final String plano;
  final DateTime? planoExpiraEm;
  final bool emPeriodoDeTeste;
  final LimitesDoPlano limites;
  final int otimizacoesHoje;
  final int leiturasHoje;
  final OrganizacaoResumo? organizacao;

  bool get gratuito => plano == 'Gratuito';

  String get nomeDoPlano => switch (plano) { 'Pro' => 'Pro', 'Frota' => 'Frota', _ => 'Gratuito' };
}
