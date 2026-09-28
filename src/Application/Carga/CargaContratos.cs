using MinhaEscolinha.Domain.Alunos;
using MinhaEscolinha.Domain.Carga;
using MinhaEscolinha.Domain.Comum;

namespace MinhaEscolinha.Application.Carga;

// ── Os campos da planilha (BL-M12-01) ──

/// <summary>Os campos do cadastro que uma coluna da planilha pode preencher: os do S1 (§5 da spec), sem o endereço.</summary>
public enum CampoImportacao
{
    AlunoNome = 10,
    AlunoNascimento = 11,
    AlunoCpf = 12,
    AlunoCategoria = 13,
    Turma = 20,
    InicioNaTurma = 21,
    Mensalidade = 22,
    Alergias = 30,
    Restricoes = 31,
    ObservacoesMedicas = 32,
    EmergenciaNome = 33,
    EmergenciaTelefone = 34,
    EmergenciaParentesco = 35,
    ConsentimentoDados = 40,
    ConsentimentoImagem = 41,
    Responsavel1Nome = 50,
    Responsavel1Cpf = 51,
    Responsavel1Celular = 52,
    Responsavel1Email = 53,
    Responsavel1Parentesco = 54,
    Responsavel2Nome = 60,
    Responsavel2Cpf = 61,
    Responsavel2Celular = 62,
    Responsavel2Email = 63,
    Responsavel2Parentesco = 64
}

public enum GrupoCampo
{
    Aluno = 10,
    Matricula = 20,
    Saude = 30,
    Consentimento = 40,
    Responsavel1 = 50,
    Responsavel2 = 60
}

/// <param name="Rotulo">O nome da coluna no modelo para baixar (e o primeiro apelido reconhecido).</param>
/// <param name="Obrigatorio">Sem esta coluna mapeada, a conferência não abre.</param>
/// <param name="Sensivel">Dado de saúde: nunca aparece na conferência nem nos exemplos do mapeamento (BL-M12-05, R5).</param>
/// <param name="Apelidos">Outros nomes de cabeçalho reconhecidos, já normalizados (minúsculas, sem acento).</param>
public sealed record DefinicaoCampo(CampoImportacao Campo, GrupoCampo Grupo, string Rotulo, string Exemplo, bool Obrigatorio, bool Sensivel,
    IReadOnlyList<string> Apelidos);

