using FluentValidation;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Domain.Agenda;
using MinhaEscolinha.Domain.Comum;
using MinhaEscolinha.Domain.Equipe;
using MinhaEscolinha.Domain.Turmas;

namespace MinhaEscolinha.Application.Equipe;

/// <summary>Professores da escolinha do contexto, com disponibilidade e indisponibilidades.</summary>
public interface IEquipeRepositorio
{
    Task<IReadOnlyList<Professor>> ListarProfessoresAsync(CancellationToken ct = default);

    /// <summary>O professor rastreado, com as faixas de disponibilidade e as indisponibilidades.</summary>
    Task<Professor?> ObterProfessorAsync(Guid id, CancellationToken ct = default);

    Task<bool> CpfEmUsoAsync(string cpf, Guid? excetoId, CancellationToken ct = default);
    Task<Professor?> ProfessorDaPessoaAsync(Guid pessoaId, CancellationToken ct = default);

    /// <summary>Professores com faixas e indisponibilidades (para os alertas da agenda).</summary>
    Task<IReadOnlyList<Professor>> ProfessoresComAgendaAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);

    void AdicionarProfessor(Professor professor);
    void AdicionarFaixa(FaixaDisponibilidade faixa);
    void AdicionarIndisponibilidade(IndisponibilidadeProfessor indisponibilidade);
    void RemoverFaixa(FaixaDisponibilidade faixa);
    void RemoverIndisponibilidade(IndisponibilidadeProfessor indisponibilidade);
    Task SalvarAsync(CancellationToken ct = default);
}

/// <summary>De onde vem o cadastro do professor novo (BL-M08-01: reaproveitar a pessoa que já existe).</summary>
public enum OrigemCadastroProfessor
{
    PessoaNova = 10,

    /// <summary>Alguém que já atua nesta escolinha (o gestor que também dá aula): liga a mesma identidade.</summary>
    PessoaComAcesso = 20,

    /// <summary>Um responsável já cadastrado: os dados vêm do cadastro dele, sem redigitar (R11).</summary>
    Responsavel = 30
}

public class ProfessorRequisicao
{
    public Guid? Id { get; set; }
    public OrigemCadastroProfessor Origem { get; set; } = OrigemCadastroProfessor.PessoaNova;

    /// <summary>A pessoa (<see cref="OrigemCadastroProfessor.PessoaComAcesso"/>) ou o responsável reaproveitado.</summary>
    public Guid? OrigemId { get; set; }

    /// <summary>Nome de quem está sendo reaproveitado, só para a tela.</summary>
    public string? OrigemNome { get; set; }

    public string Nome { get; set; } = string.Empty;
    public string? Cpf { get; set; }
    public string? Telefone { get; set; }
    public string? Email { get; set; }
}

public class ProfessorRequisicaoValidador : AbstractValidator<ProfessorRequisicao>
{
    public ProfessorRequisicaoValidador()
    {
        RuleFor(x => x.Origem).IsInEnum().WithMessage("Origem do cadastro inválida.");
        RuleFor(x => x.OrigemId).NotEmpty().When(x => x.Origem != OrigemCadastroProfessor.PessoaNova)
            .WithMessage("Escolha o cadastro que será reaproveitado.");
        RuleFor(x => x.Nome)
            .NotEmpty().WithMessage("Informe o nome do professor.")
            .MaximumLength(Professor.TamanhoNome).WithMessage($"O nome pode ter no máximo {Professor.TamanhoNome} caracteres.");
        RuleFor(x => x.Cpf).Must(Cadastros.CpfValidoOuVazio).WithMessage("CPF inválido.");
        RuleFor(x => x.Telefone).Must(Cadastros.TelefoneValidoOuVazio).WithMessage("Celular inválido: informe DDD e número.");
        RuleFor(x => x.Email)
            .MaximumLength(Professor.TamanhoEmail).WithMessage($"O e-mail pode ter no máximo {Professor.TamanhoEmail} caracteres.")
            .EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email)).WithMessage("E-mail inválido.");
    }
}

public class FaixaRequisicao
{
    public DayOfWeek DiaSemana { get; set; } = DayOfWeek.Monday;
    public TimeOnly? Inicio { get; set; }
    public TimeOnly? Fim { get; set; }
    public bool Vazia => Inicio is null && Fim is null;
}

