using MinhaEscolinha.Application.Integracao.Stripe;
using MinhaEscolinha.Domain.Plataforma;
using MinhaEscolinha.Infrastructure.Integracao.Stripe;

namespace MinhaEscolinha.Testes.Plataforma;

/// <summary>Assinatura do webhook: só passa o que o Stripe assinou, com o nosso segredo, há pouco tempo.</summary>
public class AssinaturaWebhookStripeTestes
{
    private const string Segredo = "whsec_teste";
    private const string Corpo = """{"id":"evt_1","type":"checkout.session.completed"}""";
    private static readonly DateTimeOffset Agora = new(2026, 9, 26, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Aceita_o_corpo_assinado_com_o_segredo_e_o_carimbo_atual() =>
        Assert.True(AssinaturaWebhookStripe.Valida(Corpo, AssinaturaWebhookStripe.Cabecalho(Corpo, Agora.ToUnixTimeSeconds(), Segredo), Segredo, Agora));

    [Fact]
    public void Recusa_outro_segredo() =>
        Assert.False(AssinaturaWebhookStripe.Valida(Corpo, AssinaturaWebhookStripe.Cabecalho(Corpo, Agora.ToUnixTimeSeconds(), "whsec_outro"), Segredo, Agora));

    [Fact]
    public void Recusa_corpo_alterado_depois_de_assinado()
    {
        var cabecalho = AssinaturaWebhookStripe.Cabecalho(Corpo, Agora.ToUnixTimeSeconds(), Segredo);
        Assert.False(AssinaturaWebhookStripe.Valida(Corpo.Replace("evt_1", "evt_2"), cabecalho, Segredo, Agora));
    }

    [Fact]
    public void Recusa_evento_antigo_fora_da_tolerancia()
    {
        var antigo = Agora.AddMinutes(-10).ToUnixTimeSeconds();
        Assert.False(AssinaturaWebhookStripe.Valida(Corpo, AssinaturaWebhookStripe.Cabecalho(Corpo, antigo, Segredo), Segredo, Agora));
    }

    [Fact]
    public void Aceita_quando_uma_das_assinaturas_confere()
    {
        // Na troca do segredo, o Stripe manda uma assinatura para cada segredo ativo.
        var t = Agora.ToUnixTimeSeconds();
        var cabecalho = $"t={t},v1={AssinaturaWebhookStripe.Calcular(Corpo, t, "whsec_antigo")},v1={AssinaturaWebhookStripe.Calcular(Corpo, t, Segredo)}";
        Assert.True(AssinaturaWebhookStripe.Valida(Corpo, cabecalho, Segredo, Agora));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("t=1700000000")]
    [InlineData("v1=abc")]
    [InlineData("lixo")]
    public void Recusa_cabecalho_incompleto(string? cabecalho) =>
        Assert.False(AssinaturaWebhookStripe.Valida(Corpo, cabecalho, Segredo, Agora));
}

public class EventoStripeTestes
{
    [Fact]
    public void Le_o_tipo_a_sessao_e_os_metadados()
    {
        var evento = EventoStripe.Ler("""
            {"id":"evt_9","type":"checkout.session.completed","data":{"object":{"id":"cs_test_1","object":"checkout.session",
             "metadata":{"origem":"minha-escolinha","compra_id":"abc","numero":1}}}}
            """);

        Assert.NotNull(evento);
        Assert.Equal("evt_9", evento.Id);
        Assert.Equal(EventosStripe.SessaoConcluida, evento.Tipo);
        Assert.Equal("cs_test_1", evento.ObjetoId);
        Assert.Equal("minha-escolinha", evento.Metadados[MetadadosStripe.Origem]);
        Assert.False(evento.Metadados.ContainsKey("numero"));
    }

    [Theory]
    [InlineData("não é json")]
    [InlineData("[]")]
    [InlineData("""{"type":"x"}""")]
    [InlineData("""{"id":"evt_1"}""")]
    public void Corpo_que_nao_e_evento_devolve_nulo(string corpo) => Assert.Null(EventoStripe.Ler(corpo));
}

/// <summary>Trava de ambiente: fora de Production nada é cobrado de verdade; em Production não há simulador nem teste.</summary>
public class GuardaStripeTestes
{
    private static OpcoesStripe Opcoes(string modo, string chave = "", string segredo = "", bool confirmar = false) => new()
    {
        Habilitado = true, Modo = modo, ChaveSecreta = chave, SegredoWebhook = segredo, ConfirmarProducao = confirmar,
        UrlPublica = "https://escolinha.exemplo.com.br"
    };

    [Fact]
    public void Simulado_em_desenvolvimento_passa() => GuardaStripe.Validar("Development", Opcoes(OpcoesStripe.ModoSimulado));

    [Fact]
    public void Simulado_em_producao_desliga_a_compra_em_vez_de_derrubar_o_site()
    {
        var opcoes = Opcoes(OpcoesStripe.ModoSimulado);
        string? aviso = null;
        GuardaStripe.Validar("Production", opcoes, m => aviso = m);

        Assert.False(opcoes.Habilitado);
        Assert.NotNull(aviso);
    }

