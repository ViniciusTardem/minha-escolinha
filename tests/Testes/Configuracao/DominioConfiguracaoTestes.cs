using MinhaEscolinha.Domain.Configuracao;
using MinhaEscolinha.Domain.Eventos;

namespace MinhaEscolinha.Testes.Configuracao;

/// <summary>R8 — o núcleo genérico de modalidade e evento (BL-M02-02, BL-M02-03).</summary>
public class PoliticaPerfilTestes
{
    [Fact]
    public void O_perfil_coletiva_nasce_com_adversario_e_convocacao()
    {
        var futsal = Modalidade.Nova("Futsal", PerfilModalidade.Coletiva);

        Assert.True(futsal.TemAdversario);
        Assert.True(futsal.TemConvocacao);
        Assert.False(futsal.TemGraduacao);
        Assert.Equal(new[] { TipoEvento.Jogo }, PoliticaPerfil.TiposDisponiveis(PerfilModalidade.Coletiva));
    }

    [Fact]
    public void Quem_decide_o_que_o_evento_tem_sao_os_interruptores_e_nao_o_tipo()
    {
        var semConvocacao = new RecursosModalidade(Adversario: false, PlacarSumula: false, Classificacao: false,
            Convocacao: false, Graduacao: false, Apresentacao: false);

        var recursos = PoliticaPerfil.Recursos(TipoEvento.Jogo, semConvocacao);

        // Um "jogo" sem adversário e sem convocação é válido: o núcleo não pressupõe esporte coletivo.
        Assert.False(recursos.PermiteAdversario);
        Assert.False(recursos.PermiteConvocacao);
    }

    [Fact]
    public void Tipo_de_evento_que_o_perfil_nao_tem_e_recusado()
    {
        Assert.True(PoliticaPerfil.TipoSuportado(PerfilModalidade.Coletiva, TipoEvento.Jogo));
        Assert.False(PoliticaPerfil.TipoSuportado(PerfilModalidade.Coletiva, (TipoEvento)999));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PoliticaPerfil.Recursos((TipoEvento)999, ModelosPerfil.Padrao(PerfilModalidade.Coletiva)));
    }
}

/// <summary>Créditos de mensagem (BL-M02-11): o saldo é a soma do razão e nunca fica negativo.</summary>
public class ContaCreditosTestes
{
    private static readonly DateTime Agora = new(2026, 9, 26, 12, 0, 0);

    [Fact]
    public void Concessao_e_consumo_andam_o_saldo_e_registram_o_saldo_apos()
    {
        var conta = new ContaCreditos();

        var concessao = conta.Lancar(TipoMovimentoCredito.Concessao, 100, "Pacote do piloto", Agora, null, "Equipe");
        var consumo = conta.Lancar(TipoMovimentoCredito.Consumo, -1, "Convocação", Agora, null, "Sistema");

        Assert.Equal(99, conta.Saldo);
        Assert.Equal(100, concessao!.SaldoApos);
        Assert.Equal(99, consumo!.SaldoApos);
    }

    [Fact]
    public void O_saldo_nunca_fica_negativo()
    {
        var conta = new ContaCreditos();
        conta.Lancar(TipoMovimentoCredito.Concessao, 2, "Teste", Agora, null, "Equipe");

        Assert.Null(conta.Lancar(TipoMovimentoCredito.Consumo, -3, "Demais", Agora, null, "Sistema"));
        Assert.Equal(2, conta.Saldo);
        Assert.Null(conta.Lancar(TipoMovimentoCredito.Ajuste, 0, "Nada", Agora, null, "Equipe"));
    }

    [Fact]
    public void A_chave_da_conta_asaas_aparece_so_pelo_final() =>
        Assert.Equal("wxyz", ContaAsaasEscolinha.CalcularFinal("  $aact_hmlg_000abcdefwxyz "));
}
