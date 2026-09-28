using System.Globalization;
using MinhaEscolinha.Domain.Comum;

namespace MinhaEscolinha.Domain.Financeiro;

/// <summary>Máquina da cobrança (spec §6): prevista → emitida → paga | vencida → paga em atraso | cancelada | isenta.</summary>
public enum SituacaoCobranca
{
    /// <summary>A mensalidade projetada da matrícula, ainda fora da Asaas: acompanha a matrícula até a emissão.</summary>
    Prevista = 10,

    /// <summary>Criada na Asaas, com a fatura (Pix, boleto ou cartão) disponível para a família.</summary>
    Emitida = 20,

    Paga = 30,
    Vencida = 40,
    PagaEmAtraso = 50,
    Cancelada = 60,

    /// <summary>Não gera cobrança nem entra na inadimplência (spec §6).</summary>
    Isenta = 70
}

/// <summary>
/// O que ficou para a gestão tratar (fila de divergências, BL-M06-06). Nunca muda a situação sozinho: pagamento parcial ou
/// divergente fica sinalizado para tratamento manual (spec §6, CA-9).
/// </summary>
public enum SinalCobranca
{
    Nenhum = 0,

    /// <summary>Entrou um valor diferente do original (parcial ou a mais).</summary>
    ValorDivergente = 10,

    /// <summary>Estorno pedido ou feito na Asaas depois do pagamento.</summary>
    Estorno = 20,

    /// <summary>Contestação do pagamento no cartão (chargeback).</summary>
    Chargeback = 30,

    /// <summary>A cobrança foi excluída na Asaas por fora do sistema.</summary>
    ExcluidaNaAsaas = 40,

    /// <summary>Entrou pagamento numa cobrança que já estava cancelada ou isenta aqui.</summary>
    PagaDepoisDeEncerrada = 50,

    /// <summary>Emitida na conta Asaas anterior a uma troca de conta: sai da régua até alguém conferir na conta antiga.</summary>
    ContaAnterior = 60
}

public enum MeioPagamento
{
    Pix = 10,
    Boleto = 20,
    Cartao = 30,
    Dinheiro = 40,
    Indefinido = 90
}

/// <summary>O que um pagamento que chegou fez com a cobrança.</summary>
public enum ResultadoPagamento
{
    /// <summary>Valor original confere: paga (ou paga em atraso) e a régua para.</summary>
    Conciliada = 10,

    /// <summary>Já estava paga: o mesmo pagamento chegou de novo (idempotente).</summary>
    JaPaga = 20,

    /// <summary>Valor diferente do original: sinalizada, sem mudar a situação (CA-9).</summary>
    Divergente = 30,

    /// <summary>A cobrança estava cancelada ou isenta: sinalizada para a gestão.</summary>
    DepoisDeEncerrada = 40
}

/// <summary>
/// A mensalidade de uma matrícula numa competência (BL-M06-01): uma por (matrícula, competência). Nasce prevista pela
/// projeção da matrícula (<see cref="PlanejadorCobrancas"/>), é emitida na Asaas por job e conciliada pelo webhook ou pela
/// varredura (BL-M06-02, BL-M06-05). A referência da Asaas fica junto (spec §8.1: rastreável), e o id desta cobrança vai
/// como referência externa: a emissão repetida reencontra a cobrança em vez de criar outra.
/// <para>
/// Multa e juros são os do momento da emissão (regra de ouro 14). Quem pagou nunca recebe cobrança posterior daquela
/// mensalidade (CA-8): a régua só olha cobrança em aberto, e o envio revalida.
/// </para>
/// </summary>
public class Cobranca : IEntidadeAuditavel, IEntidadeDaEscolinha
{
    public const int TamanhoNome = 150;
    public const int TamanhoIdAsaas = 60;
    public const int TamanhoUrl = 300;
    public const int TamanhoMotivo = 300;
    public const int TamanhoErro = 500;
    public const int TamanhoEtapa = 20;

    /// <summary>O copia e cola do Pix nunca é cortado (cortado, não paga): maior que isto, não é guardado.</summary>
    public const int TamanhoPix = 1000;

    private static readonly CultureInfo PtBr = new("pt-BR");