    [Fact]
    public void Teste_em_producao_nao_sobe() =>
        Assert.Throws<InvalidOperationException>(() => GuardaStripe.Validar("Production", Opcoes(OpcoesStripe.ModoTeste, "sk_test_x")));

    [Theory]
    [InlineData("")]
    [InlineData("sk_live_x")]
    [InlineData("rk_live_x")]
    [InlineData("pk_test_x")]
    public void Teste_so_aceita_chave_de_teste(string chave) =>
        Assert.Throws<InvalidOperationException>(() => GuardaStripe.Validar("Development", Opcoes(OpcoesStripe.ModoTeste, chave)));

    [Fact]
    public void Teste_com_chave_de_teste_passa_e_avisa_quando_falta_o_segredo_do_webhook()
    {
        string? aviso = null;
        GuardaStripe.Validar("Development", Opcoes(OpcoesStripe.ModoTeste, "rk_test_x"), m => aviso = m);
        Assert.NotNull(aviso);
    }

    [Fact]
    public void Producao_fora_de_production_nao_sobe() =>
        Assert.Throws<InvalidOperationException>(() =>
            GuardaStripe.Validar("Homologacao", Opcoes(OpcoesStripe.ModoProducao, "sk_live_x", "whsec_x", confirmar: true)));

    [Fact]
    public void Producao_exige_confirmacao_chave_de_producao_e_segredo_do_webhook()
    {
        Assert.Throws<InvalidOperationException>(() => GuardaStripe.Validar("Production", Opcoes(OpcoesStripe.ModoProducao, "sk_live_x", "whsec_x")));
        Assert.Throws<InvalidOperationException>(() =>
            GuardaStripe.Validar("Production", Opcoes(OpcoesStripe.ModoProducao, "sk_test_x", "whsec_x", confirmar: true)));
        Assert.Throws<InvalidOperationException>(() =>
            GuardaStripe.Validar("Production", Opcoes(OpcoesStripe.ModoProducao, "sk_live_x", confirmar: true)));
        GuardaStripe.Validar("Production", Opcoes(OpcoesStripe.ModoProducao, "sk_live_x", "whsec_x", confirmar: true));
    }

    [Fact]
    public void Modo_desconhecido_e_endereco_sem_https_nao_sobem()
    {
        Assert.Throws<InvalidOperationException>(() => GuardaStripe.Validar("Development", Opcoes("Sandbox", "sk_test_x")));
        var semHttps = Opcoes(OpcoesStripe.ModoSimulado);
        semHttps.UrlPublica = "http://escolinha.exemplo.com.br";
        Assert.Throws<InvalidOperationException>(() => GuardaStripe.Validar("Development", semHttps));
    }

    [Fact]
    public void Desligado_nao_confere_nada() =>
        GuardaStripe.Validar("Production", new OpcoesStripe { Habilitado = false, Modo = "qualquer coisa" });
}

/// <summary>A compra só anda pelas transições previstas, e o pagamento só credita uma vez.</summary>
public class CompraCreditosDominioTestes
{
    private static readonly DateTime Agora = new(2026, 9, 26, 12, 0, 0);

    private static CompraCreditos Nova() =>
        CompraCreditos.Nova(new PacoteCreditos { PacoteCreditos_ID = Guid.NewGuid(), Nome = "Básico", Creditos = 500, Preco = 50m }, Agora, null, "Gestor");

    [Fact]
    public void Congela_nome_creditos_e_valor_do_pacote()
    {
        var compra = Nova();
        Assert.Equal(("Básico", 500, 50m), (compra.NomePacote, compra.Creditos, compra.Valor));
        Assert.Equal(SituacaoCompraCreditos.AguardandoPagamento, compra.Situacao);
        Assert.NotEqual(Guid.Empty, compra.CompraCreditos_ID);
    }

    [Fact]
    public void Confirmar_de_novo_nao_muda_nada()
    {
        var compra = Nova();
        Assert.True(compra.ConfirmarPagamento(Agora, "pi_1", "card"));
        Assert.False(compra.ConfirmarPagamento(Agora.AddMinutes(1), "pi_1", "card"));
        Assert.Equal(SituacaoCompraCreditos.Paga, compra.Situacao);
    }

    [Fact]
    public void Boleto_ou_pix_emitido_so_a_partir_do_aguardando_pagamento()
    {
        var compra = Nova();
        Assert.True(compra.AguardarConfirmacao("boleto"));
        Assert.False(compra.AguardarConfirmacao("pix"));
        Assert.Equal("boleto", compra.MeioPagamento);
    }

    [Fact]
    public void Paga_nao_expira_nem_falha()
    {
        var compra = Nova();
        compra.ConfirmarPagamento(Agora, "pi_1", "card");
        Assert.False(compra.Expirar(Agora));
        Assert.False(compra.Falhar(Agora, "x"));
        Assert.Equal(SituacaoCompraCreditos.Paga, compra.Situacao);
    }

    [Fact]
    public void Pagamento_confirmado_depois_de_expirada_vale()
    {
        // O Stripe só confirma o que foi pago: o dinheiro entrou, os créditos entram.
        var compra = Nova();
        compra.Expirar(Agora);
        Assert.True(compra.ConfirmarPagamento(Agora.AddHours(1), "pi_1", "pix"));
    }
}
