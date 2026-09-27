using System.Globalization;
using FluentValidation;
using Microsoft.Extensions.Logging;
using MinhaEscolinha.Application.Acesso;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Configuracao;
using MinhaEscolinha.Application.Equipe;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Domain.Agenda;
using MinhaEscolinha.Domain.Turmas;

namespace MinhaEscolinha.Application.Turmas;

public record ResumoGeracaoSessoes(int Turmas, int Criadas, int Ajustadas, int Retiradas);

/// <summary>
/// Sessões de treino: a geração a partir dos horários (BL-M04-04), no salvar da turma e no recorrente diário, e o
/// cancelamento de uma sessão com motivo (BL-M04-06).
/// </summary>
public class ServicoSessoes(
    ITurmasRepositorio repositorio,
    IAutorizacao autorizacao,
    RegistroDeAlteracoes registro,
    IUsuarioAtual usuarioAtual,
    IRelogioBrasil relogio,
    IValidator<CancelarSessaoRequisicao> validadorCancelar,
    ILogger<ServicoSessoes> logger)
{
    private static readonly CultureInfo PtBr = new("pt-BR");

    /// <summary>Aplica o plano de geração da turma no repositório (quem chama salva, junto com a mudança da turma).</summary>
    public async Task<PlanoSessoes> SincronizarAsync(Turma turma, CancellationToken ct = default)
    {
        var agora = relogio.Agora();
        var existentes = await repositorio.SessoesDaTurmaDesdeAsync(turma.Turma_ID, DateOnly.FromDateTime(agora), ct);
        var plano = GeradorSessoes.Planejar(turma, existentes, agora);

        foreach (var ocorrencia in plano.Criar) repositorio.AdicionarSessao(GeradorSessoes.Nova(turma, ocorrencia));
        foreach (var (sessao, ocorrencia) in plano.Ajustar) GeradorSessoes.Ajustar(sessao, turma, ocorrencia);
        foreach (var sessao in plano.Remover) repositorio.RemoverSessao(sessao);
        return plano;
    }

    /// <summary>
    /// O recorrente diário (na escolinha do contexto): empurra o horizonte de todas as turmas ativas até o fim do mês
    /// seguinte. Idempotente: rodar duas vezes não duplica.
    /// </summary>
    public async Task<ResumoGeracaoSessoes> GerarHorizonteAsync(CancellationToken ct = default)
    {
        var ids = await repositorio.IdsTurmasAtivasAsync(ct);
        int criadas = 0, ajustadas = 0, retiradas = 0;
        foreach (var id in ids)
        {
            if (await repositorio.ObterTurmaAsync(id, ct) is not { } turma) continue;
            var plano = await SincronizarAsync(turma, ct);
            criadas += plano.Criar.Count;
            ajustadas += plano.Ajustar.Count;
            retiradas += plano.Remover.Count;
        }

        await repositorio.SalvarAsync(ct);
        var resumo = new ResumoGeracaoSessoes(ids.Count, criadas, ajustadas, retiradas);
        if (criadas + ajustadas + retiradas > 0)
            logger.LogInformation("Sessões: {Turmas} turma(s), {Criadas} criada(s), {Ajustadas} ajustada(s), {Retiradas} retirada(s).",
                resumo.Turmas, resumo.Criadas, resumo.Ajustadas, resumo.Retiradas);
        return resumo;
    }

    public async Task<SessaoResposta?> ObterAsync(Guid id, CancellationToken ct = default)
    {
        if (!await autorizacao.PodeAsync(Recurso.RegistroPresenca, Operacao.Ler, new AlvoEscopo(TipoAlvoEscopo.Sessao, id), ct))
            return null;
        return await repositorio.ObterSessaoAsync(id, ct) is { } sessao ? Resposta(sessao) : null;
    }

    /// <summary>
    /// Cancela uma sessão com motivo (BL-M04-06). O aviso às famílias da turma é o gatilho "Treino cancelado" (§8.3) e sai
    /// pelo motor único de mensagens (R3), que chega no S2.
    /// </summary>
    public async Task<ResultadoOperacao> CancelarAsync(CancelarSessaoRequisicao r, CancellationToken ct = default)
    {
        var validacao = await validadorCancelar.ValidateAsync(r, ct);
        if (!validacao.IsValid) return ResultadoOperacao.Invalido(validacao);
        if (!await autorizacao.PodeAsync(Recurso.RegistroPresenca, Operacao.Alterar, new AlvoEscopo(TipoAlvoEscopo.Sessao, r.SessaoId), ct)
            || await repositorio.ObterSessaoAsync(r.SessaoId, ct) is not { } sessao)
            return ResultadoOperacao.Erro("Sessão não encontrada.");
        if (!sessao.PodeCancelar) return ResultadoOperacao.Erro($"Esta sessão já está {Rotulo(sessao.Situacao)}.");

        sessao.Cancelar(r.Motivo, relogio.Agora(), usuarioAtual.Id, usuarioAtual.Nome);
        registro.Registrar(ServicoTurmas.Entidade, sessao.Turma_ID, "Sessão cancelada", Descrever(sessao), "Cancelada", r.Motivo);
        await repositorio.SalvarAsync(ct);
        return ResultadoOperacao.Ok($"Treino de {Descrever(sessao)} cancelado.");
    }

    public static SessaoResposta Resposta(SessaoTreino s) => new(
        s.SessaoTreino_ID, s.Turma_ID, s.Turma?.Nome ?? "—", s.Data, s.Inicio, s.Fim, s.Local?.Nome ?? "—", s.Professor?.Nome ?? "—",
        s.Situacao, s.MotivoCancelamento, s.CanceladaEm, s.CanceladaPorNome);

    public static Compromisso Compromisso(SessaoTreino s) => new(
        TipoCompromisso.Treino, s.SessaoTreino_ID, s.Turma?.Nome ?? "Treino", s.Inicio, s.Fim, s.Local_ID, s.Professor_ID, s.Turma_ID,
        s.Situacao == SituacaoSessao.Cancelada);

    private static string Descrever(SessaoTreino s) => $"{s.Inicio.ToString("dd/MM (ddd) HH:mm", PtBr)}–{s.Fim:HH\\:mm}";

    private static string Rotulo(SituacaoSessao situacao) => situacao switch
    {
        SituacaoSessao.Realizada => "realizada",
        SituacaoSessao.Cancelada => "cancelada",
        SituacaoSessao.Remarcada => "remarcada",
        _ => "prevista"
    };
}

