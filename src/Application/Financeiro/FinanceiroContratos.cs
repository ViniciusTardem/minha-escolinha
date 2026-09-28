using FluentValidation;
using MinhaEscolinha.Domain.Financeiro;

namespace MinhaEscolinha.Application.Financeiro;

/// <summary>
/// Os grupos do painel de pagamentos (BL-M06-03). O agrupamento de RF-FIN-003 não veio nos documentos (P40): estes são
/// provisórios, pela situação da cobrança e pelo que falta para ela andar.
/// </summary>
public enum GrupoMensalidade
{
    /// <summary>A primeira mensalidade da matrícula, esperando a gestão confirmar.</summary>
    AConfirmar = 10,

    /// <summary>Prevista, ainda fora da Asaas (antes da data de emissão, ou impedida: sem conta, sem pagador com CPF, erro).</summary>
    AEmitir = 20,

    EmAberto = 30,
    Vencidas = 40,
    Pagas = 50,
    CanceladasEIsentas = 60
}

public sealed class FiltroMensalidades
{
    /// <summary>O mês (qualquer dia dele). Vazio = o mês de hoje.</summary>
    public DateOnly? Competencia { get; set; }

    public Guid? TurmaId { get; set; }
    public GrupoMensalidade? Grupo { get; set; }
    public string? Busca { get; set; }

    /// <summary>Só as cobranças com divergência (a fila, BL-M06-06), de qualquer mês.</summary>
    public bool SoDivergencias { get; set; }
}

/// <summary>Uma cobrança numa lista (painel, ficha do aluno, divergências).</summary>
public sealed record LinhaMensalidade(
    Guid Id,
    Guid AlunoId,
    string Aluno,
    string Turma,
    DateOnly Competencia,
    DateOnly Vencimento,
    decimal Valor,
    SituacaoCobranca Situacao,
    bool AguardandoConfirmacao,
    SinalCobranca Sinal,
    string? SinalDetalhe,
    string? Pagador,
    string? UrlFatura,
    DateOnly? PagaEm,
    decimal? ValorPago,
    MeioPagamento? Meio,
    string? ErroEmissao,
    string? UltimaEtapa,
    DateOnly? UltimaEtapaData,
    string? Motivo,
    bool AlunoDesligado,
    string? Impedimento,
    bool RecusadaPelaAsaas)
{
    public GrupoMensalidade Grupo => Situacao switch
    {
        SituacaoCobranca.Prevista when AguardandoConfirmacao => GrupoMensalidade.AConfirmar,
        SituacaoCobranca.Prevista => GrupoMensalidade.AEmitir,
        SituacaoCobranca.Emitida => GrupoMensalidade.EmAberto,
        SituacaoCobranca.Vencida => GrupoMensalidade.Vencidas,
        SituacaoCobranca.Paga or SituacaoCobranca.PagaEmAtraso => GrupoMensalidade.Pagas,
        _ => GrupoMensalidade.CanceladasEIsentas
    };

    public bool EmAberto => Situacao is SituacaoCobranca.Emitida or SituacaoCobranca.Vencida;

    /// <summary>Desligado com mensalidade em aberto: o débito que sobrou do desligamento (BL-M06-14).</summary>
    public bool DebitoRemanescente => AlunoDesligado && EmAberto;

    /// <summary>Cancelar ou isentar (a paga e estornada, só pela fila de divergências): a mesma regra do domínio.</summary>
    public bool PodeSerEncerrada => Cobranca.PodeEncerrar(Situacao, Sinal);

    public bool PodeAceitarPagamentoDivergente => Cobranca.AceitaPagamentoDivergente(Situacao, Sinal, ValorPago, PagaEm);
}

public sealed record TotalGrupo(GrupoMensalidade Grupo, int Quantidade, decimal Valor);

public sealed record OpcaoTurmaFinanceiro(Guid Id, string Nome, int PeriodicidadeMeses = 1);

public sealed record PainelMensalidades(
    DateOnly Competencia,
    IReadOnlyList<LinhaMensalidade> Linhas,
    IReadOnlyList<TotalGrupo> Totais,
    int Divergencias,
    bool ContaVinculada,
    string Integracao,
    IReadOnlyList<OpcaoTurmaFinanceiro> Turmas);

/// <summary>O cartão do painel inicial da gestão.</summary>
public sealed record ResumoFinanceiro(int AConfirmar, int Vencidas, decimal ValorVencido, int Divergencias, int Impedidas, bool ContaVinculada);

/// <summary>A cobrança com o que as telas de ação mostram.</summary>
public sealed record CobrancaParaAcao(LinhaMensalidade Linha, bool PodeCancelar, bool PodeIsentar, bool PodeReceberPorFora, bool PodeConfirmar,
    bool PodeReenviar);

