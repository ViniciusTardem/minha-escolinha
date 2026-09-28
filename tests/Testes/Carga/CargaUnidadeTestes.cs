using MinhaEscolinha.Application.Carga;
using MinhaEscolinha.Domain.Alunos;
using MinhaEscolinha.Domain.Carga;

namespace MinhaEscolinha.Testes.Carga;

/// <summary>
/// A carga inicial sem banco (S7): o mapeamento pelo cabeçalho (BL-M12-01), a leitura das células, e a conferência que
/// classifica as linhas (BL-M12-02), junta o mesmo responsável (BL-M12-03, CA-1) e esconde a saúde (BL-M12-05).
/// </summary>
public class CargaUnidadeTestes
{
    private static readonly DateOnly Hoje = new(2026, 9, 26);
    private static readonly Guid Sub11 = Guid.NewGuid();
    private static readonly Guid Sub13 = Guid.NewGuid();

    private static readonly string[] Colunas =
    [
        "Nome do aluno", "Data de nascimento", "CPF do aluno", "Turma", "Mensalidade", "Alergias", "Autoriza tratamento de dados",
        "Responsável 1", "CPF do responsável 1", "Celular do responsável 1", "Parentesco do responsável 1",
        "Responsável 2", "Celular do responsável 2"
    ];

    private static LinhaLida Linha(int numero, params string?[] valores) => new(numero, valores);

    private static BaseExistente Base(IReadOnlyList<AlunoExistente>? alunos = null, IReadOnlyList<ResponsavelExistente>? responsaveis = null,
        int capacidadeSub11 = 20) =>
        new(alunos ?? [], responsaveis ?? [],
            [new TurmaExistente(Sub11, "Sub-11", 150m, capacidadeSub11, 0), new TurmaExistente(Sub13, "Sub-13", 140m, 20, 0)]);

    private static PlanoImportacao Planejar(IReadOnlyList<LinhaLida> linhas, BaseExistente? existente = null, DecisoesImportacao? decisoes = null)
    {
        var planilha = new PlanilhaLida(Colunas, linhas);
        return ConferenciaImportacao.Planejar(planilha, CatalogoCamposImportacao.Sugerir(Colunas), decisoes ?? new DecisoesImportacao(),
            existente ?? Base(), Hoje);
    }

    // ── Mapeamento e leitura (BL-M12-01) ──

    [Fact]
    public void O_modelo_e_mapeado_inteiro_e_os_nomes_comuns_de_coluna_tambem()
    {
        var modelo = CatalogoCamposImportacao.Todos.Select(c => c.Rotulo).ToList();
        var mapa = CatalogoCamposImportacao.Sugerir(modelo);
        Assert.Equal(CatalogoCamposImportacao.Todos.Count, mapa.Count);
        Assert.All(CatalogoCamposImportacao.Todos, c => Assert.Equal(modelo.IndexOf(c.Rotulo), mapa[c.Campo]));

        // Uma planilha de escolinha: nomes curtos, acentos, maiúsculas e pontuação.
        var escolinha = CatalogoCamposImportacao.Sugerir(
            ["ALUNO", "Data de Nascimento:", "Turma", "Responsável", "CPF", "WhatsApp", "2º responsável", "Celular 2", "Obs. médicas"]);
        Assert.Equal(0, escolinha[CampoImportacao.AlunoNome]);
        Assert.Equal(1, escolinha[CampoImportacao.AlunoNascimento]);
        Assert.Equal(2, escolinha[CampoImportacao.Turma]);
        Assert.Equal(3, escolinha[CampoImportacao.Responsavel1Nome]);
        Assert.Equal(4, escolinha[CampoImportacao.Responsavel1Cpf]); // "CPF" sozinho é do responsável, não do aluno
        Assert.Equal(5, escolinha[CampoImportacao.Responsavel1Celular]);
        Assert.Equal(6, escolinha[CampoImportacao.Responsavel2Nome]);
        Assert.Equal(7, escolinha[CampoImportacao.Responsavel2Celular]);
        Assert.Equal(8, escolinha[CampoImportacao.ObservacoesMedicas]);
        Assert.False(escolinha.ContainsKey(CampoImportacao.AlunoCpf));
    }

