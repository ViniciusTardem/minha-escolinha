using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MinhaEscolinha.Application.Acesso;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Configuracao;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Domain.Configuracao;
using MinhaEscolinha.Domain.Configuracao.Parametros;
using MinhaEscolinha.Infrastructure.Data.Contexto;
using MinhaEscolinha.Infrastructure.Data.Seed;
using MinhaEscolinha.Infrastructure.Seguranca;
using MinhaEscolinha.Testes.Infraestrutura;

namespace MinhaEscolinha.Testes.Configuracao;

/// <summary>
/// Fatia S0 no banco de testes: a escolinha nasce com o 1º gestor numa transação, cada escolinha só enxerga a si mesma
/// (critério de pronto do S0), parâmetros com histórico, conta de recebimento cifrada e créditos pela plataforma.
/// </summary>
[Collection(BancoDeTestes.Colecao)]
public sealed class FundacaoBancoTestes(BancoDeTestes banco) : IAsyncLifetime, IDisposable
{
    private readonly AmbienteAplicacao _app = new();

    public Task InitializeAsync() => BancoDeTestes.Indisponivel is null ? banco.LimparAsync() : Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;
    public void Dispose() => _app.Dispose();

    private async Task<EscolinhaCriada> CriarAsync(string nome, string email, bool reaproveitar = false, string? cpf = null)
    {
        var resultado = await _app.ComoPlataformaAsync(sp => sp.GetRequiredService<ServicoPlataforma>().CriarEscolinhaComGestorAsync(
            new NovaEscolinhaRequisicao
            {
                Nome = nome, Cidade = "Londrina", Uf = "PR", NomeModalidade = "Futsal",
                GestorNome = $"Gestor {nome}", GestorEmail = email, GestorCpf = cpf, ReaproveitarAcessoExistente = reaproveitar
            }));
        Assert.True(resultado.Sucesso, resultado.Mensagem);
        return Assert.IsType<EscolinhaCriada>(resultado.Dados);
    }

    private Task<T> ComoGestorAsync<T>(Guid escolinhaId, Func<IServiceProvider, Task<T>> acao) =>
        _app.NaEscolinhaAsync(escolinhaId, [PapelAtuacao.Gestor], acao);

    [FatoComBanco]
    public async Task A_escolinha_nasce_junto_com_o_gestor_modalidade_parametros_e_creditos()
    {
        var criada = await CriarAsync("Bola na Rede", "Gestor@BolaNaRede.com");

        Assert.NotNull(criada.SenhaTemporaria);
        Assert.Equal("gestor@bolanarede.com", criada.Login);

        await using var db = BancoDeTestes.NovoContextoSemEscolinha();
        var credencial = await db.Credenciais.Include(c => c.Pessoa).SingleAsync(c => c.Login == "gestor@bolanarede.com");
        Assert.True(credencial.TrocarSenha);
        Assert.Equal(PapelAtuacao.Gestor, (await db.VinculosAtuacao.IgnoreQueryFilters().SingleAsync(v => v.Escolinha_ID == criada.EscolinhaId)).Papel);
        Assert.Equal("Futsal", (await db.Modalidades.IgnoreQueryFilters().SingleAsync(m => m.Escolinha_ID == criada.EscolinhaId)).Nome);
        Assert.Equal(CatalogoParametros.Todos.Count, await db.Parametros.IgnoreQueryFilters().CountAsync(p => p.Escolinha_ID == criada.EscolinhaId));
        Assert.Equal(0, (await db.ContasCreditos.IgnoreQueryFilters().SingleAsync(c => c.Escolinha_ID == criada.EscolinhaId)).Saldo);
        Assert.True(await db.RegistrosAlteracao.IgnoreQueryFilters().AnyAsync(r => r.Escolinha_ID == criada.EscolinhaId && r.Campo == "Criação"));

        // O gestor entra com a senha temporária e cai direto na escolinha dele.
        var login = await _app.SemContextoAsync(sp => sp.GetRequiredService<IServicoAutenticacao>()
            .AutenticarAsync(new LoginRequisicao { Login = criada.Login, Senha = criada.SenhaTemporaria! }));
        var autenticada = Assert.IsType<CredencialAutenticada>(login.Dados);
        Assert.Equal(criada.EscolinhaId, Assert.Single(autenticada.Escolinhas).EscolinhaId);
    }

