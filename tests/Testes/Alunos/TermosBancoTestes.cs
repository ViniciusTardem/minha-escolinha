using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MinhaEscolinha.Application.Alunos;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Equipe;
using MinhaEscolinha.Application.Mensageria;
using MinhaEscolinha.Application.Configuracao;
using MinhaEscolinha.Application.Turmas;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Domain.Alunos;
using MinhaEscolinha.Domain.Mensageria;
using MinhaEscolinha.Testes.Infraestrutura;

namespace MinhaEscolinha.Testes.Alunos;

/// <summary>
/// Os termos de consentimento pelo link do responsável (D111), no banco de testes: o cadastro do aluno manda o link por
/// e-mail ao 1º responsável (pelo motor único), a página pública confere o CPF, cada termo tem a sua resposta, e o link
/// deixa de existir depois de respondido, reenviado ou vencido. O relógio começa no sábado, 26/09/2026, 12:00 de Brasília.
/// </summary>
[Collection(BancoDeTestes.Colecao)]
public sealed class TermosBancoTestes(BancoDeTestes banco) : IAsyncLifetime, IDisposable
{
    private const string CpfMaria = "529.982.247-25";
    private readonly AmbienteAplicacao _app = new();

    public Task InitializeAsync() => BancoDeTestes.Indisponivel is null ? banco.LimparAsync() : Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;
    public void Dispose() => _app.Dispose();

    private sealed record Base(Guid Escolinha, Guid PessoaGestor, Guid Turma);

    private Task<T> Gestor<T>(Base b, Func<IServiceProvider, Task<T>> acao)
    {
        _app.Usuario.Id = b.PessoaGestor;
        _app.Usuario.Nome = "Gestão";
        return _app.NaEscolinhaAsync(b.Escolinha, [PapelAtuacao.Gestor], acao);
    }

    private Task<T> Publico<T>(Func<ServicoRespostaTermos, Task<T>> acao)
    {
        _app.Usuario.Id = null;
        _app.Usuario.Nome = null;
        return _app.SemContextoAsync(sp => acao(sp.GetRequiredService<ServicoRespostaTermos>()));
    }

    private static Guid Id(ResultadoOperacao r)
    {
        Assert.True(r.Sucesso, r.Mensagem);
        return Assert.IsType<Guid>(r.Dados);
    }

    private async Task<Base> MontarAsync()
    {
        var criada = await _app.ComoPlataformaAsync(sp => sp.GetRequiredService<ServicoPlataforma>().CriarEscolinhaComGestorAsync(
            new NovaEscolinhaRequisicao { Nome = "Bola na Rede", NomeModalidade = "Futsal", GestorNome = "Gestão", GestorEmail = "gestao@bolanarede.com" }));
        Assert.True(criada.Sucesso, criada.Mensagem);
        var escolinha = Assert.IsType<EscolinhaCriada>(criada.Dados).EscolinhaId;
        Guid pessoa;
        await using (var db = BancoDeTestes.NovoContextoSemEscolinha())
            pessoa = (await db.Credenciais.SingleAsync(c => c.Login == "gestao@bolanarede.com")).Pessoa_ID;
        var b = new Base(escolinha, pessoa, Guid.Empty);

        var local = Id(await Gestor(b, sp => sp.GetRequiredService<ServicoLocais>().SalvarAsync(new LocalRequisicao
        {
            Nome = "Ginásio Moringão", Logradouro = "Rua Tietê", Numero = "1000", Bairro = "Centro", Cidade = "Londrina", Uf = "PR"
        })));
        var professor = Id(await Gestor(b, sp => sp.GetRequiredService<ServicoProfessores>()
            .CriarAsync(new ProfessorRequisicao { Nome = "Carlos Treinador", Telefone = "(43) 99999-0000" })));
        var requisicao = await Gestor(b, sp => sp.GetRequiredService<ServicoTurmas>().NovaAsync());
        requisicao.Nome = "Sub-11";
        requisicao.LocalId = local;
        requisicao.ProfessorId = professor;
        requisicao.Mensalidade = 150m;
        requisicao.Horarios = [new HorarioRequisicao { DiaSemana = DayOfWeek.Tuesday, Inicio = new TimeOnly(18, 0), Fim = new TimeOnly(19, 0) }];
        return b with { Turma = Id(await Gestor(b, sp => sp.GetRequiredService<ServicoTurmas>().CriarAsync(requisicao))) };
    }

