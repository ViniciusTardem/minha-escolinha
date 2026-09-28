using MinhaEscolinha.Application.Integracao.Asaas;
using MinhaEscolinha.Domain.Configuracao.Parametros;
using MinhaEscolinha.Domain.Financeiro;
using MinhaEscolinha.Domain.Mensageria;
using MinhaEscolinha.Infrastructure.Integracao.Asaas;

namespace MinhaEscolinha.Testes.Financeiro;

/// <summary>As regras puras do S4: a projeção pela matrícula, a régua, a máquina da cobrança, o webhook e a trava.</summary>
public class FinanceiroUnidadeTestes
{
    private static readonly DateTime Agora = new(2026, 9, 26, 12, 0, 0);
    private static readonly DateOnly Hoje = DateOnly.FromDateTime(Agora);

    private static readonly ParametrosFinanceiro Padrao = new(2m, 1m, 10, 10, [3], true, [1, 5, 10]);

    private static MatriculaParaCobranca Matricula(Guid? id = null, DateOnly? inicio = null, DateOnly? fim = null, decimal valor = 150m,
        bool ativo = true) =>
        new(id ?? Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), inicio ?? new DateOnly(2026, 9, 1), fim, valor, ativo);

    // ── Projeção (BL-M06-01) ──

    [Fact]
    public void A_projecao_nunca_cobra_para_tras_e_a_primeira_da_matricula_espera_confirmacao()
    {
        var m = Matricula();
        var plano = PlanejadorCobrancas.Planejar([m], [], [], Hoje, 10);

        // Setembro venceu dia 10 (antes de hoje): não nasce. Outubro nasce; novembro (dia 10/11) passa do fim de outubro.
        var outubro = Assert.Single(plano.Criar);
        Assert.Equal(new DateOnly(2026, 10, 1), outubro.Competencia);
        Assert.Equal(new DateOnly(2026, 10, 10), outubro.Vencimento);
        Assert.Equal(150m, outubro.Valor);
        Assert.True(outubro.AguardandoConfirmacao);

        // Em 30/09 o horizonte ainda acaba em 31/10; em 1º de outubro vai até 30/11: novembro nasce, e não é a primeira.
        Assert.True(PlanejadorCobrancas.Planejar([m], [outubro], [], new DateOnly(2026, 9, 30), 10).Vazio);
        var depois = PlanejadorCobrancas.Planejar([m], [outubro], [], new DateOnly(2026, 10, 1), 10);
        var novembro = Assert.Single(depois.Criar);
        Assert.Equal(new DateOnly(2026, 11, 10), novembro.Vencimento);
        Assert.False(novembro.PrimeiraDaMatricula);
        Assert.Empty(depois.Remover);
    }

    [Fact]
    public void A_projecao_e_idempotente_e_a_matricula_com_historico_nao_repete_a_confirmacao()
    {
        var m = Matricula();
        var primeira = PlanejadorCobrancas.Planejar([m], [], [], Hoje, 10);
        Assert.True(PlanejadorCobrancas.Planejar([m], primeira.Criar, [], Hoje, 10).Vazio);

        var comHistorico = PlanejadorCobrancas.Planejar([m], [], [m.MatriculaId], Hoje, 10);
        Assert.False(Assert.Single(comHistorico.Criar).PrimeiraDaMatricula);
    }

    [Fact]
    public void Trial_pausa_mensalidade_zero_e_matricula_encerrada_antes_do_vencimento_nao_geram_cobranca()
    {
        Assert.True(PlanejadorCobrancas.Planejar([Matricula(ativo: false)], [], [], Hoje, 10).Vazio);
        Assert.True(PlanejadorCobrancas.Planejar([Matricula(valor: 0)], [], [], Hoje, 10).Vazio);
        Assert.True(PlanejadorCobrancas.Planejar([Matricula(fim: new DateOnly(2026, 10, 5))], [], [], Hoje, 10).Vazio);
        // Matrícula que começa depois do vencimento de outubro: a primeira é a de novembro (o horizonte de 1º/10 a alcança).
        var tarde = PlanejadorCobrancas.Planejar([Matricula(inicio: new DateOnly(2026, 10, 20))], [], [], new DateOnly(2026, 10, 1), 10);
        Assert.Equal(new DateOnly(2026, 11, 10), Assert.Single(tarde.Criar).Vencimento);
    }

    [Fact]
    public void A_prevista_automatica_acompanha_a_matricula_e_sai_quando_deixa_de_valer_a_confirmada_fica()
    {
        var id = Guid.NewGuid();
        var automatica = PlanejadorCobrancas.Planejar([Matricula(id)], [], [id], Hoje, 10).Criar.Single();

        var reajuste = PlanejadorCobrancas.Planejar([Matricula(id, valor: 180m)], [automatica], [id], Hoje, 10);
        Assert.Same(automatica, Assert.Single(reajuste.Ajustadas));
        Assert.Equal(180m, automatica.Valor);

        var pausa = PlanejadorCobrancas.Planejar([Matricula(id, valor: 180m, ativo: false)], [automatica], [id], Hoje, 10);
        Assert.Same(automatica, Assert.Single(pausa.Remover));

        var confirmada = PlanejadorCobrancas.Planejar([Matricula(id)], [], [], Hoje, 10).Criar.Single();
        confirmada.Confirmar(120m, Agora, null, "Gestão");
        Assert.True(PlanejadorCobrancas.Planejar([Matricula(id, valor: 999m, ativo: false)], [confirmada], [], Hoje, 10).Vazio);
    }

    // ── Régua (BL-M06-04) ──

    [Fact]
    public void A_regua_tem_os_degraus_dos_parametros_com_o_tom_subindo_depois_do_vencimento()
    {
        var etapas = ReguaCobranca.Etapas(new DateOnly(2026, 10, 10), Padrao);

        Assert.Equal(["A3", "D0", "V1", "V5", "V10"], etapas.Select(e => e.Codigo));
        Assert.Equal(new DateOnly(2026, 10, 7), etapas[0].Data);
        Assert.All(etapas.Take(2), e => Assert.Equal(GatilhoMensagem.MensalidadeAVencer, e.Gatilho));
        Assert.All(etapas.Skip(2), e => Assert.Equal(GatilhoMensagem.MensalidadeVencida, e.Gatilho));
        Assert.Equal([TomMensagem.Cordial, TomMensagem.Cordial, TomMensagem.Cordial, TomMensagem.Direto, TomMensagem.Direto],
            etapas.Select(e => e.Tom));
    }

    private static Cobranca Emitida(DateOnly vencimento, DateTime emitidaEm)
    {
        var c = Cobranca.Prevista(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new DateOnly(vencimento.Year, vencimento.Month, 1), vencimento, 150m, false);
        c.PrepararEmissao(Guid.NewGuid(), "Maria Lima", 2m, 1m, DateOnly.FromDateTime(emitidaEm));
        c.RegistrarEmissao("pay_1", "https://sandbox.asaas.com/i/1", Guid.NewGuid(), emitidaEm);
        return c;
    }

    [Fact]
    public void Sai_um_degrau_por_vez_o_mais_recente_e_nunca_um_aviso_de_a_vencer_depois_do_vencimento()
    {
        var c = Emitida(new DateOnly(2026, 10, 10), new DateTime(2026, 9, 30, 6, 30, 0));

        Assert.Null(ReguaCobranca.Proxima(c, Padrao, new DateOnly(2026, 10, 6)));
        var a3 = ReguaCobranca.Proxima(c, Padrao, new DateOnly(2026, 10, 7))!;
        Assert.Equal("A3", a3.Codigo);
        c.RegistrarEtapa(a3.Codigo, a3.Data);
        Assert.Null(ReguaCobranca.Proxima(c, Padrao, new DateOnly(2026, 10, 8)));

        // O sistema ficou fora do ar de 10 a 16/10: sai só o degrau mais recente (V5), e nunca o "a vencer" depois do vencimento.
        var atrasada = ReguaCobranca.Proxima(c, Padrao, new DateOnly(2026, 10, 16))!;
        Assert.Equal("V5", atrasada.Codigo);
        Assert.Equal(TomMensagem.Direto, atrasada.Tom);
    }

    [Fact]
    public void Paga_cancelada_ou_da_conta_anterior_nao_recebe_regua()
    {
        var paga = Emitida(new DateOnly(2026, 10, 10), new DateTime(2026, 9, 30, 6, 30, 0));
        paga.RegistrarPagamento(150m, 150m, new DateOnly(2026, 10, 8), MeioPagamento.Pix, Agora);
        Assert.Null(ReguaCobranca.Proxima(paga, Padrao, new DateOnly(2026, 10, 11)));

        var anterior = Emitida(new DateOnly(2026, 10, 10), new DateTime(2026, 9, 30, 6, 30, 0));
        anterior.Sinalizar(SinalCobranca.ContaAnterior, "troca de conta", Agora);
        Assert.Null(ReguaCobranca.Proxima(anterior, Padrao, new DateOnly(2026, 10, 11)));

        // Emitida depois das datas "antes": o lembrete A3 não sai atrasado; o do dia sai.
        var tarde = Emitida(new DateOnly(2026, 10, 10), new DateTime(2026, 10, 10, 6, 30, 0));
        Assert.Equal("D0", ReguaCobranca.Proxima(tarde, Padrao, new DateOnly(2026, 10, 10))!.Codigo);
    }

    // ── Máquina da cobrança (spec §6) ──

    [Fact]
    public void Pagamento_com_o_valor_original_concilia_no_prazo_ou_em_atraso_e_repetido_nao_tem_efeito()
    {
        var noPrazo = Emitida(new DateOnly(2026, 10, 10), Agora);
        Assert.Equal(ResultadoPagamento.Conciliada, noPrazo.RegistrarPagamento(150m, 150m, new DateOnly(2026, 10, 10), MeioPagamento.Boleto, Agora));
        Assert.Equal(SituacaoCobranca.Paga, noPrazo.Situacao);
        Assert.Equal(ResultadoPagamento.JaPaga, noPrazo.RegistrarPagamento(150m, 150m, new DateOnly(2026, 10, 10), MeioPagamento.Boleto, Agora));

        // Em atraso, a Asaas cobra multa e juros: o valor pago é maior, o original confere, e não é divergência.
        var atrasada = Emitida(new DateOnly(2026, 10, 10), Agora);
        Assert.True(atrasada.Vencer(new DateOnly(2026, 10, 11)));
        Assert.Equal(ResultadoPagamento.Conciliada, atrasada.RegistrarPagamento(150m, 153.25m, new DateOnly(2026, 10, 15), MeioPagamento.Pix, Agora));
        Assert.Equal(SituacaoCobranca.PagaEmAtraso, atrasada.Situacao);
        Assert.Equal(153.25m, atrasada.ValorPago);
    }

    [Fact]
    public void Pagamento_divergente_so_sinaliza_e_pode_ser_aceito_pela_gestao()
    {
        var c = Emitida(new DateOnly(2026, 10, 10), Agora);
        Assert.Equal(ResultadoPagamento.Divergente, c.RegistrarPagamento(75m, 75m, new DateOnly(2026, 10, 9), MeioPagamento.Boleto, Agora));
        Assert.Equal(SituacaoCobranca.Emitida, c.Situacao);
        Assert.Equal(SinalCobranca.ValorDivergente, c.Sinal);
        Assert.True(c.PodeAceitarPagamentoDivergente);

        c.AceitarPagamentoDivergente(Agora);
        c.ResolverSinal("A família pagou o resto em dinheiro.", Agora, "Gestão");
        Assert.Equal(SituacaoCobranca.Paga, c.Situacao);
        Assert.Equal(75m, c.ValorPago);
        Assert.False(c.ComSinal);
    }

    [Fact]
    public void Pagamento_numa_cobranca_cancelada_vira_sinal_e_cancelar_pede_motivo_e_so_vale_em_aberto()
    {
        var c = Emitida(new DateOnly(2026, 10, 10), Agora);
        Assert.Throws<ArgumentException>(() => c.Encerrar(SituacaoCobranca.Cancelada, " ", Agora, null, "Gestão"));
        Assert.True(c.Encerrar(SituacaoCobranca.Cancelada, "Cobrança em dobro", Agora, null, "Gestão")); // estava na Asaas

        Assert.Equal(ResultadoPagamento.DepoisDeEncerrada, c.RegistrarPagamento(150m, 150m, new DateOnly(2026, 10, 9), MeioPagamento.Pix, Agora));
        Assert.Equal(SituacaoCobranca.Cancelada, c.Situacao);
        Assert.Equal(SinalCobranca.PagaDepoisDeEncerrada, c.Sinal);
        Assert.Throws<InvalidOperationException>(() => c.Encerrar(SituacaoCobranca.Isenta, "de novo", Agora, null, "Gestão"));
    }

    [Fact]
    public void Paga_so_sai_das_pagas_quando_o_dinheiro_voltou_e_a_fatura_estornada_fica_na_asaas()
    {
        var paga = Emitida(new DateOnly(2026, 10, 10), Agora);
        paga.RegistrarPagamento(150m, 150m, new DateOnly(2026, 10, 8), MeioPagamento.Cartao, Agora);
        Assert.False(paga.PodeSerEncerrada);
        Assert.Throws<InvalidOperationException>(() => paga.Encerrar(SituacaoCobranca.Cancelada, "engano", Agora, null, "Gestão"));

        paga.Sinalizar(SinalCobranca.Estorno, "A Asaas registra o estorno (REFUNDED).", Agora);
        Assert.Equal(SituacaoCobranca.Paga, paga.Situacao); // o estorno só sinaliza
        Assert.False(paga.PodeAceitarPagamentoDivergente);
        Assert.True(paga.PodeSerEncerrada);
        Assert.False(paga.Encerrar(SituacaoCobranca.Cancelada, "Devolvido: o aluno não começou.", Agora, null, "Gestão"));
        Assert.Equal(SituacaoCobranca.Cancelada, paga.Situacao);
    }

    [Fact]
    public void A_emissao_congela_multa_e_juros_e_o_vencimento_que_passou_vira_hoje()
    {
        var c = Cobranca.Prevista(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 10), 150m, true);
        Assert.False(c.ProntaParaEmitir(Hoje, 10)); // a primeira espera confirmação
        c.Confirmar(90m, Agora, null, "Gestão");
        Assert.True(c.ProntaParaEmitir(Hoje, 10));

        var anterior = c.PrepararEmissao(Guid.NewGuid(), "Maria", 2m, 1m, Hoje);
        Assert.Equal(new DateOnly(2026, 9, 10), anterior);
        Assert.Equal(Hoje, c.Vencimento);
        c.PrepararEmissao(Guid.NewGuid(), "Maria", 5m, 3m, Hoje); // retentativa com o parâmetro já mudado
        Assert.Equal(2m, c.MultaPercentual);
        Assert.Equal(1m, c.JurosPercentualMes);
        Assert.Equal(90m, c.Valor);
    }

    // ── Asaas: webhook e trava ──

    [Fact]
    public void O_webhook_da_asaas_e_lido_com_o_valor_original_a_data_da_familia_e_a_nossa_referencia()
    {
        var cobranca = Guid.NewGuid();
        var evento = EventoAsaas.Ler($$"""
            { "id": "evt_1", "event": "PAYMENT_RECEIVED",
              "payment": { "id": "pay_9", "status": "RECEIVED", "value": 153.25, "originalValue": 150.00, "billingType": "PIX",
                           "dueDate": "2026-10-10", "paymentDate": "2026-10-16", "clientPaymentDate": "2026-10-15",
                           "externalReference": "{{cobranca}}", "invoiceUrl": "https://sandbox.asaas.com/i/9" } }
            """)!;

        Assert.Equal("evt_1", evento.Id);
        var p = evento.Pagamento!;
        Assert.True(p.Recebida);
        Assert.Equal(150m, p.ValorDeReferencia);
        Assert.Equal(new DateOnly(2026, 10, 15), p.PagaEm);
        Assert.Equal(cobranca, p.CobrancaId);
        Assert.Equal(MeioPagamento.Pix, p.MeioDoPagamento);
        Assert.Null(EventoAsaas.Ler("não é json"));
        Assert.True(TokenWebhookAsaas.Confere("abc", "abc"));
        Assert.False(TokenWebhookAsaas.Confere("abc", null));
    }

    [Fact]
    public void A_trava_da_asaas_nao_deixa_simulador_nem_cobranca_real_sem_confirmacao_em_producao()
    {
        var simulado = new OpcoesAsaas { Modo = OpcoesAsaas.ModoSimulado };
        string? aviso = null;
        GuardaAsaas.Validar("Production", simulado, a => aviso = a);
        Assert.False(simulado.Habilitado);
        Assert.NotNull(aviso);

        Assert.Throws<InvalidOperationException>(() => GuardaAsaas.Validar("Production", new OpcoesAsaas { Modo = OpcoesAsaas.ModoApi }));
        GuardaAsaas.Validar("Production", new OpcoesAsaas { Modo = OpcoesAsaas.ModoApi, ConfirmarProducao = true, UrlPublica = "https://escolinha.com.br" });
        Assert.Throws<InvalidOperationException>(() => GuardaAsaas.Validar("Development", new OpcoesAsaas { Modo = "Outro" }));
        Assert.Throws<InvalidOperationException>(() => GuardaAsaas.Validar("Development", new OpcoesAsaas { UrlPublica = "http://site.com" }));
    }

    /// <summary>
    /// BL-M06-24: o financeiro é só do gestor em qualquer caminho. O controller inteiro exige o financeiro da escolinha, e
    /// toda action que grava exige Alterar; a matriz (§7) não dá esse recurso ao professor nem ao responsável.
    /// </summary>
    [Fact]
    public void Toda_tela_do_financeiro_exige_o_financeiro_da_escolinha()
    {
        var controller = typeof(MinhaEscolinha.Web.Controllers.MensalidadesController);
        var daClasse = controller.GetCustomAttributes(typeof(MinhaEscolinha.Web.Infraestrutura.PermissaoAttribute), false)
            .Cast<MinhaEscolinha.Web.Infraestrutura.PermissaoAttribute>();
        Assert.Contains(daClasse, p => p.Recurso == Domain.Acesso.Recurso.FinanceiroEscolinha);

        var gravam = controller.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.HttpPostAttribute), false).Length > 0)
            .ToList();
        Assert.NotEmpty(gravam);
        Assert.All(gravam, m => Assert.Contains(m.GetCustomAttributes(typeof(MinhaEscolinha.Web.Infraestrutura.PermissaoAttribute), false)
                .Cast<MinhaEscolinha.Web.Infraestrutura.PermissaoAttribute>(),
            p => p.Recurso == Domain.Acesso.Recurso.FinanceiroEscolinha && p.Operacao == Domain.Acesso.Operacao.Alterar));

        foreach (var papel in new[] { Domain.Acesso.PapelAtuacao.Professor, Domain.Acesso.PapelAtuacao.Responsavel })
            Assert.Equal(Domain.Acesso.NivelAcesso.Nenhum, Domain.Acesso.MatrizPermissoes.Nivel(Domain.Acesso.Recurso.FinanceiroEscolinha, papel));
    }

    [Fact]
    public void O_celular_vai_para_a_asaas_com_ddd_e_sem_o_55()
    {
        Assert.Equal("43988887777", AsaasClienteHttp.CelularNacional("+5543988887777"));
        Assert.Null(AsaasClienteHttp.CelularNacional("123"));
    }

    /// <summary>BL-M10-06: o Pix do portal vale só na mensalidade em aberto, sem divergência e dentro da validade; nunca é cortado.</summary>
    [Fact]
    public void O_pix_do_portal_vale_so_na_mensalidade_em_aberto_sem_divergencia_e_dentro_da_validade()
    {
        var c = Emitida(new DateOnly(2026, 10, 10), Agora);
        Assert.False(c.PixValidoEm(Agora));
        Assert.False(c.RegistrarPix(new string('9', Cobranca.TamanhoPix + 1), null));
        Assert.Null(c.PixCopiaECola);

        Assert.True(c.RegistrarPix("00020126580014br.gov.bcb.pix", Agora.AddDays(1)));
        Assert.True(c.PixValidoEm(Agora));
        Assert.False(c.PixValidoEm(Agora.AddDays(2))); // venceu: a rotina busca de novo

        var comDivergencia = Emitida(new DateOnly(2026, 10, 10), Agora);
        comDivergencia.RegistrarPix("00020126580014br.gov.bcb.pix", null);
        comDivergencia.Sinalizar(SinalCobranca.ValorDivergente, "Entrou R$ 100,00", Agora);
        Assert.False(comDivergencia.PixValidoEm(Agora));

        c.RegistrarPagamento(150m, 150m, Hoje, MeioPagamento.Pix, Agora);
        Assert.False(c.PixValidoEm(Agora)); // paga: o Pix sai da tela
    }
}