    [FatoComBanco]
    public async Task Se_a_gravacao_falha_nada_da_escolinha_fica_no_banco()
    {
        await CriarAsync("Primeira", "primeira@escolinha.com", cpf: "529.982.247-25");

        // Mesmo CPF para outra pessoa: o índice único derruba o SaveChanges no meio da criação.
        var escolinhaId = Guid.NewGuid();
        await Assert.ThrowsAnyAsync<DbUpdateException>(() => _app.ComoPlataformaAsync(sp =>
            sp.GetRequiredService<IExecutorEscolinha>().ExecutarAsync(escolinhaId, s => s.GetRequiredService<CriadorEscolinha>().CriarAsync(
                escolinhaId,
                new NovaEscolinhaRequisicao { Nome = "Segunda", NomeModalidade = "Futsal", GestorNome = "Outra", GestorEmail = "outra@escolinha.com", GestorCpf = "52998224725" },
                "outra@escolinha.com", CancellationToken.None))));

        await using var db = BancoDeTestes.NovoContextoSemEscolinha();
        Assert.False(await db.Escolinhas.IgnoreQueryFilters().AnyAsync(e => e.Escolinha_ID == escolinhaId));
        Assert.False(await db.Parametros.IgnoreQueryFilters().AnyAsync(p => p.Escolinha_ID == escolinhaId));
        Assert.False(await db.Credenciais.AnyAsync(c => c.Login == "outra@escolinha.com"));
    }

    [FatoComBanco]
    public async Task O_mesmo_cpf_para_outra_pessoa_e_recusado_antes_de_gravar()
    {
        await CriarAsync("Primeira", "primeira@escolinha.com", cpf: "529.982.247-25");

        var resultado = await _app.ComoPlataformaAsync(sp => sp.GetRequiredService<ServicoPlataforma>().CriarEscolinhaComGestorAsync(
            new NovaEscolinhaRequisicao { Nome = "Segunda", NomeModalidade = "Futsal", GestorNome = "Outra", GestorEmail = "outra@escolinha.com", GestorCpf = "52998224725" }));

        Assert.False(resultado.Sucesso);
        Assert.Contains("CPF", resultado.Mensagem);
        // O assistente reabre na etapa do gestor, com o campo marcado.
        Assert.Equal(nameof(NovaEscolinhaRequisicao.GestorCpf), resultado.Campo);
    }

    [FatoComBanco]
    public async Task O_gestor_de_uma_escolinha_so_enxerga_a_propria()
    {
        var a = await CriarAsync("Escolinha A", "gestor.a@escolinha.com");
        var b = await CriarAsync("Escolinha B", "gestor.b@escolinha.com");
        var modalidadeDeB = await ComoGestorAsync(b.EscolinhaId, async sp => (await sp.GetRequiredService<ServicoModalidades>().ListarAsync()).Single().Id);

        await ComoGestorAsync(a.EscolinhaId, async sp =>
        {
            Assert.Equal("Escolinha A", (await sp.GetRequiredService<ServicoEscolinha>().ObterAsync())!.Nome);
            Assert.Single(await sp.GetRequiredService<ServicoModalidades>().ListarAsync());
            Assert.Null(await sp.GetRequiredService<ServicoModalidades>().ObterAsync(modalidadeDeB)); // id de B → 404 na tela
            Assert.Equal(CatalogoParametros.Todos.Count, (await sp.GetRequiredService<IConfiguracaoRepositorio>().ListarParametrosAsync()).Count);
            return true;
        });
    }

    [FatoComBanco]
    public async Task Gravar_dado_de_outra_escolinha_lanca_excecao()
    {
        var a = await CriarAsync("Escolinha A", "gestor.a@escolinha.com");
        var b = await CriarAsync("Escolinha B", "gestor.b@escolinha.com");

        await Assert.ThrowsAsync<InvalidOperationException>(() => ComoGestorAsync(a.EscolinhaId, async sp =>
        {
            var repositorio = sp.GetRequiredService<IConfiguracaoRepositorio>();
            repositorio.AdicionarModalidade(new Modalidade { Escolinha_ID = b.EscolinhaId, Nome = "Intrusa" });
            await repositorio.SalvarAsync();
            return true;
        }));
    }

