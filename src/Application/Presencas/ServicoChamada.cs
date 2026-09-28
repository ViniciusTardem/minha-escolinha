using FluentValidation;
using Microsoft.Extensions.Logging;
using MinhaEscolinha.Application.Acesso;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Configuracao;
using MinhaEscolinha.Application.Mensageria;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Domain.Comum;
using MinhaEscolinha.Domain.Configuracao.Parametros;
using MinhaEscolinha.Domain.Mensageria;
using MinhaEscolinha.Domain.Presencas;
using MinhaEscolinha.Domain.Turmas;

namespace MinhaEscolinha.Application.Presencas;

/// <summary>
/// A chamada da sessão de treino (BL-M05-01), na quadra e sem sinal (R10, CA-21). O aparelho guarda cada toque numa fila
/// com um id próprio e manda quando há sinal; aqui cada marcação é aplicada uma vez só:
/// <list type="bullet">
/// <item>o id já recebido é reconhecido e não refaz nada (reenvio atrasado não desfaz correção posterior);</item>
/// <item>o registro da quadra prevalece sobre o que a presença tinha antes (<see cref="RegraMarcacao"/>);</item>
/// <item>a falta que atinge a regra dispara o alerta na mesma gravação (<see cref="AvaliacaoFaltas"/>).</item>
/// </list>
/// </summary>
public class ServicoChamada(
    IPresencaRepositorio repositorio,
    IAutorizacao autorizacao,
    IEscopoDeLeitura escopo,
    IParametros parametros,
    AvaliacaoFaltas avaliacao,
    IUsuarioAtual usuarioAtual,
    IRelogioBrasil relogio,
    IValidator<LoteMarcacoes> validador,
    ILogger<ServicoChamada> logger)
{
    /// <summary>Quantos dias para trás a tela mostra as sessões que ficaram sem chamada.</summary>
    public const int DiasSemChamada = 7;

    public async Task<ChamadaDoDia> HojeAsync(CancellationToken ct = default)
    {
        var hoje = DateOnly.FromDateTime(relogio.Hoje());
        var alcance = await escopo.TurmasAsync(Recurso.RegistroPresenca, ct);
        var sessoes = await repositorio.SessoesAsync(hoje.AddDays(-DiasSemChamada), hoje, alcance, ct);
        return new ChamadaDoDia(hoje,
            sessoes.Where(s => s.Data == hoje).OrderBy(s => s.Inicio).ToList(),
            sessoes.Where(s => s.Data < hoje && s.Situacao == SituacaoSessao.Prevista).OrderByDescending(s => s.Inicio).ToList());
    }

    public async Task<ChamadaDaSessao?> ChamadaAsync(Guid sessaoId, CancellationToken ct = default)
    {
        if (!await PodeMarcarAsync(sessaoId, ct) || await repositorio.ObterSessaoAsync(sessaoId, ct) is not { } sessao) return null;

        var hoje = DateOnly.FromDateTime(relogio.Hoje());
        var presencas = (await repositorio.PresencasDaSessaoAsync(sessaoId, ct)).ToDictionary(p => p.Aluno_ID);
        var alunos = (await repositorio.AlunosDaSessaoAsync(sessao, ct))
            .Where(a => a.Elegivel || presencas.ContainsKey(a.Id))
            .Select(a =>
            {
                presencas.TryGetValue(a.Id, out var p);
                // O motivo de saúde não vai para a página: ela fica guardada no aparelho para funcionar sem sinal.
                return new AlunoNaChamada(a.Id, a.Nome, a.Trial, p?.Situacao, p is { JustificativaSaude: false } ? p.Justificativa : null,
                    p?.JustificativaSaude ?? false);
            })
            .OrderBy(a => a.Nome, StringComparer.Create(new System.Globalization.CultureInfo("pt-BR"), true))
            .ToList();

        var aviso = sessao.Situacao == SituacaoSessao.Cancelada
            ? $"Treino cancelado{(sessao.MotivoCancelamento is { } motivo ? $": {motivo}" : "")}. Não há chamada."
            : sessao.Situacao == SituacaoSessao.Remarcada ? "Treino remarcado: a chamada é na sessão nova, no dia combinado."
            : sessao.Data > hoje ? "A chamada abre no dia do treino." : null;
        return new ChamadaDaSessao(sessao.SessaoTreino_ID, sessao.Turma_ID, sessao.Turma?.Nome ?? "—", sessao.Data, sessao.Inicio, sessao.Fim,
            sessao.Local?.Nome ?? "—", sessao.Professor?.Nome ?? "—", sessao.Situacao, sessao.AceitaChamada(hoje), aviso, alunos);
    }

    /// <summary>Aplica as marcações do aparelho (online ou a fila guardada sem sinal) e avalia o alerta de faltas.</summary>
    public async Task<ResultadoSincronizacao> SincronizarAsync(LoteMarcacoes lote, CancellationToken ct = default)
    {
        var validacao = await validador.ValidateAsync(lote, ct);
        if (!validacao.IsValid) return ResultadoSincronizacao.Invalido(validacao.Errors[0].ErrorMessage);

        var agora = relogio.Agora();
        var hoje = DateOnly.FromDateTime(agora);
        var autorNome = usuarioAtual.Nome ?? "—";
        var resultados = new List<ResultadoDaMarcacao>();
        var recebidas = new HashSet<Guid>(await repositorio.MarcacoesRecebidasAsync(lote.Marcacoes.Select(m => m.Id).Distinct().ToList(), ct));
        var mudancas = new Dictionary<Guid, MudancaPresenca>();

        foreach (var grupo in lote.Marcacoes.GroupBy(m => m.SessaoId))
        {
            var sessao = await PodeMarcarAsync(grupo.Key, ct) ? await repositorio.ObterSessaoAsync(grupo.Key, ct) : null;
            var recusa = sessao is null ? "Sessão não encontrada ou fora do seu acesso."
                : sessao.Situacao == SituacaoSessao.Cancelada ? "O treino foi cancelado: a chamada não vale."
                : sessao.Situacao == SituacaoSessao.Remarcada ? "O treino foi remarcado: marque na sessão nova."
                : sessao.Data > hoje ? "A chamada só vale no dia do treino ou depois."
                : null;
            if (recusa is not null)
            {
                resultados.AddRange(grupo.Select(m => recebidas.Contains(m.Id)
                    ? new ResultadoDaMarcacao(m.Id, ResultadoDaMarcacao.Repetida, null)
                    : new ResultadoDaMarcacao(m.Id, ResultadoDaMarcacao.Recusada, recusa)));
                continue;
            }

            var alunos = (await repositorio.AlunosDaSessaoAsync(sessao!, ct)).ToDictionary(a => a.Id);
            var presencas = (await repositorio.PresencasDaSessaoAsync(sessao!.SessaoTreino_ID, ct)).ToDictionary(p => p.Aluno_ID);
            var maiores = await repositorio.MaioresSequenciasAsync(sessao.SessaoTreino_ID, ct);
            var aplicou = false;

            foreach (var m in grupo.OrderBy(m => m.Dispositivo, StringComparer.Ordinal).ThenBy(m => m.Sequencia))
            {
                if (!recebidas.Add(m.Id))
                {
                    resultados.Add(new ResultadoDaMarcacao(m.Id, ResultadoDaMarcacao.Repetida, null));
                    continue;
                }
                presencas.TryGetValue(m.AlunoId, out var atual);
                if (!alunos.TryGetValue(m.AlunoId, out var aluno) || (!aluno.Elegivel && atual is null))
                {
                    resultados.Add(new ResultadoDaMarcacao(m.Id, ResultadoDaMarcacao.Recusada, "O aluno não está na turma nesta data."));
                    continue;
                }

                var nova = new NovaMarcacao(m.Id, sessao.SessaoTreino_ID, m.AlunoId, m.Situacao, m.Justificativa, m.Saude, m.Dispositivo,
                    m.Sequencia, MomentoNoAparelho(m.MarcadaEm, agora));
                var chave = (m.AlunoId, nova.Dispositivo);
                var decisao = RegraMarcacao.Decidir(nova, maiores.TryGetValue(chave, out var maior) ? maior : null);
                if (decisao.Aplicar)
                {
                    var antes = mudancas.TryGetValue(atual?.Presenca_ID ?? Guid.Empty, out var anterior) ? anterior.Antes : atual?.Situacao;
                    if (atual is null)
                    {
                        atual = Presenca.Nova(sessao, m.AlunoId);
                        repositorio.AdicionarPresenca(atual);
                        presencas[m.AlunoId] = atual;
                    }
                    atual.Aplicar(nova, usuarioAtual.Id, autorNome);
                    maiores[chave] = nova.Sequencia;
                    mudancas[atual.Presenca_ID] = new MudancaPresenca(atual, antes, aluno.Nome, sessao.Turma?.Nome ?? "—");
                    aplicou = true;
                }

                repositorio.AdicionarMarcacao(MarcacaoPresenca.De(nova, decisao.Aplicar ? ResultadoMarcacao.Aplicada : ResultadoMarcacao.Ignorada,
                    decisao.Motivo, agora, usuarioAtual.Id, autorNome));
                resultados.Add(new ResultadoDaMarcacao(m.Id, decisao.Aplicar ? ResultadoDaMarcacao.Aplicada : ResultadoDaMarcacao.Ignorada,
                    decisao.Motivo));
            }

            if (aplicou) sessao.RegistrarChamada();
        }

        // O alerta entra na fila na mesma gravação das presenças (outbox): ou as duas coisas, ou nenhuma.
        var alertas = await avaliacao.AvaliarAsync(mudancas.Values.ToList(), (await parametros.ObterAsync(ct)).Presenca, ct);
        await repositorio.SalvarAsync(ct);

        if (resultados.Any(r => r.Resultado == ResultadoDaMarcacao.Recusada))
            logger.LogWarning("Chamada: {Recusadas} marcação(ões) recusada(s) de {Total}.",
                resultados.Count(r => r.Resultado == ResultadoDaMarcacao.Recusada), resultados.Count);
        return new ResultadoSincronizacao(resultados, alertas);
    }

    private Task<bool> PodeMarcarAsync(Guid sessaoId, CancellationToken ct) =>
        autorizacao.PodeAsync(Recurso.RegistroPresenca, Operacao.Alterar, new AlvoEscopo(TipoAlvoEscopo.Sessao, sessaoId), ct);

    /// <summary>O relógio do aparelho só informa; fora de um intervalo razoável, vale a hora do servidor.</summary>
    private static DateTime MomentoNoAparelho(DateTime? marcadaEm, DateTime agora)
    {
        if (marcadaEm is not { } em) return agora;
        var brasilia = em.Kind == DateTimeKind.Unspecified ? em : DataHoraBrasil.ConverterParaBrasilia(em.ToUniversalTime());
        return brasilia < agora.AddDays(-30) || brasilia > agora.AddMinutes(10) ? agora : brasilia;
    }
}