    [Fact]
    public void A_celula_vira_o_valor_do_cadastro_como_a_escolinha_digitou()
    {
        foreach (var texto in new[] { "12/04/2015", "12/4/15", "2015-04-12", "42106" })
        {
            Assert.True(LeituraCelulas.LerData(texto, out var data), texto);
            Assert.Equal(new DateOnly(2015, 4, 12), data);
        }
        Assert.False(LeituraCelulas.LerData("abril de 2015", out _));

        Assert.Equal("52998224725", LeituraCelulas.LerCpf("529.982.247-25"));
        Assert.Equal("01234567890", LeituraCelulas.LerCpf("1234567890")); // célula numérica perdeu o zero à esquerda
        Assert.Null(LeituraCelulas.LerCpf("111.111.111-11"));

        Assert.Equal((true, false, (bool?)null), (LeituraCelulas.LerSimNao("Sim")!.Value, LeituraCelulas.LerSimNao("NÃO")!.Value, LeituraCelulas.LerSimNao("talvez")));
        Assert.Equal(Parentesco.Mae, LeituraCelulas.LerParentesco("Mãe", out var mae));
        Assert.Equal(Parentesco.Avo, LeituraCelulas.LerParentesco("Avô", out _));
        Assert.Equal(Parentesco.Outro, LeituraCelulas.LerParentesco(null, out var vazio));
        Assert.Equal(Parentesco.Outro, LeituraCelulas.LerParentesco("vizinha", out var vizinha));
        Assert.Equal((true, true, false), (mae, vazio, vizinha));
    }

    // ── Conferência (BL-M12-02, BL-M12-03, BL-M12-05) ──

    /// <summary>CA-1 na planilha: irmãos repetem o responsável (um com o CPF, o outro só com nome e celular) e viram um cadastro só.</summary>
    [Fact]
    public void Irmaos_com_o_mesmo_responsavel_viram_um_cadastro_so()
    {
        var plano = Planejar(
        [
            Linha(2, "Pedro Lima", "12/04/2015", null, "Sub-11", null, null, "sim", "Maria Lima", "529.982.247-25", "(43) 98888-7777", "mãe"),
            Linha(3, "Ana Lima", "03/02/2013", null, "sub 13", "R$ 120,00", null, "sim", "Maria  Lima", null, "43988887777", "Mãe")
        ]);

        Assert.All(plano.Linhas, l => Assert.Equal(ClasseLinha.Apta, l.Classe));
        var maria = Assert.Single(plano.Responsaveis);
        Assert.Equal(("cpf:52998224725", "Maria Lima", (Guid?)null), (maria.Chave, maria.Nome, maria.ExistenteId));
        Assert.Equal([2, 3], maria.Linhas);
        Assert.Equal((2, 1, 0, 2), (plano.AlunosNovos, plano.ResponsaveisNovos, plano.ResponsaveisReaproveitados, plano.Vinculos));
        // A turma pelo nome, sem acento nem caixa; a mensalidade da planilha ou a da turma.
        Assert.Equal([(Sub11, 150m), (Sub13, 120m)], plano.Linhas.Select(l => (l.Dados!.TurmaId, l.Dados.Mensalidade)));
        Assert.All(plano.Linhas, l => Assert.True(Assert.Single(l.Vinculos).Pagador));
    }