    [FatoComBanco]
    public async Task Sem_escolinha_no_contexto_nenhuma_linha_aparece_e_nada_grava()
    {
        await CriarAsync("Escolinha A", "gestor.a@escolinha.com");

        await _app.SemContextoAsync(async sp =>
        {
            var repositorio = sp.GetRequiredService<IConfiguracaoRepositorio>();
            Assert.Empty(await repositorio.ListarModalidadesAsync());
            Assert.Empty(await repositorio.ListarParametrosAsync());
            Assert.Null(await repositorio.ObterEscolinhaAsync());

            repositorio.AdicionarModalidade(Modalidade.Nova("Sem dono", PerfilModalidade.Coletiva));
            await Assert.ThrowsAsync<InvalidOperationException>(() => repositorio.SalvarAsync());
            return true;
        });
    }

    [FatoComBanco]
    public async Task Vinculo_revogado_perde_o_acesso_na_proxima_requisicao()
    {
        var a = await CriarAsync("Escolinha A", "gestor.a@escolinha.com");
        await using (var db = BancoDeTestes.NovoContextoSemEscolinha())
        {
            var pessoaId = (await db.Credenciais.SingleAsync(c => c.Login == "gestor.a@escolinha.com")).Pessoa_ID;
            Assert.Equal(new[] { PapelAtuacao.Gestor }, await _app.SemContextoAsync(sp => sp.GetRequiredService<IAcessoRepositorio>().PapeisAtivosAsync(pessoaId, a.EscolinhaId)));

            await db.VinculosAtuacao.IgnoreQueryFilters()
                .Where(v => v.Pessoa_ID == pessoaId)
                .ExecuteUpdateAsync(s => s.SetProperty(v => v.Situacao, SituacaoVinculo.Revogado));

            Assert.Empty(await _app.SemContextoAsync(sp => sp.GetRequiredService<IAcessoRepositorio>().PapeisAtivosAsync(pessoaId, a.EscolinhaId)));
        }
    }

    [FatoComBanco]
    public async Task Quem_atua_em_duas_escolinhas_escolhe_no_login()
    {
        var a = await CriarAsync("Escolinha A", "dono@escolinhas.com");
        var b = await CriarAsync("Escolinha B", "dono@escolinhas.com", reaproveitar: true);

        Assert.Null(b.SenhaTemporaria); // mesma pessoa, mesma senha
        var login = await _app.SemContextoAsync(sp => sp.GetRequiredService<IServicoAutenticacao>()
            .AutenticarAsync(new LoginRequisicao { Login = "dono@escolinhas.com", Senha = a.SenhaTemporaria! }));
        var credencial = Assert.IsType<CredencialAutenticada>(login.Dados);
        Assert.Equal(2, credencial.Escolinhas.Count);
    }

    [FatoComBanco]
    public async Task Email_que_ja_tem_acesso_so_vira_gestor_de_outra_escolinha_com_confirmacao()
    {
        await CriarAsync("Escolinha A", "dono@escolinhas.com");

        var resultado = await _app.ComoPlataformaAsync(sp => sp.GetRequiredService<ServicoPlataforma>().CriarEscolinhaComGestorAsync(
            new NovaEscolinhaRequisicao { Nome = "Escolinha B", NomeModalidade = "Futsal", GestorNome = "Dono", GestorEmail = "dono@escolinhas.com" }));

        Assert.False(resultado.Sucesso);
        Assert.Contains("já tem acesso", resultado.Mensagem);
    }

