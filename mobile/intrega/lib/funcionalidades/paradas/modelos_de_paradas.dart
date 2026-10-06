import 'package:latlong2/latlong.dart';

LatLng? _ponto(Object? j) => j is Map ? LatLng((j['latitude'] as num).toDouble(), (j['longitude'] as num).toDouble()) : null;
DateTime? _data(Object? v) => v == null ? null : DateTime.parse(v as String);

class Endereco {
  Endereco.deJson(Map<String, dynamic> j)
      : logradouro = j['logradouro'] as String?,
        numero = j['numero'] as String?,
        complemento = j['complemento'] as String?,
        bairro = j['bairro'] as String?,
        cidade = j['cidade'] as String?,
        uf = j['uf'] as String?,
        cep = j['cep'] as String?,
        descricao = j['descricao'] as String;

  final String? logradouro, numero, complemento, bairro, cidade, uf, cep;
  final String descricao;

  /// Endereço como a API espera em EnderecoInformado.
  Map<String, dynamic> paraInformado() => {
        'logradouro': logradouro,
        'numero': numero,
        'complemento': complemento,
        'bairro': bairro,
        'cidade': cidade,
        'uf': uf,
        'cep': cep,
      };
}

/// Status possíveis (iguais aos da API).
class StatusDaParada {
  static const pendente = 'Pendente';
  static const entregue = 'Entregue';
  static const adiada = 'Adiada';
  static const devolvida = 'Devolvida';
}

class Parada {
  Parada.deJson(Map<String, dynamic> j)
      : id = j['id'] as String,
        rotaId = j['rotaId'] as String?,
        status = j['status'] as String,
        endereco = Endereco.deJson(j['endereco'] as Map<String, dynamic>),
        local = _ponto(j['local']),
        confiancaDaLocalizacao = (j['confiancaDaLocalizacao'] as num?)?.toDouble(),
        precisaConfirmarLocal = j['precisaConfirmarLocal'] as bool,
        nomeDoDestinatario = j['nomeDoDestinatario'] as String?,
        telefoneDoDestinatario = j['telefoneDoDestinatario'] as String?,
        observacoes = j['observacoes'] as String?,
        codigosDePacote = (j['codigosDePacote'] as List).cast<String>(),
        quantidadeDePacotes = j['quantidadeDePacotes'] as int,
        marketplace = j['marketplace'] as String,
        tentativas = j['tentativas'] as int,
        novaTentativaApos = _data(j['novaTentativaApos']),
        entregueEm = _data(j['entregueEm']),
        temComprovante = j['temComprovante'] as bool;

  final String id;
  final String? rotaId;
  final String status;
  final Endereco endereco;
  final LatLng? local;
  final double? confiancaDaLocalizacao;
  final bool precisaConfirmarLocal;
  final String? nomeDoDestinatario;
  final String? telefoneDoDestinatario;
  final String? observacoes;
  final List<String> codigosDePacote;
  final int quantidadeDePacotes;
  final String marketplace;
  final int tentativas;
  final DateTime? novaTentativaApos;
  final DateTime? entregueEm;
  final bool temComprovante;

  bool get pendente => status == StatusDaParada.pendente;
  bool get novaTentativa => pendente && tentativas > 0;
}

class SugestaoDeEndereco {
  SugestaoDeEndereco.deJson(Map<String, dynamic> j)
      : descricao = j['descricao'] as String,
        endereco = Endereco.deJson(j['endereco'] as Map<String, dynamic>),
        local = _ponto(j['local'])!;

  final String descricao;
  final Endereco endereco;
  final LatLng local;
}

/// Motivos e estratégias quando a entrega não acontece (valores iguais aos da API).
enum MotivoDaFalha {
  destinatarioAusente('DestinatarioAusente', 'Destinatário ausente'),
  recusada('Recusada', 'Recusou o pacote'),
  enderecoNaoEncontrado('EnderecoNaoEncontrado', 'Endereço não encontrado'),
  acessoNegado('AcessoNegado', 'Sem acesso (portaria/condomínio)'),
  avariada('Avariada', 'Pacote avariado'),
  outro('Outro', 'Outro motivo');

  const MotivoDaFalha(this.valor, this.rotulo);
  final String valor;
  final String rotulo;
}

enum EstrategiaDeNovaTentativa {
  fimDaRota('FimDaRota', 'Tentar de novo no fim da rota'),
  aposMinutos('AposMinutos', 'Tentar de novo daqui a pouco'),
  proximoDia('ProximoDia', 'Deixar para outro dia'),
  devolverAoRemetente('DevolverAoRemetente', 'Devolver ao remetente');

  const EstrategiaDeNovaTentativa(this.valor, this.rotulo);
  final String valor;
  final String rotulo;
}