    [Fact]
    public void O_responsavel_que_ja_existe_e_reaproveitado_pelo_cpf_e_o_parecido_so_com_a_decisao_da_gestao()
    {
        var existente = new ResponsavelExistente(Guid.NewGuid(), "Maria Lima", "maria lima", "52998224725", "+5543988887777", Guid.NewGuid());
        var parecido = new ResponsavelExistente(Guid.NewGuid(), "Rita Alves", "rita alves", null, "+5543955554444", null);
        var linhas = new[]
        {
            Linha(2, "Pedro Lima", "12/04/2015", null, "Sub-11", null, null, null, "Maria Lima", "52998224725", "43988887777"),
            Linha(3, "Bruno Alves", "20/05/2015", null, "Sub-11", null, null, null, "Rita de Alves", null, "(43) 95555-4444")
        };

        var plano = Planejar(linhas, Base(responsaveis: [existente, parecido]));
        var maria = plano.Responsaveis.Single(r => r.Nome == "Maria Lima");
        Assert.Equal((existente.Id, true, true), (maria.ExistenteId!.Value, maria.PorCpf, maria.ComAcesso));
        // Mesmo celular com outro nome: vira cadastro novo, com aviso, até a gestão escolher o existente.
        var rita = plano.Responsaveis.Single(r => r.Nome == "Rita de Alves");
        Assert.Equal((null, parecido.Id), (rita.ExistenteId, Assert.Single(rita.Parecidos).Id));
        Assert.Equal(ClasseLinha.ComAviso, plano.Linhas[1].Classe);
        Assert.Contains(plano.Linhas[1].Avisos, a => a.StartsWith("Rita de Alves tem cadastro parecido"));
        Assert.Equal((1, 1), (plano.ResponsaveisNovos, plano.ResponsaveisReaproveitados));

        var decidido = Planejar(linhas, Base(responsaveis: [existente, parecido]),
            new DecisoesImportacao { Responsaveis = { [rita.Chave] = parecido.Id } });
        Assert.Equal(parecido.Id, decidido.Responsaveis.Single(r => r.Chave == rita.Chave).ExistenteId);
        Assert.Equal((0, 2), (decidido.ResponsaveisNovos, decidido.ResponsaveisReaproveitados));
        Assert.NotEqual(plano.Assinatura, decidido.Assinatura);
    }

    [Fact]
    public void A_linha_com_erro_fica_bloqueada_com_o_motivo_e_a_turma_desconhecida_espera_a_escolha()
    {
        var alunoComCpf = new AlunoExistente(Guid.NewGuid(), "Carla Souza", "carla souza", new DateOnly(2014, 1, 1), "01234567890");
        var plano = Planejar(
        [
            Linha(2, null, "12/04/2015", null, "Sub-11", null, null, null, "Maria Lima", null, "43988887777"),
            Linha(3, "Bia Souza", "31/02/2015", null, "Sub-11", null, null, null, "Rita Souza", null, "43955554444"),
            Linha(4, "Carla Souza", "01/01/2014", "012.345.678-90", "Sub-11", null, null, null, "Rita Souza", null, "43955554444"),
            Linha(5, "Davi Rocha", "01/01/2014", null, "Futsal manhã", null, null, null, "Rui Rocha", null, "43944443333"),
            Linha(6, "Eva Rocha", "01/01/2016", null, "Sub-11", "cento e cinquenta", null, null, "Rui Rocha", null, null),
            Linha(7, "Fábio Rocha", "01/01/2016", null, "Sub-11", null, null, null, "Rui Rocha", null, "43944443333"),
            Linha(8, "Fábio Rocha", "01/01/2016", null, "Sub-11", null, null, null, "Rui Rocha", null, "43944443333")
        ], Base(alunos: [alunoComCpf]));

        string[] Bloqueios(int linha) => plano.Linhas.Single(l => l.Numero == linha).Bloqueios.ToArray();
        Assert.Equal(["Sem o nome do aluno."], Bloqueios(2));
        Assert.Equal(["Data de nascimento inválida: \"31/02/2015\"."], Bloqueios(3));
        Assert.Equal(["Já existe o aluno Carla Souza com este CPF."], Bloqueios(4));
        Assert.Equal(["A turma \"Futsal manhã\" não existe: escolha a turma correspondente no quadro de turmas."], Bloqueios(5));
        Assert.Equal(["Mensalidade inválida: \"cento e cinquenta\".", "O celular do responsável 1 está vazio (é o canal com a família)."], Bloqueios(6));
        Assert.Empty(Bloqueios(7));
        Assert.Equal(["Aluno repetido na linha 7."], Bloqueios(8));
        Assert.Equal(1, plano.AlunosNovos);

        // A turma que não existe: a gestão escolhe a turma do sistema, e a linha entra.
        var turma = Assert.Single(plano.TurmasSemCorrespondencia);
        Assert.Equal(("Futsal manhã", 1, (Guid?)null), (turma.Valor, turma.Linhas, turma.Escolhida));
        var decidido = Planejar([Linha(5, "Davi Rocha", "01/01/2014", null, "Futsal manhã", null, null, null, "Rui Rocha", null, "43944443333")],
            decisoes: new DecisoesImportacao { Turmas = { [turma.Chave] = Sub13 } });
        Assert.Equal((true, Sub13, Sub13), (decidido.Linhas[0].Importar, decidido.Linhas[0].Dados!.TurmaId,
            Assert.Single(decidido.TurmasSemCorrespondencia).Escolhida!.Value));
    }