    [FatoComBanco]
    public async Task Parametro_alterado_vale_na_proxima_leitura_e_fica_no_historico()
    {
        var a = await CriarAsync("Escolinha A", "gestor.a@escolinha.com");
        var b = await CriarAsync("Escolinha B", "gestor.b@escolinha.com");
        _app.Usuario.Nome = "Gestor A";

        var resultado = await ComoGestorAsync(a.EscolinhaId, sp => sp.GetRequiredService<ServicoParametros>().AlterarAsync(
            new AlterarParametrosRequisicao { Valores = { [ChavesParametro.PresencaLimiteFaltas] = "4", [ChavesParametro.FinanceiroMulta] = "1,5" } }));
        Assert.True(resultado.Sucesso, resultado.Mensagem);
        Assert.Equal(2, resultado.Dados);

        // Escopo novo = próxima requisição: sem reiniciar nada, o valor novo vale (CA-18).
        Assert.Equal(4, await ComoGestorAsync(a.EscolinhaId, async sp => (await sp.GetRequiredService<IParametros>().ObterAsync()).Presenca.LimiteFaltas));
        Assert.Equal(3, await ComoGestorAsync(b.EscolinhaId, async sp => (await sp.GetRequiredService<IParametros>().ObterAsync()).Presenca.LimiteFaltas));

        var historico = await ComoGestorAsync(a.EscolinhaId, sp => sp.GetRequiredService<ServicoParametros>().HistoricoAsync(ChavesParametro.PresencaLimiteFaltas));
        var registro = Assert.Single(historico);
        Assert.Equal("3", registro.ValorAnterior);
        Assert.Equal("4", registro.ValorNovo);
        Assert.Equal("Gestor A", registro.AutorNome);
    }

    [FatoComBanco]
    public async Task Parametro_invalido_nao_grava_nada_do_lote()
    {
        var a = await CriarAsync("Escolinha A", "gestor.a@escolinha.com");

        var resultado = await ComoGestorAsync(a.EscolinhaId, sp => sp.GetRequiredService<ServicoParametros>().AlterarAsync(
            new AlterarParametrosRequisicao { Valores = { [ChavesParametro.PresencaLimiteFaltas] = "4", [ChavesParametro.FinanceiroMulta] = "50" } }));

        Assert.False(resultado.Sucesso);
        // A tela marca só o campo com problema.
        Assert.Equal(ChavesParametro.FinanceiroMulta, resultado.Campo);
        Assert.Equal(new[] { ChavesParametro.FinanceiroMulta }, Assert.IsAssignableFrom<IReadOnlyList<string>>(resultado.Dados));
        Assert.Equal(3, await ComoGestorAsync(a.EscolinhaId, async sp => (await sp.GetRequiredService<IParametros>().ObterAsync()).Presenca.LimiteFaltas));
    }

    [FatoComBanco]
    public async Task Reconciliacao_do_startup_devolve_o_parametro_que_faltava()
    {
        var a = await CriarAsync("Escolinha A", "gestor.a@escolinha.com");
        await using (var db = BancoDeTestes.NovoContextoSemEscolinha())
            await db.Parametros.IgnoreQueryFilters()
                .Where(p => p.Escolinha_ID == a.EscolinhaId && p.Chave == ChavesParametro.MensagemJanelaSilencio)
                .ExecuteDeleteAsync();

        var novos = await _app.SemContextoAsync(sp => sp.GetRequiredService<ReconciliadorParametros>().ReconciliarAsync());

        Assert.Equal(1, novos);
        Assert.Equal(0, await _app.SemContextoAsync(sp => sp.GetRequiredService<ReconciliadorParametros>().ReconciliarAsync())); // idempotente
    }

