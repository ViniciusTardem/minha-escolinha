using MinhaEscolinha.Application.Acesso;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Domain.Alunos;
using MinhaEscolinha.Domain.Auditoria;
using MinhaEscolinha.Domain.Comum;

namespace MinhaEscolinha.Application.Alunos;

/// <summary>
/// Alunos, responsáveis, vínculos, matrículas e consentimentos da escolinha do contexto.
/// As listas projetam só o que a lista mostra: dado de saúde nunca sai numa lista (R5).
/// </summary>
public interface IAlunosRepositorio
{
    /// <param name="somenteIds">Quando informado, só estes alunos (o filtro de risco de evasão).</param>
    Task<Pagina<AlunoNaLista>> ListarAsync(FiltroAlunos filtro, EscopoTurmas escopo, IReadOnlyCollection<Guid>? somenteIds, int pagina, int tamanho,
        CancellationToken ct = default);

    /// <summary>O aluno rastreado (para alterar).</summary>
    Task<Aluno?> ObterAlunoAsync(Guid id, CancellationToken ct = default);

    Task<Aluno?> AlunoPorCpfAsync(string cpf, CancellationToken ct = default);
    Task<IReadOnlyList<Aluno>> AlunosComMesmoNomeENascimentoAsync(string nomeBusca, DateOnly nascimento, CancellationToken ct = default);
    void AdicionarAluno(Aluno aluno);
    void DefinirVersaoOriginal(Aluno aluno, byte[] versao);

    /// <summary>Os alunos em trial, com o prazo (os sinais de trial vencendo e vencido do painel).</summary>
    Task<IReadOnlyList<(Guid Id, string Nome, DateOnly? TrialAte)>> TrialsAsync(EscopoTurmas escopo, CancellationToken ct = default);

    Task<IReadOnlyList<VinculoAlunoResponsavel>> VinculosDoAlunoAsync(Guid alunoId, CancellationToken ct = default);
    Task<VinculoAlunoResponsavel?> ObterVinculoAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<VinculoAlunoResponsavel>> VinculosDoResponsavelAsync(Guid responsavelId, CancellationToken ct = default);
    void AdicionarVinculo(VinculoAlunoResponsavel vinculo);
    void RemoverVinculo(VinculoAlunoResponsavel vinculo);

    Task<Responsavel?> ObterResponsavelAsync(Guid id, CancellationToken ct = default);
    Task<Responsavel?> ResponsavelPorCpfAsync(string cpf, CancellationToken ct = default);

    /// <summary>Busca por nome, CPF ou telefone (a reutilização no cadastro, BL-M03-01).</summary>
    Task<IReadOnlyList<ResponsavelEncontrado>> BuscarResponsaveisAsync(string termo, int limite, CancellationToken ct = default);

    /// <summary>Responsáveis com o mesmo telefone ou o mesmo nome (a sugestão de duplicidade: nunca junta sozinha).</summary>
    Task<IReadOnlyList<ResponsavelEncontrado>> ResponsaveisParecidosAsync(string? telefone, string nomeBusca, CancellationToken ct = default);

    Task<Pagina<ResponsavelNaLista>> ListarResponsaveisAsync(string? busca, EscopoTurmas escopo, int pagina, int tamanho,
        CancellationToken ct = default);

    /// <summary>Todos os responsáveis, com o CPF e os alunos (a escolha de quem reaproveitar como professor).</summary>
    Task<IReadOnlyList<ResponsavelEncontrado>> TodosResponsaveisAsync(CancellationToken ct = default);