/// <summary>
/// O catálogo dos campos da importação, com os apelidos de cabeçalho que o mapeamento reconhece sozinho. A planilha do
/// modelo usa os rótulos, então o modelo preenchido é mapeado inteiro; a planilha da escolinha pede, no máximo, o acerto
/// de algumas colunas.
/// </summary>
public static class CatalogoCamposImportacao
{
    public static readonly IReadOnlyList<DefinicaoCampo> Todos =
    [
        new(CampoImportacao.AlunoNome, GrupoCampo.Aluno, "Nome do aluno", "Pedro Lima", true, false,
            ["aluno", "nome aluno", "nome", "atleta", "nome do atleta", "nome completo", "nome completo do aluno"]),
        new(CampoImportacao.AlunoNascimento, GrupoCampo.Aluno, "Data de nascimento", "12/04/2015", true, false,
            ["nascimento", "data nascimento", "dt nascimento", "dt nasc", "data nasc", "nasc", "data de nasc"]),
        new(CampoImportacao.AlunoCpf, GrupoCampo.Aluno, "CPF do aluno", "", false, false, ["cpf aluno", "cpf atleta", "cpf do atleta"]),
        new(CampoImportacao.AlunoCategoria, GrupoCampo.Aluno, "Categoria", "Sub-11", false, false, []),
        new(CampoImportacao.Turma, GrupoCampo.Matricula, "Turma", "Sub-11", true, false, ["turma do aluno", "equipe", "grupo"]),
        new(CampoImportacao.InicioNaTurma, GrupoCampo.Matricula, "Início na turma", "", false, false,
            ["inicio", "data de inicio", "data inicio", "data de matricula", "data matricula", "matricula", "desde"]),
        new(CampoImportacao.Mensalidade, GrupoCampo.Matricula, "Mensalidade", "150,00", false, false,
            ["valor", "valor da mensalidade", "valor mensalidade", "mensal"]),
        new(CampoImportacao.Alergias, GrupoCampo.Saude, "Alergias", "", false, true, ["alergia"]),
        new(CampoImportacao.Restricoes, GrupoCampo.Saude, "Restrições", "", false, true, ["restricao", "restricoes medicas", "restricao medica"]),
        new(CampoImportacao.ObservacoesMedicas, GrupoCampo.Saude, "Observações médicas", "", false, true,
            ["obs medicas", "observacao medica", "saude", "observacoes de saude", "informacoes de saude", "problemas de saude"]),
        new(CampoImportacao.EmergenciaNome, GrupoCampo.Saude, "Contato de emergência", "", false, false,
            ["emergencia", "contato emergencia", "nome do contato de emergencia"]),
        new(CampoImportacao.EmergenciaTelefone, GrupoCampo.Saude, "Telefone de emergência", "", false, false,
            ["telefone emergencia", "tel emergencia", "celular de emergencia", "telefone do contato de emergencia"]),
        new(CampoImportacao.EmergenciaParentesco, GrupoCampo.Saude, "Parentesco do contato de emergência", "", false, false,
            ["parentesco emergencia", "parentesco do contato"]),
        new(CampoImportacao.ConsentimentoDados, GrupoCampo.Consentimento, "Autoriza tratamento de dados", "sim", false, false,
            ["tratamento de dados", "autoriza dados", "consentimento de dados", "consentimento dados", "lgpd"]),
        new(CampoImportacao.ConsentimentoImagem, GrupoCampo.Consentimento, "Autoriza uso de imagem", "sim", false, false,
            ["uso de imagem", "autoriza imagem", "consentimento de imagem", "consentimento imagem", "imagem"]),
        new(CampoImportacao.Responsavel1Nome, GrupoCampo.Responsavel1, "Responsável 1", "Maria Lima", true, false,
            ["responsavel", "nome do responsavel", "nome do responsavel 1", "responsavel 1 nome", "nome responsavel", "responsavel financeiro"]),
        new(CampoImportacao.Responsavel1Cpf, GrupoCampo.Responsavel1, "CPF do responsável 1", "", false, false,
            ["cpf do responsavel", "cpf responsavel", "cpf responsavel 1", "cpf"]),
        new(CampoImportacao.Responsavel1Celular, GrupoCampo.Responsavel1, "Celular do responsável 1", "(43) 98888-7777", true, false,
            ["celular", "whatsapp", "telefone", "fone", "celular do responsavel", "telefone do responsavel", "whatsapp do responsavel",
             "celular responsavel", "celular responsavel 1", "telefone responsavel 1"]),
        new(CampoImportacao.Responsavel1Email, GrupoCampo.Responsavel1, "E-mail do responsável 1", "", false, false,
            ["email", "e mail", "email do responsavel", "email responsavel", "email responsavel 1"]),
        new(CampoImportacao.Responsavel1Parentesco, GrupoCampo.Responsavel1, "Parentesco do responsável 1", "", false, false,
            ["parentesco", "parentesco do responsavel", "parentesco responsavel 1", "grau de parentesco"]),
        new(CampoImportacao.Responsavel2Nome, GrupoCampo.Responsavel2, "Responsável 2", "", false, false,
            ["nome do responsavel 2", "responsavel 2 nome", "segundo responsavel", "nome responsavel 2", "2o responsavel", "2 responsavel",
             "nome do 2o responsavel"]),
        new(CampoImportacao.Responsavel2Cpf, GrupoCampo.Responsavel2, "CPF do responsável 2", "", false, false,
            ["cpf responsavel 2", "cpf 2", "cpf do 2o responsavel"]),
        new(CampoImportacao.Responsavel2Celular, GrupoCampo.Responsavel2, "Celular do responsável 2", "", false, false,
            ["celular responsavel 2", "telefone do responsavel 2", "telefone responsavel 2", "whatsapp do responsavel 2", "celular 2",
             "telefone 2", "whatsapp 2", "celular do 2o responsavel"]),
        new(CampoImportacao.Responsavel2Email, GrupoCampo.Responsavel2, "E-mail do responsável 2", "", false, false,
            ["email responsavel 2", "email 2", "e mail 2"]),
        new(CampoImportacao.Responsavel2Parentesco, GrupoCampo.Responsavel2, "Parentesco do responsável 2", "", false, false,
            ["parentesco responsavel 2", "parentesco 2"])
    ];

    private static readonly IReadOnlyDictionary<CampoImportacao, DefinicaoCampo> PorCampo = Todos.ToDictionary(d => d.Campo);

    public static DefinicaoCampo Obter(CampoImportacao campo) => PorCampo[campo];

    public static string Rotulo(GrupoCampo grupo) => grupo switch
    {
        GrupoCampo.Aluno => "Aluno",
        GrupoCampo.Matricula => "Matrícula",
        GrupoCampo.Saude => "Saúde (restrito)",
        GrupoCampo.Consentimento => "Consentimentos",
        GrupoCampo.Responsavel1 => "Responsável 1 (paga a mensalidade)",
        GrupoCampo.Responsavel2 => "Responsável 2",
        _ => grupo.ToString()
    };

