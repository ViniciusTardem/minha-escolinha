using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MinhaEscolinha.Application.Alunos;
using MinhaEscolinha.Application.Carga;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Configuracao;
using MinhaEscolinha.Application.Equipe;
using MinhaEscolinha.Application.Turmas;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Domain.Alunos;
using MinhaEscolinha.Domain.Carga;
using MinhaEscolinha.Testes.Infraestrutura;

namespace MinhaEscolinha.Testes.Carga;

/// <summary>
/// Fatia S7 no banco de testes: a planilha é lida, mapeada e conferida sem gravar nada, e só a confirmação da gestão grava
/// tudo numa operação (BL-M12-01 a BL-M12-04), com o responsável reaproveitado sem duplicidade (CA-1) e a saúde fora da
/// conferência e do registro de alterações (BL-M12-05). O relógio começa no sábado, 26/09/2026, 12:00 de Brasília.
/// </summary>
[Collection(BancoDeTestes.Colecao)]
public sealed class CargaBancoTestes(BancoDeTestes banco) : IAsyncLifetime, IDisposable
{
    private const string CpfMaria = "529.982.247-25";
    private readonly AmbienteAplicacao _app = new();

    public Task InitializeAsync() => BancoDeTestes.Indisponivel is null ? banco.LimparAsync() : Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;
    public void Dispose() => _app.Dispose();

    private sealed record Base(Guid Escolinha, Guid PessoaGestor, Guid Sub11, Guid Sub13, Guid Pedro);

    private static readonly string[] Cabecalho =
    [
        "Nome do aluno", "Data de nascimento", "CPF do aluno", "Turma", "Mensalidade", "Alergias", "Observações médicas",
        "Autoriza tratamento de dados", "Autoriza uso de imagem", "Responsável 1", "CPF do responsável 1", "Celular do responsável 1",
        "Parentesco do responsável 1", "Responsável 2", "Celular do responsável 2"
    ];

    private Task<T> Gestor<T>(Base b, Func<ServicoImportacao, Task<T>> acao) => Gestor(b, sp => acao(sp.GetRequiredService<ServicoImportacao>()));

    private Task<T> Gestor<T>(Base b, Func<IServiceProvider, Task<T>> acao)
    {
        _app.Usuario.Id = b.PessoaGestor;
        _app.Usuario.Nome = "Gestão";
        return _app.NaEscolinhaAsync(b.Escolinha, [PapelAtuacao.Gestor], acao);
    }

    private static Guid Id(ResultadoOperacao r)
    {
        Assert.True(r.Sucesso, r.Mensagem);
        return Assert.IsType<Guid>(r.Dados);
    }

    private async Task<Base> MontarAsync(string nome = "Bola na Rede", string email = "gestao@bolanarede.com")
    {
        var criada = await _app.ComoPlataformaAsync(sp => sp.GetRequiredService<ServicoPlataforma>().CriarEscolinhaComGestorAsync(
            new NovaEscolinhaRequisicao { Nome = nome, NomeModalidade = "Futsal", GestorNome = "Gestão", GestorEmail = email }));
        Assert.True(criada.Sucesso, criada.Mensagem);
        var escolinha = Assert.IsType<EscolinhaCriada>(criada.Dados).EscolinhaId;
        Guid pessoa;
        await using (var db = BancoDeTestes.NovoContextoSemEscolinha())
            pessoa = (await db.Credenciais.SingleAsync(c => c.Login == email)).Pessoa_ID;
        var b = new Base(escolinha, pessoa, Guid.Empty, Guid.Empty, Guid.Empty);

        var local = Id(await Gestor(b, sp => sp.GetRequiredService<ServicoLocais>().SalvarAsync(new LocalRequisicao
        {
            Nome = "Ginásio Moringão", Logradouro = "Rua Tietê", Numero = "1000", Bairro = "Centro", Cidade = "Londrina", Uf = "PR"
        })));
        var professor = Id(await Gestor(b, sp => sp.GetRequiredService<ServicoProfessores>()
            .CriarAsync(new ProfessorRequisicao { Nome = "Carlos Treinador", Telefone = "(43) 99999-0000" })));
        var sub11 = await TurmaAsync(b, "Sub-11", local, professor, DayOfWeek.Tuesday, 150m);
        var sub13 = await TurmaAsync(b, "Sub-13", local, professor, DayOfWeek.Wednesday, 140m);

        // Um aluno cadastrado à mão antes da carga: a Maria já existe, com o CPF.
        var r = NovoAlunoRequisicao.Vazia(new DateOnly(2026, 9, 26));
        r.Nome = "Pedro Lima";
        r.DataNascimento = new DateOnly(2015, 4, 12);
        r.TurmaId = sub11;
        r.Responsaveis[0].Nome = "Maria Lima";
        r.Responsaveis[0].Cpf = CpfMaria;
        r.Responsaveis[0].Telefone = "43988887777";
        var pedro = Id(await Gestor(b, sp => sp.GetRequiredService<ServicoAlunos>().CriarAsync(r)));
        return b with { Sub11 = sub11, Sub13 = sub13, Pedro = pedro };
    }