    Task<IReadOnlyList<ResponsavelEncontrado>> ResponsaveisAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);

    void AdicionarResponsavel(Responsavel responsavel);

    Task<IReadOnlyList<Matricula>> MatriculasDoAlunoAsync(Guid alunoId, CancellationToken ct = default);
    Task<Matricula?> ObterMatriculaAsync(Guid id, CancellationToken ct = default);

    /// <summary>A composição da turma: as matrículas (ativas e encerradas) com o aluno.</summary>
    Task<IReadOnlyList<AlunoDaTurma>> AlunosDaTurmaAsync(Guid turmaId, CancellationToken ct = default);

    void AdicionarMatricula(Matricula matricula);

    Task<IReadOnlyList<ConsentimentoAluno>> ConsentimentosDoAlunoAsync(Guid alunoId, CancellationToken ct = default);
    Task<ConsentimentoAluno?> ObterConsentimentoAsync(Guid id, CancellationToken ct = default);
    void AdicionarConsentimento(ConsentimentoAluno consentimento);

    Task SalvarAsync(CancellationToken ct = default);
}

// ── Listas ──

public enum FiltroSituacaoAluno
{
    /// <summary>Trial, ativos e inativos: quem está na escolinha (o padrão da lista).</summary>
    NaEscolinha = 0,
    Trial = 10,
    Ativo = 20,
    Inativo = 30,
    Desistente = 40,
    Todos = 99
}

public class FiltroAlunos
{
    public string? Busca { get; set; }
    public Guid? TurmaId { get; set; }
    public FiltroSituacaoAluno Situacao { get; set; } = FiltroSituacaoAluno.NaEscolinha;

    /// <summary>Só os alunos em risco de evasão agora (BL-M05-11).</summary>
    public bool EmRisco { get; set; }
}

public record AlunoNaLista(
    Guid Id,
    string Nome,
    DateOnly DataNascimento,
    string? Categoria,
    SituacaoAluno Situacao,
    DateOnly? TrialAte,
    IReadOnlyList<string> Turmas,
    IReadOnlyList<string> Responsaveis);

public record AlunoDaTurma(
    Guid AlunoId,
    string Nome,
    DateOnly DataNascimento,
    SituacaoAluno Situacao,
    DateOnly? TrialAte,
    Guid MatriculaId,
    DateOnly Inicio,
    DateOnly? Fim,
    SituacaoMatricula SituacaoMatricula);

public record ResponsavelEncontrado(Guid Id, string Nome, string? Cpf, string? Telefone, IReadOnlyList<string> Alunos);

public record ResponsavelNaLista(Guid Id, string Nome, string? Telefone, string? Email, bool TemAcesso, IReadOnlyList<string> Alunos);

// ── Cadastro do aluno em etapas ──

public enum RespostaConsentimento
{
    /// <summary>O termo ainda não foi apresentado: nada é registrado e a ficha mostra a pendência.</summary>
    NaoColetado = 0,
    Concedido = 10,
    Recusado = 20
}

public class ConsentimentoRequisicao
{
    public FinalidadeConsentimento Finalidade { get; set; }
    public RespostaConsentimento Resposta { get; set; } = RespostaConsentimento.NaoColetado;

    /// <summary>Qual dos responsáveis do cadastro autorizou (0 = o primeiro).</summary>
    public int ResponsavelIndice { get; set; }

    public OrigemConsentimento Origem { get; set; } = OrigemConsentimento.TermoEmPapel;
}

/// <summary>Um responsável no cadastro do aluno: um já cadastrado (reaproveitado) ou um novo.</summary>
public class ResponsavelDoAlunoRequisicao
{
    /// <summary>Responsável já cadastrado, escolhido na busca: reaproveita sem redigitar (CA-1).</summary>
    public Guid? ResponsavelId { get; set; }

    public string? Nome { get; set; }
    public string? Cpf { get; set; }
    public string? Telefone { get; set; }
    public string? Email { get; set; }
    public Parentesco Parentesco { get; set; } = Parentesco.Mae;
    public bool Pagador { get; set; }
    public bool RecebeCobranca { get; set; }
    public bool RecebeNotificacao { get; set; } = true;

    /// <summary>Quem cadastra viu os cadastros parecidos (mesmo telefone ou nome) e confirma que é outra pessoa.</summary>
    public bool ConfirmarNovo { get; set; }

