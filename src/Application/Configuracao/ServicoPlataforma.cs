using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MinhaEscolinha.Application.Acesso;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Domain.Comum;
using MinhaEscolinha.Domain.Configuracao;
using MinhaEscolinha.Domain.Configuracao.Parametros;

namespace MinhaEscolinha.Application.Configuracao;

/// <summary>
/// Operações da equipe da plataforma (LDNA): criar a escolinha com o 1º gestor, conceder créditos e entrar numa
/// escolinha como gestor da equipe. Toda escrita numa escolinha roda num escopo com o contexto dela
/// (<see cref="IExecutorEscolinha"/>) — a plataforma não tem acesso implícito a dado nenhum.
/// </summary>
public class ServicoPlataforma(
    IAcessoRepositorio acesso,
    IConsultasPlataforma consultas,
    IExecutorEscolinha executor,
    IContextoEscolinha contexto,
    IUsuarioAtual usuarioAtual,
    IValidator<NovaEscolinhaRequisicao> validadorNova,
    IValidator<ConcederCreditosRequisicao> validadorCreditos,
    ILogger<ServicoPlataforma> logger)
{
    /// <summary>Dados = <see cref="EscolinhaCriada"/> (a senha temporária do gestor aparece uma única vez).</summary>
    public async Task<ResultadoOperacao> CriarEscolinhaComGestorAsync(NovaEscolinhaRequisicao requisicao, CancellationToken ct = default)
    {
        if (!contexto.AdministradorPlataforma) return ResultadoOperacao.Erro("Só a equipe da plataforma cria escolinhas.");

        var validacao = await validadorNova.ValidateAsync(requisicao, ct);
        if (!validacao.IsValid) return ResultadoOperacao.Invalido(validacao);

        var login = Credencial.NormalizarLogin(requisicao.GestorEmail);
        var existente = await acesso.ObterCredencialPorLoginAsync(login, ct);
        if (existente is not null && !requisicao.ReaproveitarAcessoExistente)
            return ResultadoOperacao.Erro(
                $"O e-mail {login} já tem acesso ao sistema ({existente.Pessoa?.Nome}). Marque \"usar o acesso que já existe\" " +
                "para torná-lo gestor desta escolinha também.", nameof(NovaEscolinhaRequisicao.GestorEmail));
        if (existente is null && Cpf.Normalizar(requisicao.GestorCpf) is { } cpf
            && await acesso.ObterPessoaPorCpfAsync(cpf, ct) is not null)
            return ResultadoOperacao.Erro(
                "Esse CPF já pertence a outra pessoa com acesso ao sistema, com outro e-mail. Use o e-mail de acesso dela " +
                "e marque \"usar o acesso que já existe\", ou confira o CPF.", nameof(NovaEscolinhaRequisicao.GestorCpf));

        var escolinhaId = Guid.NewGuid();
        var criada = await executor.ExecutarAsync(escolinhaId,
            sp => sp.GetRequiredService<CriadorEscolinha>().CriarAsync(escolinhaId, requisicao, login, ct), ct);

        logger.LogInformation("Escolinha {Escolinha} ({Id}) criada por {Autor} com o gestor {Gestor}.",
            criada.Nome, criada.EscolinhaId, usuarioAtual.Login, login);
        return ResultadoOperacao.Ok($"Escolinha {criada.Nome} criada.", criada);
    }

    /// <summary>
    /// A pessoa da equipe passa a atuar como gestora da escolinha (para configurar o piloto junto com o gestor).
    /// Fica na trilha de auditoria da escolinha e pode ser revogado como qualquer vínculo.
    /// </summary>
    public async Task<ResultadoOperacao> VincularEquipeComoGestorAsync(Guid escolinhaId, CancellationToken ct = default)
    {
        if (!contexto.AdministradorPlataforma || usuarioAtual.Id is not { } pessoaId)
            return ResultadoOperacao.Erro("Só a equipe da plataforma usa este acesso.");
        if (await consultas.ObterEscolinhaAsync(escolinhaId, pessoaId, ct) is not { } escolinha)
            return ResultadoOperacao.Erro("Escolinha não encontrada.");

        await executor.ExecutarAsync(escolinhaId, async sp =>
        {
            var repositorioAcesso = sp.GetRequiredService<IAcessoRepositorio>();
            var registro = sp.GetRequiredService<RegistroDeAlteracoes>();
            var vinculos = await repositorioAcesso.VinculosNaEscolinhaAsync(pessoaId, ct);
            var gestor = vinculos.FirstOrDefault(v => v.Papel == PapelAtuacao.Gestor);
            if (gestor is null)
            {
                gestor = new VinculoAtuacao { Pessoa_ID = pessoaId, Papel = PapelAtuacao.Gestor };
                repositorioAcesso.AdicionarVinculo(gestor);
            }
            else if (!gestor.Ativo)
            {
                gestor.Reativar();
            }
            else
            {
                return true;
            }

            registro.Registrar(nameof(VinculoAtuacao), null, "Papel", null, "Gestor (equipe da plataforma)",
                "Acesso da equipe da plataforma para configurar a escolinha.");
            await repositorioAcesso.SalvarAsync(ct);
            return true;
        }, ct);

        logger.LogInformation("{Autor} passou a atuar como gestor da escolinha {Escolinha}.", usuarioAtual.Login, escolinha.Nome);
        return ResultadoOperacao.Ok($"Você agora atua como gestor em {escolinha.Nome}.");
    }

    public async Task<ResultadoOperacao> ConcederCreditosAsync(ConcederCreditosRequisicao requisicao, CancellationToken ct = default)
    {
        if (!contexto.AdministradorPlataforma) return ResultadoOperacao.Erro("Só a equipe da plataforma concede créditos.");

        var validacao = await validadorCreditos.ValidateAsync(requisicao, ct);
        if (!validacao.IsValid) return ResultadoOperacao.Invalido(validacao);
        if (await consultas.ObterEscolinhaAsync(requisicao.EscolinhaId, usuarioAtual.Id, ct) is not { } escolinha)
            return ResultadoOperacao.Erro("Escolinha não encontrada.");

        return await executor.ExecutarAsync(requisicao.EscolinhaId,
            sp => sp.GetRequiredService<ServicoCreditos>().LancarDaPlataformaAsync(requisicao, ct), ct);
    }
}

