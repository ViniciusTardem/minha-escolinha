using System.Linq.Expressions;
using FluentValidation;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Domain.Alunos;
using MinhaEscolinha.Domain.Turmas;

namespace MinhaEscolinha.Application.Alunos;

internal static class RegrasAluno
{
    public static readonly DateOnly NascimentoMinimo = new(1900, 1, 1);

    public static void Saude<T>(AbstractValidator<T> v,
        Expression<Func<T, string?>> alergias, Expression<Func<T, string?>> restricoes, Expression<Func<T, string?>> observacoes,
        Expression<Func<T, string?>> contatoNome, Expression<Func<T, string?>> contatoTelefone,
        Expression<Func<T, string?>> contatoParentesco)
    {
        v.RuleFor(alergias).MaximumLength(Aluno.TamanhoSaude).WithMessage($"Alergias: no máximo {Aluno.TamanhoSaude} caracteres.");
        v.RuleFor(restricoes).MaximumLength(Aluno.TamanhoSaude).WithMessage($"Restrições: no máximo {Aluno.TamanhoSaude} caracteres.");
        v.RuleFor(observacoes).MaximumLength(Aluno.TamanhoObservacoesMedicas)
            .WithMessage($"Observações médicas: no máximo {Aluno.TamanhoObservacoesMedicas} caracteres.");
        v.RuleFor(contatoNome).MaximumLength(Aluno.TamanhoNome).WithMessage("O nome do contato de emergência é longo demais.");
        v.RuleFor(contatoTelefone).Must(Cadastros.TelefoneValidoOuVazio)
            .WithMessage("Telefone do contato de emergência inválido: informe DDD e número.");
        v.RuleFor(contatoParentesco).MaximumLength(Aluno.TamanhoParentesco)
            .WithMessage("O parentesco do contato de emergência é longo demais.");
    }

    public static void Mensalidade<T>(AbstractValidator<T> v, Expression<Func<T, decimal?>> campo) =>
        v.RuleFor(campo).InclusiveBetween(0m, Turma.MensalidadeMaxima)
            .WithMessage($"A mensalidade fica entre R$ 0 e R$ {Turma.MensalidadeMaxima:N0}.");
}

public class ResponsavelDoAlunoRequisicaoValidador : AbstractValidator<ResponsavelDoAlunoRequisicao>
{
    public ResponsavelDoAlunoRequisicaoValidador()
    {
        When(r => r.ResponsavelId is null && r.Preenchido, () =>
        {
            RuleFor(r => r.Nome)
                .NotEmpty().WithMessage("Informe o nome do responsável.")
                .MaximumLength(Responsavel.TamanhoNome).WithMessage($"O nome pode ter no máximo {Responsavel.TamanhoNome} caracteres.");
            RuleFor(r => r.Telefone)
                .NotEmpty().WithMessage("Informe o celular (WhatsApp) do responsável: é o canal com a família.")
                .Must(Cadastros.TelefoneValidoOuVazio).WithMessage("Celular inválido: informe DDD e número.");
            RuleFor(r => r.Cpf).Must(Cadastros.CpfValidoOuVazio).WithMessage("CPF do responsável inválido.");
            RuleFor(r => r.Email)
                .MaximumLength(Responsavel.TamanhoEmail).WithMessage("O e-mail é longo demais.")
                .EmailAddress().When(r => !string.IsNullOrWhiteSpace(r.Email)).WithMessage("E-mail do responsável inválido.");
        });
        RuleFor(r => r.Parentesco).IsInEnum().WithMessage("Parentesco inválido.");
    }
}