/// <summary>A disponibilidade semanal inteira do professor (a lista substitui a anterior).</summary>
public class DisponibilidadeRequisicao
{
    public Guid ProfessorId { get; set; }
    public List<FaixaRequisicao> Faixas { get; set; } = [];
}

public class DisponibilidadeRequisicaoValidador : AbstractValidator<DisponibilidadeRequisicao>
{
    public DisponibilidadeRequisicaoValidador()
    {
        RuleFor(x => x.ProfessorId).NotEmpty();
        RuleFor(x => x.Faixas)
            .Must(f => f.Where(l => !l.Vazia).All(l => l.Inicio is not null && l.Fim is not null))
            .WithMessage("Cada faixa precisa de início e fim.")
            .Must(f => f.Where(l => !l.Vazia && l.Inicio is not null && l.Fim is not null).All(l => l.Fim > l.Inicio))
            .WithMessage("O fim da faixa precisa ser depois do início.")
            .Must(f => f.All(l => Enum.IsDefined(l.DiaSemana))).WithMessage("Dia da semana inválido.")
            .Must(SemSobreposicao).WithMessage("Duas faixas se sobrepõem no mesmo dia: junte-as numa só.");
    }

    private static bool SemSobreposicao(List<FaixaRequisicao> faixas)
    {
        var validas = faixas.Where(f => !f.Vazia && f.Inicio is not null && f.Fim is not null && f.Fim > f.Inicio)
            .Select(f => new HorarioSemanal(f.DiaSemana, f.Inicio!.Value, f.Fim!.Value)).ToList();
        for (var i = 0; i < validas.Count; i++)
        for (var j = i + 1; j < validas.Count; j++)
            if (validas[i].Sobrepoe(validas[j])) return false;
        return true;
    }
}

public class IndisponibilidadeRequisicao
{
    public Guid ProfessorId { get; set; }
    public DateTime? Inicio { get; set; }
    public DateTime? Fim { get; set; }
    public string? Motivo { get; set; }
}

public class IndisponibilidadeRequisicaoValidador : AbstractValidator<IndisponibilidadeRequisicao>
{
    public IndisponibilidadeRequisicaoValidador()
    {
        RuleFor(x => x.ProfessorId).NotEmpty();
        RuleFor(x => x.Inicio).NotNull().WithMessage("Informe o início da indisponibilidade.");
        RuleFor(x => x.Fim).NotNull().WithMessage("Informe o fim da indisponibilidade.")
            .GreaterThan(x => x.Inicio).When(x => x.Inicio is not null).WithMessage("O fim precisa ser depois do início.");
        RuleFor(x => x.Motivo).MaximumLength(IndisponibilidadeProfessor.TamanhoMotivo)
            .WithMessage($"O motivo pode ter no máximo {IndisponibilidadeProfessor.TamanhoMotivo} caracteres.");
    }
}

public record ProfessorResumo(
    Guid Id, string Nome, string? Telefone, string? Email, bool TemAcesso, IReadOnlyList<string> Turmas, string Disponibilidade);

/// <summary>Quem pode ter o cadastro reaproveitado como professor.</summary>
public record CandidatoProfessor(OrigemCadastroProfessor Origem, Guid Id, string Nome, string Detalhe);

public record TurmaDoProfessor(Guid Id, string Nome, string Horarios, SituacaoTurma Situacao);

public record FichaProfessor(
    Guid Id,
    string Nome,
    string? Cpf,
    string? Telefone,
    string? Email,
    bool TemAcesso,
    IReadOnlyList<TurmaDoProfessor> Turmas,
    IReadOnlyList<FaixaDisponibilidade> Disponibilidade,
    IReadOnlyList<IndisponibilidadeProfessor> Indisponibilidades,
    IReadOnlyList<ConflitoAgenda> Conflitos);

public static class ResumoDisponibilidade
{
    /// <summary>"Seg 08:00–12:00 · Ter 17:00–21:00" ou "Não informada".</summary>
    public static string De(IEnumerable<FaixaDisponibilidade> faixas)
    {
        var lista = faixas.OrderBy(f => DiasSemana.Posicao(f.DiaSemana)).ThenBy(f => f.Inicio).ToList();
        return lista.Count == 0
            ? "Não informada"
            : string.Join(" · ", lista.Select(f => $"{DiasSemana.Abreviado(f.DiaSemana)} {f.Inicio:HH\\:mm}–{f.Fim:HH\\:mm}"));
    }
}
