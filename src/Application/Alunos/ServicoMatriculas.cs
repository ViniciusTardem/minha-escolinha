using System.Globalization;
using FluentValidation;
using MinhaEscolinha.Application.Acesso;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Configuracao;
using MinhaEscolinha.Application.Turmas;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Domain.Alunos;

namespace MinhaEscolinha.Application.Alunos;

/// <summary>
/// Matrícula em turma e em mais de uma turma (BL-M03-04): uma por turma, com mensalidade e datas próprias. Mexer em
/// matrícula mexe na cobrança (S4), então é do gestor; mudar o valor é ação sensível registrada (spec §7).
/// </summary>
public class ServicoMatriculas(
    IAlunosRepositorio repositorio,
    ITurmasRepositorio turmas,
    IAutorizacao autorizacao,
    ServicoAlunos alunos,
    Financeiro.EfeitosFinanceirosDoCadastro financeiro,
    Eventos.EfeitosConvocacaoDoCadastro convocacoes,
    RegistroDeAlteracoes registro,
    IRelogioBrasil relogio,
    IValidator<NovaMatriculaRequisicao> validadorNova,
    IValidator<EncerrarMatriculaRequisicao> validadorEncerrar,
    IValidator<MensalidadeMatriculaRequisicao> validadorMensalidade)
{
    private static readonly CultureInfo PtBr = new("pt-BR");

    public async Task<ResultadoOperacao> NovaAsync(NovaMatriculaRequisicao r, CancellationToken ct = default)
    {
        if (!autorizacao.Pode(Recurso.FinanceiroEscolinha, Operacao.Alterar)) return ResultadoOperacao.Erro("Só o gestor matricula.");
        var validacao = await validadorNova.ValidateAsync(r, ct);
        if (!validacao.IsValid) return ResultadoOperacao.Invalido(validacao);
        if (await repositorio.ObterAlunoAsync(r.AlunoId, ct) is not { } aluno) return ResultadoOperacao.Erro("Aluno não encontrado.");
        if (aluno.Situacao == SituacaoAluno.Desistente)
            return ResultadoOperacao.Erro("O aluno está desligado: use Rematricular (ela devolve a ativo com a nova data).");
        if (await turmas.ObterTurmaAsync(r.TurmaId!.Value, ct) is not { Ativa: true } turma)
            return ResultadoOperacao.Erro("Turma não encontrada ou encerrada.", nameof(r.TurmaId));
        if ((await repositorio.MatriculasDoAlunoAsync(aluno.Aluno_ID, ct)).Any(m => m.Ativa && m.Turma_ID == turma.Turma_ID))
            return ResultadoOperacao.Erro($"{aluno.Nome} já tem matrícula ativa em {turma.Nome}.", nameof(r.TurmaId));
        if (r.DataInicio!.Value < aluno.DataMatricula)
            return ResultadoOperacao.Erro($"A matrícula não pode começar antes da entrada na escolinha ({aluno.DataMatricula:dd/MM/yyyy}).",
                nameof(r.DataInicio));

        var mensalidade = r.Mensalidade ?? turma.Mensalidade;
        repositorio.AdicionarMatricula(new Matricula
        {
            Matricula_ID = Guid.NewGuid(), Aluno_ID = aluno.Aluno_ID, Turma_ID = turma.Turma_ID, DataInicio = r.DataInicio.Value, Mensalidade = mensalidade
        });
        registro.Registrar(ServicoAlunos.Entidade, aluno.Aluno_ID, "Matrícula", null,
            $"Nova matrícula em {turma.Nome} desde {r.DataInicio:dd/MM/yyyy}, mensalidade {ServicoAlunos.Moeda(mensalidade)}.");
        var aviso = await alunos.AvisoCapacidadeAsync(turma, 1, ct);
        await repositorio.SalvarAsync(ct);
        financeiro.CadastroMudou();
        return ResultadoOperacao.Ok($"Matrícula de {aluno.Nome} em {turma.Nome} registrada." + (aviso is null ? string.Empty : " " + aviso));
    }

    /// <summary>Encerra uma matrícula (mudou de turma, parou a modalidade). Não muda a situação do aluno: nenhuma transição é automática.</summary>
    public async Task<ResultadoOperacao> EncerrarAsync(EncerrarMatriculaRequisicao r, CancellationToken ct = default)
    {
        if (!autorizacao.Pode(Recurso.FinanceiroEscolinha, Operacao.Alterar)) return ResultadoOperacao.Erro("Só o gestor encerra matrícula.");
        var validacao = await validadorEncerrar.ValidateAsync(r, ct);
        if (!validacao.IsValid) return ResultadoOperacao.Invalido(validacao);
        if (await repositorio.ObterMatriculaAsync(r.MatriculaId, ct) is not { Ativa: true } matricula)
            return ResultadoOperacao.Erro("Matrícula não encontrada ou já encerrada.");
        var hoje = DateOnly.FromDateTime(relogio.Hoje());
        var data = r.Data!.Value;
        if (data > hoje) return ResultadoOperacao.Erro("O último dia não pode ser futuro.", nameof(r.Data));
        if (data < matricula.DataInicio)
            return ResultadoOperacao.Erro($"A matrícula começou em {matricula.DataInicio:dd/MM/yyyy}.", nameof(r.Data));

        matricula.Encerrar(data, r.Motivo!);
        var canceladas = await financeiro.EncerrarRecorrenciaAsync([matricula.Matricula_ID], data, $"matrícula encerrada ({r.Motivo!.Trim()})", ct);
        // Sai das convocações por esta turma nos jogos depois do último dia (CA-3).
        var retiradas = await convocacoes.RetirarAlunoAsync(matricula.Aluno_ID, matricula.Turma_ID, data.AddDays(1),
            $"Matrícula encerrada em {data:dd/MM/yyyy}.", ct);
        registro.Registrar(ServicoAlunos.Entidade, matricula.Aluno_ID, "Matrícula", $"{matricula.Turma?.Nome} (ativa)",
            $"Encerrada em {data:dd/MM/yyyy}" + (canceladas > 0 ? $"; {canceladas} mensalidade(s) prevista(s) cancelada(s)" : string.Empty) +
            (retiradas > 0 ? $"; {retiradas} convocação(ões) de jogo retirada(s)" : string.Empty), r.Motivo);

        var restantes = (await repositorio.MatriculasDoAlunoAsync(matricula.Aluno_ID, ct)).Count(m => m.Ativa && m.Matricula_ID != matricula.Matricula_ID);
        await repositorio.SalvarAsync(ct);
        financeiro.CadastroMudou();
        return ResultadoOperacao.Ok($"Matrícula em {matricula.Turma?.Nome} encerrada; a vaga foi liberada." +
                                    (canceladas > 0 ? $" {canceladas} mensalidade(s) prevista(s) cancelada(s)." : string.Empty) +
                                    (retiradas > 0 ? $" {retiradas} convocação(ões) de jogo retirada(s)." : string.Empty) +
                                    (restantes == 0 ? " Não sobrou matrícula ativa: se a saída é da escolinha, use Desligar na ficha (data e motivo)." : string.Empty));
    }

    public async Task<MensalidadeMatriculaRequisicao?> MensalidadeParaEdicaoAsync(Guid matriculaId, CancellationToken ct = default) =>
        autorizacao.Pode(Recurso.FinanceiroEscolinha, Operacao.Alterar) && await repositorio.ObterMatriculaAsync(matriculaId, ct) is { Ativa: true } m
            ? new MensalidadeMatriculaRequisicao { MatriculaId = m.Matricula_ID, Mensalidade = m.Mensalidade }
            : null;

    /// <summary>Valor próprio da matrícula (desconto, bolsa): ação sensível com autor, data e valor anterior (spec §7).</summary>
    public async Task<ResultadoOperacao> AlterarMensalidadeAsync(MensalidadeMatriculaRequisicao r, CancellationToken ct = default)
    {
        if (!autorizacao.Pode(Recurso.FinanceiroEscolinha, Operacao.Alterar)) return ResultadoOperacao.Erro("Só o gestor muda a mensalidade.");
        var validacao = await validadorMensalidade.ValidateAsync(r, ct);
        if (!validacao.IsValid) return ResultadoOperacao.Invalido(validacao);
        if (await repositorio.ObterMatriculaAsync(r.MatriculaId, ct) is not { Ativa: true } matricula)
            return ResultadoOperacao.Erro("Matrícula não encontrada ou encerrada.");

        var antes = matricula.Mensalidade;
        if (antes == r.Mensalidade!.Value) return ResultadoOperacao.Ok("Nada mudou.");
        matricula.Mensalidade = r.Mensalidade.Value;
        registro.Registrar(ServicoAlunos.Entidade, matricula.Aluno_ID, "Mensalidade",
            $"{matricula.Turma?.Nome}: {ServicoAlunos.Moeda(antes)}", $"{matricula.Turma?.Nome}: {ServicoAlunos.Moeda(matricula.Mensalidade)}",
            Cadastros.Vazio(r.Motivo));
        await repositorio.SalvarAsync(ct);
        financeiro.CadastroMudou();
        return ResultadoOperacao.Ok($"Mensalidade em {matricula.Turma?.Nome} passou de {ServicoAlunos.Moeda(antes)} para " +
                                    $"{ServicoAlunos.Moeda(matricula.Mensalidade)} (vale para as próximas cobranças).");
    }

    public async Task<(Matricula Matricula, string Aluno)?> ObterAsync(Guid matriculaId, CancellationToken ct = default)
    {
        if (!autorizacao.Pode(Recurso.FinanceiroEscolinha, Operacao.Alterar)) return null;
        if (await repositorio.ObterMatriculaAsync(matriculaId, ct) is not { } m) return null;
        var aluno = await repositorio.ObterAlunoAsync(m.Aluno_ID, ct);
        return (m, aluno?.Nome ?? "—");
    }
}