    private async Task<Guid> TurmaAsync(Base b, string nome, Guid local, Guid professor, DayOfWeek dia, decimal mensalidade)
    {
        var requisicao = await Gestor(b, sp => sp.GetRequiredService<ServicoTurmas>().NovaAsync());
        requisicao.Nome = nome;
        requisicao.LocalId = local;
        requisicao.ProfessorId = professor;
        requisicao.Mensalidade = mensalidade;
        requisicao.Horarios = [new HorarioRequisicao { DiaSemana = dia, Inicio = new TimeOnly(18, 0), Fim = new TimeOnly(19, 0) }];
        return Id(await Gestor(b, sp => sp.GetRequiredService<ServicoTurmas>().CriarAsync(requisicao)));
    }

    /// <summary>Uma planilha .xlsx de verdade, com células de texto, número e data, como a escolinha salva no Excel.</summary>
    private static MemoryStream Planilha(string[] cabecalho, params object?[][] linhas)
    {
        using var livro = new XLWorkbook();
        var aba = livro.AddWorksheet("Alunos");
        for (var c = 0; c < cabecalho.Length; c++) aba.Cell(1, c + 1).Value = cabecalho[c];
        for (var l = 0; l < linhas.Length; l++)
            for (var c = 0; c < linhas[l].Length; c++)
                aba.Cell(l + 2, c + 1).Value = linhas[l][c] switch
                {
                    null => Blank.Value,
                    DateTime data => data,
                    double numero => numero,
                    var texto => texto.ToString()
                };
        var memoria = new MemoryStream();
        livro.SaveAs(memoria);
        memoria.Position = 0;
        return memoria;
    }

    private Task<ResultadoOperacao> EnviarAsync(Base b, MemoryStream planilha, string nome = "alunos.xlsx") =>
        Gestor(b, s => s.EnviarAsync(planilha, nome, planilha.Length));

    // ── Critério de pronto do S7 ──

