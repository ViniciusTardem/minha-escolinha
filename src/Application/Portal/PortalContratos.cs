using MinhaEscolinha.Domain.Alunos;
using MinhaEscolinha.Domain.Eventos;
using MinhaEscolinha.Domain.Financeiro;
using MinhaEscolinha.Domain.Presencas;

namespace MinhaEscolinha.Application.Portal;

// ── Leitura ──

/// <summary>Um filho com vínculo confirmado pela escolinha (BL-M10-11): só estes aparecem no portal, nem o nome dos outros.</summary>
/// <param name="CuidaDasMensalidades">A pessoa é a pagadora ou recebe a cobrança deste filho: vê as mensalidades dele.</param>
public sealed record FilhoNoPortal(Guid AlunoId, string Nome, SituacaoAluno Situacao, IReadOnlyList<string> Turmas, bool CuidaDasMensalidades);

/// <summary>As presenças, as faltas e as faltas justificadas de um filho num período.</summary>
public sealed record FrequenciaNoPortal(int Presencas, int Faltas, int Justificadas)
{
    public int Marcadas => Presencas + Faltas + Justificadas;
}

/// <summary>A home do portal (CA-15): os filhos numa conta só, o que espera resposta, o que está em aberto e os avisos.</summary>
/// <param name="VinculosAConfirmar">Quantos alunos esperam a confirmação da escolinha (sem nome: bloqueio total, BL-M01-06).</param>
/// <param name="EmAberto">A soma das mensalidades em aberto dos filhos de quem a pessoa cuida das mensalidades.</param>
/// <param name="Frequencias">Os últimos 30 dias de cada filho.</param>
public sealed record InicioPortal(
    string Escolinha,
    string Nome,
    IReadOnlyList<FilhoNoPortal> Filhos,
    int VinculosAConfirmar,
    int AvisosNaoLidos,
    int ConvocacoesAResponder,
    decimal EmAberto,
    int MensalidadesEmAberto,
    IReadOnlyDictionary<Guid, FrequenciaNoPortal> Frequencias);

/// <summary>Uma convocação de um filho, com o jogo (BL-M10-07): de qual filho se trata, quando, onde e a resposta.</summary>
/// <param name="PropriaResposta">A resposta que vale é desta pessoa (pelo WhatsApp ou pelo portal): ela pode mudar (D88).</param>
/// <param name="PodeResponder">O jogo está de pé e não começou: "Vai" e "Não vai" valem (também para mudar a própria resposta).</param>
public sealed record ConvocacaoNoPortal(
    Guid ConvocacaoId,
    Guid AlunoId,
    string Aluno,
    string Jogo,
    DateTime Inicio,
    DateTime Fim,
    string Local,
    string? LinkMapa,
    string? Logistica,
    SituacaoEvento SituacaoEvento,
    string? MotivoEvento,
    SituacaoConvocacao Situacao,
    OrigemResposta? Origem,
    string? RespondidoPor,
    DateTime? RespondidaEm,
    string? OutraResposta,
    bool PropriaResposta,
    bool PodeResponder);

/// <summary>A aba Jogos: o que espera resposta, o que já foi respondido e o que já passou ou foi cancelado.</summary>
public sealed record JogosPortal(
    IReadOnlyList<FilhoNoPortal> Filhos,
    IReadOnlyList<ConvocacaoNoPortal> AResponder,
    IReadOnlyList<ConvocacaoNoPortal> Respondidas,
    IReadOnlyList<ConvocacaoNoPortal> Encerradas);

/// <summary>Uma mensalidade de um filho no portal (BL-M10-05, BL-M10-06).</summary>
/// <param name="EmAnalise">Tem divergência (valor diferente, estorno): a escolinha está conferindo, e o Pix sai da tela.</param>
/// <param name="PixCopiaECola">O Pix dentro da validade; sem ele, a fatura (que também tem o Pix).</param>
public sealed record MensalidadeNoPortal(
    Guid Id,
    Guid AlunoId,
    string Aluno,
    DateOnly Competencia,
    DateOnly Vencimento,
    decimal Valor,
    SituacaoCobranca Situacao,
    bool EmAnalise,
    DateOnly? PagaEm,
    decimal? ValorPago,
    string? UrlFatura,
    string? PixCopiaECola,
    string? PixImagem = null);