    public bool Preenchido =>
        ResponsavelId is not null || !string.IsNullOrWhiteSpace(Nome) || !string.IsNullOrWhiteSpace(Cpf) || !string.IsNullOrWhiteSpace(Telefone);
}

/// <summary>Aluno novo (BL-M03-02): dados, responsáveis, matrícula, saúde e consentimentos, gravados juntos.</summary>
public class NovoAlunoRequisicao : IComEndereco
{
    public const int MaximoResponsaveis = 2;

    public string Nome { get; set; } = string.Empty;
    public DateOnly? DataNascimento { get; set; }
    public string? Cpf { get; set; }
    public string? Categoria { get; set; }
    public bool EmTrial { get; set; }
    public DateOnly? TrialAte { get; set; }
    public string? Logradouro { get; set; }
    public string? Numero { get; set; }
    public string? Complemento { get; set; }
    public string? Bairro { get; set; }
    public string? Cidade { get; set; }
    public string? Uf { get; set; }
    public string? Cep { get; set; }

    /// <summary>Quem cadastra viu o aluno com o mesmo nome e nascimento e confirma que é outro.</summary>
    public bool ConfirmarAlunoNovo { get; set; }

    public List<ResponsavelDoAlunoRequisicao> Responsaveis { get; set; } = [];

    public Guid? TurmaId { get; set; }
    public DateOnly? DataInicio { get; set; }

    /// <summary>Vazio = a mensalidade da turma.</summary>
    public decimal? Mensalidade { get; set; }

    public string? Alergias { get; set; }
    public string? Restricoes { get; set; }
    public string? ObservacoesMedicas { get; set; }
    public string? ContatoEmergenciaNome { get; set; }
    public string? ContatoEmergenciaTelefone { get; set; }
    public string? ContatoEmergenciaParentesco { get; set; }

    public List<ConsentimentoRequisicao> Consentimentos { get; set; } = [];

    /// <summary>O formulário vazio: dois responsáveis (o 1º pagador) e as duas finalidades de consentimento.</summary>
    public static NovoAlunoRequisicao Vazia(DateOnly hoje) => new NovoAlunoRequisicao { DataInicio = hoje }.Completar();

    /// <summary>Garante as linhas fixas do formulário (dois responsáveis, as duas finalidades).</summary>
    public NovoAlunoRequisicao Completar()
    {
        while (Responsaveis.Count < MaximoResponsaveis)
            Responsaveis.Add(Responsaveis.Count == 0
                ? new ResponsavelDoAlunoRequisicao { Pagador = true, RecebeCobranca = true, RecebeNotificacao = true }
                : new ResponsavelDoAlunoRequisicao { Parentesco = Parentesco.Pai });
        foreach (var finalidade in Enum.GetValues<FinalidadeConsentimento>())
            if (Consentimentos.All(c => c.Finalidade != finalidade))
                Consentimentos.Add(new ConsentimentoRequisicao { Finalidade = finalidade });
        Consentimentos.Sort((a, b) => a.Finalidade.CompareTo(b.Finalidade));
        return this;
    }
}

/// <summary>Resultado de uma gravação barrada por possível duplicidade: a tela mostra os cadastros parecidos.</summary>
public record DuplicidadeResponsavel(int Indice, IReadOnlyList<ResponsavelEncontrado> Candidatos);

public record DuplicidadeAluno(IReadOnlyList<(Guid Id, string Nome, DateOnly DataNascimento)> Candidatos);

// ── Ficha e edições ──

public class DadosAlunoRequisicao : IComEndereco
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public DateOnly? DataNascimento { get; set; }
    public string? Cpf { get; set; }
    public string? Categoria { get; set; }
    public string? Logradouro { get; set; }
    public string? Numero { get; set; }
    public string? Complemento { get; set; }
    public string? Bairro { get; set; }
    public string? Cidade { get; set; }
    public string? Uf { get; set; }
    public string? Cep { get; set; }
    public string? Versao { get; set; }
}

