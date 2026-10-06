using Intrega.Modulos.Roteirizacao.Contratos;
using Intrega.Modulos.Roteirizacao.Motor;
using Intrega.Nucleo.Geo;

namespace Intrega.TestesUnitarios.Roteirizacao;

public class MotorLinhaRetaTestes
{
    private static readonly PontoGeo Se = new(-23.5505, -46.6333);
    private static readonly PontoGeo Paulista = new(-23.5614, -46.6559);

    [Fact]
    public async Task Matriz_tem_diagonal_zero_e_moto_e_mais_rapida_que_bicicleta()
    {
        var motor = new MotorLinhaReta();

        var moto = await motor.ObterMatrizAsync([Se, Paulista], PerfilDeVeiculo.Moto, CancellationToken.None);
        var bicicleta = await motor.ObterMatrizAsync([Se, Paulista], PerfilDeVeiculo.Bicicleta, CancellationToken.None);

        Assert.Equal(0, moto.Tempos[0, 0]);
        Assert.True(moto.Tempos[0, 1] < bicicleta.Tempos[0, 1]);
        Assert.InRange(moto.Distancias[0, 1], 2500, 4000);
    }

    [Fact]
    public async Task Trajeto_tem_um_trecho_por_par_de_pontos()
    {
        var trajeto = await new MotorLinhaReta().ObterTrajetoAsync([Se, Paulista, Se], PerfilDeVeiculo.Carro, true, CancellationToken.None);

        Assert.Equal(2, trajeto.Trechos.Count);
        Assert.Equal("Você chegou ao destino", trajeto.Trechos[0].Passos[^1].Instrucao);
    }
}
