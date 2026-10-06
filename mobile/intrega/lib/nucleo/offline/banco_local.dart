import 'dart:convert';

import 'package:path/path.dart' as caminho;
import 'package:sqflite/sqflite.dart';

/// Banco SQLite do aparelho: guarda a rota aberta e as ações feitas sem internet.
/// O entregador continua trabalhando em garagem, prédio ou área rural sem sinal.
class BancoLocal {
  BancoLocal._(this._banco);

  final Database _banco;

  static Future<BancoLocal> abrir() async {
    final pasta = await getDatabasesPath();
    final banco = await openDatabase(
      caminho.join(pasta, 'intrega.db'),
      version: 1,
      onCreate: (bd, _) async {
        await bd.execute('CREATE TABLE rotas_em_cache (id TEXT PRIMARY KEY, json TEXT NOT NULL, salva_em TEXT NOT NULL)');
        await bd.execute('''
          CREATE TABLE fila_de_sincronizacao (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            metodo TEXT NOT NULL,
            caminho TEXT NOT NULL,
            corpo TEXT,
            criada_em TEXT NOT NULL,
            tentativas INTEGER NOT NULL DEFAULT 0
          )''');
        await bd.execute('''
          CREATE TABLE posicoes_pendentes (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            rota_id TEXT,
            json TEXT NOT NULL
          )''');
      },
    );
    return BancoLocal._(banco);
  }

  // --- Rotas em cache -------------------------------------------------------

  Future<void> salvarRota(String id, Map<String, dynamic> json) => _banco.insert(
        'rotas_em_cache',
        {'id': id, 'json': jsonEncode(json), 'salva_em': DateTime.now().toIso8601String()},
        conflictAlgorithm: ConflictAlgorithm.replace,
      );

  Future<Map<String, dynamic>?> lerRota(String id) async {
    final linhas = await _banco.query('rotas_em_cache', where: 'id = ?', whereArgs: [id]);
    return linhas.isEmpty ? null : jsonDecode(linhas.first['json'] as String) as Map<String, dynamic>;
  }

  // --- Fila de ações offline --------------------------------------------------

  Future<void> enfileirar(String metodo, String caminhoDaApi, Map<String, dynamic>? corpo) => _banco.insert(
        'fila_de_sincronizacao',
        {
          'metodo': metodo,
          'caminho': caminhoDaApi,
          'corpo': corpo == null ? null : jsonEncode(corpo),
          'criada_em': DateTime.now().toUtc().toIso8601String(),
        },
      );

  Future<List<AcaoPendente>> acoesPendentes() async {
    final linhas = await _banco.query('fila_de_sincronizacao', orderBy: 'id');
    return linhas.map(AcaoPendente.deLinha).toList();
  }

  Future<int> quantidadePendente() async =>
      Sqflite.firstIntValue(await _banco.rawQuery('SELECT COUNT(*) FROM fila_de_sincronizacao')) ?? 0;

  Future<void> removerAcao(int id) => _banco.delete('fila_de_sincronizacao', where: 'id = ?', whereArgs: [id]);

  Future<void> registrarTentativa(int id) =>
      _banco.rawUpdate('UPDATE fila_de_sincronizacao SET tentativas = tentativas + 1 WHERE id = ?', [id]);

  // --- Posições GPS acumuladas ---------------------------------------------

  Future<void> guardarPosicao(String? rotaId, Map<String, dynamic> posicao) =>
      _banco.insert('posicoes_pendentes', {'rota_id': rotaId, 'json': jsonEncode(posicao)});

  Future<List<(int id, String? rotaId, Map<String, dynamic> posicao)>> posicoesPendentes({int limite = 500}) async {
    final linhas = await _banco.query('posicoes_pendentes', orderBy: 'id', limit: limite);
    return linhas
        .map((l) => (l['id'] as int, l['rota_id'] as String?, jsonDecode(l['json'] as String) as Map<String, dynamic>))
        .toList();
  }

  Future<void> removerPosicoesAte(int id) => _banco.delete('posicoes_pendentes', where: 'id <= ?', whereArgs: [id]);
}

class AcaoPendente {
  AcaoPendente({required this.id, required this.metodo, required this.caminho, this.corpo, required this.tentativas});

  final int id;
  final String metodo;
  final String caminho;
  final Map<String, dynamic>? corpo;
  final int tentativas;

  factory AcaoPendente.deLinha(Map<String, Object?> l) => AcaoPendente(
        id: l['id'] as int,
        metodo: l['metodo'] as String,
        caminho: l['caminho'] as String,
        corpo: l['corpo'] == null ? null : jsonDecode(l['corpo'] as String) as Map<String, dynamic>,
        tentativas: l['tentativas'] as int,
      );
}