    /// <summary>
    /// Critério do S7: a base entra numa operação, com os erros apontados antes da confirmação. CA-1: a Maria, que já existia,
    /// é reaproveitada pelo CPF na linha da Ana e pelo nome e celular na do Lucas, sem cadastro duplicado.
    /// </summary>
    [FatoComBanco]
    public async Task A_base_entra_numa_operacao_com_os_erros_apontados_antes_da_confirmacao()
    {
        var b = await MontarAsync();
        var planilha = Planilha(Cabecalho,
            ["Ana Lima", new DateTime(2013, 2, 3), null, "Sub-13", null, "Alergia a camarão", null, "sim", "não",
                "Maria Lima", 52998224725d, 43988887777d, "mãe", "João Lima", "43977776666"],
            ["Lucas Lima", "05/06/2017", null, "Sub 11", "R$ 120,00", null, null, "sim", "sim", "Maria Lima", null, "(43) 98888-7777", "Mãe"],
            ["Bia Souza", "10/10/2014", null, "Futsal manhã", null, null, null, null, null, "Paulo Souza", "111.444.777-35", "43966665555", "pai"],
            [null, "01/01/2015", null, "Sub-11", null, null, null, null, null, "Rui Rocha", null, "43944443333"],
            ["Carla Nunes", "20/07/2016", null, "Sub-11", null, null, "Precisa de bombinha", "sim", null, "Rita Nunes", null, "43955554444"]);

        // Enviar: nada entra no cadastro, e todas as colunas do modelo são reconhecidas.
        var lote = Id(await EnviarAsync(b, planilha));
        var mapeamento = (await Gestor(b, s => s.MapeamentoAsync(lote)))!;
        Assert.Empty(mapeamento.Faltando);
        Assert.All(mapeamento.Colunas, c => Assert.NotNull(c.Campo));
        Assert.Empty(mapeamento.Colunas.Single(c => c.Cabecalho == "Alergias").Exemplos); // saúde sem exemplo (BL-M12-05)
        Assert.Equal(["Ana Lima", "Lucas Lima", "Bia Souza"], mapeamento.Colunas[0].Exemplos);

        // Conferir: cada linha com a classe e o motivo, antes de gravar.
        var conferencia = (await Gestor(b, s => s.ConferenciaAsync(lote)))!;
        var plano = conferencia.Plano;
        Assert.Equal([ClasseLinha.Apta, ClasseLinha.Apta, ClasseLinha.Bloqueada, ClasseLinha.Bloqueada, ClasseLinha.ComAviso],
            plano.Linhas.Select(l => l.Classe));
        Assert.Equal([2, 3, 4, 5, 6], plano.Linhas.Select(l => l.Numero));
        Assert.Equal("Sem o nome do aluno.", Assert.Single(plano.Linhas[3].Bloqueios));
        Assert.Equal((3, 2, 1, 4), (plano.AlunosNovos, plano.ResponsaveisNovos, plano.ResponsaveisReaproveitados, plano.Vinculos));
        var maria = plano.Responsaveis.Single(r => r.Nome == "Maria Lima");
        Assert.Equal([2, 3], maria.Linhas);
        Assert.True(maria.PorCpf);
        Assert.Equal([true, false, false, false, true], plano.Linhas.Select(l => l.SaudeInformada));
        Assert.DoesNotContain(plano.Linhas.SelectMany(l => l.Avisos.Concat(l.Bloqueios)),
            m => m.Contains("camarão", StringComparison.OrdinalIgnoreCase) || m.Contains("bombinha", StringComparison.OrdinalIgnoreCase));
        await using (var db = BancoDeTestes.NovoContextoSemEscolinha())
            Assert.Equal(1, await db.Alunos.IgnoreQueryFilters().CountAsync(a => a.Escolinha_ID == b.Escolinha));

        // A turma da planilha que não existe: a gestão escolhe a Sub-11, e a Bia entra.
        var turma = Assert.Single(plano.TurmasSemCorrespondencia);
        Assert.True((await Gestor(b, s => s.DecidirAsync(lote, new DecisoesImportacao { Turmas = { [turma.Chave] = b.Sub11 } }))).Sucesso);
        plano = (await Gestor(b, s => s.ConferenciaAsync(lote)))!.Plano;
        Assert.Equal((4, 3, 1, 5), (plano.AlunosNovos, plano.ResponsaveisNovos, plano.ResponsaveisReaproveitados, plano.Vinculos));

        // Confirmar: as autorizações marcadas pedem a declaração da gestão; depois, tudo numa gravação.
        Assert.Equal("declaracao", (await Gestor(b, s => s.ConfirmarAsync(lote, plano.Assinatura, declaracao: false))).Campo);
        var confirmado = await Gestor(b, s => s.ConfirmarAsync(lote, plano.Assinatura, declaracao: true));
        Assert.True(confirmado.Sucesso, confirmado.Mensagem);
        Assert.StartsWith("Importação concluída: 4 aluno(s) com matrícula, 3 responsável(is) novo(s), 1 reaproveitado(s) e 5 vínculo(s).", confirmado.Mensagem);
        Assert.Contains(b.Escolinha, _app.Financeiro.Projecoes); // a primeira mensalidade de cada matrícula nasce pela projeção (S4)

        await using (var db = BancoDeTestes.NovoContextoSemEscolinha())
        {
            var alunos = await db.Alunos.IgnoreQueryFilters().Where(a => a.Escolinha_ID == b.Escolinha).ToDictionaryAsync(a => a.Nome);
            Assert.Equal(["Ana Lima", "Bia Souza", "Carla Nunes", "Lucas Lima", "Pedro Lima"], alunos.Keys.Order());
            Assert.Equal(("Alergia a camarão", "Precisa de bombinha"), (alunos["Ana Lima"].Alergias, alunos["Carla Nunes"].ObservacoesMedicas));
            Assert.Equal(new DateOnly(2013, 2, 3), alunos["Ana Lima"].DataNascimento);

            // CA-1: um cadastro só da Maria, com os três filhos.
            var responsaveis = await db.Responsaveis.IgnoreQueryFilters().Where(r => r.Escolinha_ID == b.Escolinha).ToListAsync();
            Assert.Equal(["João Lima", "Maria Lima", "Paulo Souza", "Rita Nunes"], responsaveis.Select(r => r.Nome).Order());
            var mariaId = responsaveis.Single(r => r.Nome == "Maria Lima").Responsavel_ID;
            var vinculosMaria = await db.VinculosResponsavel.IgnoreQueryFilters().Where(v => v.Responsavel_ID == mariaId).ToListAsync();
            Assert.Equal(3, vinculosMaria.Count);
            Assert.All(vinculosMaria, v => Assert.True(v.Pagador && v.RecebeCobranca && v.RecebeNotificacao));
            var joao = await db.VinculosResponsavel.IgnoreQueryFilters().SingleAsync(v => v.Aluno_ID == alunos["Ana Lima"].Aluno_ID && !v.Pagador);
            Assert.Equal((false, true, Parentesco.Outro), (joao.RecebeCobranca, joao.RecebeNotificacao, joao.Parentesco));

            // A matrícula com a mensalidade da planilha ou a da turma, e a Bia na turma escolhida.
            var matriculas = await db.Matriculas.IgnoreQueryFilters().Where(m => m.Escolinha_ID == b.Escolinha && m.Aluno_ID != b.Pedro)
                .ToDictionaryAsync(m => m.Aluno_ID);
            Assert.Equal((b.Sub13, 140m), (matriculas[alunos["Ana Lima"].Aluno_ID].Turma_ID, matriculas[alunos["Ana Lima"].Aluno_ID].Mensalidade));
            Assert.Equal((b.Sub11, 120m), (matriculas[alunos["Lucas Lima"].Aluno_ID].Turma_ID, matriculas[alunos["Lucas Lima"].Aluno_ID].Mensalidade));
            Assert.Equal((b.Sub11, 150m), (matriculas[alunos["Bia Souza"].Aluno_ID].Turma_ID, matriculas[alunos["Bia Souza"].Aluno_ID].Mensalidade));
            Assert.Equal(new DateOnly(2026, 9, 26), matriculas[alunos["Bia Souza"].Aluno_ID].DataInicio);

            // Os consentimentos marcados, como "Carga inicial", em nome de quem paga (R6).
            var consentimentos = await db.Consentimentos.IgnoreQueryFilters().Where(c => c.Escolinha_ID == b.Escolinha).ToListAsync();
            Assert.Equal(5, consentimentos.Count);
            Assert.All(consentimentos, c => Assert.Equal(OrigemConsentimento.Importacao, c.Origem));
            var imagemAna = consentimentos.Single(c => c.Aluno_ID == alunos["Ana Lima"].Aluno_ID && c.Finalidade == FinalidadeConsentimento.UsoImagem);
            Assert.Equal((false, mariaId), (imagemAna.Concedido, imagemAna.Responsavel_ID));

            // O registro de cada aluno diz a origem, e nenhum registro guarda a saúde (R5, D36).
            var registros = await db.RegistrosAlteracao.IgnoreQueryFilters().Where(r => r.Escolinha_ID == b.Escolinha).ToListAsync();
            Assert.Equal(4, registros.Count(r => r.Entidade == ServicoAlunos.Entidade && r.Motivo != null && r.Motivo.StartsWith("Carga inicial: alunos.xlsx, linha")));
            Assert.DoesNotContain(registros, r => $"{r.ValorAnterior} {r.ValorNovo} {r.Motivo}".Contains("camarão")
                                                  || $"{r.ValorAnterior} {r.ValorNovo} {r.Motivo}".Contains("bombinha"));

            // O lote confirmado não guarda mais a planilha.
            var gravado = await db.ImportacaoLotes.IgnoreQueryFilters().SingleAsync(l => l.ImportacaoLote_ID == lote);
            Assert.Equal((SituacaoImportacao.Confirmada, (string?)null, 4, 3, 1, 1), (gravado.Situacao, gravado.Conteudo, gravado.AlunosCriados,
                gravado.ResponsaveisCriados, gravado.ResponsaveisReaproveitados, gravado.LinhasNaoImportadas));
        }

        // A confirmação não se repete, e o lote encerrado só mostra o resumo.
        Assert.Equal("Este lote não está mais aberto.", (await Gestor(b, s => s.ConfirmarAsync(lote, plano.Assinatura, true))).Mensagem);
        Assert.Null(await Gestor(b, s => s.ConferenciaAsync(lote)));
        Assert.Equal(SituacaoImportacao.Confirmada, (await Gestor(b, s => s.LoteAsync(lote)))!.Situacao);
    }

