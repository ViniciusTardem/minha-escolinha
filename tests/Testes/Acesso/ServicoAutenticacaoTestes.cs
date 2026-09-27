using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using MinhaEscolinha.Application.Acesso;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Infrastructure.Tempo;
using MinhaEscolinha.Testes.Infraestrutura;

namespace MinhaEscolinha.Testes.Acesso;

/// <summary>Login, bloqueio e troca de senha (regras portadas do DP, sobre a credencial única da pessoa).</summary>
public class ServicoAutenticacaoTestes
{
    private const string SenhaCerta = "SenhaCerta123";

    private readonly FakeTimeProvider _tempo = new(new DateTimeOffset(2026, 9, 26, 15, 0, 0, TimeSpan.Zero)); // 12:00 em Brasília
    private readonly RepositorioEmMemoria _repositorio = new();
    private readonly UsuarioDeTeste _usuario = new();
    private readonly ServicoAutenticacao _servico;

    public ServicoAutenticacaoTestes()
    {
        _servico = new ServicoAutenticacao(_repositorio, new HashFalso(), new RelogioBrasil(_tempo), _usuario,
            new LoginRequisicaoValidador(), new TrocarSenhaRequisicaoValidador(), NullLogger<ServicoAutenticacao>.Instance);
    }

    private Credencial Cadastrar(string login, bool comEscolinha = true, bool plataforma = false)
    {
        var pessoa = new Pessoa { Pessoa_ID = Guid.NewGuid(), Nome = login };
        var credencial = new Credencial { Pessoa_ID = pessoa.Pessoa_ID, Pessoa = pessoa, Login = login, AdministradorPlataforma = plataforma };
        credencial.DefinirSenha(HashFalso.Hash(SenhaCerta), trocarNoProximoAcesso: false);
        _repositorio.Credenciais.Add(credencial);
        if (comEscolinha)
            _repositorio.Escolinhas[pessoa.Pessoa_ID] = [new EscolinhaDaPessoa(Guid.NewGuid(), "Escolinha Teste", [PapelAtuacao.Gestor])];
        return credencial;
    }

    private Task<Application.Comum.ResultadoOperacao> Entrar(string login, string senha) =>
        _servico.AutenticarAsync(new LoginRequisicao { Login = login, Senha = senha });

    [Fact]
    public async Task Login_ignora_maiusculas_e_espacos()
    {
        Cadastrar("maria@escolinha.com");

        var resultado = await Entrar("  Maria@Escolinha.com ", SenhaCerta);

        Assert.True(resultado.Sucesso);
        Assert.Single(Assert.IsType<CredencialAutenticada>(resultado.Dados).Escolinhas);
    }

    [Fact]
    public async Task Quinta_falha_seguida_bloqueia_por_15_minutos()
    {
        var credencial = Cadastrar("joao@escolinha.com");

        for (var i = 0; i < Credencial.MaxTentativasFalhas - 1; i++)
            Assert.Equal("Login ou senha inválidos.", (await Entrar("joao@escolinha.com", "errada")).Mensagem);
        var quinta = await Entrar("joao@escolinha.com", "errada");

        Assert.False(quinta.Sucesso);
        Assert.Contains("bloqueado", quinta.Mensagem);
        Assert.Equal(new DateTime(2026, 9, 26, 12, 15, 0), credencial.BloqueadoAte);
        Assert.False((await Entrar("joao@escolinha.com", SenhaCerta)).Sucesso);

        _tempo.Advance(TimeSpan.FromMinutes(15));
        Assert.True((await Entrar("joao@escolinha.com", SenhaCerta)).Sucesso);
    }

    [Fact]
    public async Task Login_inexistente_e_senha_errada_tem_a_mesma_mensagem()
    {
        Cadastrar("ana@escolinha.com");

        Assert.Equal((await Entrar("ninguem@escolinha.com", SenhaCerta)).Mensagem, (await Entrar("ana@escolinha.com", "errada")).Mensagem);
    }

    [Fact]
    public async Task Sem_vinculo_ativo_em_nenhuma_escolinha_o_login_e_recusado()
    {
        Cadastrar("sem.vinculo@escolinha.com", comEscolinha: false);

        var resultado = await Entrar("sem.vinculo@escolinha.com", SenhaCerta);

        Assert.False(resultado.Sucesso);
        Assert.Contains("nenhuma escolinha", resultado.Mensagem);
    }