    private async Task<(Guid Aluno, string Mensagem)> AlunoAsync(Base b, string nome, string responsavel, string? cpf, string? email,
        bool enviarTermos = true, RespostaConsentimento dados = RespostaConsentimento.NaoColetado, string telefone = "43988887777")
    {
        var r = NovoAlunoRequisicao.Vazia(new DateOnly(2026, 9, 26));
        r.Nome = nome;
        r.DataNascimento = new DateOnly(2015, 4, 12);
        r.TurmaId = b.Turma;
        r.Responsaveis[0].Nome = responsavel;
        r.Responsaveis[0].Cpf = cpf;
        r.Responsaveis[0].Telefone = telefone;
        r.Responsaveis[0].Email = email;
        r.EnviarTermos = enviarTermos;
        r.Consentimentos.Single(c => c.Finalidade == FinalidadeConsentimento.TratamentoDados).Resposta = dados;
        var resultado = await Gestor(b, sp => sp.GetRequiredService<ServicoAlunos>().CriarAsync(r));
        return (Id(resultado), resultado.Mensagem);
    }

    private static async Task<List<PedidoTermos>> PedidosAsync(Guid aluno)
    {
        await using var db = BancoDeTestes.NovoContextoSemEscolinha();
        return await db.PedidosTermos.IgnoreQueryFilters().Where(p => p.Aluno_ID == aluno).OrderBy(p => p.CriadoEm).ToListAsync();
    }

    private static async Task<List<Mensagem>> EmailsAsync(Guid escolinha)
    {
        await using var db = BancoDeTestes.NovoContextoSemEscolinha();
        return await db.Mensagens.IgnoreQueryFilters()
            .Where(m => m.Escolinha_ID == escolinha && m.Gatilho == GatilhoMensagem.TermosConsentimento).ToListAsync();
    }

    private static string TokenDoLink(string texto) => Regex.Match(texto, "/Termos/(?<t>[A-Za-z0-9_-]+)").Groups["t"].Value;

