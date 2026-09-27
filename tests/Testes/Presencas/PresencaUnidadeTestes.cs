using MinhaEscolinha.Domain.Configuracao.Parametros;
using MinhaEscolinha.Domain.Mensageria;
using MinhaEscolinha.Domain.Presencas;
using MinhaEscolinha.Domain.Turmas;

namespace MinhaEscolinha.Testes.Presencas;

/// <summary>A regra da quadra (R10, CA-21): a marcação nova vale, a atrasada do mesmo aparelho não desfaz a mais nova.</summary>
public class RegraMarcacaoTestes
{
    private static NovaMarcacao Marcacao(string aparelho, long sequencia) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), SituacaoPresenca.Falta, null, false, aparelho, sequencia, new DateTime(2026, 10, 1, 18, 5, 0));

    [Fact]
    public void A_primeira_marcacao_e_a_de_outro_aparelho_valem()
    {
        Assert.True(RegraMarcacao.Decidir(Marcacao("A", 100), null).Aplicar);
        // O aparelho B nunca mandou nada para este aluno: vale, mesmo com a sequência (o relógio) mais baixa.
        Assert.True(RegraMarcacao.Decidir(Marcacao("B", 5), null).Aplicar);
    }

    [Fact]
    public void A_marcacao_atrasada_do_mesmo_aparelho_nao_desfaz_a_mais_nova()
    {
        var decisao = RegraMarcacao.Decidir(Marcacao("A", 100), maiorSequenciaDoAparelho: 110);
        Assert.False(decisao.Aplicar);
        Assert.Contains("mais nova", decisao.Motivo);
        Assert.False(RegraMarcacao.Decidir(Marcacao("A", 110), 110).Aplicar);
        Assert.True(RegraMarcacao.Decidir(Marcacao("A", 111), 110).Aplicar);
    }
}

/// <summary>A regra do alerta de faltas (BL-M05-08) e a janela anti-duplicidade (BL-M05-09).</summary>
public class AvaliadorFaltasTestes
{
    private static PresencaContada Em(int mes, int dia, SituacaoPresenca situacao) => new(new DateTime(2026, mes, dia, 18, 0, 0), situacao);

    private const SituacaoPresenca P = SituacaoPresenca.Presente;
    private const SituacaoPresenca F = SituacaoPresenca.Falta;
    private const SituacaoPresenca J = SituacaoPresenca.Justificada;

    [Fact]
    public void No_mes_a_terceira_falta_do_mes_dispara_e_a_do_mes_anterior_nao_conta()
    {
        // CA-4: com a regra "3 faltas no mês", o alerta dispara no registro da terceira falta do mês corrente.
        var historico = new List<PresencaContada> { Em(9, 29, F), Em(10, 1, F), Em(10, 6, P), Em(10, 8, F) };
        Assert.Null(AvaliadorFaltas.Avaliar(RegraFaltas.NoMes, 3, false, Em(10, 8, F), historico));

        historico.Add(Em(10, 13, F));
        var atingida = AvaliadorFaltas.Avaliar(RegraFaltas.NoMes, 3, false, Em(10, 13, F), historico);
        Assert.NotNull(atingida);
        Assert.Equal(3, atingida.Faltas);
        Assert.Equal("outubro", atingida.Periodo);
    }

    [Fact]
    public void A_falta_justificada_so_conta_se_a_escolinha_ligar()
    {
        var historico = new List<PresencaContada> { Em(10, 1, F), Em(10, 6, J), Em(10, 8, F) };
        Assert.Null(AvaliadorFaltas.Avaliar(RegraFaltas.NoMes, 3, false, Em(10, 8, F), historico));
        Assert.NotNull(AvaliadorFaltas.Avaliar(RegraFaltas.NoMes, 3, true, Em(10, 8, F), historico));
        // A justificada que não conta nunca dispara, nem sendo a última.
        Assert.Null(AvaliadorFaltas.Avaliar(RegraFaltas.NoMes, 1, false, Em(10, 6, J), historico));
    }

    [Fact]
    public void Em_sequencia_a_presenca_interrompe_e_a_justificada_que_nao_conta_e_pulada()
    {
        var historico = new List<PresencaContada> { Em(10, 1, F), Em(10, 6, P), Em(10, 8, F), Em(10, 13, J), Em(10, 15, F) };
        Assert.Null(AvaliadorFaltas.Avaliar(RegraFaltas.Consecutivas, 3, false, Em(10, 15, F), historico));

        historico.Add(Em(10, 20, F));
        var atingida = AvaliadorFaltas.Avaliar(RegraFaltas.Consecutivas, 3, false, Em(10, 20, F), historico);
        Assert.NotNull(atingida);
        Assert.Equal(3, atingida.Faltas);
        Assert.Equal("sequência", atingida.Periodo);

        // Contando a justificada, a sequência é de 4.
        Assert.Equal(4, AvaliadorFaltas.Avaliar(RegraFaltas.Consecutivas, 3, true, Em(10, 20, F), historico)!.Faltas);
    }