/// <summary>
/// Monta a escolinha nova e o 1º gestor num único SaveChanges — tudo ou nada. Roda no escopo aberto pelo
/// <see cref="IExecutorEscolinha"/>, com o contexto já na escolinha nova.
/// </summary>
public class CriadorEscolinha(
    IAcessoRepositorio acesso,
    IConfiguracaoRepositorio configuracao,
    RegistroDeAlteracoes registro,
    IHashSenha hashSenha)
{
    public async Task<EscolinhaCriada> CriarAsync(Guid escolinhaId, NovaEscolinhaRequisicao r, string login, CancellationToken ct)
    {
        var escolinha = new Escolinha { Escolinha_ID = escolinhaId };
        ServicoEscolinha.Aplicar(escolinha, r);
        configuracao.AdicionarEscolinha(escolinha);

        configuracao.AdicionarModalidade(Modalidade.Nova(r.NomeModalidade, r.PerfilModalidade));

        foreach (var definicao in CatalogoParametros.Todos)
            configuracao.AdicionarParametro(new ParametroEscolinha { Chave = definicao.Chave, Valor = definicao.ValorPadrao });

        configuracao.AdicionarContaCreditos(new ContaCreditos { Plano = ContaCreditos.PlanoPiloto, Saldo = 0 });

        string? senhaTemporaria = null;
        var credencial = await acesso.ObterCredencialPorLoginAsync(login, ct);
        Pessoa pessoa;
        if (credencial?.Pessoa is { } existente)
        {
            pessoa = existente;
        }
        else
        {
            senhaTemporaria = GeradorSenhaTemporaria.Gerar();
            pessoa = new Pessoa { Nome = r.GestorNome.Trim(), Cpf = Cpf.Normalizar(r.GestorCpf) };
            var nova = new Credencial { Login = login, Ativa = true };
            nova.DefinirSenha(hashSenha.Gerar(senhaTemporaria), trocarNoProximoAcesso: true);
            pessoa.Credencial = nova;
            acesso.AdicionarPessoa(pessoa);
        }

        acesso.AdicionarVinculo(new VinculoAtuacao { Pessoa = pessoa, Papel = PapelAtuacao.Gestor });
        registro.Registrar(nameof(Escolinha), escolinhaId, "Criação", null,
            $"Escolinha criada com o gestor {pessoa.Nome} ({login}) e a modalidade {r.NomeModalidade.Trim()}.");

        await configuracao.SalvarAsync(ct);
        return new EscolinhaCriada(escolinhaId, escolinha.Nome, pessoa.Nome, login, senhaTemporaria);
    }
}
