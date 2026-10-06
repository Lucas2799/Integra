using Intrega.Modulos.Geocodificacao.Contratos;
using Intrega.Modulos.Paradas.Contratos;

namespace Intrega.Modulos.Etiquetas.Interpretacao;

/// <summary>Código lido pela câmera (QR code, código de barras etc.).</summary>
public sealed record CodigoLido(string Formato, string Valor);

/// <summary>
/// O que foi possível extrair da etiqueta. A confiança (0 a 1) indica se o app deve pedir
/// para o entregador conferir os dados antes de criar a parada.
/// </summary>
public sealed record EtiquetaInterpretada(
    Marketplace Marketplace,
    string? CodigoDeRastreio,
    string? NomeDoDestinatario,
    string? Telefone,
    EnderecoInformado Endereco,
    IReadOnlyList<string> CepsEncontrados,
    double Confianca,
    IReadOnlyList<string> Avisos)
{
    public const double ConfiancaMinimaSemConferencia = 0.75;
    public bool PrecisaConferir => Confianca < ConfiancaMinimaSemConferencia;
}
