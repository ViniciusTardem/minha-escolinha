using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Domain.Mensageria;

namespace MinhaEscolinha.Application.Mensageria;

/// <summary>
/// O que a Minha Escolinha usa do WhatsApp: mandar um template aprovado. Implementado pelo cliente HTTP da Cloud API
/// (modo Api) e pelo simulador (modo Simulado). Só o <see cref="ServicoEnvioMensagens"/> chama (teste de arquitetura).
/// </summary>
public interface IGatewayWhatsApp
{
    /// <exception cref="FalhaIntegracaoException">Classificada (retentável ou definitiva).</exception>
    Task<EnvioWhatsApp> EnviarTemplateAsync(PedidoEnvioWhatsApp pedido, CancellationToken ct = default);
}

/// <param name="Telefone">Só dígitos, com o código do país (<c>5543988887777</c>).</param>
/// <param name="Parametros">Os valores dos {{n}}, em ordem.</param>
/// <param name="Botoes">Os rótulos dos botões de resposta rápida; o payload de cada um é <c>{mensagem}:{índice}</c>.</param>
/// <param name="Texto">O texto renderizado (o simulador mostra; a API não precisa).</param>
public sealed record PedidoEnvioWhatsApp(
    Guid MensagemId,
    Guid EscolinhaId,
    string Telefone,
    string Template,
    string Idioma,
    IReadOnlyList<string> Parametros,
    IReadOnlyList<string> Botoes,
    string Texto);

/// <param name="IdExterno">O wamid que a Meta devolveu.</param>
/// <param name="IdContato">O wa_id do contato (no Brasil, às vezes sem o 9º dígito).</param>
public sealed record EnvioWhatsApp(string IdExterno, string? IdContato);

/// <summary>Payload do botão de resposta rápida: a mensagem e o índice do botão (o módulo de origem sabe o que cada um quer dizer).</summary>
public static class PayloadBotao
{
    public static string Montar(Guid mensagemId, int indice) => $"{mensagemId:N}:{indice}";

    public static bool TentarLer(string? payload, out Guid mensagemId, out int indice)
    {
        mensagemId = Guid.Empty;
        indice = -1;
        var partes = payload?.Split(':');
        return partes is { Length: 2 } && Guid.TryParseExact(partes[0], "N", out mensagemId) && int.TryParse(partes[1], out indice);
    }
}

/// <summary>Jobs do motor (Hangfire em produção; um falso nos testes).</summary>
public interface IAgendadorMensageria
{
    /// <summary>Manda a mensagem agora, ou no horário indicado (fim da janela de silêncio).</summary>
    void Enviar(Guid escolinhaId, Guid mensagemId, DateTime? quando = null);

    void ProcessarWebhookWhatsApp(Guid webhookId);
}

/// <summary>
/// Quem trata a resposta de um gatilho que espera resposta (a convocação, S5). O motor registra toda resposta no log;
/// a mudança de estado é do módulo de origem (BL-M09-07: "devolvê-las ao módulo de origem").
/// </summary>
public interface IReceptorResposta
{
    GatilhoMensagem Gatilho { get; }

    /// <returns>O que a resposta fez (vai para o log), ou <c>null</c> se não fez nada.</returns>
    Task<string?> ReceberAsync(Mensagem mensagem, RespostaMensagem resposta, CancellationToken ct = default);
}

/// <summary>Um responsável que recebe o aviso de uma turma, com os alunos dele que estão nela.</summary>
/// <param name="PessoaId">A identidade de quem ativou o acesso (null = sem acesso: só WhatsApp).</param>
/// <param name="TodosVerificados">Todos os vínculos com esses alunos confirmados pelo gestor (o portal só mostra aluno confirmado).</param>
public sealed record DestinatarioResponsavel(
    Guid ResponsavelId,
    string Nome,
    string? Telefone,
    Guid? PessoaId,
    bool AcessoAtivo,
    bool TodosVerificados,
    IReadOnlyList<(Guid Id, string Nome)> Alunos);

public sealed class FiltroMensagens
{
    public DateOnly? De { get; set; }
    public DateOnly? Ate { get; set; }
    public GatilhoMensagem? Gatilho { get; set; }
    public CanalMensagem? Canal { get; set; }
    public SituacaoMensagem? Situacao { get; set; }
    public string? Busca { get; set; }
    public Guid? AlunoId { get; set; }
}

/// <summary>Mensagens, templates e o índice global do WhatsApp.</summary>
public interface IMensageriaRepositorio
{
    void AdicionarMensagem(Mensagem mensagem);
    Task<bool> ChaveEmUsoAsync(string chave, CancellationToken ct = default);

    /// <summary>A mensagem para enviar ou atualizar (rastreada, com os eventos).</summary>
    Task<Mensagem?> ObterMensagemAsync(Guid id, CancellationToken ct = default);

    /// <summary>As mensagens de uma origem que ainda estão na fila (pendentes ou retidas), rastreadas.</summary>
    Task<IReadOnlyList<Mensagem>> EmFilaDaOrigemAsync(string tipoOrigem, Guid origemId, CancellationToken ct = default);

    /// <summary>A mensagem para mostrar (sem rastreio, com eventos e respostas).</summary>
    Task<Mensagem?> DetalharMensagemAsync(Guid id, CancellationToken ct = default);

    /// <summary>WhatsApp pendentes cujo horário chegou (a varredura cobre o job perdido).</summary>
    Task<IReadOnlyList<Guid>> ProntasParaEnviarAsync(DateTime agora, int limite, CancellationToken ct = default);

    Task<IReadOnlyList<Guid>> RetidasAsync(int limite, CancellationToken ct = default);
    Task<int> ContarRetidasAsync(CancellationToken ct = default);

    Task<Pagina<Mensagem>> ListarMensagensAsync(FiltroMensagens filtro, int pagina, int tamanho, CancellationToken ct = default);
    Task<IReadOnlyDictionary<SituacaoMensagem, int>> ContarPorSituacaoAsync(DateTime desde, CancellationToken ct = default);

    /// <summary>Os avisos do portal da pessoa na escolinha do contexto (rastreados: abrir marca como lido).</summary>
    Task<IReadOnlyList<Mensagem>> AvisosDoPortalAsync(Guid pessoaId, int limite, CancellationToken ct = default);

    Task<int> AvisosNaoLidosAsync(Guid pessoaId, CancellationToken ct = default);

    /// <summary>Responsáveis que recebem notificação, dos alunos que treinam na turma (trial e ativo com matrícula ativa).</summary>
    Task<IReadOnlyList<DestinatarioResponsavel>> ResponsaveisDaTurmaAsync(Guid turmaId, CancellationToken ct = default);

    Task<IReadOnlyList<TemplateMensagem>> ListarTemplatesAsync(CancellationToken ct = default);
    Task<TemplateMensagem?> ObterTemplateAsync(Guid id, CancellationToken ct = default);
    Task<TemplateMensagem?> TemplateDoGatilhoAsync(GatilhoMensagem gatilho, TomMensagem tom, CancellationToken ct = default);
    Task<TemplateMensagem?> TemplatePorNomeAsync(string nomeNaMeta, CancellationToken ct = default);
    void AdicionarTemplate(TemplateMensagem template);

    void AdicionarRastreio(RastreioWhatsApp rastreio);
    Task<RastreioWhatsApp?> RastreioPorIdExternoAsync(string idExterno, CancellationToken ct = default);
    Task<RastreioWhatsApp?> UltimoRastreioDoTelefoneAsync(string chaveTelefone, CancellationToken ct = default);

    Task SalvarAsync(CancellationToken ct = default);
}