/// <summary>O pagador do aluno para a emissão: o vínculo marcado como pagador (um por aluno, D34).</summary>
public sealed record PagadorDoAluno(Guid ResponsavelId, string Nome, string? Cpf, string? Email, string? Telefone);

public sealed class ConfirmarMensalidadeRequisicao
{
    public Guid Id { get; set; }
    public decimal? Valor { get; set; }
}

public sealed class ConfirmarMensalidadeValidador : AbstractValidator<ConfirmarMensalidadeRequisicao>
{
    public ConfirmarMensalidadeValidador()
    {
        RuleFor(r => r.Valor).NotNull().WithMessage("Informe o valor da primeira mensalidade.")
            .GreaterThan(0).WithMessage("O valor precisa ser maior que zero.")
            .LessThanOrEqualTo(100000).WithMessage("Valor alto demais para uma mensalidade.");
    }
}

public sealed class EncerrarMensalidadeRequisicao
{
    public Guid Id { get; set; }

    /// <summary>Cancelada ou Isenta.</summary>
    public SituacaoCobranca Para { get; set; }

    public string? Motivo { get; set; }
}

public sealed class EncerrarMensalidadeValidador : AbstractValidator<EncerrarMensalidadeRequisicao>
{
    public EncerrarMensalidadeValidador()
    {
        RuleFor(r => r.Para).Must(p => p is SituacaoCobranca.Cancelada or SituacaoCobranca.Isenta).WithMessage("Escolha cancelar ou isentar.");
        RuleFor(r => r.Motivo).NotEmpty().WithMessage("Informe o motivo.")
            .MaximumLength(Cobranca.TamanhoMotivo).WithMessage($"O motivo tem no máximo {Cobranca.TamanhoMotivo} caracteres.");
    }
}

public sealed class ReceberPorForaRequisicao
{
    public Guid Id { get; set; }
    public DateOnly? Data { get; set; }
    public MeioPagamento Meio { get; set; } = MeioPagamento.Dinheiro;
    public string? Observacao { get; set; }
}

public sealed class ReceberPorForaValidador : AbstractValidator<ReceberPorForaRequisicao>
{
    public ReceberPorForaValidador()
    {
        RuleFor(r => r.Data).NotNull().WithMessage("Informe a data do recebimento.");
        RuleFor(r => r.Meio).IsInEnum().WithMessage("Escolha como foi pago.");
        RuleFor(r => r.Observacao).NotEmpty().WithMessage("Diga quem recebeu e como (fica no histórico).")
            .MaximumLength(Cobranca.TamanhoMotivo).WithMessage($"A observação tem no máximo {Cobranca.TamanhoMotivo} caracteres.");
    }
}

/// <summary>Como resolver uma divergência (BL-M06-06). Toda resolução pede a explicação e fica com autoria.</summary>
public enum ResolucaoDivergencia
{
    /// <summary>Só registrar que a gestão viu e tratou por fora (a situação fica como está).</summary>
    Ciencia = 10,

    /// <summary>Aceitar o valor que entrou como o pagamento da mensalidade.</summary>
    AceitarComoPaga = 20,

    Cancelar = 30,
    Isentar = 40,

    /// <summary>A paga e estornada: cancela e cobra de novo, numa via nova do mesmo mês (P43).</summary>
    CobrarDeNovo = 50
}

public sealed class ResolverDivergenciaRequisicao
{
    public Guid Id { get; set; }
    public ResolucaoDivergencia Resolucao { get; set; }
    public string? Explicacao { get; set; }

    /// <summary>O vencimento da cobrança nova, quando a gestão cobra de novo (P43).</summary>
    public DateOnly? NovoVencimento { get; set; }
}

public sealed class ResolverDivergenciaValidador : AbstractValidator<ResolverDivergenciaRequisicao>
{
    public ResolverDivergenciaValidador()
    {
        RuleFor(r => r.Resolucao).IsInEnum().WithMessage("Escolha como resolver.");
        RuleFor(r => r.NovoVencimento).NotNull().When(r => r.Resolucao == ResolucaoDivergencia.CobrarDeNovo)
            .WithMessage("Informe o vencimento da cobrança nova.");
        RuleFor(r => r.Explicacao).NotEmpty().WithMessage("Explique a resolução (fica no histórico).")
            .MaximumLength(Cobranca.TamanhoMotivo).WithMessage($"A explicação tem no máximo {Cobranca.TamanhoMotivo} caracteres.");
    }
}

/// <summary>Cobranças, clientes da Asaas e o que a emissão precisa do cadastro, sempre na escolinha do contexto.</summary>
public interface IFinanceiroRepositorio
{
    /// <summary>As matrículas ativas e as encerradas desde a data, com a situação do aluno (a projeção).</summary>
    Task<IReadOnlyList<MatriculaParaCobranca>> MatriculasParaCobrancaAsync(DateOnly encerradasDesde, CancellationToken ct = default);