    /// <summary>
    /// O caminho principal: o cadastro manda o link por e-mail (pelo motor, sem crédito), a página pede o CPF antes de
    /// mostrar qualquer dado, o responsável aceita um termo e recusa o outro, e o link deixa de existir.
    /// </summary>
    [FatoComBanco]
    public async Task O_responsavel_recebe_o_link_por_email_confirma_o_cpf_responde_cada_termo_e_o_link_deixa_de_existir()
    {
        var b = await MontarAsync();
        var (aluno, mensagem) = await AlunoAsync(b, "Pedro Lima", "Maria Lima", CpfMaria, "maria@familia.com");
        Assert.Contains("por e-mail para Maria Lima", mensagem);

        var pedido = Assert.Single(await PedidosAsync(aluno));
        Assert.Equal((SituacaoPedidoTermos.Aguardando, true, true, "maria@familia.com", new DateTime(2026, 10, 11, 12, 0, 0)),
            (pedido.Situacao, pedido.PedeDados, pedido.PedeImagem, pedido.EnviadoPara, pedido.ExpiraEm));
        var email = Assert.Single(await EmailsAsync(b.Escolinha));
        Assert.Equal((CanalMensagem.Email, SituacaoMensagem.Pendente, "maria@familia.com"), (email.Canal, email.Situacao, email.Email));

        // O motor manda o e-mail (sem crédito de WhatsApp) e mascara o link guardado.
        Assert.Equal(SituacaoMensagem.Enviada,
            await Gestor(b, sp => sp.GetRequiredService<ServicoEnvioMensagens>().EnviarAsync(email.Mensagem_ID)));
        var enviado = Assert.Single(_app.EnvioEmail.Enviados);
        Assert.Equal(("maria@familia.com", "Bola na Rede: termos de consentimento de Pedro Lima"), (enviado.Para, enviado.Assunto));
        var token = TokenDoLink(enviado.Corpo);
        Assert.NotEmpty(token);
        email = Assert.Single(await EmailsAsync(b.Escolinha));
        Assert.Equal(0, email.CreditosConsumidos);
        Assert.DoesNotContain(token, email.Conteudo);
        Assert.DoesNotContain(token, email.Variaveis);

        // Antes do CPF, nada do aluno; CPF errado não abre.
        var abertura = Assert.IsType<TermosParaResponder>((await Publico(s => s.AbrirAsync(token, null))).Dados);
        Assert.Equal((true, false, "", 0), (abertura.PedeCpf, abertura.Identificado, abertura.Aluno, abertura.Termos.Count));
        var errado = await Publico(s => s.AbrirAsync(token, "111.444.777-35"));
        Assert.Equal((false, nameof(RespostaTermosRequisicao.Cpf)), (errado.Sucesso, errado.Campo));
        var termos = Assert.IsType<TermosParaResponder>((await Publico(s => s.AbrirAsync(token, CpfMaria))).Dados);
        Assert.Equal(("Maria", "Pedro", 2), (termos.Responsavel, termos.Aluno, termos.Termos.Count));

        // Cada termo pedido precisa da resposta; depois, um sim e um não (R6).
        var incompleta = await Publico(s => s.ResponderAsync(token, new RespostaTermosRequisicao { Cpf = CpfMaria, Dados = true }, "10.0.0.1"));
        Assert.Equal((false, nameof(RespostaTermosRequisicao.Imagem)), (incompleta.Sucesso, incompleta.Campo));
        var resposta = await Publico(s => s.ResponderAsync(token,
            new RespostaTermosRequisicao { Cpf = CpfMaria, Dados = true, Imagem = false }, "10.0.0.1"));
        Assert.True(resposta.Sucesso, resposta.Mensagem);

        await using (var db = BancoDeTestes.NovoContextoSemEscolinha())
        {
            var consentimentos = await db.Consentimentos.IgnoreQueryFilters().Where(c => c.Aluno_ID == aluno).ToListAsync();
            Assert.Equal(2, consentimentos.Count);
            Assert.All(consentimentos, c => Assert.Equal((OrigemConsentimento.LinkDoResponsavel, "Maria Lima"), (c.Origem, c.RegistradoPorNome)));
            Assert.True(consentimentos.Single(c => c.Finalidade == FinalidadeConsentimento.TratamentoDados).Concedido);
            Assert.False(consentimentos.Single(c => c.Finalidade == FinalidadeConsentimento.UsoImagem).Concedido);
            Assert.True(await db.RegistrosAlteracao.IgnoreQueryFilters()
                .AnyAsync(r => r.Registro_ID == aluno && r.Campo == "Termos de consentimento" && r.ValorNovo!.StartsWith("Respondido por Maria Lima")));
        }
        pedido = Assert.Single(await PedidosAsync(aluno));
        Assert.Equal((SituacaoPedidoTermos.Respondido, true, false, "10.0.0.1", (string?)null),
            (pedido.Situacao, pedido.AceitouDados!.Value, pedido.AceitouImagem!.Value, pedido.RespondidoDoIp!, pedido.TokenProtegido));

        // O link deixou de existir, e a ficha mostra a resposta.
        Assert.Equal(ServicoRespostaTermos.MensagemIndisponivel, (await Publico(s => s.AbrirAsync(token, CpfMaria))).Mensagem);
        var ficha = (await Gestor(b, sp => sp.GetRequiredService<ServicoTermos>().DaFichaAsync(aluno)))!;
        Assert.Equal((SituacaoPedidoTermos.Respondido, true, false, (string?)null, false),
            (ficha.Ultimo!.Situacao, ficha.Ultimo.AceitouDados!.Value, ficha.Ultimo.AceitouImagem!.Value, ficha.Ultimo.Link, ficha.PodeEnviar));
        Assert.Equal(SituacaoMensagem.Enviada, ficha.Ultimo.Email);
    }