    /// <summary>Como preencher a coluna (a aba de instruções do modelo e a tela de mapeamento).</summary>
    public static string Orientacao(CampoImportacao campo) => campo switch
    {
        CampoImportacao.AlunoNascimento or CampoImportacao.InicioNaTurma => "Data no formato dd/mm/aaaa.",
        CampoImportacao.AlunoCpf or CampoImportacao.Responsavel1Cpf or CampoImportacao.Responsavel2Cpf =>
            "Com ou sem pontos. O mesmo CPF em várias linhas (irmãos) vira um cadastro só.",
        CampoImportacao.Turma => "O nome da turma como está no sistema (lista abaixo). Nome diferente se escolhe na conferência.",
        CampoImportacao.Mensalidade => "Em reais. Vazio = a mensalidade da turma.",
        CampoImportacao.Alergias or CampoImportacao.Restricoes or CampoImportacao.ObservacoesMedicas =>
            "Dado de saúde: vai direto para a ficha, com acesso restrito, e não aparece na conferência.",
        CampoImportacao.EmergenciaTelefone or CampoImportacao.Responsavel1Celular or CampoImportacao.Responsavel2Celular =>
            "Com DDD. O celular do responsável é o WhatsApp da família.",
        CampoImportacao.ConsentimentoDados or CampoImportacao.ConsentimentoImagem =>
            "sim ou não. Vazio = fica pendente na ficha. Na confirmação, a gestão declara que a autorização foi dada.",
        CampoImportacao.Responsavel1Nome => "Obrigatório. É quem paga a mensalidade e recebe a cobrança.",
        CampoImportacao.Responsavel2Nome => "Opcional. Recebe os avisos pelo WhatsApp; precisa do celular.",
        CampoImportacao.Responsavel1Parentesco or CampoImportacao.Responsavel2Parentesco or CampoImportacao.EmergenciaParentesco =>
            "mãe, pai, avó, avô, tutor ou outro. Vazio = outro.",
        _ => Obter(campo).Obrigatorio ? "Obrigatório." : "Opcional."
    };

