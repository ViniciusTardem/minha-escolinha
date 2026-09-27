using FluentValidation;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Domain.Auditoria;
using MinhaEscolinha.Domain.Comum;
using MinhaEscolinha.Domain.Configuracao;
using MinhaEscolinha.Domain.Configuracao.Parametros;

namespace MinhaEscolinha.Application.Configuracao;

/// <summary>Dados institucionais da escolinha (criação pela plataforma e edição pelo gestor).</summary>
public class DadosEscolinhaRequisicao
{
    public string Nome { get; set; } = string.Empty;
    public string? RazaoSocial { get; set; }
    public string? Cnpj { get; set; }
    public string? Telefone { get; set; }
    public string? Email { get; set; }
    public string? Logradouro { get; set; }
    public string? Numero { get; set; }
    public string? Complemento { get; set; }
    public string? Bairro { get; set; }
    public string? Cidade { get; set; }
    public string? Uf { get; set; }
    public string? Cep { get; set; }
}

/// <summary>Escolinha nova e o 1º gestor, criados juntos numa transação (S0: "a conta e o primeiro gestor nascem juntos").</summary>
public class NovaEscolinhaRequisicao : DadosEscolinhaRequisicao
{
    public string NomeModalidade { get; set; } = "Futsal";
    public PerfilModalidade PerfilModalidade { get; set; } = PerfilModalidade.Coletiva;

    public string GestorNome { get; set; } = string.Empty;

    /// <summary>E-mail do gestor — é o login dele.</summary>
    public string GestorEmail { get; set; } = string.Empty;

    public string? GestorCpf { get; set; }

    /// <summary>O e-mail já tem acesso (gestor de outra escolinha): vincular a mesma pessoa em vez de criar outra.</summary>
    public bool ReaproveitarAcessoExistente { get; set; }
}

public record EscolinhaCriada(Guid EscolinhaId, string Nome, string GestorNome, string Login, string? SenhaTemporaria);

public record DadosEscolinhaResposta(
    Guid Id, string Nome, string? RazaoSocial, string? Cnpj, string? Telefone, string? Email, Endereco Endereco, DateTime DataCadastro);

public class ModalidadeRequisicao
{
    public Guid? Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public PerfilModalidade Perfil { get; set; } = PerfilModalidade.Coletiva;
    public bool TemAdversario { get; set; }
    public bool TemPlacarSumula { get; set; }
    public bool TemClassificacao { get; set; }
    public bool TemConvocacao { get; set; }
    public bool TemGraduacao { get; set; }
    public bool TemApresentacao { get; set; }
    public int DuracaoPadraoEventoMinutos { get; set; } = Modalidade.DuracaoPadraoEventoProvisoria;

    public RecursosModalidade Recursos =>
        new(TemAdversario, TemPlacarSumula, TemClassificacao, TemConvocacao, TemGraduacao, TemApresentacao);

    public static ModalidadeRequisicao NovaDoPerfil(PerfilModalidade perfil)
    {
        var r = ModelosPerfil.Padrao(perfil);
        return new ModalidadeRequisicao
        {
            Perfil = perfil,
            TemAdversario = r.Adversario,
            TemPlacarSumula = r.PlacarSumula,
            TemClassificacao = r.Classificacao,
            TemConvocacao = r.Convocacao,
            TemGraduacao = r.Graduacao,
            TemApresentacao = r.Apresentacao
        };
    }
}

public record ModalidadeResposta(
    Guid Id, string Nome, PerfilModalidade Perfil, RecursosModalidade Recursos, int DuracaoPadraoEventoMinutos);

/// <summary>Um parâmetro na tela: a definição, o valor efetivo e se ainda é o padrão.</summary>
public record ParametroResposta(DefinicaoParametro Definicao, string? Valor, bool EhPadrao, RegistroAlteracao? UltimaAlteracao);

public class AlterarParametrosRequisicao
{
    /// <summary>Chave → valor digitado. Só as chaves enviadas são avaliadas.</summary>
    public Dictionary<string, string?> Valores { get; set; } = [];
}

