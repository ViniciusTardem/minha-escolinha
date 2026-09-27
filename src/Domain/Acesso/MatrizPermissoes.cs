namespace MinhaEscolinha.Domain.Acesso;

/// <summary>Recursos da matriz de permissões (spec §7). A ordem e os nomes seguem a tabela da especificação.</summary>
public enum Recurso
{
    /// <summary>Cadastro de alunos e responsáveis.</summary>
    CadastroAlunosResponsaveis = 10,

    /// <summary>Documentos e observações médicas do aluno — dado sensível (R5, RF-LGP-003).</summary>
    DocumentosObservacoesMedicas = 20,

    /// <summary>Registro de presença.</summary>
    RegistroPresenca = 30,

    /// <summary>Configuração da escolinha: dados, modalidades, parâmetros de regra e créditos de mensagem.</summary>
    ConfiguracaoEscolinha = 40,

    /// <summary>Financeiro da escolinha, inclusive a conta de recebimento Asaas.</summary>
    FinanceiroEscolinha = 50,

    /// <summary>Remuneração própria do professor.</summary>
    RemuneracaoPropriaProfessor = 60,

    /// <summary>Pagamentos do próprio filho.</summary>
    PagamentosProprioFilho = 70,

    /// <summary>Criar evento e convocar.</summary>
    EventosConvocacao = 80,

    /// <summary>Responder convocação (gestor e professor: só registrar a resposta que veio por fora).</summary>
    ResponderConvocacao = 90,

    /// <summary>Cadastro de professores e remuneração.</summary>
    CadastroProfessores = 100
}

/// <summary>T total · P parcial (só o escopo próprio) · L somente leitura (do escopo próprio) · — sem acesso.</summary>
public enum NivelAcesso
{
    Nenhum = 0,
    Leitura = 10,
    Parcial = 20,
    Total = 30
}

public enum Operacao
{
    Ler = 10,
    Alterar = 20
}

/// <summary>
/// Matriz de permissões da spec §7, em código (BL-M01-03). Nega por padrão: recurso ou papel fora da tabela = sem acesso.
/// O escopo próprio (as turmas do professor, os filhos com vínculo verificado do responsável) é resolvido pela aplicação;
/// aqui fica só a regra "que nível este papel tem" e "o que o nível permite".
/// </summary>
public static class MatrizPermissoes
{
    private static readonly Dictionary<(Recurso, PapelAtuacao), NivelAcesso> Tabela = new()
    {
        [(Recurso.CadastroAlunosResponsaveis, PapelAtuacao.Gestor)] = NivelAcesso.Total,
        [(Recurso.CadastroAlunosResponsaveis, PapelAtuacao.Professor)] = NivelAcesso.Parcial,
        [(Recurso.CadastroAlunosResponsaveis, PapelAtuacao.Responsavel)] = NivelAcesso.Parcial,

        [(Recurso.DocumentosObservacoesMedicas, PapelAtuacao.Gestor)] = NivelAcesso.Total,
        [(Recurso.DocumentosObservacoesMedicas, PapelAtuacao.Professor)] = NivelAcesso.Parcial,
        [(Recurso.DocumentosObservacoesMedicas, PapelAtuacao.Responsavel)] = NivelAcesso.Parcial,

        [(Recurso.RegistroPresenca, PapelAtuacao.Gestor)] = NivelAcesso.Total,
        [(Recurso.RegistroPresenca, PapelAtuacao.Professor)] = NivelAcesso.Parcial,
        [(Recurso.RegistroPresenca, PapelAtuacao.Responsavel)] = NivelAcesso.Leitura,

        [(Recurso.ConfiguracaoEscolinha, PapelAtuacao.Gestor)] = NivelAcesso.Total,

        [(Recurso.FinanceiroEscolinha, PapelAtuacao.Gestor)] = NivelAcesso.Total,

        [(Recurso.RemuneracaoPropriaProfessor, PapelAtuacao.Gestor)] = NivelAcesso.Total,
        [(Recurso.RemuneracaoPropriaProfessor, PapelAtuacao.Professor)] = NivelAcesso.Parcial,

        [(Recurso.PagamentosProprioFilho, PapelAtuacao.Gestor)] = NivelAcesso.Total,
        [(Recurso.PagamentosProprioFilho, PapelAtuacao.Responsavel)] = NivelAcesso.Parcial,

        [(Recurso.EventosConvocacao, PapelAtuacao.Gestor)] = NivelAcesso.Total,
        [(Recurso.EventosConvocacao, PapelAtuacao.Professor)] = NivelAcesso.Parcial,

        [(Recurso.ResponderConvocacao, PapelAtuacao.Gestor)] = NivelAcesso.Parcial,
        [(Recurso.ResponderConvocacao, PapelAtuacao.Professor)] = NivelAcesso.Parcial,
        [(Recurso.ResponderConvocacao, PapelAtuacao.Responsavel)] = NivelAcesso.Parcial,

        [(Recurso.CadastroProfessores, PapelAtuacao.Gestor)] = NivelAcesso.Total
    };

    public static NivelAcesso Nivel(Recurso recurso, PapelAtuacao papel) =>
        Tabela.TryGetValue((recurso, papel), out var nivel) ? nivel : NivelAcesso.Nenhum;

    /// <summary>Maior nível entre os papéis (a pessoa acumula papéis na mesma escolinha).</summary>
    public static NivelAcesso MaiorNivel(Recurso recurso, IEnumerable<PapelAtuacao> papeis) =>
        papeis.Select(p => Nivel(recurso, p)).DefaultIfEmpty(NivelAcesso.Nenhum).Max();

    /// <summary>
    /// O que o nível permite. <paramref name="alvoNoEscopo"/> diz se o registro alvo está no escopo próprio do papel
    /// (a turma do professor, o filho verificado do responsável). Sem alvo (a tela de lista), passe <c>true</c>:
    /// a lista é que filtra pelo escopo.
    /// </summary>
    public static bool Permite(NivelAcesso nivel, Operacao operacao, bool alvoNoEscopo) => nivel switch
    {
        NivelAcesso.Total => true,
        NivelAcesso.Parcial => alvoNoEscopo,
        NivelAcesso.Leitura => operacao == Operacao.Ler && alvoNoEscopo,
        _ => false
    };

    /// <summary>Recurso de dado sensível (R5): só quem tem necessidade operacional, e o campo sai do view model dos demais.</summary>
    public static bool Sensivel(Recurso recurso) => recurso == Recurso.DocumentosObservacoesMedicas;

    /// <summary>Todos os pares da tabela (para os testes e para a tela de ajuda).</summary>
    public static IEnumerable<(Recurso Recurso, PapelAtuacao Papel, NivelAcesso Nivel)> Entradas() =>
        Tabela.Select(e => (e.Key.Item1, e.Key.Item2, e.Value));
}
