using MinhaEscolinha.Application.Mensageria;
using MinhaEscolinha.Domain.Alunos;
using MinhaEscolinha.Domain.Comum;
using MinhaEscolinha.Domain.Configuracao.Parametros;
using MinhaEscolinha.Domain.Mensageria;

namespace MinhaEscolinha.Testes.Mensageria;

/// <summary>O catálogo fechado de gatilhos (BL-M09-14, CA-20), os templates (BL-M09-03) e a mediação do responsável (CA-19).</summary>
public class CatalogoGatilhosTestes
{
    [Fact]
    public void Cada_gatilho_tem_uma_regra_e_um_template_padrao()
    {
        var gatilhos = Enum.GetValues<GatilhoMensagem>();
        Assert.Equal(gatilhos.Length, CatalogoGatilhos.Todas.Count);
        Assert.Equal(gatilhos.OrderBy(g => g), CatalogoGatilhos.Todas.Select(r => r.Gatilho).OrderBy(g => g));
        Assert.All(gatilhos, g => Assert.Contains(TemplatesPadrao.Todos, t => t.Gatilho == g && t.Tom == TomMensagem.Cordial));
    }

    [Fact]
    public void Os_gatilhos_que_esperam_resposta_sao_so_os_da_convocacao()
    {
        Assert.Equal([GatilhoMensagem.ConvocacaoCriada, GatilhoMensagem.ConvocacaoSemResposta],
            CatalogoGatilhos.Todas.Where(r => r.EsperaResposta).Select(r => r.Gatilho).Order());
        Assert.All(TemplatesPadrao.Todos.Where(t => CatalogoGatilhos.Obter(t.Gatilho).EsperaResposta),
            t => Assert.Equal(2, TemplatesPadrao.Criar(t).ListaBotoes.Count));
    }

    [Fact]
    public void Nenhum_destinatario_e_o_aluno()
    {
        // CA-19 por construção: o motor só conhece responsável e professor, e nenhum gatilho aponta para o aluno.
        Assert.Equal([TipoDestinatario.Responsavel, TipoDestinatario.Professor], Enum.GetValues<TipoDestinatario>());
        Assert.DoesNotContain(Enum.GetNames<DestinatarioGatilho>(), n => n.StartsWith("Aluno", StringComparison.Ordinal));
    }

    [Fact]
    public void Os_templates_padrao_seguem_as_regras_da_meta()
    {
        Assert.Equal(TemplatesPadrao.Todos.Count, TemplatesPadrao.Todos.Select(t => t.NomeNaMeta).Distinct().Count());
        Assert.All(TemplatesPadrao.Todos, p =>
        {
            var t = TemplatesPadrao.Criar(p);
            Assert.Matches("^[a-z0-9_]+$", t.NomeNaMeta);
            Assert.True(t.Corpo.Length <= TemplateMensagem.TamanhoCorpo);
            Assert.Equal(Enumerable.Range(1, t.NomesVariaveis.Count), RenderizadorTemplate.Marcadores(t.Corpo));
            Assert.False(t.Corpo.TrimStart().StartsWith("{{", StringComparison.Ordinal), t.NomeNaMeta);
            Assert.False(t.Corpo.TrimEnd().EndsWith("}}", StringComparison.Ordinal), t.NomeNaMeta);
            Assert.Equal(CategoriaTemplate.Utilidade, t.Categoria);
        });
    }

    [Fact]
    public void Renderizar_troca_os_marcadores_na_ordem_das_variaveis()
    {
        var template = TemplatesPadrao.Criar(TemplatesPadrao.Todos.Single(t => t.Gatilho == GatilhoMensagem.TreinoCancelado));
        var texto = template.Renderizar(new Dictionary<string, string>
        {
            ["responsavel"] = "Maria", ["escolinha"] = "Escolinha Bola na Rede", ["turma"] = "Sub-9", ["quando"] = "terça, 06/10 às 18:00",
            ["alunos"] = "Pedro"
        });
        Assert.Equal("Olá, Maria! Escolinha Bola na Rede informa: o treino da turma Sub-9 de terça, 06/10 às 18:00 foi cancelado. " +
                     "Motivo: —. Aviso para: Pedro.", texto);
    }
}

