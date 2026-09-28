using Microsoft.Extensions.Logging;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Configuracao;
using MinhaEscolinha.Application.Integracao.Asaas;
using MinhaEscolinha.Application.Mensageria;
using MinhaEscolinha.Domain.Comum;
using MinhaEscolinha.Domain.Financeiro;

namespace MinhaEscolinha.Application.Financeiro;

public sealed record ResumoRotinaCobrancas(int Criadas, int Ajustadas, int Retiradas, int Vencidas, int Avisos, int ParaEmitir)
{
    public override string ToString() =>
        $"{Criadas} prevista(s) nova(s), {Ajustadas} ajustada(s), {Retiradas} retirada(s), {Vencidas} vencida(s), " +
        $"{Avisos} aviso(s) da régua, {ParaEmitir} para emitir";
}

/// <summary>
/// A rotina diária das mensalidades de uma escolinha (e o mesmo caminho sob demanda, depois de uma confirmação):
/// <list type="number">
/// <item>projeção das previstas pela matrícula (<see cref="PlanejadorCobrancas"/>, BL-M06-01);</item>
/// <item>emitida que passou do vencimento vira vencida (se o aviso da Asaas não chegou antes);</item>
/// <item>régua (<see cref="ReguaCobranca"/>, BL-M06-04): no máximo um degrau por cobrança, pelo motor (R3), na mesma
/// gravação da última etapa (outbox);</item>
/// <item>as prontas para a Asaas viram um job de emissão cada (BL-M06-02);</item>
/// <item>a em aberto sem o Pix do portal (ou com ele vencido) vira um job de busca (BL-M10-06).</item>
/// </list>
/// Os parâmetros são os do momento (regra de ouro 14) e tudo é idempotente: a retentativa não duplica.
/// </summary>
public class RotinaCobrancas(
    IFinanceiroRepositorio repositorio,
    IConfiguracaoRepositorio configuracao,
    IParametros parametros,
    EmissorGatilhos emissor,
    IAgendadorFinanceiro agendador,
    IContextoEscolinha contexto,
    OpcoesAsaas opcoes,
    IRelogioBrasil relogio,
    ILogger<RotinaCobrancas> logger)
{
    /// <summary>As matrículas encerradas há até tantos dias ainda entram na projeção (as previstas delas podem ter de sair).</summary>
    public const int DiasEncerradas = 70;

    public async Task<ResumoRotinaCobrancas> ExecutarAsync(CancellationToken ct = default)
    {
        var p = (await parametros.ObterAsync(ct)).Financeiro;
        var hoje = DateOnly.FromDateTime(relogio.Hoje());

        var plano = await ProjetarAsync(ct);

        var vencidas = 0;
        var avisos = 0;
        var agora = relogio.Agora();
        foreach (var cobranca in await repositorio.EmAbertoAsync(ct))
        {
            if (cobranca.Vencer(hoje)) vencidas++;
            // O Pix do portal que falta ou venceu é buscado de novo (BL-M10-06).
            if (opcoes.Habilitado && contexto.EscolinhaId is { } escolinhaId && cobranca.IdAsaas is not null && !cobranca.ComSinal
                && !cobranca.PixValidoEm(agora))
                agendador.ObterPix(escolinhaId, cobranca.Cobranca_ID);
            if (cobranca.ComSinal || ReguaCobranca.Proxima(cobranca, p, hoje) is not { } etapa) continue;

            var aluno = await repositorio.NomeDoAlunoAsync(cobranca.Aluno_ID, ct);
            avisos += await emissor.MensalidadeAsync(cobranca, etapa.Gatilho, etapa.Tom, etapa.Codigo, aluno, ct);
            cobranca.RegistrarEtapa(etapa.Codigo, etapa.Data);
        }
        await repositorio.SalvarAsync(ct);

        var paraEmitir = await AgendarEmissoesAsync(ct);
        var resumo = new ResumoRotinaCobrancas(plano.Criar.Count, plano.Ajustadas.Count, plano.Remover.Count, vencidas, avisos, paraEmitir);
        logger.LogInformation("Mensalidades da escolinha {Escolinha}: {Resumo}.", contexto.EscolinhaId, resumo);
        return resumo;
    }

    /// <summary>A projeção das previstas, gravada.</summary>
    public async Task<PlanoCobrancas> ProjetarAsync(CancellationToken ct = default)
    {
        var p = (await parametros.ObterAsync(ct)).Financeiro;
        var hoje = DateOnly.FromDateTime(relogio.Hoje());
        var matriculas = await repositorio.MatriculasParaCobrancaAsync(hoje.AddDays(-DiasEncerradas), ct);
        var existentes = await repositorio.CobrancasDesdeAsync(PlanejadorCobrancas.CompetenciaDe(hoje), ct);
        var comHistorico = await repositorio.MatriculasComCobrancaAsync(matriculas.Select(m => m.MatriculaId).ToList(), ct);

        var plano = PlanejadorCobrancas.Planejar(matriculas, existentes, comHistorico, hoje, p.DiaVencimento);
        foreach (var nova in plano.Criar) repositorio.Adicionar(nova);
        foreach (var sobra in plano.Remover) repositorio.Remover(sobra);
        if (!plano.Vazio) await repositorio.SalvarAsync(ct);
        return plano;
    }

    /// <summary>
    /// As previstas que já podem ir para a Asaas viram um job de emissão cada. A impedida (sem conta, sem pagador com CPF)
    /// não vira job: ela aparece no painel com o motivo e sai na primeira rotina depois de resolvida.
    /// </summary>
    public async Task<int> AgendarEmissoesAsync(CancellationToken ct = default)
    {
        if (!opcoes.Habilitado || contexto.EscolinhaId is not { } escolinhaId) return 0;
        if (await configuracao.ObterContaAsaasAsync(ct) is null) return 0;
        var p = (await parametros.ObterAsync(ct)).Financeiro;
        var hoje = DateOnly.FromDateTime(relogio.Hoje());
        var agendadas = 0;
        foreach (var cobranca in await repositorio.PrevistasAteAsync(hoje.AddDays(p.EmissaoDiasAntesVencimento), ct))
        {
            if (!cobranca.ProntaParaEmitir(hoje, p.EmissaoDiasAntesVencimento)) continue;
            if (await repositorio.PagadorAsync(cobranca.Aluno_ID, ct) is not { } pagador || !Cpf.EhValido(Cpf.Normalizar(pagador.Cpf))) continue;
            agendador.Emitir(escolinhaId, cobranca.Cobranca_ID);
            agendadas++;
        }
        return agendadas;
    }
}