    /// <summary>Regra de ouro 4: entre a conferência e a confirmação, a base mudou; nada é gravado, e a tela pede outra conferência.</summary>
    [FatoComBanco]
    public async Task A_confirmacao_nao_grava_se_a_escolinha_mudou_depois_da_conferencia()
    {
        var b = await MontarAsync();
        var lote = Id(await EnviarAsync(b, Planilha(Cabecalho,
            ["Davi Rocha", "01/01/2014", "012.345.678-90", "Sub-11", null, null, null, null, null, "Rui Rocha", null, "43944443333"])));
        var assinatura = (await Gestor(b, s => s.ConferenciaAsync(lote)))!.Plano.Assinatura;

        // Outra pessoa da gestão cadastra o Davi, com o mesmo CPF, antes da confirmação.
        var r = NovoAlunoRequisicao.Vazia(new DateOnly(2026, 9, 26));
        r.Nome = "Davi Rocha";
        r.DataNascimento = new DateOnly(2014, 1, 1);
        r.Cpf = "012.345.678-90";
        r.TurmaId = b.Sub11;
        r.Responsaveis[0].Nome = "Rui Rocha";
        r.Responsaveis[0].Telefone = "43944443333";
        Id(await Gestor(b, sp => sp.GetRequiredService<ServicoAlunos>().CriarAsync(r)));

        var resultado = await Gestor(b, s => s.ConfirmarAsync(lote, assinatura, true));
        Assert.StartsWith("O resultado mudou desde a conferência", resultado.Mensagem);
        var davi = Assert.Single((await Gestor(b, s => s.ConferenciaAsync(lote)))!.Plano.Linhas);
        Assert.Equal("Já existe o aluno Davi Rocha com este CPF.", Assert.Single(davi.Bloqueios));
        await using var db = BancoDeTestes.NovoContextoSemEscolinha();
        Assert.Equal(1, await db.Alunos.IgnoreQueryFilters().CountAsync(a => a.Escolinha_ID == b.Escolinha && a.Nome == "Davi Rocha"));
        Assert.Equal(1, await db.Responsaveis.IgnoreQueryFilters().CountAsync(x => x.Escolinha_ID == b.Escolinha && x.Nome == "Rui Rocha"));
    }