    /// <summary>
    /// Sem e-mail no cadastro, a gestão copia o link na ficha. Sem CPF no cadastro, o link abre direto nos termos. Enviar
    /// de novo invalida o link anterior; o link vence na validade do parâmetro (15 dias).
    /// </summary>
    [FatoComBanco]
    public async Task Sem_email_a_gestao_copia_o_link_e_o_reenvio_e_a_validade_encerram_o_link_anterior()
    {
        var b = await MontarAsync();
        var (aluno, mensagem) = await AlunoAsync(b, "Bruno Alves", "Rita Alves", null, null);
        Assert.Contains("Rita Alves não tem e-mail", mensagem);
        Assert.Empty(await EmailsAsync(b.Escolinha));

        Task<TermosDaFicha?> Ficha() => Gestor(b, sp => sp.GetRequiredService<ServicoTermos>().DaFichaAsync(aluno));
        var primeiro = (await Ficha())!.Ultimo!;
        Assert.Equal((SituacaoPedidoTermos.Aguardando, (string?)null, (SituacaoMensagem?)null), (primeiro.Situacao, primeiro.EnviadoPara, primeiro.Email));
        var token1 = TokenDoLink(Assert.IsType<string>(primeiro.Link));

        var sem = Assert.IsType<TermosParaResponder>((await Publico(s => s.AbrirAsync(token1, null))).Dados);
        Assert.Equal((false, true, "Bruno"), (sem.PedeCpf, sem.Identificado, sem.Aluno));

        var reenvio = await Gestor(b, sp => sp.GetRequiredService<ServicoTermos>().EnviarAsync(aluno));
        Assert.True(reenvio.Sucesso, reenvio.Mensagem);
        var token2 = TokenDoLink((await Ficha())!.Ultimo!.Link!);
        Assert.NotEqual(token1, token2);
        Assert.False((await Publico(s => s.AbrirAsync(token1, null))).Sucesso);
        Assert.Equal([SituacaoPedidoTermos.Aguardando, SituacaoPedidoTermos.Cancelado], (await PedidosAsync(aluno)).Select(p => p.Situacao).Order());

        _app.Tempo.Advance(TimeSpan.FromDays(15));
        Assert.False((await Publico(s => s.AbrirAsync(token2, null))).Sucesso);
        var vencido = (await Ficha())!;
        Assert.Equal((true, (string?)null, true), (vencido.Ultimo!.Vencido, vencido.Ultimo.Link, vencido.PodeEnviar));
    }

    /// <summary>
    /// O link pede só o que a gestão não coletou no cadastro. A resposta pela página antes de o e-mail sair recolhe o
    /// e-mail da fila (regra de ouro 5). E quem desmarca o envio no cadastro não gera link.
    /// </summary>
    [FatoComBanco]
    public async Task O_link_pede_so_o_termo_nao_coletado_e_a_resposta_recolhe_o_email_que_ainda_nao_saiu()
    {
        var b = await MontarAsync();
        var (aluno, _) = await AlunoAsync(b, "Ana Lima", "Maria Lima", CpfMaria, "maria@familia.com", dados: RespostaConsentimento.Concedido);
        var pedido = Assert.Single(await PedidosAsync(aluno));
        Assert.Equal((false, true), (pedido.PedeDados, pedido.PedeImagem));

        var link = (await Gestor(b, sp => sp.GetRequiredService<ServicoTermos>().DaFichaAsync(aluno)))!.Ultimo!.Link!;
        var token = TokenDoLink(link);
        var termos = Assert.IsType<TermosParaResponder>((await Publico(s => s.AbrirAsync(token, CpfMaria))).Dados);
        Assert.Equal([FinalidadeConsentimento.UsoImagem], termos.Termos.Select(t => t.Finalidade));
        Assert.True((await Publico(s => s.ResponderAsync(token, new RespostaTermosRequisicao { Cpf = CpfMaria, Imagem = true }, null))).Sucesso);

        var email = Assert.Single(await EmailsAsync(b.Escolinha));
        Assert.Equal((SituacaoMensagem.Suprimida, "Os termos já foram respondidos."), (email.Situacao, email.Motivo));
        Assert.Equal(SituacaoMensagem.Suprimida,
            await Gestor(b, sp => sp.GetRequiredService<ServicoEnvioMensagens>().EnviarAsync(email.Mensagem_ID)));
        Assert.Empty(_app.EnvioEmail.Enviados);

        var semEnvio = await AlunoAsync(b, "Theo Martins", "Cláudia Martins", "111.444.777-35", "claudia@familia.com", enviarTermos: false,
            telefone: "43977776666");
        Assert.Empty(await PedidosAsync(semEnvio.Aluno));
        var ficha = (await Gestor(b, sp => sp.GetRequiredService<ServicoTermos>().DaFichaAsync(semEnvio.Aluno)))!;
        Assert.Equal(((PedidoTermosDaFicha?)null, true), (ficha.Ultimo, ficha.PodeEnviar));
    }
}