/// <summary>O mês de um filho (CA-16): o total das mensalidades do mês, o que já foi pago e o que falta.</summary>
public sealed record MesDoFilho(Guid AlunoId, string Aluno, decimal Total, decimal Pago, decimal APagar);

/// <summary>A aba Pagamentos: o total do mês com o detalhamento por filho, o que pagar agora, o que vem e o que já foi pago.</summary>
/// <param name="ComOutroResponsavel">Os filhos cujas mensalidades ficam com outro responsável (sem valores).</param>
public sealed record PagamentosPortal(
    DateOnly Mes,
    IReadOnlyList<FilhoNoPortal> Filhos,
    IReadOnlyList<MesDoFilho> PorFilho,
    IReadOnlyList<MensalidadeNoPortal> EmAberto,
    IReadOnlyList<MensalidadeNoPortal> Proximas,
    IReadOnlyList<MensalidadeNoPortal> Pagas,
    IReadOnlyList<string> ComOutroResponsavel)
{
    public decimal TotalDoMes => PorFilho.Sum(f => f.Total);
    public decimal PagoNoMes => PorFilho.Sum(f => f.Pago);
    public decimal APagarNoMes => PorFilho.Sum(f => f.APagar);
    public decimal TotalEmAberto => EmAberto.Sum(m => m.Valor);
}

public sealed record PresencaNoPortal(DateTime Sessao, string Turma, SituacaoPresenca Situacao);

/// <summary>As presenças de um filho nos treinos (só leitura, CA-15).</summary>
public sealed record PresencasPortal(FilhoNoPortal Filho, DateOnly De, DateOnly Ate, FrequenciaNoPortal Frequencia, IReadOnlyList<PresencaNoPortal> Presencas);

// ── Repositório ──

/// <summary>Um vínculo da pessoa logada com um aluno: uma linha por ficha de responsável e aluno.</summary>
/// <param name="CuidaDasMensalidades">O vínculo é o do pagador ou recebe a cobrança.</param>
public sealed record VinculoNoPortal(
    Guid ResponsavelId,
    string Responsavel,
    Guid AlunoId,
    string Aluno,
    SituacaoAluno Situacao,
    VerificacaoVinculo Verificacao,
    bool CuidaDasMensalidades);

public sealed record PresencaDoFilho(Guid AlunoId, DateTime Sessao, string Turma, SituacaoPresenca Situacao);

/// <summary>As leituras do portal, sempre pelos alunos que o serviço já filtrou (os filhos com vínculo confirmado).</summary>
public interface IPortalRepositorio
{
    /// <summary>Todos os vínculos das fichas de responsável da pessoa (confirmados ou não: o serviço filtra).</summary>
    Task<IReadOnlyList<VinculoNoPortal>> VinculosAsync(Guid pessoaId, CancellationToken ct = default);

    Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> TurmasAtivasAsync(IReadOnlyCollection<Guid> alunos, CancellationToken ct = default);

    /// <summary>As convocações já disparadas dos alunos em eventos que começam a partir da data, com o evento e as turmas, sem rastreio.</summary>
    Task<IReadOnlyList<(Convocacao Convocacao, Evento Evento)>> ConvocacoesAsync(IReadOnlyCollection<Guid> alunos, DateTime desde,
        CancellationToken ct = default);

    /// <summary>
    /// As mensalidades dos alunos desde a competência, mais as em aberto de qualquer mês, sem rastreio. Com
    /// <see cref="DateOnly.MaxValue"/>, só as em aberto.
    /// </summary>
    Task<IReadOnlyList<Cobranca>> MensalidadesAsync(IReadOnlyCollection<Guid> alunos, DateOnly desdeCompetencia, CancellationToken ct = default);

    /// <summary>As presenças marcadas dos alunos nas sessões do período, da mais recente para a mais antiga.</summary>
    Task<IReadOnlyList<PresencaDoFilho>> PresencasAsync(IReadOnlyCollection<Guid> alunos, DateTime de, DateTime ate, CancellationToken ct = default);
}