public class NovoAlunoRequisicaoValidador : AbstractValidator<NovoAlunoRequisicao>
{
    public NovoAlunoRequisicaoValidador(IRelogioBrasil relogio)
    {
        var hoje = DateOnly.FromDateTime(relogio.Hoje());

        RuleFor(x => x.Nome)
            .NotEmpty().WithMessage("Informe o nome do aluno.")
            .MaximumLength(Aluno.TamanhoNome).WithMessage($"O nome pode ter no máximo {Aluno.TamanhoNome} caracteres.");
        RuleFor(x => x.DataNascimento)
            .NotNull().WithMessage("Informe a data de nascimento.")
            .InclusiveBetween(RegrasAluno.NascimentoMinimo, hoje).WithMessage("Data de nascimento inválida.");
        RuleFor(x => x.Cpf).Must(Cadastros.CpfValidoOuVazio).WithMessage("CPF do aluno inválido.");
        RuleFor(x => x.Categoria).MaximumLength(Aluno.TamanhoCategoria).WithMessage("A categoria é longa demais.");
        RuleFor(x => x.TrialAte).NotNull().When(x => x.EmTrial).WithMessage("Informe até quando vai o trial.");
        RuleFor(x => x.TrialAte).GreaterThanOrEqualTo(x => x.DataInicio).When(x => x.EmTrial && x.TrialAte is not null && x.DataInicio is not null)
            .WithMessage("O trial termina depois do início na turma.");
        Cadastros.RegrasEndereco(this);

        RuleFor(x => x.Responsaveis)
            .Must(r => r.Count > 0 && r[0].Preenchido).WithMessage("Informe ao menos um responsável: toda comunicação passa por ele.")
            .Must(r => r.Count <= NovoAlunoRequisicao.MaximoResponsaveis).WithMessage("No cadastro cabem até dois responsáveis; outros entram depois, na ficha.")
            .Must(r => r.Count(x => x.Preenchido && x.Pagador) <= 1).WithMessage("Só um responsável é o pagador.")
            .Must(r => r.Where(x => x.ResponsavelId is not null).Select(x => x.ResponsavelId).Distinct().Count()
                       == r.Count(x => x.ResponsavelId is not null))
            .WithMessage("O mesmo responsável foi escolhido duas vezes.");
        RuleForEach(x => x.Responsaveis).SetValidator(new ResponsavelDoAlunoRequisicaoValidador());

        RuleFor(x => x.TurmaId).NotEmpty().WithMessage("Escolha a turma da matrícula.");
        RuleFor(x => x.DataInicio).NotNull().WithMessage("Informe a data de início na turma.");
        RegrasAluno.Mensalidade(this, x => x.Mensalidade);

        RegrasAluno.Saude(this, x => x.Alergias, x => x.Restricoes, x => x.ObservacoesMedicas,
            x => x.ContatoEmergenciaNome, x => x.ContatoEmergenciaTelefone, x => x.ContatoEmergenciaParentesco);

        RuleForEach(x => x.Consentimentos).ChildRules(c =>
        {
            c.RuleFor(x => x.Finalidade).IsInEnum().WithMessage("Finalidade de consentimento inválida.");
            c.RuleFor(x => x.Resposta).IsInEnum().WithMessage("Resposta de consentimento inválida.");
            c.RuleFor(x => x.Origem).IsInEnum().WithMessage("Origem do consentimento inválida.");
        });
        RuleFor(x => x).Must(x => x.Consentimentos
                .Where(c => c.Resposta != RespostaConsentimento.NaoColetado)
                .All(c => c.ResponsavelIndice >= 0 && c.ResponsavelIndice < x.Responsaveis.Count && x.Responsaveis[c.ResponsavelIndice].Preenchido))
            .WithMessage("Escolha, entre os responsáveis informados, quem autorizou.").OverridePropertyName("Consentimentos");
    }
}

public class DadosAlunoRequisicaoValidador : AbstractValidator<DadosAlunoRequisicao>
{
    public DadosAlunoRequisicaoValidador(IRelogioBrasil relogio)
    {
        var hoje = DateOnly.FromDateTime(relogio.Hoje());
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Nome)
            .NotEmpty().WithMessage("Informe o nome do aluno.")
            .MaximumLength(Aluno.TamanhoNome).WithMessage($"O nome pode ter no máximo {Aluno.TamanhoNome} caracteres.");
        RuleFor(x => x.DataNascimento)
            .NotNull().WithMessage("Informe a data de nascimento.")
            .InclusiveBetween(RegrasAluno.NascimentoMinimo, hoje).WithMessage("Data de nascimento inválida.");
        RuleFor(x => x.Cpf).Must(Cadastros.CpfValidoOuVazio).WithMessage("CPF do aluno inválido.");
        RuleFor(x => x.Categoria).MaximumLength(Aluno.TamanhoCategoria).WithMessage("A categoria é longa demais.");
        Cadastros.RegrasEndereco(this);
    }
}

public class SaudeAlunoRequisicaoValidador : AbstractValidator<SaudeAlunoRequisicao>
{
    public SaudeAlunoRequisicaoValidador()
    {
        RuleFor(x => x.Id).NotEmpty();
        RegrasAluno.Saude(this, x => x.Alergias, x => x.Restricoes, x => x.ObservacoesMedicas,
            x => x.ContatoEmergenciaNome, x => x.ContatoEmergenciaTelefone, x => x.ContatoEmergenciaParentesco);
    }
}

