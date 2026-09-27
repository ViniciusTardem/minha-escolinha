using MinhaEscolinha.Domain.Acesso;

namespace MinhaEscolinha.Web.Infraestrutura;

public enum SituacaoModulo
{
    /// <summary>Pronto para uso.</summary>
    Disponivel,

    /// <summary>Tela existe, parte do conteúdo chega nas próximas fatias.</summary>
    EmConstrucao,

    /// <summary>Só a página explicativa ("em breve").</summary>
    Planejado
}

/// <summary>Um item de navegação do painel.</summary>
/// <param name="Recurso">Recurso da matriz que o menu exige (nulo = qualquer pessoa com escolinha no contexto).</param>
public record Modulo(
    string Chave,
    string Titulo,
    string Icone,
    string Secao,
    string Resumo,
    string Descricao,
    string[] Recursos,
    string Fatia,
    SituacaoModulo Situacao,
    string Url,
    Recurso? Recurso = null,
    bool SoPlataforma = false,
    bool ForaDoMenu = false);

/// <summary>
/// Fonte única da estrutura do painel: menu lateral, busca rápida (Ctrl+K), páginas "em breve" e o roteiro do Painel
/// saem daqui (padrão do AlternativaDepartamentoPessoal). Quando a fatia de um módulo ficar pronta, troque a
/// <see cref="Modulo.Situacao"/> e aponte a <see cref="Modulo.Url"/> para o controller real.
/// </summary>
public static class CatalogoModulos
{
    public const string SecaoOperacao = "Operação";
    public const string SecaoFinanceiro = "Financeiro";
    public const string SecaoComunicacao = "Comunicação";
    public const string SecaoEscolinha = "Escolinha";
    public const string SecaoPlataforma = "Plataforma";

    public static readonly Modulo Painel = new(
        "painel", "Painel", "ki-element-11", "",
        "Visão geral da escolinha: o que já está configurado e o que vem a seguir.",
        "O ponto de partida: a situação da configuração da escolinha e o roteiro das próximas entregas do piloto.",
        ["Situação da configuração", "Próximas entregas do piloto"],
        "S0", SituacaoModulo.Disponivel, "/");

