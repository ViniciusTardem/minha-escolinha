using MinhaEscolinha.Application.Acesso;
using MinhaEscolinha.Application.Alunos;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Configuracao;
using MinhaEscolinha.Application.Mensageria;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Domain.Presencas;

namespace MinhaEscolinha.Application.Presencas;

/// <summary>
/// A presença vista de fora da quadra: a aba da ficha do aluno e o risco de evasão (BL-M05-11).
/// <list type="bullet">
/// <item>o professor vê só as presenças das turmas dele (o escopo vale também para o que vem junto, D41);</item>
/// <item>o motivo de falta por saúde é dado sensível (R5, BL-M05-13): só a gestão, o professor da turma e a família veem;</item>
/// <item>os alertas de faltas, com para quem saíram e como estão, são da gestão (CA-6), como o log de mensagens.</item>
/// </list>
/// </summary>
public class ServicoPresenca(
    IPresencaRepositorio repositorio,
    IMensageriaRepositorio mensageria,
    IAutorizacao autorizacao,
    IEscopoDeLeitura escopo,
    PoliticaSaude saude,
    IParametros parametros,
    IRelogioBrasil relogio) : IRiscoEvasao
{
    public const int PresencasNaFicha = 40;
    public const int AlertasNaFicha = 20;

    public async Task<PresencaDoAluno?> DoAlunoAsync(Guid alunoId, CancellationToken ct = default)
    {
        if (!await autorizacao.PodeAsync(Recurso.RegistroPresenca, Operacao.Ler, new AlvoEscopo(TipoAlvoEscopo.Aluno, alunoId), ct))
            return null;

        var p = (await parametros.ObterAsync(ct)).Presenca;
        var agora = relogio.Agora();
        var alcance = await escopo.TurmasAsync(Recurso.RegistroPresenca, ct);
        var acessoSaude = await saude.AcessoAsync(alunoId, ct);

        var situacoes = (await repositorio.SituacoesDoAlunoAsync(alunoId, agora.AddDays(-p.JanelaRiscoDias), ct))
            .Where(s => alcance.Inclui(s.TurmaId))
            .ToList();
        var f = CalculadoraFrequencia.Calcular(situacoes.Select(s => s.Situacao), p.FaltaJustificadaContaParaAlerta);
        var minimo = CalculadoraFrequencia.MinimoDoAluno(situacoes.Select(s => s.Meta), p.FrequenciaMinimaPercentual, p.UsarMetaDaTurma);
        var frequencia = new FrequenciaDoAluno(f.Presentes, f.Consideradas, f.Percentual, p.JanelaRiscoDias, minimo,
            CalculadoraFrequencia.EmRisco(f, minimo, p.MinimoSessoesRisco));

        var presencas = (await repositorio.PresencasDoAlunoAsync(alunoId, PresencasNaFicha, ct))
            .Where(r => alcance.Inclui(r.TurmaId))
            .Select(r =>
            {
                var visivel = !r.JustificativaSaude || acessoSaude == AcessoSaude.Completo
                                                    || (acessoSaude == AcessoSaude.Treino && alcance.Inclui(r.TurmaId));
                return r with { MotivoVisivel = visivel, Justificativa = visivel ? r.Justificativa : null };
            })
            .ToList();

        IReadOnlyList<AlertaNaFicha>? alertas = null;
        if (autorizacao.Pode(Recurso.ConfiguracaoEscolinha, Operacao.Ler))
        {
            var lista = await repositorio.AlertasDoAlunoAsync(alunoId, AlertasNaFicha, ct);
            var mensagens = (await mensageria.MensagensDasOrigensAsync(EmissorGatilhos.OrigemAlertaFaltas,
                    lista.Select(a => a.Alerta.AlertaFaltas_ID).ToList(), ct))
                .ToLookup(m => m.Origem_ID);
            alertas = lista
                .Select(a => new AlertaNaFicha(a.Alerta.AlertaFaltas_ID, a.Alerta.AvaliadoEm, a.Turma, a.Alerta.Descricao, a.Alerta.Situacao,
                    a.Alerta.Motivo,
                    mensagens[a.Alerta.AlertaFaltas_ID]
                        .Select(m => new DestinoDoAlerta(m.Mensagem_ID, m.DestinatarioNome, m.Canal, m.Situacao, m.EnviadaEm))
                        .ToList()))
                .ToList();
        }

        return new PresencaDoAluno(frequencia, presencas, alertas);
    }

    /// <summary>
    /// Os alunos (em trial ou ativos) com a frequência da janela abaixo do mínimo, do menor para o maior. Sinal na tela,
    /// sem mensagem (R9): a decisão é de quem gere.
    /// </summary>
    public async Task<IReadOnlyList<RiscoEvasao>> AlunosEmRiscoAsync(CancellationToken ct = default)
    {
        if (!autorizacao.Pode(Recurso.RegistroPresenca, Operacao.Ler)) return [];
        var alcance = await escopo.TurmasAsync(Recurso.RegistroPresenca, ct);
        if (!alcance.Todas && alcance.Turmas.Count == 0) return [];

        var p = (await parametros.ObterAsync(ct)).Presenca;
        var linhas = await repositorio.SituacoesDesdeAsync(relogio.Agora().AddDays(-p.JanelaRiscoDias), alcance, ct);
        return linhas
            .GroupBy(l => (l.AlunoId, l.Nome))
            .Select(g => (Aluno: g.Key, Frequencia: CalculadoraFrequencia.Calcular(g.Select(x => x.Situacao), p.FaltaJustificadaContaParaAlerta),
                Minimo: CalculadoraFrequencia.MinimoDoAluno(g.Select(x => x.Meta), p.FrequenciaMinimaPercentual, p.UsarMetaDaTurma)))
            .Where(x => CalculadoraFrequencia.EmRisco(x.Frequencia, x.Minimo, p.MinimoSessoesRisco))
            .Select(x => new RiscoEvasao(x.Aluno.AlunoId, x.Aluno.Nome, x.Frequencia.Presentes, x.Frequencia.Consideradas,
                x.Frequencia.Percentual!.Value))
            .OrderBy(r => r.Percentual).ThenBy(r => r.Nome)
            .ToList();
    }
}
