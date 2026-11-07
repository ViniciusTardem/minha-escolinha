namespace MinhaEscolinha.Domain.Configuracao.Parametros;

/// <summary>Chaves dos parâmetros — o texto é gravado no banco: não renomeie uma chave existente.</summary>
public static class ChavesParametro
{
    // Presença e evasão (BL-M02-06)
    public const string PresencaRegraFaltas = "presenca.faltas.regra";
    public const string PresencaLimiteFaltas = "presenca.faltas.limite";
    public const string PresencaTomAlerta = "presenca.alerta.tom";
    public const string PresencaJanelaAntiDuplicidade = "presenca.alerta.janela_antiduplicidade_dias";
    public const string PresencaRiscoPercentual = "presenca.risco.frequencia_minima";
    public const string PresencaRiscoJanela = "presenca.risco.janela_dias";
    public const string PresencaFaltaJustificadaConta = "presenca.falta_justificada.conta_para_alerta";
    public const string PresencaRiscoMinimoSessoes = "presenca.risco.minimo_sessoes";
    public const string PresencaRiscoUsarMetaTurma = "presenca.risco.usar_meta_da_turma";

    // Financeiro (BL-M02-07)
    public const string FinanceiroMulta = "financeiro.multa.percentual";
    public const string FinanceiroJuros = "financeiro.juros.percentual_mes";
    public const string FinanceiroDiaVencimento = "financeiro.vencimento.dia";
    public const string FinanceiroEmissaoDiasAntes = "financeiro.emissao.dias_antes_vencimento";
    public const string FinanceiroReguaDiasAntes = "financeiro.regua.dias_antes";
    public const string FinanceiroReguaNoDia = "financeiro.regua.no_dia";
    public const string FinanceiroReguaDiasDepois = "financeiro.regua.dias_depois";
    public const string FinanceiroPortalTodosResponsaveis = "financeiro.portal.todos_responsaveis_veem";

    // Convocação (BL-M02-08)
    public const string ConvocacaoModoDisparo = "convocacao.disparo.modo";
    public const string ConvocacaoJanelaDisparo = "convocacao.disparo.janela";
    public const string ConvocacaoLembreteHorasAntes = "convocacao.lembrete.horas_antes";
    public const string ConvocacaoSinalizarInadimplencia = "convocacao.inadimplencia.sinalizar";
    public const string ConvocacaoDiasAtrasoSinalizacao = "convocacao.inadimplencia.dias_atraso";
    public const string ConvocacaoBloquearInadimplente = "convocacao.inadimplencia.bloquear";
    public const string ConvocacaoNovaConfirmacaoAoMudar = "convocacao.alteracao.nova_confirmacao";
    public const string ConvocacaoProfessorSoPropriaTurma = "convocacao.jogo_conjunto.professor_so_propria_turma";
    public const string ConvocacaoAvisoMudancaHorasAntes = "convocacao.mudanca_resposta.horas_antes";

    // Mensagem (BL-M02-09)
    public const string MensagemJanelaSilencio = "mensagem.silencio.janela";
    public const string MensagemAvisoSaldoCreditos = "mensagem.creditos.aviso_saldo_abaixo_de";
    public const string MensagemTom = "mensagem.tom";
    public const string MensagemDescadastroPorPalavra = "mensagem.descadastro.palavra";

    // Matrícula
    public const string MatriculaTrialDiasAviso = "matricula.trial.dias_aviso_antes";
    public const string MatriculaTermosValidadeDias = "matricula.termos.validade_dias";

    // Nível de turma (BL-M02-12): o valor da escolinha é o inicial das turmas novas.
    public const string TurmaMensalidade = "turma.mensalidade";
    public const string TurmaCapacidade = "turma.capacidade";
    public const string TurmaMinimoConfirmados = "turma.minimo_confirmados";
}

/// <summary>
/// Catálogo único dos parâmetros de regra (R7, BL-M02-06 a 09 e 12). Qualquer módulo lê por aqui
/// (IParametros na aplicação); a tela de configuração é gerada a partir das definições.
/// Acrescentar um parâmetro = uma entrada aqui; a reconciliação do startup grava o padrão nas escolinhas existentes.
/// </summary>
public static class CatalogoParametros
{
    private static readonly OpcaoParametro[] OpcoesRegraFaltas =
    [
        new("NoMes", "Faltas no mês"),
        new("Consecutivas", "Faltas consecutivas")
    ];

    private static readonly OpcaoParametro[] OpcoesTom =
    [
        new("Cordial", "Cordial"),
        new("Direto", "Direto")
    ];

    private static readonly OpcaoParametro[] OpcoesModoDisparo =
    [
        new("Imediato", "Na hora do disparo"),
        new("Janela", "Só dentro da janela de disparo")
    ];