    public static readonly IReadOnlyList<Modulo> Todos =
    [
        Painel,

        new("turmas", "Turmas", "ki-category", SecaoOperacao,
            "Turmas com horários, local, professor, vagas e mensalidade.",
            "Cada turma tem modalidade, categoria, horários, local, capacidade, professor responsável e mensalidade. " +
            "As sessões de treino do mês são geradas a partir dos horários.",
            ["Horários e local de treino", "Ocupação de vagas", "Sessões de treino geradas pelos horários", "Agenda do professor e da quadra"],
            "S1", SituacaoModulo.Disponivel, "/Turmas", Recurso.CadastroAlunosResponsaveis),

        new("alunos", "Alunos", "ki-people", SecaoOperacao,
            "Alunos, responsáveis e matrículas, sem cadastro duplicado.",
            "Ficha do aluno com responsáveis (um cadastro para irmãos), matrículas em uma ou mais turmas, consentimentos e dados de saúde " +
            "restritos a quem precisa.",
            ["Responsável reaproveitado entre irmãos", "Matrícula em mais de uma turma", "Consentimento de imagem e de dados", "Desligamento com motivo"],
            "S1", SituacaoModulo.Disponivel, "/Alunos", Recurso.CadastroAlunosResponsaveis),

        new("responsaveis", "Responsáveis", "ki-users", SecaoOperacao,
            "Pais, mães e responsáveis: um cadastro por pessoa, com os filhos.",
            "Cada responsável tem um cadastro só na escolinha, reaproveitado entre irmãos. O vínculo com o aluno diz quem paga, " +
            "quem recebe a cobrança e quem recebe as notificações.",
            ["Um cadastro por pessoa", "Filhos e vínculos", "Contato do WhatsApp"],
            "S1", SituacaoModulo.Disponivel, "/Responsaveis", Recurso.CadastroAlunosResponsaveis),

        new("agenda", "Agenda", "ki-calendar", SecaoOperacao,
            "Treinos e jogos por turma, professor e quadra.",
            "Calendário das sessões de treino e dos jogos, com alerta de conflito de agenda do professor e do local.",
            ["Por turma, professor ou quadra", "Filtro por período e modalidade", "Alerta de conflito do professor e do local"],
            "S1", SituacaoModulo.Disponivel, "/Agenda", Recurso.RegistroPresenca),

        new("chamada", "Chamada", "ki-check-squared", SecaoOperacao,
            "Chamada na quadra, em poucos toques, mesmo sem internet.",
            "O professor marca presença e falta pelo celular. Faltas que atingem a regra avisam o responsável na hora, e a frequência baixa " +
            "sinaliza risco de evasão.",
            ["Presença em um toque", "Funciona sem sinal", "Alerta de faltas ao responsável", "Risco de evasão"],
            "S3", SituacaoModulo.Planejado, "/chamada", Recurso.RegistroPresenca),

        new("jogos", "Jogos e convocações", "ki-cup", SecaoOperacao,
            "Os jogos do fim de semana e as convocações em um fluxo só.",
            "Monte os jogos do fim de semana, escolha os convocados e dispare tudo de uma vez. As respostas do WhatsApp aparecem no painel " +
            "sem atualizar a tela.",
            ["Vários jogos num fluxo só", "Confirmação pelo WhatsApp em tempo real", "Lembrete a quem não respondeu", "Cancelamento em um toque"],
            "S5", SituacaoModulo.Planejado, "/jogos", Recurso.EventosConvocacao),

        new("mensalidades", "Mensalidades", "ki-dollar", SecaoFinanceiro,
            "Cobrança recorrente pela Asaas, lembretes e conciliação automática.",
            "A mensalidade é emitida sozinha (Pix, boleto ou cartão), o lembrete sai antes do vencimento e o pagamento é conciliado sem ninguém " +
            "conferir na mão.",
            ["Emissão automática", "Régua de cobrança", "Conciliação automática", "Fila de pagamentos divergentes"],
            "S4", SituacaoModulo.Planejado, "/mensalidades", Recurso.FinanceiroEscolinha),

        new("mensagens", "Mensagens", "ki-message-text-2", SecaoComunicacao,
            "Tudo o que saiu pelo WhatsApp e pelo portal, com entrega e leitura.",
            "Registro de cada mensagem: enviada, entregue, lida ou com falha, e o consumo de créditos.",
            ["Log de entrega por mensagem", "Templates por categoria", "Convites de acesso às famílias"],
            "S2", SituacaoModulo.Planejado, "/mensagens", Recurso.ConfiguracaoEscolinha),

        new("escolinha", "Dados da escolinha", "ki-home-3", SecaoEscolinha,
            "Identificação, endereço e contato da escolinha.",
            "Nome, razão social, CNPJ, endereço e contato — o nome aparece nas mensagens às famílias.",
            ["Identificação e contato", "Endereço"],
            "S0", SituacaoModulo.Disponivel, "/Escolinha", Recurso.ConfiguracaoEscolinha),

        new("professores", "Professores", "ki-teacher", SecaoEscolinha,
            "Professores, disponibilidade semanal e indisponibilidades.",
            "Dados e contato de cada professor, as turmas dele e a escala de disponibilidade: treino marcado fora dela ou num horário " +
            "já ocupado gera alerta.",
            ["Turmas sob responsabilidade", "Disponibilidade semanal", "Indisponibilidades pontuais", "Alertas de conflito"],
            "S1", SituacaoModulo.Disponivel, "/Professores", Recurso.CadastroProfessores),

        new("locais", "Locais de treino", "ki-geolocation", SecaoEscolinha,
            "Quadras e locais onde as turmas treinam e jogam.",
            "Nome, endereço e capacidade de cada quadra ou local. A agenda mostra a ocupação de cada um e alerta dois treinos no mesmo horário.",
            ["Endereço e capacidade", "Turmas que treinam ali"],
            "S1", SituacaoModulo.Disponivel, "/Locais", Recurso.ConfiguracaoEscolinha),

        new("modalidades", "Modalidades", "ki-medal-star", SecaoEscolinha,
            "Modalidades oferecidas e o que cada uma usa (adversário, convocação...).",
            "Perfil e interruptores de cada modalidade: adversário, placar, classificação, convocação, graduação e apresentação.",
            ["Perfil da modalidade", "Interruptores de recurso", "Duração padrão do evento"],
            "S0", SituacaoModulo.Disponivel, "/Modalidades", Recurso.ConfiguracaoEscolinha),

        new("parametros", "Parâmetros de regra", "ki-setting-3", SecaoEscolinha,
            "Regras de faltas, cobrança, convocação e mensagens da escolinha.",
            "Tudo o que é regra sensível do negócio é configurável aqui, por escolinha, sem nova entrega de software.",
            ["Presença e evasão", "Financeiro", "Convocação", "Mensagens", "Matrícula e turmas"],
            "S0", SituacaoModulo.Disponivel, "/Parametros", Recurso.ConfiguracaoEscolinha),

        new("conta-recebimento", "Conta de recebimento", "ki-bank", SecaoEscolinha,
            "A conta Asaas da escolinha, onde caem as mensalidades.",
            "Sem a conta de recebimento vinculada nenhuma cobrança é emitida. A chave fica cifrada e trocar pede a sua senha.",
            ["Vincular ou trocar a conta", "Sandbox fora de produção"],
            "S0", SituacaoModulo.Disponivel, "/ContaRecebimento", Recurso.FinanceiroEscolinha),

        new("creditos", "Créditos de mensagem", "ki-wallet", SecaoEscolinha,
            "Saldo, histórico e compra de pacotes de créditos de WhatsApp.",
            "Cada mensagem de WhatsApp consome um crédito. Aqui ficam o saldo, cada concessão ou consumo e a compra de pacotes, " +
            "paga pelo Stripe (cartão, boleto ou Pix): os créditos entram sozinhos quando o pagamento é confirmado.",
            ["Saldo e plano", "Histórico de concessões e consumo", "Compra de pacotes pelo Stripe"],
            "S0", SituacaoModulo.Disponivel, "/Creditos", Recurso.ConfiguracaoEscolinha),

        new("importacao", "Importação", "ki-file-up", SecaoEscolinha,
            "A base de alunos e responsáveis a partir da planilha.",
            "Envie a planilha, confira as linhas (novas, reaproveitadas, com aviso ou bloqueadas) e só então confirme.",
            ["Validação linha a linha", "Responsáveis sem duplicidade", "Conferência antes de gravar"],
            "S7", SituacaoModulo.Planejado, "/importacao", Recurso.ConfiguracaoEscolinha),

        new("plataforma", "Escolinhas", "ki-grid", SecaoPlataforma,
            "Todas as escolinhas da plataforma: criar, conceder créditos, entrar como equipe.",
            "Área da equipe LDNA: cria a escolinha junto com o 1º gestor, concede créditos de mensagem e dá acesso da equipe (auditado).",
            ["Nova escolinha com o 1º gestor", "Créditos de mensagem", "Acesso da equipe como gestor"],
            "S0", SituacaoModulo.Disponivel, "/Plataforma", SoPlataforma: true),

        new("pacotes", "Pacotes de créditos", "ki-package", SecaoPlataforma,
            "Pacotes de créditos que as escolinhas compram pelo Stripe da LDNA.",
            "Nome, quantidade de créditos e preço de cada pacote à venda, e a integração com o Stripe: modo, endereço do " +
            "webhook e os últimos eventos recebidos.",
            ["Pacotes à venda", "Integração com o Stripe"],
            "Extra", SituacaoModulo.Disponivel, "/Pacotes", SoPlataforma: true),

        new("jobs", "Jobs", "ki-timer", SecaoPlataforma,
            "Painel do Hangfire: tudo o que roda em segundo plano.",
            "Jobs agendados e recorrentes, falhas e retentativas, servidores ativos.",
            ["Jobs agendados e recorrentes", "Falhas e retentativas", "Servidores ativos"],
            "F0", SituacaoModulo.Disponivel, "/jobs", SoPlataforma: true)
    ];