public class SaudeAlunoRequisicao
{
    public Guid Id { get; set; }
    public string? Alergias { get; set; }
    public string? Restricoes { get; set; }
    public string? ObservacoesMedicas { get; set; }
    public string? ContatoEmergenciaNome { get; set; }
    public string? ContatoEmergenciaTelefone { get; set; }
    public string? ContatoEmergenciaParentesco { get; set; }
    public string? Versao { get; set; }
}

/// <summary>Uma transição da máquina do aluno (BL-M03-07); o desligamento exige data e motivo (BL-M03-08, CA-2).</summary>
public class MudarSituacaoRequisicao
{
    public Guid AlunoId { get; set; }
    public SituacaoAluno Para { get; set; }
    public DateOnly? Data { get; set; }
    public string? Motivo { get; set; }

    /// <summary>Rematrícula: a turma da nova matrícula (e o valor; vazio = o da turma).</summary>
    public Guid? TurmaId { get; set; }

    public decimal? Mensalidade { get; set; }

    /// <summary>Trial: o novo prazo (a data limite do trial pode ser ajustada sem mudar a situação).</summary>
    public DateOnly? TrialAte { get; set; }
}

public class NovaMatriculaRequisicao
{
    public Guid AlunoId { get; set; }
    public Guid? TurmaId { get; set; }
    public DateOnly? DataInicio { get; set; }
    public decimal? Mensalidade { get; set; }
}

public class EncerrarMatriculaRequisicao
{
    public Guid MatriculaId { get; set; }
    public DateOnly? Data { get; set; }
    public string? Motivo { get; set; }
}

public class MensalidadeMatriculaRequisicao
{
    public Guid MatriculaId { get; set; }
    public decimal? Mensalidade { get; set; }
    public string? Motivo { get; set; }
}

/// <summary>Mais um responsável para o aluno. A lista tem um item só: os campos seguem o nome do cadastro do aluno (Responsaveis[0]).</summary>
public class VincularResponsavelRequisicao
{
    public Guid AlunoId { get; set; }
    public List<ResponsavelDoAlunoRequisicao> Responsaveis { get; set; } = [];

    public ResponsavelDoAlunoRequisicao Pedido
    {
        get
        {
            if (Responsaveis.Count == 0) Responsaveis.Add(new ResponsavelDoAlunoRequisicao { Parentesco = Parentesco.Pai });
            return Responsaveis[0];
        }
    }
}

public class VinculoRequisicao
{
    public Guid VinculoId { get; set; }
    public Parentesco Parentesco { get; set; }
    public bool Pagador { get; set; }
    public bool RecebeCobranca { get; set; }
    public bool RecebeNotificacao { get; set; }
}

public class ResponsavelRequisicao : IComEndereco
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Cpf { get; set; }
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

public class RegistrarConsentimentoRequisicao
{
    public Guid AlunoId { get; set; }
    public FinalidadeConsentimento Finalidade { get; set; }
    public RespostaConsentimento Resposta { get; set; } = RespostaConsentimento.Concedido;
    public Guid? ResponsavelId { get; set; }
    public OrigemConsentimento Origem { get; set; } = OrigemConsentimento.TermoEmPapel;
}

public class RevogarConsentimentoRequisicao
{
    public Guid ConsentimentoId { get; set; }
    public string? Motivo { get; set; }
}

/// <summary>Quanto do dado de saúde a pessoa vê (R5, BL-M03-10).</summary>
public enum AcessoSaude
{
    Nenhum = 0,

    /// <summary>O professor da turma: o necessário ao treino — alergias, restrições e contato de emergência (P20).</summary>
    Treino = 10,

    /// <summary>O gestor e o responsável do próprio aluno.</summary>
    Completo = 20
}