    [Fact]
    public async Task Equipe_da_plataforma_entra_sem_escolinha()
    {
        Cadastrar("equipe@ldna.com", comEscolinha: false, plataforma: true);

        var resultado = await Entrar("equipe@ldna.com", SenhaCerta);

        Assert.True(resultado.Sucesso);
        var credencial = Assert.IsType<CredencialAutenticada>(resultado.Dados);
        Assert.True(credencial.AdministradorPlataforma);
        Assert.Empty(credencial.Escolinhas);
    }

    [Fact]
    public async Task Credencial_inativa_recebe_a_mensagem_generica()
    {
        Cadastrar("inativa@escolinha.com").Ativa = false;

        Assert.Equal("Login ou senha inválidos.", (await Entrar("inativa@escolinha.com", SenhaCerta)).Mensagem);
    }

    [Fact]
    public async Task Trocar_senha_renova_o_carimbo_e_derruba_as_outras_sessoes()
    {
        var credencial = Cadastrar("carlos@escolinha.com");
        credencial.TrocarSenha = true;
        _usuario.Id = credencial.Pessoa_ID;
        var carimboAntigo = credencial.CarimboSeguranca;

        var resultado = await _servico.TrocarPropriaSenhaAsync(new TrocarSenhaRequisicao
        {
            SenhaAtual = SenhaCerta, NovaSenha = "NovaSenha456", ConfirmacaoSenha = "NovaSenha456"
        });

        Assert.True(resultado.Sucesso);
        Assert.False(credencial.TrocarSenha);
        Assert.NotEqual(carimboAntigo, credencial.CarimboSeguranca);
        Assert.Null(await _servico.ValidarSessaoAsync(credencial.Pessoa_ID, carimboAntigo));
    }

    [Fact]
    public async Task ConferirSenha_so_aceita_a_senha_de_quem_esta_logado()
    {
        var credencial = Cadastrar("gestor@escolinha.com");
        _usuario.Id = credencial.Pessoa_ID;

        Assert.True(await _servico.ConferirSenhaAsync(SenhaCerta));
        Assert.False(await _servico.ConferirSenhaAsync("outra"));
    }

    private sealed class HashFalso : IHashSenha
    {
        public static string Hash(string senha) => "hash:" + senha;
        public string Gerar(string senha) => Hash(senha);
        public bool Verificar(string senha, string hash) => hash == Hash(senha);
    }

    private sealed class RepositorioEmMemoria : IAcessoRepositorio
    {
        public List<Credencial> Credenciais { get; } = [];
        public Dictionary<Guid, IReadOnlyList<EscolinhaDaPessoa>> Escolinhas { get; } = [];

        public Task<Credencial?> ObterCredencialPorLoginAsync(string login, CancellationToken ct = default) =>
            Task.FromResult(Credenciais.FirstOrDefault(c => c.Login == login));

        public Task<Credencial?> ObterCredencialAsync(Guid pessoaId, CancellationToken ct = default) =>
            Task.FromResult(Credenciais.FirstOrDefault(c => c.Pessoa_ID == pessoaId));

        public Task<bool> LoginEmUsoAsync(string login, CancellationToken ct = default) =>
            Task.FromResult(Credenciais.Any(c => c.Login == login));

        public Task<Pessoa?> ObterPessoaPorCpfAsync(string cpf, CancellationToken ct = default) =>
            Task.FromResult(Credenciais.Select(c => c.Pessoa).FirstOrDefault(p => p?.Cpf == cpf));

        public Task<IReadOnlyList<EscolinhaDaPessoa>> EscolinhasDaPessoaAsync(Guid pessoaId, CancellationToken ct = default) =>
            Task.FromResult(Escolinhas.GetValueOrDefault(pessoaId) ?? []);

        public Task<IReadOnlyList<PapelAtuacao>> PapeisAtivosAsync(Guid pessoaId, Guid escolinhaId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<PapelAtuacao>>(
                (Escolinhas.GetValueOrDefault(pessoaId) ?? []).FirstOrDefault(e => e.EscolinhaId == escolinhaId)?.Papeis ?? []);

        public Task<IReadOnlyList<VinculoAtuacao>> VinculosNaEscolinhaAsync(Guid pessoaId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<VinculoAtuacao>>([]);

        public void AdicionarPessoa(Pessoa pessoa) { }

        public void AdicionarVinculo(VinculoAtuacao vinculo) { }

        public Task SalvarAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
}