    public static readonly string[] Secoes = ["", SecaoOperacao, SecaoFinanceiro, SecaoComunicacao, SecaoEscolinha, SecaoPlataforma];

    /// <summary>Módulos que por enquanto só têm a página explicativa (servida pelo ModulosController).</summary>
    public static IEnumerable<string> ChavesPaginaExplicativa =>
        Todos.Where(m => m.Situacao == SituacaoModulo.Planejado).Select(m => m.Chave);

    public static Modulo? Obter(string? chave) =>
        Todos.FirstOrDefault(m => string.Equals(m.Chave, chave, StringComparison.OrdinalIgnoreCase));

    /// <summary>O que aparece no menu: módulos da plataforma para a equipe; os da escolinha conforme a permissão.</summary>
    public static IEnumerable<Modulo> NoMenu(bool plataforma, bool temEscolinha, Func<Recurso, bool> pode) =>
        Todos.Where(m => !m.ForaDoMenu && (m.SoPlataforma
            ? plataforma
            : temEscolinha && (m.Recurso is not { } recurso || pode(recurso))));

    public static string RotuloSituacao(SituacaoModulo situacao) => situacao switch
    {
        SituacaoModulo.Disponivel => "Disponível",
        SituacaoModulo.EmConstrucao => "Em construção",
        _ => "Em breve"
    };
}
