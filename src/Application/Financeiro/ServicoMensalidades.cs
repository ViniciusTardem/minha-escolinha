using System.Globalization;
using FluentValidation;
using MinhaEscolinha.Application.Acesso;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Configuracao;
using MinhaEscolinha.Application.Integracao.Asaas;
using MinhaEscolinha.Application.Mensageria;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Domain.Financeiro;

namespace MinhaEscolinha.Application.Financeiro;

/// <summary>
/// As telas do financeiro da escolinha (BL-M06-03, BL-M06-06): o painel de pagamentos, a aba da ficha, a fila de
/// divergências e as ações da gestão. Tudo exige o financeiro da escolinha, que é só do gestor (§7, BL-M06-24): o
/// professor não vê nem pelo endereço. Cancelar, isentar, receber por fora e resolver divergência são ações sensíveis:
/// ficam no registro de alterações com autor, data e o valor anterior (spec §7).
/// </summary>
public class ServicoMensalidades(
    IFinanceiroRepositorio repositorio,
    IConfiguracaoRepositorio configuracao,
    IAutorizacao autorizacao,
    RotinaCobrancas rotina,
    EmissorGatilhos emissor,
    IAgendadorFinanceiro agendador,
    IContextoEscolinha contexto,
    IParametros parametros,
    RegistroDeAlteracoes registro,
    OpcoesAsaas opcoes,
    IUsuarioAtual usuario,
    IRelogioBrasil relogio,
    IValidator<ConfirmarMensalidadeRequisicao> validadorConfirmar,
    IValidator<EncerrarMensalidadeRequisicao> validadorEncerrar,
    IValidator<ReceberPorForaRequisicao> validadorReceber,
    IValidator<ResolverDivergenciaRequisicao> validadorResolver)
{
    public const string Entidade = nameof(Cobranca);
    public const int CobrancasNaFicha = 24;

    private static readonly CultureInfo PtBr = new("pt-BR");

    public bool PodeVer => autorizacao.Pode(Recurso.FinanceiroEscolinha, Operacao.Ler);
    public bool PodeAlterar => autorizacao.Pode(Recurso.FinanceiroEscolinha, Operacao.Alterar);

    public async Task<PainelMensalidades?> PainelAsync(FiltroMensalidades filtro, CancellationToken ct = default)
    {
        if (!PodeVer) return null;
        var competencia = PlanejadorCobrancas.CompetenciaDe(filtro.Competencia ?? DateOnly.FromDateTime(relogio.Hoje()));
        // Sem mês escolhido: o mês corrente; na entrada no sistema (mês corrente vazio e o seguinte já projetado), o seguinte.
        if (filtro.Competencia is null && !filtro.SoDivergencias
            && (await repositorio.ListarAsync(new FiltroMensalidades { Competencia = competencia }, ct)).Count == 0
            && (await repositorio.ListarAsync(new FiltroMensalidades { Competencia = competencia.AddMonths(1) }, ct)).Count > 0)
            competencia = competencia.AddMonths(1);
        filtro.Competencia = competencia;

        var linhas = await repositorio.ListarAsync(filtro, ct);
        var todasDoMes = filtro.Grupo is null && filtro.TurmaId is null && string.IsNullOrWhiteSpace(filtro.Busca) && !filtro.SoDivergencias
            ? linhas
            : await repositorio.ListarAsync(new FiltroMensalidades { Competencia = competencia }, ct);
        var totais = Enum.GetValues<GrupoMensalidade>()
            .Select(g => new TotalGrupo(g, todasDoMes.Count(l => l.Grupo == g),
                todasDoMes.Where(l => l.Grupo == g).Sum(l => l.Grupo == GrupoMensalidade.Pagas ? l.ValorPago ?? l.Valor : l.Valor)))
            .ToList();

        return new PainelMensalidades(competencia, linhas, totais, await repositorio.ContarDivergenciasAsync(ct),
            await configuracao.ObterContaAsaasAsync(ct) is not null, opcoes.Descricao, await repositorio.TurmasAsync(ct));
    }

    /// <summary>A aba Financeiro da ficha do aluno: <c>null</c> para quem não vê o financeiro (a aba nem aparece).</summary>
    public async Task<IReadOnlyList<LinhaMensalidade>?> DoAlunoAsync(Guid alunoId, CancellationToken ct = default) =>
        PodeVer ? await repositorio.DoAlunoAsync(alunoId, CobrancasNaFicha, ct) : null;

    /// <summary>O cartão do painel inicial (só para quem vê o financeiro).</summary>
    public async Task<ResumoFinanceiro?> ResumoAsync(CancellationToken ct = default)
    {
        if (!PodeVer) return null;
        var p = (await parametros.ObterAsync(ct)).Financeiro;
        return await repositorio.ResumoAsync(DateOnly.FromDateTime(relogio.Hoje()).AddDays(p.EmissaoDiasAntesVencimento), ct);
    }

    public async Task<CobrancaParaAcao?> ParaAcaoAsync(Guid id, CancellationToken ct = default)
    {
        if (!PodeVer || await repositorio.LinhaAsync(id, ct) is not { } linha) return null;
        var aberta = linha.EmAberto || linha.Situacao == SituacaoCobranca.Prevista;
        return new CobrancaParaAcao(linha, aberta, aberta, aberta, linha.AguardandoConfirmacao,
            linha.EmAberto && linha.UrlFatura is not null && linha.Sinal == SinalCobranca.Nenhum);
    }

    /// <summary>Roda agora a rotina das mensalidades (a mesma da madrugada): projeção, vencidas, régua e emissões. Idempotente.</summary>
    public async Task<ResultadoOperacao> AtualizarAsync(CancellationToken ct = default)
    {
        if (!PodeAlterar) return ResultadoOperacao.Erro("Só o gestor mexe nas mensalidades.");
        var r = await rotina.ExecutarAsync(ct);
        return ResultadoOperacao.Ok(r.Criadas + r.Ajustadas + r.Retiradas + r.Vencidas + r.Avisos + r.ParaEmitir == 0
            ? "As mensalidades já estavam em dia."
            : $"{r.Criadas} prevista(s) nova(s), {r.Ajustadas} ajustada(s), {r.Retiradas} retirada(s), {r.Vencidas} vencida(s), " +
              $"{r.Avisos} aviso(s) da régua e {r.ParaEmitir} indo para a Asaas.");
    }

    public async Task<ResultadoOperacao> ConfirmarAsync(ConfirmarMensalidadeRequisicao r, CancellationToken ct = default)
    {
        if (!PodeAlterar) return ResultadoOperacao.Erro("Só o gestor confirma mensalidades.");
        var validacao = await validadorConfirmar.ValidateAsync(r, ct);
        if (!validacao.IsValid) return ResultadoOperacao.Invalido(validacao);
        if (await repositorio.ObterAsync(r.Id, ct) is not { } cobranca) return ResultadoOperacao.Erro("Mensalidade não encontrada.");
        if (!cobranca.AguardandoConfirmacao) return ResultadoOperacao.Erro("Esta mensalidade não espera confirmação.");

        var antes = cobranca.Valor;
        cobranca.Confirmar(r.Valor!.Value, relogio.Agora(), usuario.Id, usuario.Nome ?? "—");
        registro.Registrar(Entidade, cobranca.Cobranca_ID, "Primeira mensalidade confirmada", Cobranca.Dinheiro(antes),
            Cobranca.Dinheiro(cobranca.Valor), $"Competência {Cobranca.RotuloCompetencia(cobranca.Competencia)}.");
        await repositorio.SalvarAsync(ct);
        var emitir = await rotina.AgendarEmissoesAsync(ct);
        return ResultadoOperacao.Ok($"Mensalidade confirmada ({Cobranca.Dinheiro(cobranca.Valor)})." +
                                    (emitir > 0 ? " Já está indo para a Asaas." : " Vai para a Asaas na data de emissão."));
    }

    /// <summary>Confirma várias com o valor projetado (a matrícula) — a entrada no sistema de uma turma inteira.</summary>
    public async Task<ResultadoOperacao> ConfirmarVariasAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        if (!PodeAlterar) return ResultadoOperacao.Erro("Só o gestor confirma mensalidades.");
        if (ids.Count == 0) return ResultadoOperacao.Erro("Marque as mensalidades a confirmar.");
        var confirmadas = 0;
        foreach (var id in ids.Distinct().Take(200))
        {
            if (await repositorio.ObterAsync(id, ct) is not { AguardandoConfirmacao: true } cobranca) continue;
            cobranca.Confirmar(cobranca.Valor, relogio.Agora(), usuario.Id, usuario.Nome ?? "—");
            registro.Registrar(Entidade, cobranca.Cobranca_ID, "Primeira mensalidade confirmada", null, Cobranca.Dinheiro(cobranca.Valor),
                $"Competência {Cobranca.RotuloCompetencia(cobranca.Competencia)} (confirmação em lote).");
            confirmadas++;
        }
        if (confirmadas == 0) return ResultadoOperacao.Erro("Nenhuma das marcadas esperava confirmação.");
        await repositorio.SalvarAsync(ct);
        await rotina.AgendarEmissoesAsync(ct);
        return ResultadoOperacao.Ok($"{confirmadas} mensalidade(s) confirmada(s).");
    }

    /// <summary>Cancelar ou isentar (spec §6): sai da régua na hora; a que já estava na Asaas é excluída lá por um job.</summary>
    public async Task<ResultadoOperacao> EncerrarAsync(EncerrarMensalidadeRequisicao r, CancellationToken ct = default)
    {
        if (!PodeAlterar) return ResultadoOperacao.Erro("Só o gestor cancela ou isenta mensalidades.");
        var validacao = await validadorEncerrar.ValidateAsync(r, ct);
        if (!validacao.IsValid) return ResultadoOperacao.Invalido(validacao);
        if (await repositorio.ObterAsync(r.Id, ct) is not { } cobranca) return ResultadoOperacao.Erro("Mensalidade não encontrada.");
        if (!cobranca.EmAberto && cobranca.Situacao != SituacaoCobranca.Prevista)
            return ResultadoOperacao.Erro($"Mensalidade {Cobranca.Rotulo(cobranca.Situacao).ToLowerInvariant()} não pode mudar.");

        var antes = Cobranca.Rotulo(cobranca.Situacao);
        var naAsaas = cobranca.Encerrar(r.Para, r.Motivo!.Trim(), relogio.Agora(), usuario.Id, usuario.Nome ?? "—");
        await emissor.CancelarPendentesAsync(EmissorGatilhos.OrigemCobranca, cobranca.Cobranca_ID,
            $"Mensalidade {Cobranca.Rotulo(r.Para).ToLowerInvariant()} pela gestão.", ct);
        registro.Registrar(Entidade, cobranca.Cobranca_ID, "Situação", antes,
            $"{Cobranca.Rotulo(r.Para)} ({Cobranca.Dinheiro(cobranca.Valor)}, {Cobranca.RotuloCompetencia(cobranca.Competencia)})", r.Motivo);
        await repositorio.SalvarAsync(ct);
        if (naAsaas && contexto.EscolinhaId is { } escolinhaId) agendador.ExcluirNaAsaas(escolinhaId, cobranca.Cobranca_ID);
        return ResultadoOperacao.Ok($"Mensalidade {Cobranca.Rotulo(r.Para).ToLowerInvariant()}." +
                                    (naAsaas ? " A fatura na Asaas vai ser excluída." : string.Empty));
    }

    /// <summary>Recebido por fora (dinheiro na secretaria, P18): paga com autoria, a régua para e a Asaas é avisada por job.</summary>
    public async Task<ResultadoOperacao> ReceberPorForaAsync(ReceberPorForaRequisicao r, CancellationToken ct = default)
    {
        if (!PodeAlterar) return ResultadoOperacao.Erro("Só o gestor registra recebimentos.");
        var validacao = await validadorReceber.ValidateAsync(r, ct);
        if (!validacao.IsValid) return ResultadoOperacao.Invalido(validacao);
        if (r.Data > DateOnly.FromDateTime(relogio.Hoje()))
            return ResultadoOperacao.Erro("O recebimento não pode ter data futura.", nameof(ReceberPorForaRequisicao.Data));
        if (await repositorio.ObterAsync(r.Id, ct) is not { } cobranca) return ResultadoOperacao.Erro("Mensalidade não encontrada.");
        if (!cobranca.EmAberto && cobranca.Situacao != SituacaoCobranca.Prevista)
            return ResultadoOperacao.Erro($"Mensalidade {Cobranca.Rotulo(cobranca.Situacao).ToLowerInvariant()} não recebe pagamento.");

        var antes = Cobranca.Rotulo(cobranca.Situacao);
        cobranca.ReceberPorFora(r.Data!.Value, r.Meio, r.Observacao!.Trim(), relogio.Agora(), usuario.Id, usuario.Nome ?? "—");
        await emissor.CancelarPendentesAsync(EmissorGatilhos.OrigemCobranca, cobranca.Cobranca_ID, ConciliacaoCobrancas.MotivoReguaParou, ct);
        await emissor.PagamentoConfirmadoAsync(cobranca, await repositorio.NomeDoAlunoAsync(cobranca.Aluno_ID, ct), ct);
        registro.Registrar(Entidade, cobranca.Cobranca_ID, "Situação", antes,
            $"{Cobranca.Rotulo(cobranca.Situacao)} por fora ({Cobranca.Rotulo(r.Meio)}, {r.Data:dd/MM/yyyy}, {Cobranca.Dinheiro(cobranca.Valor)})",
            r.Observacao);
        await repositorio.SalvarAsync(ct);
        if (cobranca.IdAsaas is not null && contexto.EscolinhaId is { } escolinhaId)
            agendador.ReceberEmDinheiroNaAsaas(escolinhaId, cobranca.Cobranca_ID);
        return ResultadoOperacao.Ok("Recebimento registrado. A régua desta mensalidade parou.");
    }

    /// <summary>Manda de novo o link da fatura aos responsáveis financeiros (pelo motor, no gatilho da mensalidade).</summary>
    public async Task<ResultadoOperacao> ReenviarAsync(Guid id, CancellationToken ct = default)
    {
        if (!PodeAlterar) return ResultadoOperacao.Erro("Só o gestor reenvia cobranças.");
        if (await repositorio.ObterAsync(id, ct) is not { } cobranca) return ResultadoOperacao.Erro("Mensalidade não encontrada.");
        if (!cobranca.EmAberto || cobranca.UrlFatura is null || cobranca.ComSinal)
            return ResultadoOperacao.Erro("Só a mensalidade em aberto, com a fatura na Asaas e sem divergência, é reenviada.");

        var agora = relogio.Agora();
        var vencida = DateOnly.FromDateTime(agora) > cobranca.Vencimento;
        var etapa = $"M{agora.ToString("yyMMddHHmm", CultureInfo.InvariantCulture)}";
        var enviadas = await emissor.MensalidadeAsync(cobranca,
            vencida ? Domain.Mensageria.GatilhoMensagem.MensalidadeVencida : Domain.Mensageria.GatilhoMensagem.MensalidadeAVencer,
            Domain.Mensageria.TomMensagem.Cordial, etapa, await repositorio.NomeDoAlunoAsync(cobranca.Aluno_ID, ct), ct);
        if (enviadas == 0) return ResultadoOperacao.Erro("Nenhum responsável financeiro com celular para receber (ou o reenvio já saiu agora há pouco).");
        registro.Registrar(Entidade, cobranca.Cobranca_ID, "Cobrança reenviada", null, $"{enviadas} mensagem(ns) na fila", null);
        await repositorio.SalvarAsync(ct);
        return ResultadoOperacao.Ok($"Cobrança reenviada: {enviadas} mensagem(ns) na fila.");
    }

    /// <summary>
    /// A recusa definitiva da Asaas não se repete sozinha (regra de ouro 8). Depois que a gestão corrige o que foi recusado
    /// (o dado do pagador, por exemplo), a mensalidade volta para a emissão.
    /// </summary>
    public async Task<ResultadoOperacao> TentarDeNovoAsync(Guid id, CancellationToken ct = default)
    {
        if (!PodeAlterar) return ResultadoOperacao.Erro("Só o gestor mexe nas mensalidades.");
        if (await repositorio.ObterAsync(id, ct) is not { Situacao: SituacaoCobranca.Prevista, ErroEmissaoDefinitivo: true } cobranca)
            return ResultadoOperacao.Erro("Só a mensalidade recusada pela Asaas volta para a emissão.");

        var erro = cobranca.ErroEmissao;
        cobranca.LiberarNovaTentativa();
        registro.Registrar(Entidade, cobranca.Cobranca_ID, "Emissão", $"Recusada pela Asaas: {erro}", "Nova tentativa", null);
        await repositorio.SalvarAsync(ct);
        var emitir = await rotina.AgendarEmissoesAsync(ct);
        return ResultadoOperacao.Ok("A mensalidade voltou para a emissão." +
                                    (emitir > 0 ? " Já está indo para a Asaas." : " Vai para a Asaas na data de emissão."));
    }

    /// <summary>A fila de divergências (BL-M06-06): toda resolução é manual, com explicação e autor.</summary>
    public async Task<ResultadoOperacao> ResolverAsync(ResolverDivergenciaRequisicao r, CancellationToken ct = default)
    {
        if (!PodeAlterar) return ResultadoOperacao.Erro("Só o gestor resolve divergências.");
        var validacao = await validadorResolver.ValidateAsync(r, ct);
        if (!validacao.IsValid) return ResultadoOperacao.Invalido(validacao);
        if (await repositorio.ObterAsync(r.Id, ct) is not { ComSinal: true } cobranca)
            return ResultadoOperacao.Erro("Esta mensalidade não tem divergência aberta.");

        var agora = relogio.Agora();
        var autor = usuario.Nome ?? "—";
        var sinal = Cobranca.Rotulo(cobranca.Sinal);
        var explicacao = r.Explicacao!.Trim();
        string resultado;
        switch (r.Resolucao)
        {
            case ResolucaoDivergencia.AceitarComoPaga when cobranca.PodeAceitarPagamentoDivergente:
                cobranca.AceitarPagamentoDivergente(agora);
                resultado = $"{Cobranca.Rotulo(cobranca.Situacao)} com {Cobranca.Dinheiro(cobranca.ValorPago!.Value)}";
                await emissor.PagamentoConfirmadoAsync(cobranca, await repositorio.NomeDoAlunoAsync(cobranca.Aluno_ID, ct), ct);
                break;
            case ResolucaoDivergencia.AceitarComoPaga:
                return ResultadoOperacao.Erro("Não há pagamento divergente para aceitar nesta mensalidade.", nameof(r.Resolucao));
            case ResolucaoDivergencia.Cancelar or ResolucaoDivergencia.Isentar when cobranca.PodeSerEncerrada:
                // Inclusive a paga e estornada (o dinheiro voltou para a família). Na fila, a fatura na Asaas fica como está:
                // recebeu pagamento, foi excluída lá ou é de uma conta anterior.
                var para = r.Resolucao == ResolucaoDivergencia.Cancelar ? SituacaoCobranca.Cancelada : SituacaoCobranca.Isenta;
                cobranca.Encerrar(para, explicacao, agora, usuario.Id, autor);
                resultado = Cobranca.Rotulo(para);
                break;
            case ResolucaoDivergencia.Cancelar or ResolucaoDivergencia.Isentar:
                return ResultadoOperacao.Erro($"Mensalidade {Cobranca.Rotulo(cobranca.Situacao).ToLowerInvariant()} não pode mudar.", nameof(r.Resolucao));
            case ResolucaoDivergencia.CobrarDeNovo when cobranca.PodeCobrarDeNovo:
                // P43: o dinheiro voltou para a família; a mensalidade estornada é cancelada e uma via nova do mesmo mês vai
                // para a Asaas, com o vencimento que a gestão escolheu.
                var hoje = DateOnly.FromDateTime(agora);
                if (r.NovoVencimento!.Value < hoje)
                    return ResultadoOperacao.Erro("O vencimento da cobrança nova não pode ser no passado.", nameof(r.NovoVencimento));
                var nova = Cobranca.CobrarDeNovo(cobranca, r.NovoVencimento.Value, agora, usuario.Id, autor);
                cobranca.Encerrar(SituacaoCobranca.Cancelada, $"Estornada e cobrada de novo: {explicacao}", agora, usuario.Id, autor);
                repositorio.Adicionar(nova);
                registro.Registrar(Entidade, nova.Cobranca_ID, "Cobrança de novo", Cobranca.Dinheiro(cobranca.Valor),
                    $"{Cobranca.Dinheiro(nova.Valor)}, vence {nova.Vencimento:dd/MM/yyyy}", explicacao);
                resultado = $"cancelada e cobrada de novo, com vencimento em {nova.Vencimento:dd/MM/yyyy}";
                break;
            case ResolucaoDivergencia.CobrarDeNovo:
                return ResultadoOperacao.Erro("Só a mensalidade paga e estornada é cobrada de novo.", nameof(r.Resolucao));
            default:
                resultado = "ciência registrada";
                break;
        }

        cobranca.ResolverSinal(explicacao, agora, autor);
        registro.Registrar(Entidade, cobranca.Cobranca_ID, "Divergência", sinal, resultado, explicacao);
        await repositorio.SalvarAsync(ct);
        if (r.Resolucao == ResolucaoDivergencia.CobrarDeNovo) await rotina.AgendarEmissoesAsync(ct);
        return ResultadoOperacao.Ok($"Divergência resolvida: {resultado}.");
    }

    public static string RotuloGrupo(GrupoMensalidade grupo) => grupo switch
    {
        GrupoMensalidade.AConfirmar => "A confirmar",
        GrupoMensalidade.AEmitir => "A emitir",
        GrupoMensalidade.EmAberto => "Em aberto",
        GrupoMensalidade.Vencidas => "Vencidas",
        GrupoMensalidade.Pagas => "Pagas",
        _ => "Canceladas e isentas"
    };

    public static string RotuloMes(DateOnly competencia) => competencia.ToString("MMMM 'de' yyyy", PtBr);
}