    public Guid Cobranca_ID { get; set; }
    public Guid Escolinha_ID { get; set; }
    public Guid Aluno_ID { get; set; }
    public Guid Matricula_ID { get; set; }
    public Guid Turma_ID { get; set; }

    /// <summary>O mês da mensalidade (sempre o dia 1).</summary>
    public DateOnly Competencia { get; set; }

    public DateOnly Vencimento { get; private set; }
    public decimal Valor { get; private set; }

    /// <summary>A primeira mensalidade da matrícula no sistema: sai só depois que a gestão confirma (P17: pró-rata e início).</summary>
    public bool PrimeiraDaMatricula { get; set; }

    public DateTime? ConfirmadaEm { get; private set; }
    public Guid? ConfirmadaPor_ID { get; private set; }
    public string? ConfirmadaPorNome { get; private set; }

    public SituacaoCobranca Situacao { get; private set; } = SituacaoCobranca.Prevista;

    // ── Emissão ──
    /// <summary>O responsável pagador no momento da emissão (o cliente na Asaas).</summary>
    public Guid? Pagador_ID { get; private set; }

    public string? PagadorNome { get; private set; }
    public decimal? MultaPercentual { get; private set; }
    public decimal? JurosPercentualMes { get; private set; }

    /// <summary>Id da cobrança na Asaas (<c>pay_...</c>).</summary>
    public string? IdAsaas { get; private set; }

    /// <summary>A fatura da Asaas (Pix, boleto ou cartão). Endereço permanente da cobrança, não um link temporário.</summary>
    public string? UrlFatura { get; private set; }

    /// <summary>A conta Asaas da escolinha em que foi emitida (<c>ContaAsaasEscolinha.Identificador</c>).</summary>
    public Guid? ContaAsaas { get; private set; }

    public DateTime? EmitidaEm { get; private set; }
    public int TentativasEmissao { get; private set; }
    public string? ErroEmissao { get; private set; }

    /// <summary>A Asaas recusou o pedido (dado inválido): não adianta tentar de novo sem mudar algo.</summary>
    public bool ErroEmissaoDefinitivo { get; private set; }

    public DateTime? ConferidaEm { get; private set; }

    // ── Pix no portal (BL-M10-06) ──
    /// <summary>
    /// O Pix copia e cola da cobrança, buscado na Asaas por um job depois da emissão, para o portal mostrar sem chamar a
    /// Asaas no request (regra de ouro 2). Não é link: é o conteúdo do QR code, com a validade que a Asaas informar.
    /// </summary>
    public string? PixCopiaECola { get; private set; }

    public DateTime? PixExpiraEm { get; private set; }

    // ── Pagamento ──
    public MeioPagamento? Meio { get; private set; }
    public decimal? ValorPago { get; private set; }
    public DateOnly? PagaEm { get; private set; }
    public DateTime? ConciliadaEm { get; private set; }

    // ── Cancelamento, isenção e recebimento manual ──
    public string? Motivo { get; private set; }
    public DateTime? EncerradaEm { get; private set; }
    public Guid? EncerradaPor_ID { get; private set; }
    public string? EncerradaPorNome { get; private set; }

    // ── Régua ──
    /// <summary>A última etapa da régua que saiu ("A3", "D0", "V5"...) e a data dela.</summary>
    public string? UltimaEtapa { get; private set; }

    public DateOnly? UltimaEtapaData { get; private set; }

    // ── Divergência ──
    public SinalCobranca Sinal { get; private set; } = SinalCobranca.Nenhum;
    public string? SinalDetalhe { get; private set; }
    public DateTime? SinalEm { get; private set; }
    public string? SinalResolucao { get; private set; }
    public DateTime? SinalResolvidoEm { get; private set; }
    public string? SinalResolvidoPorNome { get; private set; }

    public byte[] Versao { get; set; } = [];

    public DateTime DataCadastro { get; set; }
    public Guid? UsuarioCadastro_ID { get; set; }
    public DateTime? DataAtualizacao { get; set; }
    public Guid? UsuarioAtualizacao_ID { get; set; }

    public bool EmAberto => Situacao is SituacaoCobranca.Emitida or SituacaoCobranca.Vencida;

    /// <summary>O Pix que o portal pode mostrar agora: a cobrança em aberto, sem divergência, e o copia e cola dentro da validade.</summary>
    public bool PixValidoEm(DateTime agora) => EmAberto && !ComSinal && PixCopiaECola is not null && (PixExpiraEm is null || PixExpiraEm > agora);