/// <summary>A mensagem: fila, entrega que só avança (BL-M09-08) e resposta (BL-M09-07).</summary>
public class MensagemTestes
{
    private static readonly DateTime Agora = new(2026, 9, 26, 12, 0, 0);

    private static Mensagem Nova(CanalMensagem canal = CanalMensagem.WhatsApp, GatilhoMensagem gatilho = GatilhoMensagem.TreinoCancelado) =>
        Mensagem.Nova(CatalogoGatilhos.Obter(gatilho), canal, "Sessao", Guid.NewGuid(), TipoDestinatario.Responsavel, Guid.NewGuid(), "Maria Lima",
            canal == CanalMensagem.WhatsApp ? "+5543988887777" : null, canal == CanalMensagem.Portal ? Guid.NewGuid() : null, null,
            "treino_cancelado_v1", "[]", "texto", urgente: false, referencia: null, Agora);

    [Fact]
    public void WhatsApp_nasce_na_fila_e_o_aviso_do_portal_nasce_disponivel_sem_credito()
    {
        Assert.Equal(SituacaoMensagem.Pendente, Nova().Situacao);
        var portal = Nova(CanalMensagem.Portal);
        Assert.Equal(SituacaoMensagem.Enviada, portal.Situacao);
        Assert.Equal(0, portal.CreditosConsumidos);
        Assert.Throws<ArgumentException>(() => Mensagem.Nova(CatalogoGatilhos.Obter(GatilhoMensagem.TreinoCancelado), CanalMensagem.WhatsApp,
            "Sessao", Guid.NewGuid(), TipoDestinatario.Responsavel, Guid.NewGuid(), "Sem celular", null, null, null, "t", "[]", "x", false, null, Agora));
        // O convite não vira aviso no portal: quem é convidado ainda não tem acesso.
        Assert.Throws<InvalidOperationException>(() => Nova(CanalMensagem.Portal, GatilhoMensagem.ConviteAcesso));
    }

    [Fact]
    public void A_entrega_so_avanca_e_retorno_atrasado_nao_desfaz()
    {
        var m = Nova();
        m.ReservarCredito();
        m.MarcarEnviada("wamid.1", Agora);
        Assert.Equal(1, m.CreditosConsumidos);

        Assert.True(m.AtualizarEntrega(RetornoEntrega.Lida, Agora.AddMinutes(2), null, Agora.AddMinutes(2)));
        Assert.Equal(SituacaoMensagem.Lida, m.Situacao);
        Assert.Equal(Agora.AddMinutes(2), m.EntregueEm);

        // O "entregue" que chega depois do "lida" não volta a mensagem para entregue; a falha depois de lida não vale.
        Assert.False(m.AtualizarEntrega(RetornoEntrega.Entregue, Agora.AddMinutes(1), null, Agora.AddMinutes(3)));
        Assert.False(m.AtualizarEntrega(RetornoEntrega.Falhou, Agora.AddMinutes(4), "erro", Agora.AddMinutes(4)));
        Assert.Equal(SituacaoMensagem.Lida, m.Situacao);
    }

    [Fact]
    public void Falha_depois_de_enviada_vale_enquanto_nao_foi_lida()
    {
        var m = Nova();
        m.MarcarEnviada("wamid.1", Agora);
        Assert.True(m.AtualizarEntrega(RetornoEntrega.Falhou, Agora.AddMinutes(1), "131026: número sem WhatsApp", Agora.AddMinutes(1)));
        Assert.Equal(SituacaoMensagem.Falhou, m.Situacao);
        Assert.Contains("131026", m.Motivo);
    }

    [Fact]
    public void Retida_volta_para_a_fila_e_suprimida_nao_sai_mais()
    {
        var m = Nova();
        m.Reter("Sem créditos", Agora);
        Assert.Equal(SituacaoMensagem.Retida, m.Situacao);
        m.Liberar("Voltou o saldo", Agora);
        Assert.Equal(SituacaoMensagem.Pendente, m.Situacao);
        m.Suprimir("O compromisso já passou", Agora);
        Assert.Throws<InvalidOperationException>(() => m.MarcarEnviada("x", Agora));
        Assert.Contains(m.Eventos, e => e.Tipo == TipoEventoMensagem.Suprimida);
    }