    [Fact]
    public void O_aluno_parecido_so_entra_quando_a_gestao_confirma_que_e_outra_crianca()
    {
        var existente = new AlunoExistente(Guid.NewGuid(), "Pedro Lima", "pedro lima", new DateOnly(2015, 4, 12), null);
        var linhas = new[] { Linha(2, "Pedro  Lima", "12/04/2015", null, "Sub-11", null, null, null, "Maria Lima", null, "43988887777") };

        var plano = Planejar(linhas, Base(alunos: [existente]));
        var pedro = Assert.Single(plano.Linhas);
        Assert.Equal((ClasseLinha.ComAviso, true, false), (pedro.Classe, pedro.AlunoParecido, pedro.Importar));
        Assert.Equal(0, plano.AlunosNovos);

        var confirmado = Planejar(linhas, Base(alunos: [existente]), new DecisoesImportacao { AlunosNovos = [2] });
        Assert.True(Assert.Single(confirmado.Linhas).Importar);
    }

    /// <summary>BL-M12-05: a saúde entra na linha, mas nenhuma mensagem da conferência mostra o conteúdo.</summary>
    [Fact]
    public void A_saude_entra_sem_aparecer_na_conferencia_e_o_segundo_responsavel_incompleto_fica_de_fora()
    {
        var plano = Planejar(
        [
            Linha(2, "Pedro Lima", "12/04/2015", null, "Sub-11", null, "Alergia a amendoim", "talvez", "Maria Lima", null, "43988887777", null,
                "João Lima", null)
        ]);

        var pedro = Assert.Single(plano.Linhas);
        Assert.Equal((ClasseLinha.ComAviso, true, true), (pedro.Classe, pedro.SaudeInformada, pedro.Importar));
        Assert.Equal("Alergia a amendoim", pedro.Dados!.Alergias);
        Assert.DoesNotContain(pedro.Avisos.Concat(pedro.Bloqueios), m => m.Contains("amendoim", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("O celular do responsável 2 está vazio (é o canal com a família): o responsável 2 fica de fora; complete na ficha.", pedro.Avisos);
        Assert.Contains("Autorização de tratamento de dados \"talvez\" não reconhecida (use sim ou não): fica pendente na ficha.", pedro.Avisos);
        Assert.Single(pedro.Vinculos);
        Assert.Null(pedro.Dados.ConsentimentoDados);
    }

    [Fact]
    public void A_turma_cheia_avisa_e_nao_bloqueia()
    {
        var plano = Planejar(
        [
            Linha(2, "Pedro Lima", "12/04/2015", null, "Sub-11", null, null, "sim", "Maria Lima", "52998224725", "43988887777"),
            Linha(3, "Bruno Alves", "20/05/2015", null, "Sub-11", null, null, "sim", "Rita Alves", null, "43955554444")
        ], Base(capacidadeSub11: 1));

        Assert.All(plano.Linhas, l =>
        {
            Assert.True(l.Importar);
            Assert.Contains("A turma Sub-11 passa da capacidade (2 de 1 vagas).", l.Avisos);
        });
    }

    [Fact]
    public void O_lote_encerrado_apaga_a_planilha_e_nao_muda_mais()
    {
        var lote = ImportacaoLote.Nova("alunos.xlsx", "cifrado", 3, "{}", new DateTime(2026, 9, 26, 12, 0, 0), Guid.NewGuid(), "Gestão");
        lote.Confirmar(new DateTime(2026, 9, 26, 12, 5, 0), Guid.NewGuid(), "Gestão", 2, 1, 1, 1);
        Assert.Equal((SituacaoImportacao.Confirmada, (string?)null, 2), (lote.Situacao, lote.Conteudo, lote.AlunosCriados));
        Assert.Throws<InvalidOperationException>(() => lote.Descartar(new DateTime(2026, 9, 26, 12, 6, 0), null, "Gestão"));
        Assert.Throws<InvalidOperationException>(() => lote.DefinirMapeamento("{}"));
    }
}