    /// <returns>Se guardou (o copia e cola grande demais não é cortado nem guardado: quem chama registra no log).</returns>
    public bool RegistrarPix(string copiaECola, DateTime? expiraEm)
    {
        if (string.IsNullOrWhiteSpace(copiaECola) || copiaECola.Length > TamanhoPix) return false;
        PixCopiaECola = copiaECola;
        PixExpiraEm = expiraEm;
        return true;
    }
    public bool Quitada => Situacao is SituacaoCobranca.Paga or SituacaoCobranca.PagaEmAtraso;
    public bool Encerrada => Situacao is SituacaoCobranca.Cancelada or SituacaoCobranca.Isenta;
    public bool AguardandoConfirmacao => Situacao == SituacaoCobranca.Prevista && PrimeiraDaMatricula && ConfirmadaEm is null;
    public bool ComSinal => Sinal != SinalCobranca.Nenhum;

    /// <summary>A gestão mexeu nesta prevista (confirmou, com o valor dela): a projeção não mexe mais.</summary>
    public bool Confirmada => ConfirmadaEm is not null;

    public static Cobranca Prevista(Guid matriculaId, Guid alunoId, Guid turmaId, DateOnly competencia, DateOnly vencimento, decimal valor,
        bool primeira)
    {
        if (valor <= 0) throw new ArgumentOutOfRangeException(nameof(valor), "A mensalidade precisa de valor.");
        return new Cobranca
        {
            Cobranca_ID = Guid.NewGuid(),
            Matricula_ID = matriculaId,
            Aluno_ID = alunoId,
            Turma_ID = turmaId,
            Competencia = new DateOnly(competencia.Year, competencia.Month, 1),
            Vencimento = vencimento,
            Valor = valor,
            PrimeiraDaMatricula = primeira,
            Situacao = SituacaoCobranca.Prevista
        };
    }

    /// <summary>A data em que ela deve ir para a Asaas.</summary>
    public DateOnly DataEmissao(int diasAntesDoVencimento) => Vencimento.AddDays(-diasAntesDoVencimento);

    /// <summary>A projeção acompanha a matrícula (valor e vencimento) enquanto ninguém confirmou a prevista.</summary>
    /// <returns>Se mudou algo.</returns>
    public bool AjustarProjecao(decimal valor, DateOnly vencimento)
    {
        if (Situacao != SituacaoCobranca.Prevista || Confirmada) return false;
        if (Valor == valor && Vencimento == vencimento) return false;
        if (valor <= 0) throw new ArgumentOutOfRangeException(nameof(valor), "A mensalidade precisa de valor.");
        Valor = valor;
        Vencimento = vencimento;
        return true;
    }

    /// <summary>A gestão confirma a primeira mensalidade (e pode mudar o valor, pela entrada no meio do mês).</summary>
    public void Confirmar(decimal valor, DateTime agora, Guid? autorId, string autorNome)
    {
        if (!AguardandoConfirmacao) throw new InvalidOperationException("Só a primeira mensalidade prevista espera confirmação.");
        if (valor <= 0) throw new ArgumentOutOfRangeException(nameof(valor), "A mensalidade precisa de valor.");
        Valor = valor;
        ConfirmadaEm = agora;
        ConfirmadaPor_ID = autorId;
        ConfirmadaPorNome = TextoHelper.Truncar(autorNome, TamanhoNome);
    }

    /// <summary>Pode ir para a Asaas hoje: prevista, confirmada quando é a primeira, e já na data de emissão.</summary>
    public bool ProntaParaEmitir(DateOnly hoje, int diasAntesDoVencimento) =>
        Situacao == SituacaoCobranca.Prevista && !AguardandoConfirmacao && !ErroEmissaoDefinitivo && hoje >= DataEmissao(diasAntesDoVencimento);

