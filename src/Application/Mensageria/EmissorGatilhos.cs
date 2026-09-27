using System.Globalization;
using System.Text.Json;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Configuracao;
using MinhaEscolinha.Domain.Mensageria;
using MinhaEscolinha.Domain.Turmas;

namespace MinhaEscolinha.Application.Mensageria;

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
    IRelogioBrasil relogio,
    OpcoesWhatsApp opcoes)
{
    private static readonly CultureInfo PtBr = new("pt-BR");

    public const string OrigemSessao = "Sessao";
    public const string OrigemConvite = "Convite";

    /// <summary>
    /// Treino cancelado (§8.3): os responsáveis que recebem notificação, dos alunos que treinam na turma, um aviso por
    /// responsável com os nomes dos filhos dele na turma. Urgente se o treino é nas próximas 24 horas.
    /// </summary>
    public async Task<int> TreinoCanceladoAsync(SessaoTreino sessao, string turma, CancellationToken ct = default)
    {
        var regra = CatalogoGatilhos.Obter(GatilhoMensagem.TreinoCancelado);
        var template = await TemplateAsync(regra.Gatilho, TomMensagem.Cordial, ct);
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
    /// Convite de acesso (§8.3) para a ficha convidada, só pelo WhatsApp (quem é convidado ainda não tem acesso). O link
    /// carrega o segredo do convite: ele fica na fila só até o envio e depois é mascarado no conteúdo guardado.
    /// </summary>
    public async Task ConviteAsync(ConviteAcesso convite, string token, CancellationToken ct = default)
    {
        var regra = CatalogoGatilhos.Obter(GatilhoMensagem.ConviteAcesso);
        var template = await TemplateAsync(regra.Gatilho, TomMensagem.Cordial, ct);
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
    /// A origem deixou de valer (regra de ouro 5: quando o evento chega, cancele o que ficou pendente): o que ainda está na
    /// fila dela é suprimido na hora, com o motivo, na mesma gravação de quem chama. Fica de fora a mensagem com envio em
    /// andamento ou com crédito já reservado (tentativa que volta à fila): o trabalho de envio revalida a origem e estorna.
    /// </summary>
    public async Task<int> CancelarPendentesAsync(string tipoOrigem, Guid origemId, string motivo, CancellationToken ct = default)
    {
        var agora = relogio.Agora();
        var suprimidas = 0;
        foreach (var mensagem in await repositorio.EmFilaDaOrigemAsync(tipoOrigem, origemId, ct))
        {
            if (mensagem.ReservadaPorOutro(agora) || mensagem.CreditoReservado) continue;
            mensagem.Suprimir(motivo, agora);
            suprimidas++;
        }
        return suprimidas;
    }

    private async Task<int> CriarAsync(RegraGatilho regra, TemplateMensagem template, string tipoOrigem, Guid origemId,
        TipoDestinatario tipoDestinatario, Guid destinatarioId, string nome, string? telefone, Guid? pessoaPortal, Guid? alunoId,
        IReadOnlyDictionary<string, string> valores, bool urgente, DateTime? referencia, DateTime agora, CancellationToken ct)
    {
        var parametros = template.ValoresEmOrdem(valores);
        var conteudo = RenderizadorTemplate.Renderizar(template.Corpo, parametros);
        var variaveis = JsonSerializer.Serialize(parametros);
        var criadas = 0;

        if (!string.IsNullOrWhiteSpace(telefone)
            && !await repositorio.ChaveEmUsoAsync(Mensagem.MontarChave(regra.Gatilho, origemId, tipoDestinatario, destinatarioId, CanalMensagem.WhatsApp), ct))
        {
            repositorio.AdicionarMensagem(Mensagem.Nova(regra, CanalMensagem.WhatsApp, tipoOrigem, origemId, tipoDestinatario, destinatarioId,
                nome, telefone, pessoaPortal, alunoId, template.NomeNaMeta, variaveis, conteudo, urgente, referencia, agora));
            criadas++;
        }

        if (regra.AvisoNoPortal && pessoaPortal is not null
            && !await repositorio.ChaveEmUsoAsync(Mensagem.MontarChave(regra.Gatilho, origemId, tipoDestinatario, destinatarioId, CanalMensagem.Portal), ct))
        {
            repositorio.AdicionarMensagem(Mensagem.Nova(regra, CanalMensagem.Portal, tipoOrigem, origemId, tipoDestinatario, destinatarioId,
                nome, null, pessoaPortal, alunoId, template.NomeNaMeta, variaveis, conteudo, urgente, referencia, agora));
            criadas++;
        }
        return criadas;
    }

    /// <summary>O template do catálogo; se ainda não foi gravado (startup), o texto padrão.</summary>
    private async Task<TemplateMensagem> TemplateAsync(GatilhoMensagem gatilho, TomMensagem tom, CancellationToken ct) =>
        await repositorio.TemplateDoGatilhoAsync(gatilho, tom, ct)
        ?? TemplatesPadrao.Criar(TemplatesPadrao.Todos.First(p => p.Gatilho == gatilho && p.Tom == tom));

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