    [Fact]
    public void O_alerta_deixa_de_valer_quando_uma_das_faltas_e_corrigida()
    {
        var falta = new Presenca { SessaoInicio = new DateTime(2026, 10, 8, 18, 0, 0) };
        var alerta = AlertaFaltas.Novo(falta, RegraFaltas.NoMes, 3, false, new RegraAtingida(3, "outubro"),
            new DateTime(2026, 10, 8, 19, 0, 0), null, 15);
        Assert.True(AvaliadorFaltas.AindaVale(alerta, false, [Em(10, 1, F), Em(10, 6, F), Em(10, 8, F)]));
        Assert.False(AvaliadorFaltas.AindaVale(alerta, false, [Em(10, 1, F), Em(10, 6, P), Em(10, 8, F)]));
    }

    [Fact]
    public void Dentro_da_janela_o_alerta_nasce_suprimido_com_o_motivo()
    {
        var falta = new Presenca { SessaoInicio = new DateTime(2026, 10, 13, 18, 0, 0) };
        var ultimo = new DateTime(2026, 10, 8, 19, 0, 0);

        var dentro = AlertaFaltas.Novo(falta, RegraFaltas.NoMes, 3, false, new RegraAtingida(4, "outubro"), ultimo.AddDays(14), ultimo, 15);
        Assert.Equal(SituacaoAlertaFaltas.Suprimido, dentro.Situacao);
        Assert.Contains("janela anti-duplicidade de 15 dia(s)", dentro.Motivo);
        Assert.Contains("08/10/2026 19:00", dentro.Motivo);

        var fora = AlertaFaltas.Novo(falta, RegraFaltas.NoMes, 3, false, new RegraAtingida(4, "outubro"), ultimo.AddDays(15), ultimo, 15);
        Assert.Equal(SituacaoAlertaFaltas.Disparado, fora.Situacao);
        Assert.Equal("4 faltas em outubro", fora.Descricao);
    }
}

/// <summary>O risco de evasão (BL-M05-11): a frequência da janela abaixo do mínimo.</summary>
public class FrequenciaTestes
{
    [Fact]
    public void A_frequencia_e_o_risco_seguem_o_minimo_e_a_regra_da_justificada()
    {
        SituacaoPresenca[] situacoes =
        [
            SituacaoPresenca.Presente, SituacaoPresenca.Presente, SituacaoPresenca.Presente,
            SituacaoPresenca.Falta, SituacaoPresenca.Falta, SituacaoPresenca.Justificada
        ];

        var semJustificada = CalculadoraFrequencia.Calcular(situacoes, contaJustificada: false);
        Assert.Equal(new Frequencia(3, 5), semJustificada);
        Assert.Equal(60m, semJustificada.Percentual);
        Assert.False(CalculadoraFrequencia.EmRisco(semJustificada, 60m));

        var comJustificada = CalculadoraFrequencia.Calcular(situacoes, contaJustificada: true);
        Assert.Equal(50m, comJustificada.Percentual);
        Assert.True(CalculadoraFrequencia.EmRisco(comJustificada, 60m));

        Assert.False(CalculadoraFrequencia.EmRisco(CalculadoraFrequencia.Calcular([], false), 60m));
    }
}

public class SessaoEAvisoTestes
{
    [Fact]
    public void A_chamada_vale_no_dia_e_depois_e_a_primeira_marcacao_faz_a_sessao_acontecer()
    {
        var sessao = new SessaoTreino { Data = new DateOnly(2026, 10, 1), Situacao = SituacaoSessao.Prevista };
        Assert.False(sessao.AceitaChamada(new DateOnly(2026, 9, 30)));
        Assert.True(sessao.AceitaChamada(new DateOnly(2026, 10, 1)));
        Assert.True(sessao.AceitaChamada(new DateOnly(2026, 10, 5)));

        sessao.RegistrarChamada();
        Assert.Equal(SituacaoSessao.Realizada, sessao.Situacao);
        Assert.False(sessao.PodeCancelar);

        var cancelada = new SessaoTreino { Data = new DateOnly(2026, 10, 1), Situacao = SituacaoSessao.Cancelada };
        Assert.False(cancelada.AceitaChamada(new DateOnly(2026, 10, 1)));
    }

    [Fact]
    public void So_o_aviso_do_portal_ainda_nao_aberto_sai_do_portal()
    {
        var regra = CatalogoGatilhos.Obter(GatilhoMensagem.FaltasAtingidas);
        var agora = new DateTime(2026, 10, 8, 22, 0, 0);
        Mensagem Aviso(CanalMensagem canal) => Mensagem.Nova(regra, canal, "AlertaFaltas", Guid.NewGuid(), TipoDestinatario.Responsavel,
            Guid.NewGuid(), "Maria Lima", "+5543988887777", Guid.NewGuid(), Guid.NewGuid(), "faltas_atingidas_cordial_v1", "[]", "texto", false, null, agora);

        var naoLido = Aviso(CanalMensagem.Portal);
        Assert.True(naoLido.RecolherDoPortal("A falta foi corrigida.", agora));
        Assert.Equal(SituacaoMensagem.Suprimida, naoLido.Situacao);
        Assert.Contains(naoLido.Eventos, e => e.Tipo == TipoEventoMensagem.Suprimida);

        var lido = Aviso(CanalMensagem.Portal);
        lido.MarcarLidaNoPortal(agora);
        Assert.False(lido.RecolherDoPortal("A falta foi corrigida.", agora));
        Assert.Equal(SituacaoMensagem.Lida, lido.Situacao);

        Assert.False(Aviso(CanalMensagem.WhatsApp).RecolherDoPortal("A falta foi corrigida.", agora));
    }
}