    /// <summary>
    /// Congela o pagador, a multa e os juros da emissão (regra de ouro 14). O vencimento que já passou (a primeira confirmada
    /// depois da data) vira hoje: a Asaas não aceita vencimento no passado.
    /// </summary>
    /// <returns>O vencimento anterior, quando mudou.</returns>
    public DateOnly? PrepararEmissao(Guid pagadorId, string pagadorNome, decimal multaPercentual, decimal jurosPercentualMes, DateOnly hoje)
    {
        if (Situacao != SituacaoCobranca.Prevista) throw new InvalidOperationException("Só a cobrança prevista é emitida.");
        Pagador_ID = pagadorId;
        PagadorNome = TextoHelper.Truncar(pagadorNome, TamanhoNome);
        MultaPercentual ??= multaPercentual;
        JurosPercentualMes ??= jurosPercentualMes;
        if (Vencimento >= hoje) return null;
        var anterior = Vencimento;
        Vencimento = hoje;
        return anterior;
    }

    public void RegistrarEmissao(string idAsaas, string? urlFatura, Guid conta, DateTime agora)
    {
        if (Situacao != SituacaoCobranca.Prevista) throw new InvalidOperationException("Só a cobrança prevista é emitida.");
        Situacao = SituacaoCobranca.Emitida;
        IdAsaas = TextoHelper.Truncar(idAsaas, TamanhoIdAsaas);
        UrlFatura = TextoHelper.Truncar(urlFatura, TamanhoUrl);
        ContaAsaas = conta;
        EmitidaEm = agora;
        ConferidaEm = agora;
        ErroEmissao = null;
        ErroEmissaoDefinitivo = false;
    }

    public void RegistrarFalhaEmissao(string erro, bool definitiva)
    {
        TentativasEmissao++;
        ErroEmissao = TextoHelper.Truncar(erro, TamanhoErro);
        ErroEmissaoDefinitivo = definitiva;
    }

    /// <summary>Depois que algo mudou (pagador com CPF, conta vinculada), a emissão recusada pode tentar de novo.</summary>
    public void LiberarNovaTentativa()
    {
        ErroEmissaoDefinitivo = false;
        ErroEmissao = null;
    }

    public void MarcarConferida(DateTime agora) => ConferidaEm = agora;

    /// <summary>Emitida que passou do vencimento sem pagamento. Com divergência aberta, nada muda sozinho (CA-9).</summary>
    /// <returns>Se mudou.</returns>
    public bool Vencer(DateOnly hoje)
    {
        if (Situacao != SituacaoCobranca.Emitida || hoje <= Vencimento || ComSinal) return false;
        Situacao = SituacaoCobranca.Vencida;
        return true;
    }

    /// <summary>
    /// Um pagamento confirmado chegou (webhook, varredura ou revalidação do envio). Compara o valor <b>original</b> da cobrança
    /// na Asaas: juros e multa de atraso não viram divergência. Valor diferente só sinaliza (CA-9).
    /// </summary>
    public ResultadoPagamento RegistrarPagamento(decimal valorOriginal, decimal valorPago, DateOnly pagaEm, MeioPagamento meio, DateTime agora)
    {
        if (Quitada) return ResultadoPagamento.JaPaga;
        if (Encerrada)
        {
            Sinalizar(SinalCobranca.PagaDepoisDeEncerrada,
                $"Entrou {Dinheiro(valorPago)} em {pagaEm:dd/MM/yyyy}, mas a cobrança estava {Rotulo(Situacao).ToLowerInvariant()}.", agora);
            return ResultadoPagamento.DepoisDeEncerrada;
        }
        if (valorOriginal != Valor)
        {
            // O que entrou fica registrado (para a gestão aceitar, se for o caso), mas a situação não muda.
            ValorPago = valorPago;
            PagaEm = pagaEm;
            Meio = meio;
            Sinalizar(SinalCobranca.ValorDivergente,
                $"Entrou {Dinheiro(valorPago)} (valor original {Dinheiro(valorOriginal)}) para uma mensalidade de {Dinheiro(Valor)}.", agora);
            return ResultadoPagamento.Divergente;
        }

        Quitar(valorPago, pagaEm, meio, agora);
        return ResultadoPagamento.Conciliada;
    }

