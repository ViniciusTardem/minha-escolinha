using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MinhaEscolinha.Application.Acesso;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Configuracao;

namespace MinhaEscolinha.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddAplicacao(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<LoginRequisicaoValidador>(ServiceLifetime.Scoped);

        // Contexto da escolinha: um por escopo (requisição, job, execução da plataforma).
        services.AddScoped<IContextoEscolinha, ContextoEscolinha>();
        services.AddScoped<IAutorizacao, Autorizacao>();
        // Cada fatia que cria entidade com escopo (turma, aluno) registra o resolvedor real antes; até lá, nega.
        services.TryAddScoped<IResolvedorEscopo, ResolvedorEscopoSemCadastros>();

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
        return services;
    }
}