public class VincularContaAsaasRequisicao
{
    public AmbienteAsaas Ambiente { get; set; } = AmbienteAsaas.Sandbox;
    public string ChaveApi { get; set; } = string.Empty;

    /// <summary>A senha de quem está vinculando: trocar a conta de recebimento é ação sensível.</summary>
    public string SenhaAtual { get; set; } = string.Empty;
}

public record ContaAsaasResposta(bool Vinculada, AmbienteAsaas? Ambiente, string? FinalChave, DateTime? VinculadaEm);

public class ConcederCreditosRequisicao
{
    public Guid EscolinhaId { get; set; }
    public int Quantidade { get; set; }
    public string Motivo { get; set; } = string.Empty;
    public string? Plano { get; set; }
    public string? Pacote { get; set; }
}

public record CreditosResposta(string Plano, string? Pacote, int Saldo, int AvisoSaldoAbaixoDe, Pagina<MovimentoCredito> Movimentos);

public record EscolinhaNaPlataforma(
    Guid Id,
    string Nome,
    string? Cnpj,
    string? Cidade,
    string? Uf,
    IReadOnlyList<string> Gestores,
    int SaldoCreditos,
    bool ContaAsaasVinculada,
    DateTime DataCadastro,
    bool SouGestor);

// ── Validadores ──

public static class RegrasEscolinha
{
    public static void AplicarDados<T>(AbstractValidator<T> v) where T : DadosEscolinhaRequisicao
    {
        v.RuleFor(x => x.Nome)
            .NotEmpty().WithMessage("Informe o nome da escolinha.")
            .MaximumLength(Escolinha.TamanhoNome).WithMessage($"O nome pode ter no máximo {Escolinha.TamanhoNome} caracteres.");
        v.RuleFor(x => x.RazaoSocial).MaximumLength(Escolinha.TamanhoRazaoSocial)
            .WithMessage($"A razão social pode ter no máximo {Escolinha.TamanhoRazaoSocial} caracteres.");
        v.RuleFor(x => x.Cnpj)
            .Must(c => string.IsNullOrWhiteSpace(c) || Domain.Comum.Cnpj.Normalizar(c) is not null).WithMessage("CNPJ inválido.");
        v.RuleFor(x => x.Telefone)
            .Must(t => string.IsNullOrWhiteSpace(t) || Domain.Comum.Telefone.ParaE164(t) is not null)
            .WithMessage("Telefone inválido: informe DDD e número.");
        v.RuleFor(x => x.Email)
            .MaximumLength(Escolinha.TamanhoEmail).WithMessage($"O e-mail pode ter no máximo {Escolinha.TamanhoEmail} caracteres.")
            .EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email)).WithMessage("E-mail inválido.");
        v.RuleFor(x => x.Uf)
            .Must(u => string.IsNullOrWhiteSpace(u) || u.Trim().Length == 2).WithMessage("UF com 2 letras.");
        v.RuleFor(x => x.Cep)
            .Must(c => string.IsNullOrWhiteSpace(c) || TextoHelper.SomenteDigitos(c).Length == 8).WithMessage("CEP com 8 dígitos.");
        v.RuleFor(x => x.Logradouro).MaximumLength(Endereco.TamanhoLogradouro);
        v.RuleFor(x => x.Numero).MaximumLength(Endereco.TamanhoNumero);
        v.RuleFor(x => x.Complemento).MaximumLength(Endereco.TamanhoComplemento);
        v.RuleFor(x => x.Bairro).MaximumLength(Endereco.TamanhoBairro);
        v.RuleFor(x => x.Cidade).MaximumLength(Endereco.TamanhoCidade);
    }
}

public class DadosEscolinhaRequisicaoValidador : AbstractValidator<DadosEscolinhaRequisicao>
{
    public DadosEscolinhaRequisicaoValidador() => RegrasEscolinha.AplicarDados(this);
}