public class MudarSituacaoRequisicaoValidador : AbstractValidator<MudarSituacaoRequisicao>
{
    public MudarSituacaoRequisicaoValidador()
    {
        RuleFor(x => x.AlunoId).NotEmpty();
        RuleFor(x => x.Para).IsInEnum().WithMessage("Situação inválida.");
        RuleFor(x => x.Data).NotNull().WithMessage("Informe a data.");
        RuleFor(x => x.Motivo)
            .NotEmpty().When(x => x.Para == SituacaoAluno.Desistente).WithMessage("O desligamento exige o motivo.")
            .MaximumLength(Aluno.TamanhoMotivo).WithMessage($"O motivo pode ter no máximo {Aluno.TamanhoMotivo} caracteres.");
        RegrasAluno.Mensalidade(this, x => x.Mensalidade);
    }
}

public class NovaMatriculaRequisicaoValidador : AbstractValidator<NovaMatriculaRequisicao>
{
    public NovaMatriculaRequisicaoValidador()
    {
        RuleFor(x => x.AlunoId).NotEmpty();
        RuleFor(x => x.TurmaId).NotEmpty().WithMessage("Escolha a turma.");
        RuleFor(x => x.DataInicio).NotNull().WithMessage("Informe a data de início na turma.");
        RegrasAluno.Mensalidade(this, x => x.Mensalidade);
    }
}

public class EncerrarMatriculaRequisicaoValidador : AbstractValidator<EncerrarMatriculaRequisicao>
{
    public EncerrarMatriculaRequisicaoValidador()
    {
        RuleFor(x => x.MatriculaId).NotEmpty();
        RuleFor(x => x.Data).NotNull().WithMessage("Informe o último dia na turma.");
        RuleFor(x => x.Motivo)
            .NotEmpty().WithMessage("Informe o motivo (ex.: mudou de turma, parou a modalidade).")
            .MaximumLength(Matricula.TamanhoMotivo).WithMessage($"O motivo pode ter no máximo {Matricula.TamanhoMotivo} caracteres.");
    }
}

public class MensalidadeMatriculaRequisicaoValidador : AbstractValidator<MensalidadeMatriculaRequisicao>
{
    public MensalidadeMatriculaRequisicaoValidador()
    {
        RuleFor(x => x.MatriculaId).NotEmpty();
        RuleFor(x => x.Mensalidade).NotNull().WithMessage("Informe a mensalidade.");
        RegrasAluno.Mensalidade(this, x => x.Mensalidade);
        RuleFor(x => x.Motivo).MaximumLength(Matricula.TamanhoMotivo).WithMessage("O motivo é longo demais.");
    }
}

public class VinculoRequisicaoValidador : AbstractValidator<VinculoRequisicao>
{
    public VinculoRequisicaoValidador()
    {
        RuleFor(x => x.VinculoId).NotEmpty();
        RuleFor(x => x.Parentesco).IsInEnum().WithMessage("Parentesco inválido.");
    }
}

public class ResponsavelRequisicaoValidador : AbstractValidator<ResponsavelRequisicao>
{
    public ResponsavelRequisicaoValidador()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Nome)
            .NotEmpty().WithMessage("Informe o nome do responsável.")
            .MaximumLength(Responsavel.TamanhoNome).WithMessage($"O nome pode ter no máximo {Responsavel.TamanhoNome} caracteres.");
        RuleFor(x => x.Telefone)
            .NotEmpty().WithMessage("Informe o celular (WhatsApp) do responsável: é o canal com a família.")
            .Must(Cadastros.TelefoneValidoOuVazio).WithMessage("Celular inválido: informe DDD e número.");
        RuleFor(x => x.Cpf).Must(Cadastros.CpfValidoOuVazio).WithMessage("CPF inválido.");
        RuleFor(x => x.Email)
            .MaximumLength(Responsavel.TamanhoEmail).WithMessage("O e-mail é longo demais.")
            .EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email)).WithMessage("E-mail inválido.");
        Cadastros.RegrasEndereco(this);
    }
}

public class RegistrarConsentimentoRequisicaoValidador : AbstractValidator<RegistrarConsentimentoRequisicao>
{
    public RegistrarConsentimentoRequisicaoValidador()
    {
        RuleFor(x => x.AlunoId).NotEmpty();
        RuleFor(x => x.Finalidade).IsInEnum().WithMessage("Finalidade inválida.");
        RuleFor(x => x.Resposta).Must(r => r is RespostaConsentimento.Concedido or RespostaConsentimento.Recusado)
            .WithMessage("Informe se o responsável autorizou ou recusou.");
        RuleFor(x => x.ResponsavelId).NotEmpty().WithMessage("Escolha o responsável que autorizou.");
        RuleFor(x => x.Origem).IsInEnum().WithMessage("Origem inválida.");
    }
}