    /// <summary>
    /// "Data de Nascimento:" → "data de nascimento": minúsculas, sem acento, sem pontuação, espaços simples. O ordinal vira
    /// letra ("2º responsável" → "2o responsavel").
    /// </summary>
    public static string NormalizarCabecalho(string? cabecalho)
    {
        var texto = TextoHelper.NormalizarParaBusca(cabecalho).Replace('º', 'o').Replace('ª', 'a');
        var limpo = new string(texto.Select(c => char.IsLetterOrDigit(c) ? c : ' ').ToArray());
        return string.Join(' ', limpo.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    /// O mapeamento que o cabeçalho sugere: cada campo na primeira coluna cujo nome é o rótulo ou um apelido dele.
    /// Coluna sem correspondência fica de fora (a gestão escolhe na tela).
    /// </summary>
    public static Dictionary<CampoImportacao, int> Sugerir(IReadOnlyList<string> cabecalhos)
    {
        var mapa = new Dictionary<CampoImportacao, int>();
        var usadas = new HashSet<int>();
        // Primeiro os rótulos exatos, depois os apelidos: "CPF" solto não toma a coluna "CPF do aluno".
        foreach (var exato in new[] { true, false })
            foreach (var definicao in Todos)
            {
                if (mapa.ContainsKey(definicao.Campo)) continue;
                IReadOnlyList<string> nomes = exato ? [NormalizarCabecalho(definicao.Rotulo)] : definicao.Apelidos;
                for (var i = 0; i < cabecalhos.Count; i++)
                {
                    if (usadas.Contains(i) || !nomes.Contains(NormalizarCabecalho(cabecalhos[i]))) continue;
                    mapa[definicao.Campo] = i;
                    usadas.Add(i);
                    break;
                }
            }
        return mapa;
    }
}

// ── A planilha lida ──

/// <summary>Uma linha com dado: o número dela na planilha (para a gestão achar) e as células, em texto.</summary>
public sealed record LinhaLida(int Numero, IReadOnlyList<string?> Celulas);

/// <summary>A primeira aba da planilha: o cabeçalho (1ª linha com dado) e as linhas seguintes que têm algum dado.</summary>
public sealed record PlanilhaLida(IReadOnlyList<string> Cabecalhos, IReadOnlyList<LinhaLida> Linhas);

public sealed record ResultadoLeituraPlanilha(PlanilhaLida? Planilha, string? Erro);

/// <summary>Leitura e modelo da planilha (.xlsx), fora da Application (a biblioteca fica na Infrastructure).</summary>
public interface IPlanilhas
{
    /// <summary>Lê a primeira aba. Datas viram "dd/MM/aaaa" e números saem sem casas decimais inúteis.</summary>
    ResultadoLeituraPlanilha Ler(Stream arquivo);

    /// <summary>O modelo para baixar: a aba "Alunos" só com o cabeçalho, e a aba "Instruções" com cada coluna e as turmas.</summary>
    byte[] Modelo(IReadOnlyList<string> turmas);
}

// ── O que já existe no cadastro ──

public sealed record AlunoExistente(Guid Id, string Nome, string NomeBusca, DateOnly Nascimento, string? Cpf);

public sealed record ResponsavelExistente(Guid Id, string Nome, string NomeBusca, string? Cpf, string? Telefone, Guid? PessoaId);

public sealed record TurmaExistente(Guid Id, string Nome, decimal Mensalidade, int Capacidade, int Ocupadas);

/// <summary>A base da escolinha que a conferência compara com a planilha (a deduplicação, BL-M12-03).</summary>
public sealed record BaseExistente(
    IReadOnlyList<AlunoExistente> Alunos,
    IReadOnlyList<ResponsavelExistente> Responsaveis,
    IReadOnlyList<TurmaExistente> Turmas);

public interface ICargaRepositorio
{
    /// <summary>Os lotes mais recentes, sem o conteúdo (a lista não decifra planilha nenhuma).</summary>
    Task<IReadOnlyList<LoteNaLista>> ListarAsync(int limite, CancellationToken ct = default);

    Task<LoteNaLista?> ResumoAsync(Guid id, CancellationToken ct = default);

    /// <summary>O lote rastreado (para alterar).</summary>
    Task<ImportacaoLote?> ObterAsync(Guid id, CancellationToken ct = default);

    /// <summary>Os lotes abertos enviados antes da data (os que vencem sem confirmação).</summary>
    Task<IReadOnlyList<ImportacaoLote>> AbertosAntesDeAsync(DateTime limite, CancellationToken ct = default);

    void Adicionar(ImportacaoLote lote);

    Task<BaseExistente> BaseAsync(CancellationToken ct = default);

    /// <exception cref="Comum.ConflitoDeConcorrenciaException">O lote ou um cadastro mudou enquanto a gestão conferia.</exception>
    Task SalvarAsync(CancellationToken ct = default);
}

// ── As decisões da conferência (BL-M12-04) ──

/// <summary>O que a gestão decidiu na conferência, guardado no lote entre uma tela e outra.</summary>
public sealed class DecisoesImportacao
{
    /// <summary>A turma do sistema para cada valor da coluna Turma que não bateu com o nome de uma turma (chave normalizada).</summary>
    public Dictionary<string, Guid> Turmas { get; set; } = [];

    /// <summary>As linhas com aluno parecido (mesmo nome e nascimento) que a gestão confirmou como outra criança.</summary>
    public List<int> AlunosNovos { get; set; } = [];

    /// <summary>Para cada responsável da planilha com cadastro parecido: o cadastro a reaproveitar, ou vazio para criar outro.</summary>
    public Dictionary<string, Guid?> Responsaveis { get; set; } = [];
}

// ── O plano da importação (a conferência) ──

public enum ClasseLinha
{
    Apta = 10,
    ComAviso = 20,
    Bloqueada = 30
}

/// <summary>Um responsável da planilha: um cadastro novo ou um existente reaproveitado, com as linhas em que aparece.</summary>
/// <param name="Chave">O CPF, ou o nome com o celular: o mesmo responsável em várias linhas (irmãos) vira um cadastro só.</param>
/// <param name="ExistenteId">O cadastro reaproveitado (mesmo CPF, ou escolhido pela gestão entre os parecidos).</param>
/// <param name="Parecidos">Cadastros com o mesmo celular ou o mesmo nome, sem o mesmo CPF: a gestão decide (D33).</param>
public sealed record ResponsavelPlanejado(
    string Chave,
    string Nome,
    string? Cpf,
    string? Telefone,
    string? Email,
    Guid? ExistenteId,
    string? ExistenteNome,
    bool ComAcesso,
    bool PorCpf,
    IReadOnlyList<ResponsavelExistente> Parecidos,
    IReadOnlyList<int> Linhas);

/// <summary>O vínculo de uma linha com um responsável planejado.</summary>
public sealed record VinculoPlanejado(string Chave, Parentesco Parentesco, bool Pagador);

/// <summary>Uma linha conferida: a classe, os motivos e o que ela cria. A saúde só diz se veio (BL-M12-05).</summary>
public sealed record LinhaPlanejada(
    int Numero,
    string Aluno,
    string? Turma,
    ClasseLinha Classe,
    IReadOnlyList<string> Bloqueios,
    IReadOnlyList<string> Avisos,
    bool Importar,
    bool AlunoParecido,
    bool SaudeInformada,
    bool ContatoEmergenciaInformado,
    IReadOnlyList<VinculoPlanejado> Vinculos,
    DadosDaLinha? Dados);

/// <summary>Os dados já lidos e validados de uma linha que pode entrar (só existe quando a linha não está bloqueada).</summary>
public sealed record DadosDaLinha(
    string Nome,
    DateOnly Nascimento,
    string? Cpf,
    string? Categoria,
    Guid TurmaId,
    DateOnly Inicio,
    decimal Mensalidade,
    string? Alergias,
    string? Restricoes,
    string? ObservacoesMedicas,
    string? EmergenciaNome,
    string? EmergenciaTelefone,
    string? EmergenciaParentesco,
    bool? ConsentimentoDados,
    bool? ConsentimentoImagem);

/// <summary>Um valor da coluna Turma que não é o nome de nenhuma turma ativa: a gestão escolhe a turma na conferência.</summary>
public sealed record TurmaDaPlanilha(string Chave, string Valor, int Linhas, Guid? Escolhida);

public sealed record PlanoImportacao(
    IReadOnlyList<LinhaPlanejada> Linhas,
    IReadOnlyList<ResponsavelPlanejado> Responsaveis,
    IReadOnlyList<TurmaDaPlanilha> TurmasSemCorrespondencia)
{
    public IEnumerable<LinhaPlanejada> Importadas => Linhas.Where(l => l.Importar);
    public int AlunosNovos => Linhas.Count(l => l.Importar);
    public int NaoImportadas => Linhas.Count - AlunosNovos;

    /// <summary>Os responsáveis que entram: os das linhas importadas.</summary>
    public IReadOnlyList<ResponsavelPlanejado> ResponsaveisUsados =>
        Responsaveis.Where(r => Importadas.Any(l => l.Vinculos.Any(v => v.Chave == r.Chave))).ToList();

    public int ResponsaveisNovos => ResponsaveisUsados.Count(r => r.ExistenteId is null);
    public int ResponsaveisReaproveitados => ResponsaveisUsados.Count(r => r.ExistenteId is not null);
    public int Vinculos => Importadas.Sum(l => l.Vinculos.Count);
    public bool TemConsentimento => Importadas.Any(l => l.Dados!.ConsentimentoDados is not null || l.Dados.ConsentimentoImagem is not null);

    /// <summary>
    /// O que a gestão viu na conferência: a confirmação só grava se o plano recalculado na hora for o mesmo (a base pode ter
    /// mudado entre uma tela e outra, regra de ouro 4).
    /// </summary>
    public string Assinatura =>
        $"{AlunosNovos}.{ResponsaveisNovos}.{ResponsaveisReaproveitados}.{Vinculos}." +
        string.Join(',', Importadas.Select(l => l.Numero));
}

// ── Telas ──

public sealed record LoteNaLista(Guid Id, string Arquivo, SituacaoImportacao Situacao, int Linhas, DateTime EnviadoEm, string EnviadoPor,
    DateTime? EncerradoEm, string? EncerradoPor, int AlunosCriados, int ResponsaveisCriados, int ResponsaveisReaproveitados,
    int NaoImportadas);

/// <summary>Uma coluna da planilha no mapeamento: o cabeçalho, até três exemplos (nunca de campo de saúde) e o campo escolhido.</summary>
public sealed record ColunaDaPlanilha(int Indice, string Cabecalho, IReadOnlyList<string> Exemplos, int Preenchidas, CampoImportacao? Campo);

public sealed record MapeamentoLote(Guid Id, string Arquivo, int Linhas, IReadOnlyList<ColunaDaPlanilha> Colunas,
    IReadOnlyList<CampoImportacao> Faltando);

/// <param name="Mapeados">Os campos que têm coluna (sem a coluna de autorização, os consentimentos ficam pendentes nas fichas).</param>
public sealed record ConferenciaLote(Guid Id, string Arquivo, PlanoImportacao Plano, IReadOnlyList<TurmaExistente> Turmas,
    IReadOnlyList<CampoImportacao> Faltando, IReadOnlyCollection<CampoImportacao> Mapeados);
