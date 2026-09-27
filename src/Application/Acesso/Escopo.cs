using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Domain.Acesso;

namespace MinhaEscolinha.Application.Acesso;

/// <summary>
/// As perguntas de escopo feitas ao banco (na escolinha do contexto). "Professor" aqui é a pessoa logada ligada a uma
/// ficha de professor da escolinha (<c>Professor.Pessoa_ID</c>).
/// </summary>
public interface IConsultasEscopo
{
    Task<bool> ProfessorDaTurmaAsync(Guid pessoaId, Guid turmaId, CancellationToken ct = default);

    /// <summary>O aluno tem matrícula ativa numa turma da pessoa.</summary>
    Task<bool> ProfessorDoAlunoAsync(Guid pessoaId, Guid alunoId, CancellationToken ct = default);

    /// <summary>A sessão é da pessoa (professor da sessão ou da turma dela).</summary>
    Task<bool> ProfessorDaSessaoAsync(Guid pessoaId, Guid sessaoId, CancellationToken ct = default);

    Task<bool> ProprioProfessorAsync(Guid pessoaId, Guid professorId, CancellationToken ct = default);

    /// <summary>As turmas (ativas e encerradas) de que a pessoa é o professor responsável.</summary>
    Task<IReadOnlyList<Guid>> TurmasDoProfessorAsync(Guid pessoaId, CancellationToken ct = default);

    /// <summary>A pessoa é responsável pelo aluno, com o vínculo confirmado pelo gestor (BL-M01-06).</summary>
    Task<bool> ResponsavelVerificadoDoAlunoAsync(Guid pessoaId, Guid alunoId, CancellationToken ct = default);

    /// <summary>A ficha de responsável é da pessoa.</summary>
    Task<bool> ProprioResponsavelAsync(Guid pessoaId, Guid responsavelId, CancellationToken ct = default);
}

/// <summary>
/// O escopo próprio de cada papel (BL-M08-05, matriz §7):
/// <list type="bullet">
/// <item><b>professor</b>: as turmas de que é o responsável, os alunos matriculados nelas e as sessões dele;</item>
/// <item><b>responsável</b>: a própria ficha e os filhos com vínculo verificado pelo gestor (S2, BL-M01-06): enquanto o
/// vínculo está a confirmar, o aluno não está no escopo — bloqueio total.</item>
/// </list>
/// </summary>
public sealed class ResolvedorEscopoCadastros(IConsultasEscopo consultas) : IResolvedorEscopo
{
    public Task<bool> NoEscopoAsync(PapelAtuacao papel, AlvoEscopo alvo, Guid pessoaId, CancellationToken ct = default) =>
        papel switch
        {
            PapelAtuacao.Professor => alvo.Tipo switch
            {
                TipoAlvoEscopo.Turma => consultas.ProfessorDaTurmaAsync(pessoaId, alvo.Id, ct),
                TipoAlvoEscopo.Aluno => consultas.ProfessorDoAlunoAsync(pessoaId, alvo.Id, ct),
                TipoAlvoEscopo.Sessao => consultas.ProfessorDaSessaoAsync(pessoaId, alvo.Id, ct),
                TipoAlvoEscopo.Professor => consultas.ProprioProfessorAsync(pessoaId, alvo.Id, ct),
                _ => Task.FromResult(false)
            },
            PapelAtuacao.Responsavel => alvo.Tipo switch
            {
                TipoAlvoEscopo.Aluno => consultas.ResponsavelVerificadoDoAlunoAsync(pessoaId, alvo.Id, ct),
                TipoAlvoEscopo.Responsavel => consultas.ProprioResponsavelAsync(pessoaId, alvo.Id, ct),
                _ => Task.FromResult(false)
            },
            _ => Task.FromResult(false)
        };
}

/// <summary>As turmas que uma lista pode mostrar: todas (nível total) ou as do professor.</summary>
public sealed record EscopoTurmas(bool Todas, IReadOnlyCollection<Guid> Turmas)
{
    public static readonly EscopoTurmas Tudo = new(true, []);
    public static readonly EscopoTurmas Nenhuma = new(false, []);

    public bool Inclui(Guid turmaId) => Todas || Turmas.Contains(turmaId);
}

/// <summary>
/// A camada "dado" do motor de permissões nas listas, buscas e exportações: quem tem nível parcial vê só o escopo próprio.
/// Toda consulta de lista de turma, aluno ou sessão passa por aqui (BL-M08-05).
/// </summary>
public interface IEscopoDeLeitura
{
    Task<EscopoTurmas> TurmasAsync(Recurso recurso, CancellationToken ct = default);
}

public sealed class EscopoDeLeitura(
    IAutorizacao autorizacao, IContextoEscolinha contexto, IUsuarioAtual usuarioAtual, IConsultasEscopo consultas) : IEscopoDeLeitura
{
    private IReadOnlyList<Guid>? _turmasDoProfessor;

    public async Task<EscopoTurmas> TurmasAsync(Recurso recurso, CancellationToken ct = default)
    {
        if (autorizacao.Nivel(recurso) == NivelAcesso.Total) return EscopoTurmas.Tudo;
        if (usuarioAtual.Id is not { } pessoaId
            || !contexto.Papeis.Contains(PapelAtuacao.Professor)
            || MatrizPermissoes.Nivel(recurso, PapelAtuacao.Professor) == NivelAcesso.Nenhum)
            return EscopoTurmas.Nenhuma;

        _turmasDoProfessor ??= await consultas.TurmasDoProfessorAsync(pessoaId, ct);
        return new EscopoTurmas(false, _turmasDoProfessor);
    }
}