/// <summary>
/// O alerta de faltas (BL-M05-08) avaliado a cada falta registrada, com os parâmetros do momento (regra de ouro 14):
/// <list type="bullet">
/// <item>a falta que atinge a regra (<see cref="AvaliadorFaltas"/>) cria o alerta e a mensagem aos responsáveis, pelo motor;</item>
/// <item>dentro da janela anti-duplicidade, o alerta fica suprimido e registrado, sem mensagem (BL-M05-09, CA-5);</item>
/// <item>a falta corrigida que desfaz a regra recolhe o que ainda não saiu e anula o alerta que ninguém recebeu (regra de ouro 5).</item>
/// </list>
/// </summary>
public class AvaliacaoFaltas(
    IPresencaRepositorio repositorio,
    IMensageriaRepositorio mensageria,
    EmissorGatilhos emissor,
    IRelogioBrasil relogio)
{
    /// <summary>Quanto das presenças do aluno a regra olha para trás (a sequência não passa disso).</summary>
    public const int DiasHistorico = 400;

    /// <returns>Quantos alertas foram disparados.</returns>
    public async Task<int> AvaliarAsync(IReadOnlyList<MudancaPresenca> mudancas, ParametrosPresenca p, CancellationToken ct = default)
    {
        var agora = relogio.Agora();
        var conta = p.FaltaJustificadaContaParaAlerta;
        var disparados = 0;
        var ultimoDisparo = new Dictionary<Guid, DateTime?>();
        var doLote = mudancas.Select(m => m.Presenca).ToList();

        foreach (var mudanca in mudancas.OrderBy(m => m.Presenca.SessaoInicio))
        {
            var presenca = mudanca.Presenca;
            var contavaAntes = mudanca.Antes is { } antes && AvaliadorFaltas.Conta(antes, conta);
            var contaAgora = AvaliadorFaltas.Conta(presenca.Situacao, conta);
            if (contavaAntes == contaAgora) continue;

            var historico = await PresencasContadasAsync(presenca.Aluno_ID, presenca.Turma_ID, presenca.SessaoInicio, doLote, ct);
            if (contaAgora)
            {
                var atingida = AvaliadorFaltas.Avaliar(p.Regra, p.LimiteFaltas, conta, new PresencaContada(presenca.SessaoInicio, presenca.Situacao),
                    historico);
                if (atingida is null) continue;

                if (!ultimoDisparo.TryGetValue(presenca.Aluno_ID, out var ultimo))
                    ultimo = await repositorio.UltimoAlertaDisparadoAsync(presenca.Aluno_ID, ct);
                var alerta = AlertaFaltas.Novo(presenca, p.Regra, p.LimiteFaltas, conta, atingida, agora, ultimo, p.JanelaAntiDuplicidadeDias);
                repositorio.AdicionarAlerta(alerta);
                if (alerta.Situacao != SituacaoAlertaFaltas.Disparado) continue;

                await emissor.FaltasAtingidasAsync(alerta, mudanca.Aluno, mudanca.Turma, Tom(p.Tom), ct);
                ultimoDisparo[presenca.Aluno_ID] = agora;
                disparados++;
                continue;
            }

            // A falta deixou de contar: os alertas que ela sustentava e já não valem são recolhidos.
            foreach (var alerta in await repositorio.AlertasDisparadosAsync(presenca.Aluno_ID, presenca.Turma_ID, presenca.SessaoInicio.AddDays(-62), ct))
            {
                if (AvaliadorFaltas.AindaVale(alerta, alerta.ContaJustificada, historico)) continue;
                await emissor.CancelarPendentesAsync(EmissorGatilhos.OrigemAlertaFaltas, alerta.AlertaFaltas_ID,
                    "A falta que disparou o alerta foi corrigida antes do envio.", ct);
                if (!await mensageria.AlgumaSaiuDaOrigemAsync(EmissorGatilhos.OrigemAlertaFaltas, alerta.AlertaFaltas_ID, ct))
                    alerta.Anular("A falta que atingiu a regra foi corrigida antes de o aviso sair.", agora);
                ultimoDisparo.Remove(presenca.Aluno_ID);
            }
        }
        return disparados;
    }

    /// <summary>As presenças do aluno na turma como a regra as vê: as gravadas (com as mudanças do lote) e as novas do lote.</summary>
    private async Task<IReadOnlyList<PresencaContada>> PresencasContadasAsync(Guid alunoId, Guid turmaId, DateTime referencia,
        IReadOnlyList<Presenca> doLote, CancellationToken ct)
    {
        var gravadas = await repositorio.PresencasDoAlunoNaTurmaAsync(alunoId, turmaId, referencia.AddDays(-DiasHistorico), ct);
        return gravadas.Concat(doLote.Where(p => p.Aluno_ID == alunoId && p.Turma_ID == turmaId))
            .DistinctBy(p => p.Presenca_ID)
            .Select(p => new PresencaContada(p.SessaoInicio, p.Situacao))
            .ToList();
    }

    private static TomMensagem Tom(string tom) => Enum.TryParse<TomMensagem>(tom, ignoreCase: true, out var valor) ? valor : TomMensagem.Cordial;
}