public class NovaEscolinhaRequisicaoValidador : AbstractValidator<NovaEscolinhaRequisicao>
{
    public NovaEscolinhaRequisicaoValidador()
    {
        RegrasEscolinha.AplicarDados(this);
        RuleFor(x => x.NomeModalidade)
            .NotEmpty().WithMessage("Informe a modalidade (ex.: Futsal).")
            .MaximumLength(Modalidade.TamanhoNome).WithMessage($"A modalidade pode ter no máximo {Modalidade.TamanhoNome} caracteres.");
        RuleFor(x => x.PerfilModalidade).IsInEnum().WithMessage("Perfil de modalidade inválido.");
        RuleFor(x => x.GestorNome)
            .NotEmpty().WithMessage("Informe o nome do gestor.")
            .MaximumLength(Pessoa.TamanhoNome).WithMessage($"O nome pode ter no máximo {Pessoa.TamanhoNome} caracteres.");
        RuleFor(x => x.GestorEmail)
            .NotEmpty().WithMessage("Informe o e-mail do gestor (é o login dele).")
            .MaximumLength(Credencial.TamanhoLogin).WithMessage($"O e-mail pode ter no máximo {Credencial.TamanhoLogin} caracteres.")
            .EmailAddress().WithMessage("E-mail do gestor inválido.");
        RuleFor(x => x.GestorCpf)
            .Must(c => string.IsNullOrWhiteSpace(c) || Cpf.Normalizar(c) is not null).WithMessage("CPF do gestor inválido.");
    }
}

public class ModalidadeRequisicaoValidador : AbstractValidator<ModalidadeRequisicao>
{
    public ModalidadeRequisicaoValidador()
    {
        RuleFor(x => x.Nome)
            .NotEmpty().WithMessage("Informe o nome da modalidade.")
            .MaximumLength(Modalidade.TamanhoNome).WithMessage($"O nome pode ter no máximo {Modalidade.TamanhoNome} caracteres.");
        RuleFor(x => x.Perfil).IsInEnum().WithMessage("Perfil de modalidade inválido.");
        RuleFor(x => x.DuracaoPadraoEventoMinutos)
            .InclusiveBetween(10, 600).WithMessage("A duração padrão do evento fica entre 10 e 600 minutos.");
    }
}

public class VincularContaAsaasRequisicaoValidador : AbstractValidator<VincularContaAsaasRequisicao>
{
    public VincularContaAsaasRequisicaoValidador()
    {
        RuleFor(x => x.Ambiente).IsInEnum().WithMessage("Ambiente inválido.");
        RuleFor(x => x.ChaveApi)
            .NotEmpty().WithMessage("Cole a chave de API da conta Asaas da escolinha.")
            .MinimumLength(20).WithMessage("A chave de API parece incompleta.")
            .MaximumLength(500).WithMessage("A chave de API é longa demais.");
        RuleFor(x => x.SenhaAtual).NotEmpty().WithMessage("Confirme com a sua senha.");
    }
}

public class ConcederCreditosRequisicaoValidador : AbstractValidator<ConcederCreditosRequisicao>
{
    public ConcederCreditosRequisicaoValidador()
    {
        RuleFor(x => x.EscolinhaId).NotEmpty();
        RuleFor(x => x.Quantidade).NotEqual(0).WithMessage("Informe a quantidade de créditos.")
            .InclusiveBetween(-100000, 100000).WithMessage("Quantidade fora do limite (até 100.000 por lançamento).");
        RuleFor(x => x.Motivo)
            .NotEmpty().WithMessage("Informe o motivo (ex.: pacote do piloto, recarga, correção).")
            .MaximumLength(MovimentoCredito.TamanhoMotivo);
        RuleFor(x => x.Plano).MaximumLength(ContaCreditos.TamanhoPlano);
        RuleFor(x => x.Pacote).MaximumLength(ContaCreditos.TamanhoPlano);
    }
}
