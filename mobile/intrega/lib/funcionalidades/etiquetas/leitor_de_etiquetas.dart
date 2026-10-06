import 'package:google_mlkit_barcode_scanning/google_mlkit_barcode_scanning.dart';
import 'package:google_mlkit_text_recognition/google_mlkit_text_recognition.dart';

/// O que a câmera conseguiu ler da etiqueta (processado no próprio celular, sem internet e sem custo).
class LeituraDaEtiqueta {
  LeituraDaEtiqueta(this.texto, this.codigos);

  final String texto;
  final List<({String formato, String valor})> codigos;

  bool get vazia => texto.trim().isEmpty && codigos.isEmpty;
}

/// Usa o Google ML Kit: reconhecimento de texto (OCR) + QR code/código de barras na mesma foto.
class LeitorDeEtiquetas {
  final _texto = TextRecognizer(script: TextRecognitionScript.latin);
  final _codigos = BarcodeScanner();

  Future<LeituraDaEtiqueta> ler(String caminhoDaFoto) async {
    final imagem = InputImage.fromFilePath(caminhoDaFoto);
    final texto = await _texto.processImage(imagem);
    final codigos = await _codigos.processImage(imagem);
    return LeituraDaEtiqueta(
      // Linha a linha, na ordem em que aparecem na etiqueta.
      texto.blocks.expand((b) => b.lines).map((l) => l.text).join('\n'),
      [
        for (final c in codigos)
          if (c.rawValue != null && c.rawValue!.isNotEmpty) (formato: c.format.name, valor: c.rawValue!),
      ],
    );
  }

  Future<void> fechar() async {
    await _texto.close();
    await _codigos.close();
  }
}