public record PermissoesFicha(
    bool EditarDados,
    bool EditarSaude,
    bool MudarSituacao,
    bool Matricular,
    bool EditarVinculos,
    bool Consentimentos,
    bool VerValores,
    bool VerHistorico,
    bool VerDocumentos);

public record ResponsavelDaFicha(
    Guid VinculoId,
    Guid ResponsavelId,
    string Nome,
    string? Cpf,
    string? Telefone,
    string? Email,
    Parentesco Parentesco,
    bool Pagador,
    bool RecebeCobranca,
    bool RecebeNotificacao,
    IReadOnlyList<string> OutrosAlunos);

public record MatriculaDaFicha(
    Guid Id,
    Guid TurmaId,
    string Turma,
    DateOnly Inicio,
    DateOnly? Fim,
    decimal? Mensalidade,
    SituacaoMatricula Situacao,
    string? Motivo);

public record SaudeDaFicha(
    string? Alergias,
    string? Restricoes,
    string? ObservacoesMedicas,
    string? ContatoEmergenciaNome,
    string? ContatoEmergenciaTelefone,
    string? ContatoEmergenciaParentesco);

public record ConsentimentoDaFicha(
    Guid Id,
    FinalidadeConsentimento Finalidade,
    EstadoConsentimento Estado,
    string VersaoTermo,
    string Responsavel,
    OrigemConsentimento Origem,
    DateTime RegistradoEm,
    string RegistradoPorNome,
    DateTime? RevogadoEm,
    string? RevogadoPorNome,
    string? MotivoRevogacao);

/// <summary>
/// A ficha do aluno montada para quem pede: cada bloco só vem preenchido para quem pode vê-lo (camada "campo" do
/// motor de permissões). CPF e valores só para o gestor; a saúde conforme <see cref="AcessoSaude"/>.
/// </summary>
public record FichaAluno(
    Guid Id,
    string Nome,
    DateOnly DataNascimento,
    int Idade,
    string? Cpf,
    string? Categoria,
    Endereco Endereco,
    SituacaoAluno Situacao,
    DateOnly SituacaoDesde,
    DateOnly? TrialAte,
    int? DiasParaFimDoTrial,
    bool TrialVencido,
    DateOnly DataMatricula,
    DateOnly? DataDesligamento,
    string? MotivoDesligamento,
    string? Versao,
    IReadOnlyList<ResponsavelDaFicha> Responsaveis,
    IReadOnlyList<MatriculaDaFicha> Matriculas,
    AcessoSaude AcessoSaude,
    SaudeDaFicha? Saude,
    IReadOnlyDictionary<FinalidadeConsentimento, EstadoConsentimento> EstadoConsentimentos,
    IReadOnlyList<ConsentimentoDaFicha> Consentimentos,
    IReadOnlyList<RegistroAlteracao> Historico,
    PermissoesFicha Pode)
{
    public bool TemMatriculaAtiva => Matriculas.Any(m => m.Situacao == SituacaoMatricula.Ativa);
    public bool TemPagador => Responsaveis.Any(r => r.Pagador);
    public bool AlguemRecebeNotificacao => Responsaveis.Any(r => r.RecebeNotificacao);
}

public record FichaResponsavel(
    Guid Id,
    string Nome,
    string? Cpf,
    string? Telefone,
    string? Email,
    Endereco Endereco,
    bool TemAcesso,
    IReadOnlyList<(Guid AlunoId, string Aluno, Parentesco Parentesco, bool Pagador, bool RecebeCobranca, bool RecebeNotificacao, SituacaoAluno Situacao,
        VerificacaoVinculo Verificacao)> Alunos,
    bool PodeEditar,
    bool VerFinanceiro);

/// <summary>"•••.456.789-••": o suficiente para reconhecer o cadastro sem expor o CPF inteiro.</summary>
public static class CpfMascarado
{
    public static string? De(string? cpf) => cpf is { Length: 11 } ? $"•••.{cpf[3..6]}.{cpf[6..9]}-••" : null;
}
