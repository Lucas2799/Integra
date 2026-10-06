import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intrega/funcionalidades/rotas/modelos_de_rotas.dart';
import 'package:intrega/nucleo/api/erro_da_api.dart';

Map<String, dynamic> _parada({String status = 'Pendente', int tentativas = 0}) => {
      'id': 'p1',
      'rotaId': 'r1',
      'status': status,
      'endereco': {'logradouro': 'Avenida Paulista', 'numero': '1578', 'descricao': 'Avenida Paulista, 1578 - São Paulo/SP'},
      'local': {'latitude': -23.56, 'longitude': -46.65},
      'confiancaDaLocalizacao': 0.9,
      'precisaConfirmarLocal': false,
      'codigosDePacote': ['BR123'],
      'quantidadeDePacotes': 1,
      'marketplace': 'Shopee',
      'tentativas': tentativas,
      'temComprovante': false,
    };

void main() {
  test('lê os detalhes da rota no formato da API (em português)', () {
    final rota = DetalhesDaRota.deJson({
      'resumo': {
        'id': 'r1',
        'nome': 'Rota 06/10',
        'data': '2026-10-06',
        'status': 'EmAndamento',
        'veiculo': 'Moto',
        'distanciaTotalMetros': 39200,
        'duracaoTotalSegundos': 3420,
        'totalDeParadas': 2,
        'entregues': 1,
        'pendentes': 1,
      },
      'saida': {'latitude': -23.55, 'longitude': -46.63, 'descricao': 'Praça da Sé'},
      'precisaOtimizar': false,
      'proximaParadaId': 'p1',
      'paradas': [
        {'ordem': 1, 'previsaoDeChegada': '2026-10-06T21:20:00Z', 'atrasadaParaJanela': false, 'parada': _parada(tentativas: 1)},
        {'ordem': null, 'atrasadaParaJanela': false, 'parada': _parada(status: 'Entregue')..['id'] = 'p2'},
      ],
    });

    expect(rota.resumo.veiculo, PerfilDeVeiculo.moto);
    expect(rota.resumo.statusLegivel, 'Em andamento');
    expect(rota.proxima?.parada.id, 'p1');
    expect(rota.proxima?.parada.novaTentativa, isTrue);
    expect(rota.pendentes, hasLength(1));
  });

  test('erro 402 da API vira "limite do plano" (abre a tela de planos)', () {
    final erro = ErroDaApi.de(DioException(
      requestOptions: RequestOptions(path: '/rotas/1/otimizar'),
      response: Response(
        requestOptions: RequestOptions(path: '/rotas/1/otimizar'),
        statusCode: 402,
        data: {'detail': 'Limite diário do plano gratuito atingido (2).', 'codigo': 'plano.limite_otimizacoes'},
      ),
    ));

    expect(erro.limiteDoPlano, isTrue);
    expect(erro.codigo, 'plano.limite_otimizacoes');
    expect(erro.mensagem, contains('Limite diário'));
  });

  test('sem resposta do servidor é tratado como sem conexão', () {
    final erro = ErroDaApi.de(DioException(requestOptions: RequestOptions(path: '/rotas')));

    expect(erro.semConexao, isTrue);
  });
}