    /// <summary>A importação é da gestão (cria aluno, mensalidade e saúde), e o lote de uma escolinha não existe para a outra.</summary>
    [FatoComBanco]
    public async Task So_a_gestao_importa_e_o_lote_de_uma_escolinha_nao_existe_para_a_outra()
    {
        var a = await MontarAsync();
        var lote = Id(await EnviarAsync(a, Planilha(Cabecalho,
            ["Ana Lima", "03/02/2013", null, "Sub-13", null, null, null, null, null, "Maria Lima", CpfMaria, "43988887777"])));

        _app.Usuario.Id = Guid.NewGuid();
        var professor = await _app.NaEscolinhaAsync(a.Escolinha, [PapelAtuacao.Professor], async sp =>
        {
            var s = sp.GetRequiredService<ServicoImportacao>();
            return (s.PodeImportar, (await s.EnviarAsync(Planilha(Cabecalho), "x.xlsx", 10)).Mensagem, await s.ListarAsync(),
                await s.ConferenciaAsync(lote), await s.ModeloAsync());
        });
        Assert.Equal((false, "Só a gestão importa a planilha.", 0, (ConferenciaLote?)null, (byte[]?)null),
            (professor.PodeImportar, professor.Mensagem, professor.Item3.Count, professor.Item4, professor.Item5));

        var b = await MontarAsync("Escolinha B", "gestao@escolinhab.com");
        Assert.Null(await Gestor(b, s => s.LoteAsync(lote)));
        Assert.Null(await Gestor(b, s => s.MapeamentoAsync(lote)));
        Assert.False((await Gestor(b, s => s.ConfirmarAsync(lote, "x", true))).Sucesso);
        Assert.False((await Gestor(b, s => s.DescartarAsync(lote))).Sucesso);
        Assert.Empty(await Gestor(b, s => s.ListarAsync()));
        Assert.Equal(SituacaoImportacao.Aberta, (await Gestor(a, s => s.LoteAsync(lote)))!.Situacao);
    }

