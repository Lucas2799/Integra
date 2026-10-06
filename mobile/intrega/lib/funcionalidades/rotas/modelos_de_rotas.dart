import 'package:latlong2/latlong.dart';

import '../paradas/modelos_de_paradas.dart';

DateTime? _data(Object? v) => v == null ? null : DateTime.parse(v as String);

enum PerfilDeVeiculo {
  carro('Carro', 'Carro'),
  moto('Moto', 'Moto'),
  bicicleta('Bicicleta', 'Bicicleta'),
  aPe('APe', 'A pé');

  const PerfilDeVeiculo(this.valor, this.rotulo);
  final String valor;
  final String rotulo;

  static PerfilDeVeiculo de(String valor) => values.firstWhere((p) => p.valor == valor, orElse: () => carro);
}

class ResumoDaRota {
  ResumoDaRota.deJson(Map<String, dynamic> j)
      : id = j['id'] as String,
        nome = j['nome'] as String,
        data = DateTime.parse(j['data'] as String),
        status = j['status'] as String,
        veiculo = PerfilDeVeiculo.de(j['veiculo'] as String),
        distanciaTotalMetros = (j['distanciaTotalMetros'] as num).toDouble(),
        duracaoTotalSegundos = (j['duracaoTotalSegundos'] as num).toDouble(),
        totalDeParadas = j['totalDeParadas'] as int,
        entregues = j['entregues'] as int,
        pendentes = j['pendentes'] as int,
        otimizadaEm = _data(j['otimizadaEm']);

  final String id;
  final String nome;
  final DateTime data;
  final String status;
  final PerfilDeVeiculo veiculo;
  final double distanciaTotalMetros;
  final double duracaoTotalSegundos;
  final int totalDeParadas;
  final int entregues;
  final int pendentes;
  final DateTime? otimizadaEm;

  String get statusLegivel => switch (status) {
        'Rascunho' => 'Rascunho',
        'Planejada' => 'Planejada',
        'EmAndamento' => 'Em andamento',
        'Concluida' => 'Concluída',
        _ => status,
      };
}

class ParadaNaRota {
  ParadaNaRota.deJson(Map<String, dynamic> j)
      : ordem = j['ordem'] as int?,
        previsaoDeChegada = _data(j['previsaoDeChegada']),
        distanciaDoTrechoMetros = (j['distanciaDoTrechoMetros'] as num?)?.toDouble(),
        atrasadaParaJanela = j['atrasadaParaJanela'] as bool,
        parada = Parada.deJson(j['parada'] as Map<String, dynamic>);

  final int? ordem;
  final DateTime? previsaoDeChegada;
  final double? distanciaDoTrechoMetros;
  final bool atrasadaParaJanela;
  final Parada parada;
}

class DetalhesDaRota {
  DetalhesDaRota.deJson(this.json)
      : resumo = ResumoDaRota.deJson(json['resumo'] as Map<String, dynamic>),
        saida = LatLng(((json['saida'] as Map)['latitude'] as num).toDouble(), ((json['saida'] as Map)['longitude'] as num).toDouble()),
        descricaoDaSaida = (json['saida'] as Map)['descricao'] as String?,
        geometria = json['geometria'] as String?,
        precisaOtimizar = json['precisaOtimizar'] as bool,
        recalculosRestantes = json['recalculosRestantes'] as int?,
        proximaParadaId = json['proximaParadaId'] as String?,
        paradas = (json['paradas'] as List).map((p) => ParadaNaRota.deJson(p as Map<String, dynamic>)).toList();

  /// JSON original, guardado no aparelho para abrir a rota sem internet.
  final Map<String, dynamic> json;
  final ResumoDaRota resumo;
  final LatLng saida;
  final String? descricaoDaSaida;
  final String? geometria;
  final bool precisaOtimizar;
  final int? recalculosRestantes;
  final String? proximaParadaId;
  final List<ParadaNaRota> paradas;

  ParadaNaRota? get proxima => paradas.where((p) => p.parada.id == proximaParadaId).firstOrNull;
  List<ParadaNaRota> get pendentes => paradas.where((p) => p.parada.pendente).toList();
}

class PassoDeNavegacao {
  PassoDeNavegacao.deJson(Map<String, dynamic> j)
      : instrucao = j['instrucao'] as String,
        manobra = j['manobra'] as String,
        modificador = j['modificador'] as String?,
        distanciaMetros = (j['distanciaMetros'] as num).toDouble(),
        local = LatLng((j['latitude'] as num).toDouble(), (j['longitude'] as num).toDouble());

  final String instrucao;
  final String manobra;
  final String? modificador;
  final double distanciaMetros;
  final LatLng local;
}

class Navegacao {
  Navegacao.deJson(Map<String, dynamic> j)
      : paradaId = j['paradaId'] as String,
        descricaoDaParada = j['descricaoDaParada'] as String,
        distanciaMetros = (j['distanciaMetros'] as num).toDouble(),
        duracaoSegundos = (j['duracaoSegundos'] as num).toDouble(),
        geometria = j['geometria'] as String,
        passos = (j['passos'] as List).map((p) => PassoDeNavegacao.deJson(p as Map<String, dynamic>)).toList();

  final String paradaId;
  final String descricaoDaParada;
  final double distanciaMetros;
  final double duracaoSegundos;
  final String geometria;
  final List<PassoDeNavegacao> passos;
}

class ItemDeCarregamento {
  ItemDeCarregamento.deJson(Map<String, dynamic> j)
      : posicaoNoCarregamento = j['posicaoNoCarregamento'] as int,
        ordemDeEntrega = j['ordemDeEntrega'] as int,
        descricao = j['descricao'] as String,
        nomeDoDestinatario = j['nomeDoDestinatario'] as String?,
        codigosDePacote = (j['codigosDePacote'] as List).cast<String>(),
        quantidadeDePacotes = j['quantidadeDePacotes'] as int;

  final int posicaoNoCarregamento;
  final int ordemDeEntrega;
  final String descricao;
  final String? nomeDoDestinatario;
  final List<String> codigosDePacote;
  final int quantidadeDePacotes;
}
