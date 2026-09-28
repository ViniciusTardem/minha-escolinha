using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MinhaEscolinha.Application.Acesso;
using MinhaEscolinha.Application.Alunos;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Configuracao;
using MinhaEscolinha.Application.Equipe;
using MinhaEscolinha.Application.Eventos;
using MinhaEscolinha.Application.Financeiro;
using MinhaEscolinha.Application.Mensageria;
using MinhaEscolinha.Application.Plataforma;
using MinhaEscolinha.Application.Presencas;
using MinhaEscolinha.Application.Turmas;

namespace MinhaEscolinha.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddAplicacao(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<LoginRequisicaoValidador>(ServiceLifetime.Scoped);

        // Contexto da escolinha: um por escopo (requisição, job, execução da plataforma).
        services.AddScoped<IContextoEscolinha, ContextoEscolinha>();
        services.AddScoped<IAutorizacao, Autorizacao>();
        // Escopo próprio: turmas, alunos e sessões do professor (S1); filhos verificados do responsável (S2).
        services.TryAddScoped<IResolvedorEscopo, ResolvedorEscopoCadastros>();
        services.AddScoped<IEscopoDeLeitura, EscopoDeLeitura>();

        // M01 — acesso
        services.AddScoped<IServicoAutenticacao, ServicoAutenticacao>();

        // M02 — configuração da escolinha
        services.AddScoped<RegistroDeAlteracoes>();
        services.AddScoped<IParametros, LeitorParametros>();
        services.AddScoped<ServicoPlataforma>();
        services.AddScoped<CriadorEscolinha>();
        services.AddScoped<ServicoEscolinha>();
        services.AddScoped<ServicoModalidades>();
        services.AddScoped<ServicoParametros>();
        services.AddScoped<ReconciliadorParametros>();
        services.AddScoped<ServicoContaAsaas>();
        services.AddScoped<ServicoCreditos>();

        // S1 — cadastro: equipe (M08), turmas e sessões (M04), alunos e responsáveis (M03)
        services.AddScoped<ServicoProfessores>();
        services.AddScoped<ServicoLocais>();
        services.AddScoped<ServicoSessoes>();
        services.AddScoped<ServicoTurmas>();
        services.AddScoped<ServicoAgenda>();
        services.AddScoped<IOcupacaoAgenda>(sp => sp.GetRequiredService<ServicoAgenda>());
        services.AddScoped<PoliticaSaude>();
        services.AddScoped<ServicoAlunos>();
        services.AddScoped<ServicoMatriculas>();
        services.AddScoped<ServicoResponsaveis>();
        services.AddScoped<ServicoConsentimentos>();

        // S2 — motor único de mensagens (M09), convites e ativação (M01), portal do responsável (começo do M10)
        services.AddScoped<EmissorGatilhos>();
        services.AddScoped<ServicoEnvioMensagens>();
        services.AddScoped<ServicoRetornoMensagens>();
        services.AddScoped<ProcessadorWebhooksWhatsApp>();
        services.AddScoped<VarreduraMensagens>();
        services.AddScoped<ServicoMensagens>();
        services.AddScoped<ServicoTemplates>();
        services.AddScoped<IValidadorOrigemMensagem, ValidadorOrigemConvite>();
        services.AddScoped<ServicoConvites>();
        services.AddScoped<ServicoVerificacaoVinculos>();
        services.AddScoped<ServicoAtivacaoConvite>();
        services.AddScoped<AtivacaoNaEscolinha>();
        services.AddScoped<ServicoPortal>();

        // S3 — chamada na quadra (M05), alerta de faltas pelo motor e risco de evasão
        services.AddScoped<ServicoChamada>();
        services.AddScoped<AvaliacaoFaltas>();
        services.AddScoped<ServicoPresenca>();
        services.AddScoped<IRiscoEvasao>(sp => sp.GetRequiredService<ServicoPresenca>());
        services.AddScoped<IValidadorOrigemMensagem, ValidadorOrigemAlertaFaltas>();

        // S4 — mensalidade (M06): projeção pela matrícula, emissão e conciliação na Asaas da escolinha, régua pelo motor
        services.AddScoped<ServicoAcessoAsaas>();
        services.AddScoped<EmissaoCobrancas>();
        services.AddScoped<ConciliacaoCobrancas>();
        services.AddScoped<ProcessadorWebhooksAsaas>();
        services.AddScoped<VarreduraCobrancas>();
        services.AddScoped<OperacoesAsaas>();
        services.AddScoped<RotinaCobrancas>();
        services.AddScoped<EfeitosFinanceirosDoCadastro>();
        services.AddScoped<ServicoMensalidades>();
        services.AddScoped<IValidadorOrigemMensagem, ValidadorOrigemCobranca>();

        // S5 — eventos e convocações (M07): o jogo, a montagem do fim de semana, o disparo pelo motor e as respostas pelo botão
        services.AddScoped<TextosEventos>();
        services.AddScoped<ServicoEventos>();
        services.AddScoped<ServicoConvocacoes>();
        services.AddScoped<RespostasConvocacao>();
        services.AddScoped<IReceptorResposta, ReceptorRespostaConvocacao>();
        services.AddScoped<IReceptorResposta, ReceptorRespostaLembreteConvocacao>();
        services.AddScoped<RotinaConvocacoes>();
        services.AddScoped<EfeitosConvocacaoDoCadastro>();
        services.AddScoped<IValidadorOrigemMensagem, ValidadorOrigemConvocacao>();
        services.AddScoped<IValidadorOrigemMensagem, ValidadorOrigemEvento>();
        // O tempo real é da web (SignalR); sem ela (testes, jobs), ninguém é avisado.
        services.TryAddSingleton<INotificadorConvocacoes, NotificadorConvocacoesNulo>();

        // Plataforma — a LDNA vende pacotes de créditos às escolinhas (Stripe da LDNA; mensalidade de aluno segue na Asaas)
        services.AddScoped<ServicoPacotesCreditos>();
        services.AddScoped<ServicoCompraCreditos>();
        services.AddScoped<ProcessadorPagamentosStripe>();
        services.AddScoped<ConferenciaCompraCreditos>();
        return services;
    }
}
