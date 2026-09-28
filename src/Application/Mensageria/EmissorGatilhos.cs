using System.Globalization;
using System.Text.Json;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Configuracao;
using MinhaEscolinha.Domain.Eventos;
using MinhaEscolinha.Domain.Financeiro;
using MinhaEscolinha.Domain.Mensageria;
using MinhaEscolinha.Domain.Presencas;
using MinhaEscolinha.Domain.Turmas;

namespace MinhaEscolinha.Application.Mensageria;

/// <summary>O evento como as mensagens o descrevem (quem chama monta: conhece as turmas e o local).</summary>
/// <param name="Jogo">"jogo da Sub-11 contra o Tigre" (minúsculo: entra no meio da frase).</param>
/// <param name="Quando">"sábado, 03/10 às 09:00".</param>
/// <param name="Mapa">O link de mapa do local (CA-11).</param>
public sealed record DadosEventoMensagem(string Jogo, string Quando, string Local, string Mapa, DateTime Inicio);

/// <summary>
/// A porta dos módulos para o motor único (BL-M09-01, R3): cada método é um gatilho do catálogo. Resolve quem recebe
/// (responsável ou professor, nunca o aluno — R4, BL-M09-13), renderiza o template e grava as mensagens na fila (outbox)
/// no mesmo SaveChanges da mudança de negócio. O despacho sai depois do commit; a varredura cobre o que ficar para trás.
/// <para>
/// Canal (BL-M12-11, modo parcial): WhatsApp para todos, como piso, e o aviso no portal para quem ativou o acesso e tem os
/// vínculos confirmados — assim ninguém fica sem a mensagem enquanto nem todos ativaram.
/// </para>
/// </summary>
public class EmissorGatilhos(
    IMensageriaRepositorio repositorio,
    IConfiguracaoRepositorio configuracao,
    IParametros parametros,
    IRelogioBrasil relogio,
    OpcoesWhatsApp opcoes)
{
    private static readonly CultureInfo PtBr = new("pt-BR");

    public const string OrigemSessao = "Sessao";
    public const string OrigemTurma = "Turma";
    public const string OrigemConvite = "Convite";
    public const string OrigemAlertaFaltas = "AlertaFaltas";
    public const string OrigemCobranca = "Cobranca";
    public const string OrigemConvocacao = "Convocacao";
    public const string OrigemEvento = "Evento";

    /// <summary>
    /// Treino cancelado (§8.3): os responsáveis que recebem notificação, dos alunos que treinam na turma, um aviso por
    /// responsável com os nomes dos filhos dele na turma. Urgente se o treino é nas próximas 24 horas.
    /// </summary>
    public async Task<int> TreinoCanceladoAsync(SessaoTreino sessao, string turma, CancellationToken ct = default)
    {
        var regra = CatalogoGatilhos.Obter(GatilhoMensagem.TreinoCancelado);
        var template = await TemplateAsync(regra.Gatilho, await TomDosAvisosAsync(ct), ct);
        var escolinha = await NomeEscolinhaAsync(ct);
        var agora = relogio.Agora();
        var quando = sessao.Inicio.ToString("dddd, dd/MM 'às' HH:mm", PtBr);
        var urgente = sessao.Inicio <= agora.AddHours(24);

        var criadas = 0;
        foreach (var d in await repositorio.ResponsaveisDaTurmaAsync(sessao.Turma_ID, ct))
        {
            var valores = new Dictionary<string, string>
            {
                ["responsavel"] = PrimeiroNome(d.Nome),
                ["turma"] = turma,
                ["quando"] = quando,
                ["motivo"] = sessao.MotivoCancelamento ?? "—",
                ["alunos"] = Juntar(d.Alunos.Select(a => PrimeiroNome(a.Nome))),
                ["escolinha"] = escolinha
            };
            criadas += await CriarAsync(regra, template, OrigemSessao, sessao.SessaoTreino_ID, TipoDestinatario.Responsavel,
                d.ResponsavelId, d.Nome, d.Telefone, d.AcessoAtivo && d.TodosVerificados ? d.PessoaId : null,
                d.Alunos.Count == 1 ? d.Alunos[0].Id : null, valores, urgente, sessao.Inicio, agora, ct);
        }
        return criadas;
    }

    /// <summary>
    /// Treino remarcado (§8.3): os responsáveis que recebem notificação, dos alunos da turma, um aviso por responsável com os
    /// nomes dos filhos dele. Vale para a remarcação de uma sessão (BL-M04-07, origem = a sessão nova) e para a mudança do
    /// horário da turma (P33, origem = a turma, uma etapa por mudança). Urgente se o treino afetado é nas próximas 24 horas.
    /// </summary>
    public async Task<int> TreinoRemarcadoAsync(Guid turmaId, string turma, string tipoOrigem, Guid origemId, string de, string para,
        bool urgente, DateTime? referencia, string? etapa, CancellationToken ct = default)
    {
        var regra = CatalogoGatilhos.Obter(GatilhoMensagem.TreinoRemarcado);
        var template = await TemplateAsync(regra.Gatilho, await TomDosAvisosAsync(ct), ct);
        var escolinha = await NomeEscolinhaAsync(ct);
        var agora = relogio.Agora();

        var criadas = 0;
        foreach (var d in await repositorio.ResponsaveisDaTurmaAsync(turmaId, ct))
        {
            var valores = new Dictionary<string, string>
            {
                ["responsavel"] = PrimeiroNome(d.Nome),
                ["escolinha"] = escolinha,
                ["turma"] = turma,
                ["de"] = de,
                ["para"] = para,
                ["alunos"] = Juntar(d.Alunos.Select(a => PrimeiroNome(a.Nome)))
            };
            criadas += await CriarAsync(regra, template, tipoOrigem, origemId, TipoDestinatario.Responsavel, d.ResponsavelId, d.Nome,
                d.Telefone, d.AcessoAtivo && d.TodosVerificados ? d.PessoaId : null, d.Alunos.Count == 1 ? d.Alunos[0].Id : null,
                valores, urgente, referencia, agora, ct, etapa);
        }
        return criadas;
    }

    /// <summary>
    /// Convite de acesso (§8.3) para a ficha convidada, só pelo WhatsApp (quem é convidado ainda não tem acesso). O link
    /// carrega o segredo do convite: ele fica na fila só até o envio e depois é mascarado no conteúdo guardado.
    /// </summary>
    public async Task ConviteAsync(ConviteAcesso convite, string token, CancellationToken ct = default)
    {
        var regra = CatalogoGatilhos.Obter(GatilhoMensagem.ConviteAcesso);
        var template = await TemplateAsync(regra.Gatilho, await TomDosAvisosAsync(ct), ct);
        var valores = new Dictionary<string, string>
        {
            ["nome"] = PrimeiroNome(convite.Nome),
            ["escolinha"] = await NomeEscolinhaAsync(ct),
            ["link"] = opcoes.LinkConvite(token),
            ["validade"] = convite.ExpiraEm.ToString("dd/MM/yyyy", PtBr)
        };
        var tipo = convite.Tipo == TipoConvite.Responsavel ? TipoDestinatario.Responsavel : TipoDestinatario.Professor;
        await CriarAsync(regra, template, OrigemConvite, convite.ConviteAcesso_ID, tipo, convite.FichaId, convite.Nome, convite.Telefone,
            null, null, valores, false, null, relogio.Agora(), ct);
    }

    /// <summary>
    /// Regra de faltas atingida (§8.3, BL-M05-08): os responsáveis do aluno que recebem notificação, um aviso por responsável,
    /// no tom escolhido pela escolinha (<c>presenca.alerta.tom</c>). Não é urgente: respeita a janela de silêncio.
    /// </summary>
    public async Task<int> FaltasAtingidasAsync(AlertaFaltas alerta, string aluno, string turma, TomMensagem tom, CancellationToken ct = default)
    {
        var regra = CatalogoGatilhos.Obter(GatilhoMensagem.FaltasAtingidas);
        var template = await TemplateAsync(regra.Gatilho, tom, ct);
        var escolinha = await NomeEscolinhaAsync(ct);
        var agora = relogio.Agora();

        var criadas = 0;
        foreach (var d in await repositorio.ResponsaveisDoAlunoAsync(alerta.Aluno_ID, ct))
        {
            var valores = new Dictionary<string, string>
            {
                ["responsavel"] = PrimeiroNome(d.Nome),
                ["escolinha"] = escolinha,
                ["aluno"] = PrimeiroNome(aluno),
                ["faltas"] = alerta.QuantidadeFaltas,
                ["periodo"] = alerta.Periodo,
                ["turma"] = turma
            };
            criadas += await CriarAsync(regra, template, OrigemAlertaFaltas, alerta.AlertaFaltas_ID, TipoDestinatario.Responsavel,
                d.ResponsavelId, d.Nome, d.Telefone, d.AcessoAtivo && d.TodosVerificados ? d.PessoaId : null, alerta.Aluno_ID,
                valores, false, null, agora, ct);
        }
        return criadas;
    }

    /// <summary>
    /// Mensalidade a vencer ou vencida (§8.3, BL-M06-04): um degrau da régua (ou um reenvio manual) para os responsáveis
    /// financeiros do aluno — os vínculos que recebem cobrança —, com o link da fatura da Asaas. O degrau entra na chave: a
    /// mesma cobrança manda um aviso por degrau. Essencial (spec §8.2): sem crédito, fica retida.
    /// </summary>
    public async Task<int> MensalidadeAsync(Cobranca cobranca, GatilhoMensagem gatilho, TomMensagem tom, string etapa, string aluno,
        CancellationToken ct = default)
    {
        if (gatilho is not (GatilhoMensagem.MensalidadeAVencer or GatilhoMensagem.MensalidadeVencida))
            throw new ArgumentException("A régua só manda mensalidade a vencer ou vencida.", nameof(gatilho));
        if (string.IsNullOrWhiteSpace(cobranca.UrlFatura)) throw new InvalidOperationException("Cobrança sem fatura na Asaas.");

        var regra = CatalogoGatilhos.Obter(gatilho);
        var template = await TemplateAsync(gatilho, tom, ct);
        var escolinha = await NomeEscolinhaAsync(ct);
        var agora = relogio.Agora();

        var criadas = 0;
        foreach (var d in await repositorio.ResponsaveisFinanceirosDoAlunoAsync(cobranca.Aluno_ID, ct))
        {
            var valores = new Dictionary<string, string>
            {
                ["responsavel"] = PrimeiroNome(d.Nome),
                ["escolinha"] = escolinha,
                ["aluno"] = PrimeiroNome(aluno),
                ["valor"] = Cobranca.Dinheiro(cobranca.Valor),
                ["vencimento"] = cobranca.Vencimento.ToString("dd/MM/yyyy", PtBr),
                ["link"] = cobranca.UrlFatura!
            };
            criadas += await CriarAsync(regra, template, OrigemCobranca, cobranca.Cobranca_ID, TipoDestinatario.Responsavel,
                d.ResponsavelId, d.Nome, d.Telefone, d.AcessoAtivo && d.TodosVerificados ? d.PessoaId : null, cobranca.Aluno_ID,
                valores, false, null, agora, ct, etapa);
        }
        return criadas;
    }

    /// <summary>
    /// Pagamento confirmado (§8.3, na conciliação): o agradecimento aos responsáveis financeiros do aluno. Uma vez por
    /// cobrança. Não é essencial: sem crédito, não fica na fila.
    /// </summary>
    public async Task<int> PagamentoConfirmadoAsync(Cobranca cobranca, string aluno, CancellationToken ct = default)
    {
        var regra = CatalogoGatilhos.Obter(GatilhoMensagem.PagamentoConfirmado);
        var template = await TemplateAsync(regra.Gatilho, await TomDosAvisosAsync(ct), ct);
        var escolinha = await NomeEscolinhaAsync(ct);
        var agora = relogio.Agora();

        var criadas = 0;
        foreach (var d in await repositorio.ResponsaveisFinanceirosDoAlunoAsync(cobranca.Aluno_ID, ct))
        {
            var valores = new Dictionary<string, string>
            {
                ["responsavel"] = PrimeiroNome(d.Nome),
                ["escolinha"] = escolinha,
                ["aluno"] = PrimeiroNome(aluno),
                ["valor"] = Cobranca.Dinheiro(cobranca.ValorPago ?? cobranca.Valor)
            };
            criadas += await CriarAsync(regra, template, OrigemCobranca, cobranca.Cobranca_ID, TipoDestinatario.Responsavel,
                d.ResponsavelId, d.Nome, d.Telefone, d.AcessoAtivo && d.TodosVerificados ? d.PessoaId : null, cobranca.Aluno_ID,
                valores, false, null, agora, ct);
        }
        return criadas;
    }

    /// <summary>
    /// Convocação criada (§8.3, BL-M07-07): os responsáveis do convocado que recebem notificação, uma mensagem por convocado
    /// (CA-11), com o nome do filho (CA-13), a data, a hora, o local com o link de mapa e os botões Vai/Não vai. Essencial:
    /// sem crédito, fica retida. Fora da janela de disparo, quando a escolinha pede, sai no começo da próxima (RF-CON-003).
    /// </summary>
    /// <param name="etapa">A convocação que sai de novo (P45: o horário mudou) é uma etapa nova da mesma convocação.</param>
    public async Task<int> ConvocacaoAsync(Convocacao convocacao, DadosEventoMensagem evento, string aluno, DateTime? agendarPara,
        CancellationToken ct = default, string? etapa = null)
    {
        var regra = CatalogoGatilhos.Obter(GatilhoMensagem.ConvocacaoCriada);
        var template = await TemplateAsync(regra.Gatilho, await TomDosAvisosAsync(ct), ct);
        var escolinha = await NomeEscolinhaAsync(ct);
        var agora = relogio.Agora();

        var criadas = 0;
        foreach (var d in await repositorio.ResponsaveisDoAlunoAsync(convocacao.Aluno_ID, ct))
        {
            var valores = new Dictionary<string, string>
            {
                ["responsavel"] = PrimeiroNome(d.Nome),
                ["escolinha"] = escolinha,
                ["aluno"] = PrimeiroNome(aluno),
                ["jogo"] = evento.Jogo,
                ["quando"] = evento.Quando,
                ["local"] = evento.Local,
                ["mapa"] = evento.Mapa
            };
            criadas += await CriarAsync(regra, template, OrigemConvocacao, convocacao.Convocacao_ID, TipoDestinatario.Responsavel,
                d.ResponsavelId, d.Nome, d.Telefone, d.AcessoAtivo && d.TodosVerificados ? d.PessoaId : null, convocacao.Aluno_ID,
                valores, false, evento.Inicio, agora, ct, etapa, agendarPara);
        }
        return criadas;
    }

    /// <summary>
    /// Convocação sem resposta (§8.3, BL-M07-11): o lembrete aos responsáveis do convocado que ainda não respondeu, na
    /// janela configurada antes do evento. Uma vez por convocação.
    /// </summary>
    public async Task<int> LembreteConvocacaoAsync(Convocacao convocacao, DadosEventoMensagem evento, string aluno, CancellationToken ct = default)
    {
        var regra = CatalogoGatilhos.Obter(GatilhoMensagem.ConvocacaoSemResposta);
        var template = await TemplateAsync(regra.Gatilho, await TomDosAvisosAsync(ct), ct);
        var escolinha = await NomeEscolinhaAsync(ct);
        var agora = relogio.Agora();

        var criadas = 0;
        foreach (var d in await repositorio.ResponsaveisDoAlunoAsync(convocacao.Aluno_ID, ct))
        {
            var valores = new Dictionary<string, string>
            {
                ["responsavel"] = PrimeiroNome(d.Nome),
                ["escolinha"] = escolinha,
                ["aluno"] = PrimeiroNome(aluno),
                ["jogo"] = evento.Jogo,
                ["quando"] = evento.Quando
            };
            criadas += await CriarAsync(regra, template, OrigemConvocacao, convocacao.Convocacao_ID, TipoDestinatario.Responsavel,
                d.ResponsavelId, d.Nome, d.Telefone, d.AcessoAtivo && d.TodosVerificados ? d.PessoaId : null, convocacao.Aluno_ID,
                valores, false, evento.Inicio, agora, ct);
        }
        return criadas;
    }

    /// <summary>
    /// Evento alterado (§8.3, BL-M07-13): mudou o quando ou o onde. Um aviso por responsável dos convocados (P13), com os
    /// nomes dos filhos dele. Urgente se o evento é nas próximas 24 horas. Cada alteração é uma etapa própria.
    /// </summary>
    public async Task<int> EventoAlteradoAsync(Guid eventoId, DadosEventoMensagem evento, IReadOnlyCollection<Guid> convocados, string etapa,
        CancellationToken ct = default)
    {
        var regra = CatalogoGatilhos.Obter(GatilhoMensagem.EventoAlterado);
        var template = await TemplateAsync(regra.Gatilho, await TomDosAvisosAsync(ct), ct);
        var escolinha = await NomeEscolinhaAsync(ct);
        var agora = relogio.Agora();

        var criadas = 0;
        foreach (var d in await repositorio.ResponsaveisDosAlunosAsync(convocados, ct))
        {
            var valores = new Dictionary<string, string>
            {
                ["responsavel"] = PrimeiroNome(d.Nome),
                ["escolinha"] = escolinha,
                ["jogo"] = evento.Jogo,
                ["quando"] = evento.Quando,
                ["local"] = evento.Local,
                ["mapa"] = evento.Mapa,
                ["alunos"] = Juntar(d.Alunos.Select(a => PrimeiroNome(a.Nome)))
            };
            criadas += await CriarAsync(regra, template, OrigemEvento, eventoId, TipoDestinatario.Responsavel, d.ResponsavelId, d.Nome,
                d.Telefone, d.AcessoAtivo && d.TodosVerificados ? d.PessoaId : null, d.Alunos.Count == 1 ? d.Alunos[0].Id : null,
                valores, true, evento.Inicio, agora, ct, etapa);
        }
        return criadas;
    }

    /// <summary>
    /// Evento cancelado ou adiado (§8.3, BL-M07-13, CA-14): um aviso por responsável dos convocados, com os nomes dos
    /// filhos e o motivo. Urgente se o evento era nas próximas 24 horas.
    /// </summary>
    public async Task<int> EventoCanceladoAsync(Guid eventoId, DadosEventoMensagem evento, IReadOnlyCollection<Guid> convocados, bool adiado,
        string motivo, CancellationToken ct = default)
    {
        var regra = CatalogoGatilhos.Obter(GatilhoMensagem.EventoCancelado);
        var template = await TemplateAsync(regra.Gatilho, await TomDosAvisosAsync(ct), ct);
        var escolinha = await NomeEscolinhaAsync(ct);
        var agora = relogio.Agora();

        var criadas = 0;
        foreach (var d in await repositorio.ResponsaveisDosAlunosAsync(convocados, ct))
        {
            var valores = new Dictionary<string, string>
            {
                ["responsavel"] = PrimeiroNome(d.Nome),
                ["escolinha"] = escolinha,
                ["jogo"] = evento.Jogo,
                ["quando"] = evento.Quando,
                ["situacao"] = adiado ? "adiado (a nova data será avisada)" : "cancelado",
                ["motivo"] = motivo,
                ["alunos"] = Juntar(d.Alunos.Select(a => PrimeiroNome(a.Nome)))
            };
            criadas += await CriarAsync(regra, template, OrigemEvento, eventoId, TipoDestinatario.Responsavel, d.ResponsavelId, d.Nome,
                d.Telefone, d.AcessoAtivo && d.TodosVerificados ? d.PessoaId : null, d.Alunos.Count == 1 ? d.Alunos[0].Id : null,
                valores, true, evento.Inicio, agora, ct);
        }
        return criadas;
    }

    /// <summary>
    /// A origem deixou de valer (regra de ouro 5: quando o evento chega, cancele o que ficou pendente): o WhatsApp que ainda
    /// está na fila é suprimido na hora, e o aviso do portal que ninguém abriu sai do portal, com o motivo, na mesma gravação
    /// de quem chama. Fica de fora a mensagem com envio em andamento ou com crédito já reservado (tentativa que volta à
    /// fila): o trabalho de envio revalida a origem e estorna.
    /// </summary>
    public async Task<int> CancelarPendentesAsync(string tipoOrigem, Guid origemId, string motivo, CancellationToken ct = default)
    {
        var agora = relogio.Agora();
        var recolhidas = 0;
        foreach (var mensagem in await repositorio.CancelaveisDaOrigemAsync(tipoOrigem, origemId, ct))
        {
            if (mensagem.Canal == CanalMensagem.Portal)
            {
                if (mensagem.RecolherDoPortal(motivo, agora)) recolhidas++;
                continue;
            }
            if (mensagem.ReservadaPorOutro(agora) || mensagem.CreditoReservado) continue;
            mensagem.Suprimir(motivo, agora);
            recolhidas++;
        }
        return recolhidas;
    }

    /// <param name="agendarPara">O WhatsApp só sai a partir desta hora (a janela de disparo da convocação).</param>
    private async Task<int> CriarAsync(RegraGatilho regra, TemplateMensagem template, string tipoOrigem, Guid origemId,
        TipoDestinatario tipoDestinatario, Guid destinatarioId, string nome, string? telefone, Guid? pessoaPortal, Guid? alunoId,
        IReadOnlyDictionary<string, string> valores, bool urgente, DateTime? referencia, DateTime agora, CancellationToken ct,
        string? etapa = null, DateTime? agendarPara = null)
    {
        var parametros = template.ValoresEmOrdem(valores);
        var conteudo = RenderizadorTemplate.Renderizar(template.Corpo, parametros);
        var variaveis = JsonSerializer.Serialize(parametros);
        var criadas = 0;

        if (!string.IsNullOrWhiteSpace(telefone)
            && !await repositorio.ChaveEmUsoAsync(
                Mensagem.MontarChave(regra.Gatilho, origemId, tipoDestinatario, destinatarioId, CanalMensagem.WhatsApp, etapa), ct))
        {
            var whatsApp = Mensagem.Nova(regra, CanalMensagem.WhatsApp, tipoOrigem, origemId, tipoDestinatario, destinatarioId,
                nome, telefone, pessoaPortal, alunoId, template.NomeNaMeta, variaveis, conteudo, urgente, referencia, agora, etapa);
            if (agendarPara is { } quando && quando > agora)
                whatsApp.Agendar(quando, $"Janela de disparo: sai às {quando:HH:mm} de {quando:dd/MM}.", agora);
            repositorio.AdicionarMensagem(whatsApp);
            criadas++;
        }

        if (regra.AvisoNoPortal && pessoaPortal is not null
            && !await repositorio.ChaveEmUsoAsync(
                Mensagem.MontarChave(regra.Gatilho, origemId, tipoDestinatario, destinatarioId, CanalMensagem.Portal, etapa), ct))
        {
            repositorio.AdicionarMensagem(Mensagem.Nova(regra, CanalMensagem.Portal, tipoOrigem, origemId, tipoDestinatario, destinatarioId,
                nome, null, pessoaPortal, alunoId, template.NomeNaMeta, variaveis, conteudo, urgente, referencia, agora, etapa));
            criadas++;
        }
        return criadas;
    }

    /// <summary>O template do catálogo; se ainda não foi gravado (startup), o texto padrão.</summary>
    private async Task<TemplateMensagem> TemplateAsync(GatilhoMensagem gatilho, TomMensagem tom, CancellationToken ct) =>
        await repositorio.TemplateDoGatilhoAsync(gatilho, tom, ct)
        ?? TemplatesPadrao.Criar(TemplatesPadrao.Todos.First(p => p.Gatilho == gatilho && p.Tom == tom));

    /// <summary>O tom dos avisos escolhido pela escolinha (P02). As faltas e a régua da mensalidade têm o tom próprio.</summary>
    private async Task<TomMensagem> TomDosAvisosAsync(CancellationToken ct) =>
        (await parametros.ObterAsync(ct)).Mensagem.Tom == "Direto" ? TomMensagem.Direto : TomMensagem.Cordial;

    private async Task<string> NomeEscolinhaAsync(CancellationToken ct) => (await configuracao.ObterEscolinhaAsync(ct))?.Nome ?? "—";

    public static string PrimeiroNome(string nome) =>
        nome.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? nome;

    /// <summary>"Ana", "Ana e Pedro", "Ana, Pedro e Caio".</summary>
    public static string Juntar(IEnumerable<string> nomes)
    {
        var lista = nomes.ToList();
        return lista.Count <= 1 ? lista.FirstOrDefault() ?? "—" : string.Join(", ", lista[..^1]) + " e " + lista[^1];
    }
}