    public static readonly IReadOnlyList<DefinicaoParametro> Todos =
    [
        // ── Presença e evasão ──
        new(ChavesParametro.PresencaRegraFaltas, DominioParametro.Presenca, TipoParametro.Escolha, EscopoParametro.Escolinha,
            "Regra do alerta de faltas", "Como contar as faltas que disparam o aviso ao responsável.",
            "NoMes", null, null, null, true, "RF-PRE-002, RF-PRE-003", OpcoesRegraFaltas),
        new(ChavesParametro.PresencaLimiteFaltas, DominioParametro.Presenca, TipoParametro.Inteiro, EscopoParametro.Escolinha,
            "Faltas para disparar o alerta", "O alerta sai no registro da falta que atinge este número (não no fechamento do mês).",
            "3", 1, 30, "faltas", true, "RF-PRE-003, CA-PRE-01"),
        new(ChavesParametro.PresencaTomAlerta, DominioParametro.Presenca, TipoParametro.Escolha, EscopoParametro.Escolinha,
            "Tom da mensagem de faltas", "Variação de texto usada no aviso de faltas ao responsável.",
            "Cordial", null, null, null, true, "RF-PRE-002", OpcoesTom),
        new(ChavesParametro.PresencaJanelaAntiDuplicidade, DominioParametro.Presenca, TipoParametro.Inteiro, EscopoParametro.Escolinha,
            "Janela anti-duplicidade", "O mesmo aluno não gera dois alertas de faltas dentro deste prazo.",
            "15", 1, 90, "dias", false, "RF-PRE-011"),
        new(ChavesParametro.PresencaRiscoPercentual, DominioParametro.Presenca, TipoParametro.Percentual, EscopoParametro.Escolinha,
            "Frequência mínima", "Abaixo desta frequência na janela de risco, o aluno aparece sinalizado como risco de evasão.",
            "60", 10, 100, "%", true, "RF-PRE-005"),
        new(ChavesParametro.PresencaRiscoJanela, DominioParametro.Presenca, TipoParametro.Inteiro, EscopoParametro.Escolinha,
            "Janela de risco", "Período recente usado para calcular a frequência do risco de evasão.",
            "30", 7, 180, "dias", true, "RF-PRE-005"),
        new(ChavesParametro.PresencaFaltaJustificadaConta, DominioParametro.Presenca, TipoParametro.Booleano, EscopoParametro.Escolinha,
            "Falta justificada conta para o alerta", "Desligado: a falta justificada é contada em separado e não dispara o alerta.",
            "false", null, null, null, true, "Pendência 12.2"),
        new(ChavesParametro.PresencaRiscoMinimoSessoes, DominioParametro.Presenca, TipoParametro.Inteiro, EscopoParametro.Escolinha,
            "Mínimo de sessões para o risco", "Com menos sessões marcadas na janela, o aluno ainda não entra no risco de evasão (evita o falso alarme do aluno novo).",
            "4", 1, 30, "sessões", true, "P39, RF-PRE-005"),
        new(ChavesParametro.PresencaRiscoUsarMetaTurma, DominioParametro.Presenca, TipoParametro.Booleano, EscopoParametro.Escolinha,
            "Usar a meta de frequência da turma", "Ligado: na turma com meta de frequência, o risco usa a meta dela no lugar da frequência mínima da escolinha.",
            "true", null, null, null, true, "P32"),

        // ── Financeiro ──
        new(ChavesParametro.FinanceiroMulta, DominioParametro.Financeiro, TipoParametro.Percentual, EscopoParametro.Escolinha,
            "Multa por atraso", "Aplicada sobre a mensalidade vencida; fica congelada na emissão da cobrança.",
            "2", 0, 10, "%", true, "RF-FIN-009"),
        new(ChavesParametro.FinanceiroJuros, DominioParametro.Financeiro, TipoParametro.Percentual, EscopoParametro.Escolinha,
            "Juros ao mês", "Juros de mora sobre a mensalidade vencida; ficam congelados na emissão da cobrança.",
            "1", 0, 10, "% ao mês", true, "RF-FIN-009"),
        new(ChavesParametro.FinanceiroDiaVencimento, DominioParametro.Financeiro, TipoParametro.Inteiro, EscopoParametro.Escolinha,
            "Dia do vencimento", "Dia do mês em que a mensalidade vence.",
            "10", 1, 28, "dia do mês", true, "RF-FIN-004"),
        new(ChavesParametro.FinanceiroEmissaoDiasAntes, DominioParametro.Financeiro, TipoParametro.Inteiro, EscopoParametro.Escolinha,
            "Emissão antes do vencimento", "Quantos dias antes do vencimento a cobrança é emitida na Asaas.",
            "10", 1, 30, "dias", true, "RF-FIN-004"),
        new(ChavesParametro.FinanceiroReguaDiasAntes, DominioParametro.Financeiro, TipoParametro.ListaDias, EscopoParametro.Escolinha,
            "Lembretes antes do vencimento", "Dias antes do vencimento em que sai o lembrete (separe por vírgula).",
            "3", 1, 30, "dias antes", true, "RF-FIN-004"),
        new(ChavesParametro.FinanceiroReguaNoDia, DominioParametro.Financeiro, TipoParametro.Booleano, EscopoParametro.Escolinha,
            "Lembrete no dia do vencimento", "Envia o lembrete na data do vencimento.",
            "true", null, null, null, true, "RF-FIN-004"),
        new(ChavesParametro.FinanceiroReguaDiasDepois, DominioParametro.Financeiro, TipoParametro.ListaDias, EscopoParametro.Escolinha,
            "Cobranças após o vencimento", "Dias depois do vencimento em que sai a cobrança, com tom crescente (separe por vírgula).",
            "1,5,10", 1, 60, "dias depois", true, "RF-FIN-004"),
        new(ChavesParametro.FinanceiroPortalTodosResponsaveis, DominioParametro.Financeiro, TipoParametro.Booleano, EscopoParametro.Escolinha,
            "Todos os responsáveis veem as mensalidades", "Desligado: no portal, só quem paga ou recebe a cobrança vê as mensalidades do filho. Ligado: todo responsável com vínculo confirmado vê.",
            "false", null, null, null, true, "P50"),

        // ── Convocação ──
        new(ChavesParametro.ConvocacaoModoDisparo, DominioParametro.Convocacao, TipoParametro.Escolha, EscopoParametro.Escolinha,
            "Quando a convocação sai", "Na hora em que o professor dispara, ou só dentro da janela de disparo.",
            "Imediato", null, null, null, true, "RF-CON-003", OpcoesModoDisparo),
        new(ChavesParametro.ConvocacaoJanelaDisparo, DominioParametro.Convocacao, TipoParametro.Janela, EscopoParametro.Escolinha,
            "Janela de disparo", "Horário em que as convocações podem sair quando o modo é \"só dentro da janela\".",
            "08:00-20:00", null, null, null, true, "RF-CON-003"),
        new(ChavesParametro.ConvocacaoLembreteHorasAntes, DominioParametro.Convocacao, TipoParametro.Inteiro, EscopoParametro.Escolinha,
            "Lembrete a quem não respondeu", "Quantas horas antes do evento sai o lembrete para os responsáveis pendentes.",
            "24", 1, 168, "horas antes", true, "RF-CON-005"),
        new(ChavesParametro.ConvocacaoSinalizarInadimplencia, DominioParametro.Convocacao, TipoParametro.Booleano, EscopoParametro.Escolinha,
            "Sinalizar inadimplência na convocação", "Mostra ao professor, na seleção dos convocados, quem está com mensalidade atrasada.",
            "true", null, null, null, true, "RF-CON-016"),
        new(ChavesParametro.ConvocacaoDiasAtrasoSinalizacao, DominioParametro.Convocacao, TipoParametro.Inteiro, EscopoParametro.Escolinha,
            "Atraso que sinaliza", "Dias de atraso a partir dos quais a inadimplência aparece sinalizada.",
            "10", 1, 90, "dias", true, "RF-CON-016"),
        new(ChavesParametro.ConvocacaoBloquearInadimplente, DominioParametro.Convocacao, TipoParametro.Booleano, EscopoParametro.Escolinha,
            "Bloquear convocação de inadimplente", "Sinalizar, não bloquear (R9): vem desligado e só liga por decisão da escolinha.",
            "false", null, null, null, false, "RF-CON-017, R9"),
        new(ChavesParametro.ConvocacaoNovaConfirmacaoAoMudar, DominioParametro.Convocacao, TipoParametro.Booleano, EscopoParametro.Escolinha,
            "Nova confirmação ao mudar o horário", "Ligado: mudar a data ou a hora do jogo volta os convocados para \"aguardando\" e manda a convocação de novo.",
            "true", null, null, null, true, "P45"),
        new(ChavesParametro.ConvocacaoProfessorSoPropriaTurma, DominioParametro.Convocacao, TipoParametro.Booleano, EscopoParametro.Escolinha,
            "No jogo de várias turmas, o professor vê só a dele", "Ligado: o professor vê e registra só os convocados das turmas dele. A gestão vê todos.",
            "true", null, null, null, true, "P47"),
        new(ChavesParametro.ConvocacaoAvisoMudancaHorasAntes, DominioParametro.Convocacao, TipoParametro.Inteiro, EscopoParametro.Escolinha,
            "Avisar mudança de resposta perto do jogo", "A resposta que muda a menos destas horas do jogo aparece em destaque para o professor e a gestão.",
            "24", 1, 168, "horas antes", true, "P51"),

        // ── Mensagem ──
        new(ChavesParametro.MensagemJanelaSilencio, DominioParametro.Mensagem, TipoParametro.Janela, EscopoParametro.Escolinha,
            "Janela de silêncio", "Nada sai neste horário, exceto cancelamento ou alteração de evento ou treino nas próximas 24 horas.",
            "21:00-07:00", null, null, null, false, "RF-MSG-009"),
        new(ChavesParametro.MensagemAvisoSaldoCreditos, DominioParametro.Mensagem, TipoParametro.Inteiro, EscopoParametro.Escolinha,
            "Aviso de saldo baixo", "Com o saldo de créditos abaixo deste número, o gestor é avisado.",
            "50", 0, 100000, "créditos", true, "RF-MSG-006"),
        new(ChavesParametro.MensagemTom, DominioParametro.Mensagem, TipoParametro.Escolha, EscopoParametro.Escolinha,
            "Tom dos avisos", "Variação de texto dos avisos (treino, jogo, convocação, pagamento, convite). As faltas e a régua da mensalidade têm o tom próprio.",
            "Cordial", null, null, null, true, "RF-MSG-003, P02", OpcoesTom),
        new(ChavesParametro.MensagemDescadastroPorPalavra, DominioParametro.Mensagem, TipoParametro.Booleano, EscopoParametro.Escolinha,
            "Descadastro por palavra", "Ligado: quem responde SAIR, PARAR ou STOP deixa de receber os avisos. A cobrança continua para quem paga.",
            "true", null, null, null, true, "P36"),

        // ── Matrícula ──
        new(ChavesParametro.MatriculaTrialDiasAviso, DominioParametro.Matricula, TipoParametro.Inteiro, EscopoParametro.Escolinha,
            "Aviso do fim do trial", "Quantos dias antes do fim do trial o gestor vê o aviso (e outro no próprio dia).",
            "3", 1, 30, "dias antes", false, "Spec §6 (Aluno)"),
        new(ChavesParametro.MatriculaTermosValidadeDias, DominioParametro.Matricula, TipoParametro.Inteiro, EscopoParametro.Escolinha,
            "Validade do link dos termos", "Por quantos dias o link de aceite dos termos (tratamento de dados e uso de imagem) vale para o responsável.",
            "15", 1, 90, "dias", true, "D111"),

        // ── Nível de turma (valor inicial das turmas novas) ──
        new(ChavesParametro.TurmaMensalidade, DominioParametro.Financeiro, TipoParametro.Moeda, EscopoParametro.Turma,
            "Mensalidade", "Valor mensal de cada turma. Sem padrão: informado ao criar a turma.",
            null, 0, 100000, "R$", false, "RF-FIN-001"),
        new(ChavesParametro.TurmaCapacidade, DominioParametro.Matricula, TipoParametro.Inteiro, EscopoParametro.Turma,
            "Capacidade de vagas", "Vagas de cada turma; a ocupação é mostrada contra este número.",
            "20", 1, 200, "vagas", true, "RF-ALU-013"),
        new(ChavesParametro.TurmaMinimoConfirmados, DominioParametro.Convocacao, TipoParametro.Inteiro, EscopoParametro.Turma,
            "Mínimo de confirmados por jogo", "O painel de confirmações mostra os confirmados contra este mínimo.",
            "7", 1, 30, "atletas", true, "RF-CON-011")
    ];

    private static readonly Dictionary<string, DefinicaoParametro> PorChave =
        Todos.ToDictionary(d => d.Chave, StringComparer.Ordinal);

    public static DefinicaoParametro? Obter(string chave) => PorChave.GetValueOrDefault(chave);

    public static DefinicaoParametro ObterObrigatorio(string chave) =>
        Obter(chave) ?? throw new KeyNotFoundException($"Parâmetro desconhecido: {chave}.");

    public static IEnumerable<DefinicaoParametro> DoDominio(DominioParametro dominio) => Todos.Where(d => d.Dominio == dominio);

    public static string RotuloDominio(DominioParametro dominio) => dominio switch
    {
        DominioParametro.Presenca => "Presença e evasão",
        DominioParametro.Financeiro => "Financeiro",
        DominioParametro.Convocacao => "Convocação",
        DominioParametro.Mensagem => "Mensagens",
        DominioParametro.Matricula => "Matrícula e turmas",
        _ => dominio.ToString()
    };
}