    /// <summary>A gestão recebeu por fora (dinheiro na secretaria), com autoria (P18).</summary>
    public void ReceberPorFora(DateOnly data, MeioPagamento meio, string motivo, DateTime agora, Guid? autorId, string autorNome)
    {
        if (!EmAberto && Situacao != SituacaoCobranca.Prevista)
            throw new InvalidOperationException($"Cobrança {Rotulo(Situacao).ToLowerInvariant()} não recebe pagamento.");
        if (string.IsNullOrWhiteSpace(motivo)) throw new ArgumentException("O recebimento por fora exige uma observação.", nameof(motivo));
        Quitar(Valor, data, meio, agora);
        Motivo = TextoHelper.Truncar(motivo, TamanhoMotivo);
        EncerradaEm = agora;
        EncerradaPor_ID = autorId;
        EncerradaPorNome = TextoHelper.Truncar(autorNome, TamanhoNome);
    }

    /// <summary>A divergência de valor pode ser resolvida aceitando o que entrou como o pagamento da mensalidade.</summary>
    public static bool AceitaPagamentoDivergente(SituacaoCobranca situacao, SinalCobranca sinal, decimal? valorPago, DateOnly? pagaEm) =>
        sinal == SinalCobranca.ValorDivergente && situacao is SituacaoCobranca.Prevista or SituacaoCobranca.Emitida or SituacaoCobranca.Vencida
        && valorPago is not null && pagaEm is not null;

    public bool PodeAceitarPagamentoDivergente => AceitaPagamentoDivergente(Situacao, Sinal, ValorPago, PagaEm);

    /// <summary>Divergência resolvida aceitando o que entrou como pagamento da mensalidade.</summary>
    public void AceitarPagamentoDivergente(DateTime agora)
    {
        if (!PodeAceitarPagamentoDivergente) throw new InvalidOperationException("Não há pagamento divergente para aceitar.");
        Quitar(ValorPago!.Value, PagaEm!.Value, Meio ?? MeioPagamento.Indefinido, agora);
    }

    /// <summary>
    /// Paga ou paga em atraso pela data em que a família pagou: se o aviso da Asaas se perdeu e a rotina já tinha marcado a
    /// cobrança como vencida, quem pagou no prazo continua "paga".
    /// </summary>
    private void Quitar(decimal valorPago, DateOnly pagaEm, MeioPagamento meio, DateTime agora)
    {
        Situacao = pagaEm > Vencimento ? SituacaoCobranca.PagaEmAtraso : SituacaoCobranca.Paga;
        ValorPago = valorPago;
        PagaEm = pagaEm;
        Meio = meio;
        ConciliadaEm = agora;
    }

    /// <summary>
    /// Cancelar ou isentar vale para a que ainda não foi paga e para a paga cujo dinheiro voltou para a família (estorno ou
    /// contestação no cartão): esta só pela fila de divergências, com a explicação da gestão.
    /// </summary>
    public static bool PodeEncerrar(SituacaoCobranca situacao, SinalCobranca sinal) =>
        situacao is SituacaoCobranca.Prevista or SituacaoCobranca.Emitida or SituacaoCobranca.Vencida
        || (situacao is SituacaoCobranca.Paga or SituacaoCobranca.PagaEmAtraso && sinal is SinalCobranca.Estorno or SinalCobranca.Chargeback);

    public bool PodeSerEncerrada => PodeEncerrar(Situacao, Sinal);

    /// <summary>Cancelar ou isentar, com motivo e autor (ação sensível, spec §7).</summary>
    /// <returns>
    /// Se a cobrança precisa ser excluída na Asaas: a emitida e ainda não paga. A paga e estornada fica lá como está (a
    /// Asaas não exclui pagamento recebido).
    /// </returns>
    public bool Encerrar(SituacaoCobranca para, string motivo, DateTime agora, Guid? autorId, string autorNome)
    {
        if (para is not (SituacaoCobranca.Cancelada or SituacaoCobranca.Isenta))
            throw new ArgumentException("Encerrar é cancelar ou isentar.", nameof(para));
        if (!PodeSerEncerrada)
            throw new InvalidOperationException($"Cobrança {Rotulo(Situacao).ToLowerInvariant()} não pode ser {Rotulo(para).ToLowerInvariant()}.");
        if (string.IsNullOrWhiteSpace(motivo)) throw new ArgumentException("Cancelar ou isentar exige motivo.", nameof(motivo));
        var excluirNaAsaas = IdAsaas is not null && !Quitada;
        Situacao = para;
        Motivo = TextoHelper.Truncar(motivo, TamanhoMotivo);
        EncerradaEm = agora;
        EncerradaPor_ID = autorId;
        EncerradaPorNome = TextoHelper.Truncar(autorNome, TamanhoNome);
        return excluirNaAsaas;
    }