/// <summary>
/// Os efeitos do cadastro (S1) na cobrança, chamados por quem muda a matrícula ou a situação do aluno:
/// <list type="bullet">
/// <item>o encerramento da recorrência (BL-M06-14, CA-2): quando a matrícula termina (desligamento ou encerramento), as
/// mensalidades previstas com vencimento depois do fim são canceladas, com o motivo, na mesma gravação. As já emitidas
/// ficam como estão: o débito em aberto que sobrar aparece sinalizado para a gestão decidir (R9), nunca é cancelado sozinho;</item>
/// <item>depois da gravação, a projeção das previstas é refeita num job (matrícula nova, valor novo, pausa, volta).</item>
/// </list>
/// </summary>
public class EfeitosFinanceirosDoCadastro(
    IFinanceiroRepositorio repositorio,
    IAgendadorFinanceiro agendador,
    IContextoEscolinha contexto,
    IRelogioBrasil relogio,
    IUsuarioAtual usuario)
{
    /// <returns>Quantas previstas foram canceladas (quem chama salva).</returns>
    public async Task<int> EncerrarRecorrenciaAsync(IReadOnlyCollection<Guid> matriculas, DateOnly fim, string motivo, CancellationToken ct = default)
    {
        if (matriculas.Count == 0) return 0;
        var agora = relogio.Agora();
        var previstas = await repositorio.PrevistasDasMatriculasAposAsync(matriculas, fim, ct);
        foreach (var cobranca in previstas)
            cobranca.Encerrar(SituacaoCobranca.Cancelada, $"Recorrência encerrada: {motivo}", agora, usuario.Id, usuario.Nome ?? "Sistema");
        return previstas.Count;
    }

    /// <summary>Depois da gravação: a projeção das previstas acompanha o cadastro (job, fora do request).</summary>
    public void CadastroMudou()
    {
        if (contexto.EscolinhaId is { } escolinhaId) agendador.Projetar(escolinhaId);
    }
}