    [FatoComBanco]
    public async Task Conta_de_recebimento_fica_cifrada_pede_senha_e_so_aceita_sandbox_fora_de_producao()
    {
        var a = await CriarAsync("Escolinha A", "gestor.a@escolinha.com");
        await using (var db = BancoDeTestes.NovoContextoSemEscolinha())
            _app.Usuario.Id = (await db.Credenciais.SingleAsync(c => c.Login == "gestor.a@escolinha.com")).Pessoa_ID;
        const string chave = "$aact_hmlg_000MzkwODA2MWY2OGM3MWRlNTZjZTE1wxyz";

        Assert.False(await ComoGestorAsync(a.EscolinhaId, sp => sp.GetRequiredService<ServicoContaAsaas>().PodeEmitirCobrancaAsync()));

        var senhaErrada = await ComoGestorAsync(a.EscolinhaId, sp => sp.GetRequiredService<ServicoContaAsaas>().VincularAsync(
            new VincularContaAsaasRequisicao { Ambiente = AmbienteAsaas.Sandbox, ChaveApi = chave, SenhaAtual = "errada" }));
        Assert.False(senhaErrada.Sucesso);

        var producao = await ComoGestorAsync(a.EscolinhaId, sp => sp.GetRequiredService<ServicoContaAsaas>().VincularAsync(
            new VincularContaAsaasRequisicao { Ambiente = AmbienteAsaas.Producao, ChaveApi = chave, SenhaAtual = a.SenhaTemporaria! }));
        Assert.False(producao.Sucesso);

        var ok = await ComoGestorAsync(a.EscolinhaId, sp => sp.GetRequiredService<ServicoContaAsaas>().VincularAsync(
            new VincularContaAsaasRequisicao { Ambiente = AmbienteAsaas.Sandbox, ChaveApi = chave, SenhaAtual = a.SenhaTemporaria! }));
        Assert.True(ok.Sucesso, ok.Mensagem);

        await ComoGestorAsync(a.EscolinhaId, async sp =>
        {
            var conta = (await sp.GetRequiredService<IConfiguracaoRepositorio>().ObterContaAsaasAsync())!;
            Assert.DoesNotContain("aact", conta.ChaveApiProtegida);
            Assert.Equal(chave, sp.GetRequiredService<IProtetorSegredos>().Desproteger(conta.ChaveApiProtegida));
            Assert.Equal("wxyz", conta.FinalChave);
            Assert.True(await sp.GetRequiredService<ServicoContaAsaas>().PodeEmitirCobrancaAsync());
            var auditoria = await sp.GetRequiredService<IConfiguracaoRepositorio>().ListarAlteracoesAsync(nameof(ContaAsaasEscolinha), null, 10);
            Assert.DoesNotContain(auditoria, r => (r.ValorNovo ?? "").Contains("aact")); // a chave nunca vai para a auditoria
            return true;
        });
    }

    [FatoComBanco]
    public async Task So_a_plataforma_concede_creditos_e_o_saldo_nunca_fica_negativo()
    {
        var a = await CriarAsync("Escolinha A", "gestor.a@escolinha.com");

        var concessao = await _app.ComoPlataformaAsync(sp => sp.GetRequiredService<ServicoPlataforma>().ConcederCreditosAsync(
            new ConcederCreditosRequisicao { EscolinhaId = a.EscolinhaId, Quantidade = 100, Motivo = "Pacote do piloto" }));
        Assert.True(concessao.Sucesso, concessao.Mensagem);

        var negativo = await _app.ComoPlataformaAsync(sp => sp.GetRequiredService<ServicoPlataforma>().ConcederCreditosAsync(
            new ConcederCreditosRequisicao { EscolinhaId = a.EscolinhaId, Quantidade = -150, Motivo = "Correção" }));
        Assert.False(negativo.Sucesso);

        var gestorTentando = await ComoGestorAsync(a.EscolinhaId, sp => sp.GetRequiredService<ServicoPlataforma>().ConcederCreditosAsync(
            new ConcederCreditosRequisicao { EscolinhaId = a.EscolinhaId, Quantidade = 10, Motivo = "Por conta própria" }));
        Assert.False(gestorTentando.Sucesso);

        var creditos = await ComoGestorAsync(a.EscolinhaId, sp => sp.GetRequiredService<ServicoCreditos>().ObterAsync(1));
        Assert.Equal(100, creditos!.Saldo);
        Assert.Equal(100, creditos.Movimentos.Itens.Sum(m => m.Quantidade));
    }

    [FatoComBanco]
    public async Task O_seed_cria_o_administrador_da_plataforma_uma_vez_so()
    {
        var configuracao = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Seed:Administrador:Login"] = "Equipe@LDNA.com",
            ["Seed:Administrador:Nome"] = "Equipe LDNA",
            ["Seed:Administrador:Senha"] = "SenhaDoSeed123"
        }).Build();

        await using var db = BancoDeTestes.NovoContextoSemEscolinha();
        await SeedAdministradorPlataforma.ExecutarAsync(db, configuracao, new HashSenhaBCrypt(), NullLogger.Instance);
        await SeedAdministradorPlataforma.ExecutarAsync(db, configuracao, new HashSenhaBCrypt(), NullLogger.Instance);

        var admin = await db.Credenciais.SingleAsync(c => c.AdministradorPlataforma);
        Assert.Equal("equipe@ldna.com", admin.Login);
        Assert.True(admin.TrocarSenha);
    }
}