    /// <summary>As cobranças das competências a partir da data, rastreadas (a projeção ajusta e retira).</summary>
    Task<IReadOnlyList<Cobranca>> CobrancasDesdeAsync(DateOnly competencia, CancellationToken ct = default);

    /// <summary>Das matrículas informadas, as que já tiveram alguma cobrança, em qualquer época.</summary>
    Task<IReadOnlyList<Guid>> MatriculasComCobrancaAsync(IReadOnlyCollection<Guid> matriculas, CancellationToken ct = default);

    /// <summary>Previstas com vencimento até a data (candidatas à emissão), rastreadas.</summary>
    Task<IReadOnlyList<Cobranca>> PrevistasAteAsync(DateOnly vencimento, CancellationToken ct = default);

    /// <summary>Emitidas e vencidas, rastreadas.</summary>
    Task<IReadOnlyList<Cobranca>> EmAbertoAsync(CancellationToken ct = default);

    /// <summary>As previstas que a Asaas recusou de vez (não tentam sozinhas de novo), rastreadas.</summary>
    Task<IReadOnlyList<Cobranca>> RecusadasAsync(CancellationToken ct = default);

    /// <summary>As em aberto com a cobrança na Asaas, das menos conferidas para as mais (a varredura), rastreadas.</summary>
    Task<IReadOnlyList<Cobranca>> ParaConferirAsync(DateTime conferidasAntesDe, int limite, CancellationToken ct = default);

    /// <summary>Previstas das matrículas com vencimento depois da data (o encerramento da recorrência), rastreadas.</summary>
    Task<IReadOnlyList<Cobranca>> PrevistasDasMatriculasAposAsync(IReadOnlyCollection<Guid> matriculas, DateOnly data, CancellationToken ct = default);

    Task<Cobranca?> ObterAsync(Guid id, CancellationToken ct = default);
    Task<Cobranca?> ObterPorIdAsaasAsync(string idAsaas, CancellationToken ct = default);

    Task<PagadorDoAluno?> PagadorAsync(Guid alunoId, CancellationToken ct = default);
    Task<string> NomeDoAlunoAsync(Guid alunoId, CancellationToken ct = default);

    Task<ClienteAsaas?> ClienteAsaasAsync(Guid responsavelId, Guid conta, CancellationToken ct = default);
    void AdicionarCliente(ClienteAsaas cliente);

    /// <summary>As linhas do painel (sem rastreio), com os nomes, o pagador e o impedimento de emissão.</summary>
    Task<IReadOnlyList<LinhaMensalidade>> ListarAsync(FiltroMensalidades filtro, CancellationToken ct = default);

    Task<IReadOnlyList<LinhaMensalidade>> DoAlunoAsync(Guid alunoId, int limite, CancellationToken ct = default);
    Task<LinhaMensalidade?> LinhaAsync(Guid id, CancellationToken ct = default);
    Task<int> ContarDivergenciasAsync(CancellationToken ct = default);

    /// <summary>O cartão do painel: a confirmar, vencidas (e o valor), divergências e as previstas impedidas de emitir.</summary>
    Task<ResumoFinanceiro> ResumoAsync(DateOnly emitirAte, CancellationToken ct = default);
    Task<IReadOnlyList<OpcaoTurmaFinanceiro>> TurmasAsync(CancellationToken ct = default);

    void Adicionar(Cobranca cobranca);
    void Remover(Cobranca cobranca);
    Task SalvarAsync(CancellationToken ct = default);
}

/// <summary>Os trabalhos do financeiro (Hangfire): toda chamada à Asaas sai de um job, nunca do request (regra de ouro 2).</summary>
public interface IAgendadorFinanceiro
{
    /// <summary>Refaz a projeção das previstas logo depois de uma mudança no cadastro (matrícula, situação, valor).</summary>
    void Projetar(Guid escolinhaId);

    void Emitir(Guid escolinhaId, Guid cobrancaId);

    /// <summary>Busca na Asaas o Pix copia e cola da mensalidade em aberto, para o portal (BL-M10-06).</summary>
    void ObterPix(Guid escolinhaId, Guid cobrancaId);

    /// <summary>Relê a cobrança na Asaas e concilia (o envio que viu a cobrança paga lá, o retorno do simulador).</summary>
    void Conferir(Guid escolinhaId, Guid cobrancaId);

    void ExcluirNaAsaas(Guid escolinhaId, Guid cobrancaId);
    void ReceberEmDinheiroNaAsaas(Guid escolinhaId, Guid cobrancaId);
    void ProcessarWebhookAsaas(Guid webhookId);
    void RegistrarWebhook(Guid escolinhaId);

    /// <summary>O cadastro do responsável mudou: o cliente dele na Asaas recebe os dados novos (P42).</summary>
    void AtualizarClienteAsaas(Guid escolinhaId, Guid responsavelId);
}