    [FatoComBanco]
    public async Task O_arquivo_que_nao_serve_volta_com_o_motivo_e_o_modelo_traz_as_colunas_e_as_turmas()
    {
        var b = await MontarAsync();
        Assert.Equal("Envie a planilha no formato .xlsx (no Excel ou no Google Planilhas: salvar como .xlsx).",
            (await Gestor(b, s => s.EnviarAsync(new MemoryStream([1, 2, 3]), "alunos.csv", 3))).Mensagem);
        Assert.StartsWith("Não deu para abrir o arquivo como planilha .xlsx",
            (await Gestor(b, s => s.EnviarAsync(new MemoryStream([1, 2, 3]), "alunos.xlsx", 3))).Mensagem);
        Assert.Equal("A planilha passa de 2 MB: divida em partes.",
            (await Gestor(b, s => s.EnviarAsync(new MemoryStream([1]), "alunos.xlsx", ImportacaoLote.MaximoBytes + 1))).Mensagem);

        // O modelo: o cabeçalho com todas as colunas (o mapeamento reconhece todas), nenhuma linha, e as turmas nas instruções.
        var modelo = (await Gestor(b, s => s.ModeloAsync()))!;
        var lido = new MinhaEscolinha.Infrastructure.Carga.PlanilhasClosedXml().Ler(new MemoryStream(modelo));
        Assert.Equal(CatalogoCamposImportacao.Todos.Select(c => c.Rotulo), lido.Planilha!.Cabecalhos);
        Assert.Equal("A planilha não tem linhas com dado abaixo do cabeçalho.",
            (await Gestor(b, s => s.EnviarAsync(new MemoryStream(modelo), "modelo.xlsx", modelo.Length))).Mensagem);
        using var livro = new XLWorkbook(new MemoryStream(modelo));
        var instrucoes = livro.Worksheet("Instruções").CellsUsed().Select(c => c.GetString()).ToList();
        Assert.Contains("Sub-11", instrucoes);
        Assert.Contains("Sub-13", instrucoes);
    }

    [FatoComBanco]
    public async Task O_lote_descartado_ou_esquecido_apaga_a_planilha_sem_gravar_nada()
    {
        var b = await MontarAsync();
        object?[] linha = ["Ana Lima", "03/02/2013", null, "Sub-13", null, "Alergia a camarão", null, null, null, "Maria Lima", CpfMaria, "43988887777"];
        var descartado = Id(await EnviarAsync(b, Planilha(Cabecalho, linha)));
        Assert.True((await Gestor(b, s => s.DescartarAsync(descartado))).Sucesso);

        // O lote aberto e esquecido vence em 7 dias: o envio seguinte o descarta.
        var esquecido = Id(await EnviarAsync(b, Planilha(Cabecalho, linha)));
        _app.Tempo.Advance(TimeSpan.FromDays(ImportacaoLote.DiasAberto + 1));
        var novo = Id(await EnviarAsync(b, Planilha(Cabecalho, linha)));

        await using var db = BancoDeTestes.NovoContextoSemEscolinha();
        var lotes = await db.ImportacaoLotes.IgnoreQueryFilters().Where(l => l.Escolinha_ID == b.Escolinha).ToDictionaryAsync(l => l.ImportacaoLote_ID);
        Assert.Equal((SituacaoImportacao.Descartada, (string?)null, "Gestão"), (lotes[descartado].Situacao, lotes[descartado].Conteudo, lotes[descartado].EncerradoPorNome));
        Assert.Equal((SituacaoImportacao.Descartada, (string?)null, "Sistema"), (lotes[esquecido].Situacao, lotes[esquecido].Conteudo, lotes[esquecido].EncerradoPorNome));
        Assert.Equal(SituacaoImportacao.Aberta, lotes[novo].Situacao);
        Assert.DoesNotContain("camarão", lotes[novo].Conteudo); // a planilha aberta fica cifrada
        Assert.Equal(1, await db.Alunos.IgnoreQueryFilters().CountAsync(a => a.Escolinha_ID == b.Escolinha));
    }
}