    [Fact]
    public void Quem_responde_leu_e_a_resposta_fica_na_linha_do_tempo()
    {
        var m = Nova(gatilho: GatilhoMensagem.ConvocacaoCriada);
        m.MarcarEnviada("wamid.1", Agora);
        m.RegistrarResposta(CanalMensagem.WhatsApp, "Vai", PayloadBotao.Montar(m.Mensagem_ID, 0), "wamid.in", Agora.AddMinutes(5), Agora.AddMinutes(5));
        Assert.Equal(SituacaoMensagem.Lida, m.Situacao);
        Assert.Equal(Agora.AddMinutes(5), m.RespondidaEm);
        Assert.Contains(m.Eventos, e => e.Tipo == TipoEventoMensagem.Respondida);
    }

    [Fact]
    public void A_reserva_de_envio_impede_um_segundo_trabalho()
    {
        var m = Nova();
        m.ReservarEnvio(Agora.AddMinutes(2));
        Assert.True(m.ReservadaPorOutro(Agora));
        Assert.False(m.ReservadaPorOutro(Agora.AddMinutes(3)));
    }

    [Fact]
    public void Payload_do_botao_leva_a_mensagem_e_o_indice()
    {
        var id = Guid.NewGuid();
        Assert.True(PayloadBotao.TentarLer(PayloadBotao.Montar(id, 1), out var lido, out var indice));
        Assert.Equal(id, lido);
        Assert.Equal(1, indice);
        Assert.False(PayloadBotao.TentarLer("qualquer coisa", out _, out _));
    }
}

/// <summary>Janela de silêncio, 9º dígito e o convite (spec §8.2, BL-M01-04).</summary>
public class PoliticaEnvioTestes
{
    private static readonly JanelaHorario Noite = new(new TimeOnly(21, 0), new TimeOnly(7, 0));

    [Theory]
    [InlineData("2026-09-26 22:30", "2026-09-27 07:00")]
    [InlineData("2026-09-27 06:10", "2026-09-27 07:00")]
    [InlineData("2026-09-27 21:00", "2026-09-28 07:00")]
    public void Dentro_da_janela_sai_no_fim_dela(string agora, string sai) =>
        Assert.Equal(DateTime.Parse(sai), PoliticaEnvio.FimDaJanelaDeSilencio(Noite, DateTime.Parse(agora)));

    [Theory]
    [InlineData("2026-09-27 07:00")]
    [InlineData("2026-09-27 12:00")]
    [InlineData("2026-09-27 20:59")]
    public void Fora_da_janela_sai_agora(string agora) => Assert.Null(PoliticaEnvio.FimDaJanelaDeSilencio(Noite, DateTime.Parse(agora)));

    [Fact]
    public void A_mensagem_urgente_so_escapa_da_janela_nas_24_horas_do_compromisso()
    {
        var agora = new DateTime(2026, 9, 26, 22, 0, 0);
        Mensagem Com(DateTime referencia) => Mensagem.Nova(CatalogoGatilhos.Obter(GatilhoMensagem.TreinoCancelado), CanalMensagem.WhatsApp,
            "Sessao", Guid.NewGuid(), TipoDestinatario.Responsavel, Guid.NewGuid(), "Maria", "+5543988887777", null, null, "t", "[]", "x",
            urgente: true, referencia, agora);

        Assert.True(Com(agora.AddHours(20)).UrgenteEm(agora));
        Assert.False(Com(agora.AddHours(30)).UrgenteEm(agora));
    }

    [Theory]
    [InlineData("+5543988887777", "554388887777")]
    [InlineData("554388887777", "554388887777")]
    [InlineData("5543988887777", "554388887777")]
    [InlineData("+554333334444", "554333334444")]
    public void A_chave_do_whatsapp_casa_o_celular_com_ou_sem_o_nono_digito(string numero, string chave) =>
        Assert.Equal(chave, Telefone.ChaveWhatsApp(numero));

    [Fact]
    public void O_convite_guarda_so_o_hash_e_cai_na_quinta_tentativa_errada()
    {
        var agora = new DateTime(2026, 9, 26, 12, 0, 0);
        var (convite, token) = ConviteAcesso.Emitir(TipoConvite.Responsavel, Guid.NewGuid(), "Maria", "+5543988887777", agora, null, "Gestor");

        Assert.NotEqual(token, convite.TokenHash);
        Assert.Equal(ConviteAcesso.Hash(token), convite.TokenHash);
        Assert.True(convite.Valido(agora.AddDays(6)));
        Assert.True(convite.Expirado(agora.AddDays(7)));

        for (var i = 0; i < ConviteAcesso.MaxTentativasIdentificacao; i++) convite.RegistrarTentativaErrada(agora);
        Assert.Equal(SituacaoConvite.Cancelado, convite.Situacao);
        Assert.False(convite.Valido(agora));
    }