/// <summary>A mensagem de faltas só sai se o alerta ainda vale: a falta que atingiu a regra pode ter sido corrigida.</summary>
public class ValidadorOrigemAlertaFaltas(IPresencaRepositorio repositorio) : IValidadorOrigemMensagem
{
    public string TipoOrigem => EmissorGatilhos.OrigemAlertaFaltas;

    public async Task<string?> MotivoParaNaoEnviarAsync(Mensagem mensagem, CancellationToken ct = default)
    {
        var alerta = await repositorio.ObterAlertaAsync(mensagem.Origem_ID, ct);
        if (alerta is null) return "O alerta de faltas não existe mais.";
        if (alerta.Situacao != SituacaoAlertaFaltas.Disparado) return "O alerta foi anulado: a falta foi corrigida.";

        var presencas = await repositorio.PresencasDoAlunoNaTurmaAsync(alerta.Aluno_ID, alerta.Turma_ID,
            alerta.SessaoInicio.AddDays(-AvaliacaoFaltas.DiasHistorico), ct);
        return AvaliadorFaltas.AindaVale(alerta, alerta.ContaJustificada, presencas.Select(p => new PresencaContada(p.SessaoInicio, p.Situacao)))
            ? null
            : "A falta que disparou o alerta foi corrigida antes do envio.";
    }
}