/// <summary>
/// Consultas de agenda (BL-M04-11) por turma, professor ou quadra, com filtros de período, modalidade e situação, e a
/// ocupação de agenda (BL-M04-08) que o evento do S5 consulta. Cada sessão vem com os alertas de conflito do professor.
/// </summary>
public class ServicoAgenda(
    ITurmasRepositorio turmas,
    IEquipeRepositorio equipe,
    IConfiguracaoRepositorio configuracao,
    IEscopoDeLeitura escopo,
    IAutorizacao autorizacao,
    IRelogioBrasil relogio) : IOcupacaoAgenda
{
    /// <summary>O período máximo de uma consulta (a tela é por mês).</summary>
    public const int DiasMaximos = 93;

    public async Task<AgendaResposta> ConsultarAsync(FiltroAgenda filtro, CancellationToken ct = default)
    {
        var hoje = DateOnly.FromDateTime(relogio.Hoje());
        var de = filtro.De ?? new DateOnly(hoje.Year, hoje.Month, 1);
        var ate = filtro.Ate ?? de.AddMonths(1).AddDays(-1);
        if (ate < de) (de, ate) = (ate, de);
        if (ate.DayNumber - de.DayNumber > DiasMaximos) ate = de.AddDays(DiasMaximos);

        var alcance = await escopo.TurmasAsync(Recurso.RegistroPresenca, ct);
        var todas = await turmas.ListarSessoesAsync(de, ate, ct);
        var professores = (await equipe.ProfessoresComAgendaAsync(todas.Select(s => s.Professor_ID).Distinct().ToList(), ct))
            .ToDictionary(p => p.Professor_ID);
        var compromissos = todas.Select(ServicoSessoes.Compromisso).ToList();
        var sobrepostos = DetectorConflitos.SobrepostosPorProfessor(compromissos);
        var localOcupado = DetectorConflitos.SobrepostosPorLocal(compromissos);
        var modalidades = await configuracao.ListarModalidadesAsync(ct);
        var nomesModalidade = modalidades.ToDictionary(m => m.Modalidade_ID, m => m.Nome);
        var podeCancelar = autorizacao.Pode(Recurso.RegistroPresenca, Operacao.Alterar);

        var visiveis = todas
            .Where(s => alcance.Inclui(s.Turma_ID))
            .Where(s => filtro.Id is not { } id || filtro.Recorte switch
            {
                RecorteAgenda.Professor => s.Professor_ID == id,
                RecorteAgenda.Local => s.Local_ID == id,
                _ => s.Turma_ID == id
            })
            .Where(s => filtro.ModalidadeId is null || s.Turma?.Modalidade_ID == filtro.ModalidadeId)
            .Where(s => filtro.Situacao is null || s.Situacao == filtro.Situacao)
            .OrderBy(s => s.Inicio).ThenBy(s => s.Turma?.Nome)
            .Select(s => new ItemAgenda(s.SessaoTreino_ID, s.Data, s.Inicio, s.Fim, s.Turma_ID, s.Turma?.Nome ?? "—", s.Turma?.Categoria,
                s.Turma is { } t ? nomesModalidade.GetValueOrDefault(t.Modalidade_ID) ?? "—" : "—",
                s.Local_ID, s.Local?.Nome ?? "—", s.Professor_ID, s.Professor?.Nome ?? "—", s.Situacao, s.MotivoCancelamento,
                Alertas(s, professores.GetValueOrDefault(s.Professor_ID), sobrepostos, localOcupado),
                podeCancelar && s.PodeCancelar))
            .ToList();

        var turmasNoEscopo = (await turmas.ListarTurmasAsync(alcance, incluirEncerradas: false, ct))
            .Select(t => new OpcaoAgenda(t.Turma_ID, t.Nome)).ToList();
        var professoresNoEscopo = alcance.Todas
            ? (await equipe.ListarProfessoresAsync(ct)).Select(p => new OpcaoAgenda(p.Professor_ID, p.Nome)).ToList()
            : todas.Where(s => alcance.Inclui(s.Turma_ID)).Select(s => new OpcaoAgenda(s.Professor_ID, s.Professor?.Nome ?? "—"))
                .DistinctBy(o => o.Id).ToList();
        var locais = (await turmas.ListarLocaisAsync(ct)).Select(l => new OpcaoAgenda(l.Local_ID, l.Nome)).ToList();

        return new AgendaResposta(filtro.Recorte, filtro.Id, de, ate, filtro.ModalidadeId, filtro.Situacao, visiveis,
            turmasNoEscopo, professoresNoEscopo, locais,
            modalidades.Select(m => new OpcaoAgenda(m.Modalidade_ID, m.Nome)).ToList());
    }

    public async Task<IReadOnlyList<Compromisso>> CompromissosAsync(DateTime de, DateTime ate, Guid? professorId, Guid? localId,
        CancellationToken ct = default)
    {
        var sessoes = await turmas.ListarSessoesAsync(DateOnly.FromDateTime(de), DateOnly.FromDateTime(ate), ct);
        return sessoes
            .Where(s => s.Inicio < ate && de < s.Fim)
            .Where(s => professorId is null || s.Professor_ID == professorId)
            .Where(s => localId is null || s.Local_ID == localId)
            .Select(ServicoSessoes.Compromisso)
            .ToList();
    }

    /// <summary>
    /// Os alertas de uma sessão (cancelada não alerta): professor em dois lugares, indisponível ou fora da disponibilidade, e local
    /// com outro treino no mesmo horário (P31). O texto não diz qual é o outro treino: ele pode estar fora do escopo de quem vê.
    /// </summary>
    private static IReadOnlyList<string> Alertas(SessaoTreino s, Domain.Equipe.Professor? professor, IReadOnlySet<Guid> sobrepostos,
        IReadOnlySet<Guid> localOcupado)
    {
        if (s.Situacao == SituacaoSessao.Cancelada) return [];
        var alertas = new List<string>();
        if (sobrepostos.Contains(s.SessaoTreino_ID)) alertas.Add("Professor com outro treino no mesmo horário");
        if (localOcupado.Contains(s.SessaoTreino_ID)) alertas.Add("Local com outro treino no mesmo horário");
        if (professor is null) return alertas;

        foreach (var indisponivel in professor.Indisponibilidades.Where(i => i.Inicio < s.Fim && s.Inicio < i.Fim))
            alertas.Add(string.IsNullOrWhiteSpace(indisponivel.Motivo) ? "Professor indisponível" : $"Professor indisponível: {indisponivel.Motivo}");

        var inicio = TimeOnly.FromDateTime(s.Inicio);
        var fim = TimeOnly.FromDateTime(s.Fim);
        if (professor.TemDisponibilidadeDeclarada && !professor.Disponibilidade.Any(f => f.Cobre(s.Inicio.DayOfWeek, inicio, fim)))
            alertas.Add("Fora da disponibilidade do professor");
        return alertas;
    }
}