    [Fact]
    public void O_vinculo_so_e_confirmado_ou_recusado_depois_de_pedido_e_recusar_exige_motivo()
    {
        var vinculo = new VinculoAlunoResponsavel();
        var agora = new DateTime(2026, 9, 26, 12, 0, 0);
        Assert.Throws<InvalidOperationException>(() => vinculo.Confirmar(agora, null, "Gestor"));

        vinculo.SolicitarVerificacao(agora);
        Assert.Throws<ArgumentException>(() => vinculo.Recusar(agora, null, "Gestor", " "));
        vinculo.Confirmar(agora, null, "Gestor");
        Assert.True(vinculo.Verificado);
        vinculo.SolicitarVerificacao(agora.AddDays(1));
        Assert.True(vinculo.Verificado);
    }
}

/// <summary>O webhook da Cloud API: assinatura X-Hub-Signature-256 e a leitura de retornos e respostas.</summary>
public class WebhookWhatsAppTestes
{
    private const string Segredo = "segredo_de_teste";

    private const string Corpo = """
        {"object":"whatsapp_business_account","entry":[{"id":"WABA","changes":[{"field":"messages","value":{
          "messaging_product":"whatsapp","metadata":{"display_phone_number":"5543000000000","phone_number_id":"123"},
          "statuses":[{"id":"wamid.A","status":"delivered","timestamp":"1790000000","recipient_id":"554388887777"},
                      {"id":"wamid.B","status":"failed","timestamp":"1790000001","recipient_id":"554377776666",
                       "errors":[{"code":131026,"title":"Message undeliverable","error_data":{"details":"sem WhatsApp"}}]}],
          "messages":[{"from":"554388887777","id":"wamid.IN1","timestamp":"1790000010","type":"button",
                       "button":{"payload":"abc:0","text":"Vai"},"context":{"from":"5543000000000","id":"wamid.A"}},
                      {"from":"554388887777","id":"wamid.IN2","timestamp":"1790000020","type":"text","text":{"body":"Obrigada!"}}]
        }}]}]}
        """;

    [Fact]
    public void Le_os_retornos_e_as_respostas_com_o_contexto()
    {
        var evento = EventoWhatsApp.Ler(Corpo)!;
        Assert.Equal(2, evento.Status.Count);
        Assert.Equal("delivered", evento.Status[0].Status);
        Assert.Contains("131026", evento.Status[1].Erro);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1790000000).UtcDateTime, evento.Status[0].EmUtc);

        var botao = evento.Mensagens[0];
        Assert.Equal(("button", "Vai", "abc:0", "wamid.A"), (botao.Tipo, botao.Texto, botao.Payload, botao.Contexto));
        var texto = evento.Mensagens[1];
        Assert.Equal(("text", "Obrigada!", null, null), (texto.Tipo, texto.Texto, texto.Payload, texto.Contexto));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("não é json")]
    [InlineData("""{"object":"page","entry":[]}""")]
    public void Corpo_que_nao_e_do_whatsapp_nao_e_lido(string corpo) => Assert.Null(EventoWhatsApp.Ler(corpo));

    [Fact]
    public void A_assinatura_confere_o_corpo_bruto_com_o_segredo_do_app()
    {
        var assinatura = AssinaturaWebhookWhatsApp.Montar(Corpo, Segredo);
        Assert.StartsWith("sha256=", assinatura);
        Assert.True(AssinaturaWebhookWhatsApp.Valida(Corpo, assinatura, Segredo));
        Assert.False(AssinaturaWebhookWhatsApp.Valida(Corpo + " ", assinatura, Segredo));
        Assert.False(AssinaturaWebhookWhatsApp.Valida(Corpo, assinatura, "outro_segredo"));
        Assert.False(AssinaturaWebhookWhatsApp.Valida(Corpo, assinatura["sha256=".Length..], Segredo));
        Assert.False(AssinaturaWebhookWhatsApp.Valida(Corpo, null, Segredo));
    }
}