    public void RegistrarEtapa(string etapa, DateOnly data)
    {
        UltimaEtapa = TextoHelper.Truncar(etapa, TamanhoEtapa);
        UltimaEtapaData = data;
    }

    public void Sinalizar(SinalCobranca sinal, string detalhe, DateTime agora)
    {
        if (sinal == SinalCobranca.Nenhum) throw new ArgumentException("Sinal vazio.", nameof(sinal));
        if (Sinal == sinal && SinalResolvidoEm is null && SinalDetalhe == detalhe) return;
        Sinal = sinal;
        SinalDetalhe = TextoHelper.Truncar(detalhe, TamanhoMotivo);
        SinalEm = agora;
        SinalResolucao = null;
        SinalResolvidoEm = null;
        SinalResolvidoPorNome = null;
    }

    public void ResolverSinal(string resolucao, DateTime agora, string autorNome)
    {
        if (!ComSinal) throw new InvalidOperationException("Não há divergência para resolver.");
        if (string.IsNullOrWhiteSpace(resolucao)) throw new ArgumentException("A resolução exige uma explicação.", nameof(resolucao));
        Sinal = SinalCobranca.Nenhum;
        SinalResolucao = TextoHelper.Truncar(resolucao, TamanhoMotivo);
        SinalResolvidoEm = agora;
        SinalResolvidoPorNome = TextoHelper.Truncar(autorNome, TamanhoNome);
    }

    public static string Dinheiro(decimal valor) => valor.ToString("C", PtBr);

    /// <summary>"outubro/2026".</summary>
    public static string RotuloCompetencia(DateOnly competencia) => competencia.ToString("MMMM'/'yyyy", PtBr);

    public static string Rotulo(SituacaoCobranca situacao) => situacao switch
    {
        SituacaoCobranca.Prevista => "Prevista",
        SituacaoCobranca.Emitida => "Emitida",
        SituacaoCobranca.Paga => "Paga",
        SituacaoCobranca.Vencida => "Vencida",
        SituacaoCobranca.PagaEmAtraso => "Paga em atraso",
        SituacaoCobranca.Cancelada => "Cancelada",
        SituacaoCobranca.Isenta => "Isenta",
        _ => situacao.ToString()
    };

    public static string Rotulo(SinalCobranca sinal) => sinal switch
    {
        SinalCobranca.ValorDivergente => "Valor divergente",
        SinalCobranca.Estorno => "Estorno",
        SinalCobranca.Chargeback => "Contestação no cartão",
        SinalCobranca.ExcluidaNaAsaas => "Excluída na Asaas",
        SinalCobranca.PagaDepoisDeEncerrada => "Paga depois de encerrada",
        SinalCobranca.ContaAnterior => "Conta Asaas anterior",
        _ => "Sem divergência"
    };

    public static string Rotulo(MeioPagamento meio) => meio switch
    {
        MeioPagamento.Pix => "Pix",
        MeioPagamento.Boleto => "Boleto",
        MeioPagamento.Cartao => "Cartão",
        MeioPagamento.Dinheiro => "Dinheiro",
        _ => "Outro"
    };
}

/// <summary>
/// O responsável pagador como cliente na conta Asaas da escolinha (toda cobrança exige um cliente). Um por responsável e
/// conta: trocar a conta cria o cliente de novo na conta nova. O cliente nasce com as notificações da Asaas desligadas:
/// só o motor da escolinha fala com a família (R3).
/// </summary>
public class ClienteAsaas : IEntidadeDaEscolinha
{
    public const int TamanhoId = 60;

    public Guid ClienteAsaas_ID { get; set; }
    public Guid Escolinha_ID { get; set; }
    public Guid Responsavel_ID { get; set; }

    /// <summary><c>ContaAsaasEscolinha.Identificador</c> da conta em que o cliente existe.</summary>
    public Guid Conta { get; set; }

    /// <summary>Id do cliente na Asaas (<c>cus_...</c>).</summary>
    public string IdAsaas { get; set; } = string.Empty;

    public DateTime CriadoEm { get; set; }
}
