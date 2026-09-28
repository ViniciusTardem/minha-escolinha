using System.Text.Json;
using MinhaEscolinha.Application.Acesso;
using MinhaEscolinha.Application.Alunos;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Configuracao;
using MinhaEscolinha.Application.Financeiro;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Domain.Alunos;
using MinhaEscolinha.Domain.Carga;
using MinhaEscolinha.Domain.Comum;

namespace MinhaEscolinha.Application.Carga;

/// <summary>
/// A carga inicial da escolinha por planilha (S7): enviar e ler (BL-M12-01), mapear as colunas, conferir com as linhas
/// classificadas (BL-M12-02) e os responsáveis sem duplicidade (BL-M12-03), e gravar tudo numa operação só, depois da
/// confirmação da gestão (BL-M12-04). A saúde não aparece na conferência e vai direto para os campos restritos da ficha
/// (BL-M12-05, R5). Só a gestão importa: a carga cria alunos, valores de mensalidade e dado de saúde de uma vez.
/// </summary>
public class ServicoImportacao(
    ICargaRepositorio repositorio,
    IAlunosRepositorio alunos,
    IPlanilhas planilhas,
    IProtetorSegredos protetor,
    IAutorizacao autorizacao,
    IUsuarioAtual usuarioAtual,
    RegistroDeAlteracoes registro,
    EfeitosFinanceirosDoCadastro financeiro,
    IRelogioBrasil relogio)
{
    public const string Entidade = nameof(ImportacaoLote);
    public const int LotesNaLista = 20;
    private const int TamanhoExemplo = 40;
    private const string LoteNaoEncontrado = "Lote de importação não encontrado.";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Quem importa cria aluno, matrícula com valor e dado de saúde: só quem tem tudo isso na matriz (a gestão).</summary>
    public bool PodeImportar =>
        autorizacao.Nivel(Recurso.CadastroAlunosResponsaveis) == NivelAcesso.Total
        && autorizacao.Pode(Recurso.FinanceiroEscolinha, Operacao.Alterar)
        && autorizacao.Nivel(Recurso.DocumentosObservacoesMedicas) == NivelAcesso.Total;

    public async Task<IReadOnlyList<LoteNaLista>> ListarAsync(CancellationToken ct = default) =>
        PodeImportar ? await repositorio.ListarAsync(LotesNaLista, ct) : [];

    /// <summary>Lê a planilha e abre o lote, com o mapeamento que o cabeçalho sugere. Nada entra no cadastro aqui.</summary>
    public async Task<ResultadoOperacao> EnviarAsync(Stream arquivo, string nomeArquivo, long tamanho, CancellationToken ct = default)
    {
        if (!PodeImportar) return ResultadoOperacao.Erro("Só a gestão importa a planilha.");
        if (tamanho <= 0) return ResultadoOperacao.Erro("Escolha a planilha (.xlsx).", "arquivo");
        if (!nomeArquivo.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            return ResultadoOperacao.Erro("Envie a planilha no formato .xlsx (no Excel ou no Google Planilhas: salvar como .xlsx).", "arquivo");
        if (tamanho > ImportacaoLote.MaximoBytes)
            return ResultadoOperacao.Erro($"A planilha passa de {ImportacaoLote.MaximoBytes / 1024 / 1024} MB: divida em partes.", "arquivo");

        var leitura = planilhas.Ler(arquivo);
        if (leitura.Planilha is not { } planilha) return ResultadoOperacao.Erro(leitura.Erro ?? "Não deu para ler a planilha.", "arquivo");
        if (planilha.Linhas.Count == 0)
            return ResultadoOperacao.Erro("A planilha não tem linhas com dado abaixo do cabeçalho.", "arquivo");
        if (planilha.Linhas.Count > ImportacaoLote.MaximoLinhas)
            return ResultadoOperacao.Erro($"A planilha tem {planilha.Linhas.Count} linhas; o limite é {ImportacaoLote.MaximoLinhas}: divida em partes.", "arquivo");
        if (planilha.Cabecalhos.Count > ImportacaoLote.MaximoColunas)
            return ResultadoOperacao.Erro($"A planilha tem {planilha.Cabecalhos.Count} colunas; o limite é {ImportacaoLote.MaximoColunas}.", "arquivo");

        var agora = relogio.Agora();
        await MarcarEsquecidosAsync(agora, ct);

        var mapa = CatalogoCamposImportacao.Sugerir(planilha.Cabecalhos);
        var nome = Path.GetFileName(nomeArquivo);
        var lote = ImportacaoLote.Nova(nome, protetor.Proteger(JsonSerializer.Serialize(planilha, Json)), planilha.Linhas.Count,
            JsonSerializer.Serialize(mapa, Json), agora, usuarioAtual.Id, usuarioAtual.Nome ?? "Gestão");
        repositorio.Adicionar(lote);
        registro.Registrar(Entidade, lote.ImportacaoLote_ID, "Envio", null, $"{lote.NomeArquivo}: {planilha.Linhas.Count} linha(s) com dado.");
        await repositorio.SalvarAsync(ct);
        return ResultadoOperacao.Ok(
            $"Planilha lida: {planilha.Linhas.Count} linha(s). {mapa.Count} de {planilha.Cabecalhos.Count} coluna(s) reconhecida(s); confira o mapeamento.",
            lote.ImportacaoLote_ID);
    }

    public async Task<MapeamentoLote?> MapeamentoAsync(Guid id, CancellationToken ct = default)
    {
        if (await AbertoAsync(id, ct) is not { } aberto) return null;
        var (lote, planilha) = aberto;
        var mapa = Mapa(lote);
        var campoDaColuna = mapa.ToDictionary(m => m.Value, m => m.Key);
        var colunas = planilha.Cabecalhos.Select((cabecalho, i) =>
        {
            var campo = campoDaColuna.TryGetValue(i, out var c) ? c : (CampoImportacao?)null;
            var valores = planilha.Linhas.Select(l => i < l.Celulas.Count ? LeituraCelulas.Texto(l.Celulas[i]) : null).Where(v => v is not null).ToList();
            // Coluna de saúde não mostra exemplo (BL-M12-05).
            var exemplos = campo is { } escolhido && CatalogoCamposImportacao.Obter(escolhido).Sensivel
                ? []
                : valores.Take(3).Select(v => v!.Length > TamanhoExemplo ? v[..TamanhoExemplo] + "…" : v!).ToList();
            return new ColunaDaPlanilha(i, string.IsNullOrWhiteSpace(cabecalho) ? $"Coluna {i + 1}" : cabecalho, exemplos, valores.Count, campo);
        }).ToList();
        return new MapeamentoLote(lote.ImportacaoLote_ID, lote.NomeArquivo, lote.Linhas, colunas, Faltando(mapa));
    }

    /// <summary>Grava a escolha de cada coluna (a coluna sem campo fica de fora). O mesmo campo em duas colunas não vale.</summary>
    public async Task<ResultadoOperacao> MapearAsync(Guid id, IReadOnlyDictionary<int, CampoImportacao> escolhas, CancellationToken ct = default)
    {
        if (await AbertoAsync(id, ct) is not { } aberto) return ResultadoOperacao.Erro(LoteNaoEncontrado);
        var (lote, planilha) = aberto;
        var validas = escolhas.Where(e => e.Key >= 0 && e.Key < planilha.Cabecalhos.Count && Enum.IsDefined(e.Value)).ToList();
        if (validas.GroupBy(e => e.Value).FirstOrDefault(g => g.Count() > 1) is { } repetido)
            return ResultadoOperacao.Erro($"\"{CatalogoCamposImportacao.Obter(repetido.Key).Rotulo}\" foi escolhido em mais de uma coluna.");

        var mapa = validas.ToDictionary(e => e.Value, e => e.Key);
        lote.DefinirMapeamento(JsonSerializer.Serialize(mapa, Json));
        await repositorio.SalvarAsync(ct);
        var faltando = Faltando(mapa);
        return faltando.Count == 0
            ? ResultadoOperacao.Ok("Mapeamento gravado. Confira as linhas antes de importar.")
            : ResultadoOperacao.Erro($"Falta a coluna de: {string.Join(", ", faltando.Select(f => CatalogoCamposImportacao.Obter(f).Rotulo))}.");
    }

    public async Task<ConferenciaLote?> ConferenciaAsync(Guid id, CancellationToken ct = default)
    {
        if (await AbertoAsync(id, ct) is not { } aberto) return null;
        var (lote, planilha) = aberto;
        var mapa = Mapa(lote);
        var existente = await repositorio.BaseAsync(ct);
        var plano = ConferenciaImportacao.Planejar(planilha, mapa, Decisoes(lote), existente, DateOnly.FromDateTime(relogio.Hoje()));
        return new ConferenciaLote(lote.ImportacaoLote_ID, lote.NomeArquivo, plano, existente.Turmas, Faltando(mapa), mapa.Keys.ToList());
    }

    /// <summary>As escolhas da conferência: a turma de cada valor desconhecido, os alunos parecidos e os responsáveis parecidos.</summary>
    public async Task<ResultadoOperacao> DecidirAsync(Guid id, DecisoesImportacao decisoes, CancellationToken ct = default)
    {
        if (await AbertoAsync(id, ct) is not { } aberto) return ResultadoOperacao.Erro(LoteNaoEncontrado);
        var turmas = (await repositorio.BaseAsync(ct)).Turmas.Select(t => t.Id).ToHashSet();
        var limpas = new DecisoesImportacao
        {
            Turmas = decisoes.Turmas.Where(t => turmas.Contains(t.Value)).ToDictionary(t => t.Key, t => t.Value),
            AlunosNovos = decisoes.AlunosNovos.Distinct().Order().ToList(),
            Responsaveis = decisoes.Responsaveis
        };
        aberto.Lote.DefinirDecisoes(JsonSerializer.Serialize(limpas, Json));
        await repositorio.SalvarAsync(ct);
        return ResultadoOperacao.Ok("Escolhas aplicadas. Confira de novo o resumo antes de importar.");
    }

    /// <summary>
    /// Grava as linhas que podem entrar, numa operação só (BL-M12-04). A conferência é refeita com a base relida; se o
    /// resultado não for o que a gestão viu (<paramref name="assinatura"/>), nada é gravado e a tela pede outra conferência.
    /// </summary>
    /// <param name="declaracao">A gestão declara que as autorizações marcadas na planilha foram dadas pelos responsáveis (R6, P15).</param>
    public async Task<ResultadoOperacao> ConfirmarAsync(Guid id, string? assinatura, bool declaracao, CancellationToken ct = default)
    {
        if (await AbertoAsync(id, ct) is not { } aberto) return ResultadoOperacao.Erro("Este lote não está mais aberto.");
        var (lote, planilha) = aberto;
        var mapa = Mapa(lote);
        if (Faltando(mapa).Count > 0) return ResultadoOperacao.Erro("Termine o mapeamento das colunas antes de importar.");

        var existente = await repositorio.BaseAsync(ct);
        var plano = ConferenciaImportacao.Planejar(planilha, mapa, Decisoes(lote), existente, DateOnly.FromDateTime(relogio.Hoje()));
        if (plano.Assinatura != assinatura)
            return ResultadoOperacao.Erro("O resultado mudou desde a conferência (a escolinha ou as escolhas mudaram): confira de novo antes de importar.");
        if (plano.AlunosNovos == 0) return ResultadoOperacao.Erro("Nenhuma linha pode entrar: corrija a planilha ou as escolhas da conferência.");
        if (plano.TemConsentimento && !declaracao)
            return ResultadoOperacao.Erro("Confirme que as autorizações marcadas na planilha foram dadas pelos responsáveis.", "declaracao");

        var agora = relogio.Agora();
        var autor = usuarioAtual.Nome ?? "Gestão";
        var turmas = existente.Turmas.ToDictionary(t => t.Id);
        var fichas = new Dictionary<string, (Guid Id, string Nome, bool ComAcesso)>();
        foreach (var r in plano.ResponsaveisUsados)
        {
            if (r.ExistenteId is { } existenteId)
            {
                fichas[r.Chave] = (existenteId, r.ExistenteNome ?? r.Nome, r.ComAcesso);
                continue;
            }
            var novo = new Responsavel { Responsavel_ID = Guid.NewGuid(), Cpf = r.Cpf, Telefone = r.Telefone, Email = r.Email };
            novo.DefinirNome(r.Nome);
            alunos.AdicionarResponsavel(novo);
            fichas[r.Chave] = (novo.Responsavel_ID, novo.Nome, false);
        }

        foreach (var linha in plano.Importadas)
        {
            var d = linha.Dados!;
            var aluno = Aluno.Novo(d.Nome, d.Nascimento, d.Inicio, null);
            aluno.Aluno_ID = Guid.NewGuid();
            aluno.Cpf = d.Cpf;
            aluno.Categoria = d.Categoria;
            aluno.Alergias = d.Alergias;
            aluno.Restricoes = d.Restricoes;
            aluno.ObservacoesMedicas = d.ObservacoesMedicas;
            aluno.ContatoEmergenciaNome = d.EmergenciaNome;
            aluno.ContatoEmergenciaTelefone = d.EmergenciaTelefone;
            aluno.ContatoEmergenciaParentesco = d.EmergenciaParentesco;
            alunos.AdicionarAluno(aluno);

            Guid? pagador = null;
            foreach (var v in linha.Vinculos)
            {
                var ficha = fichas[v.Chave];
                var vinculo = new VinculoAlunoResponsavel
                {
                    VinculoAlunoResponsavel_ID = Guid.NewGuid(), Aluno_ID = aluno.Aluno_ID, Responsavel_ID = ficha.Id,
                    Parentesco = v.Parentesco, RecebeNotificacao = true
                };
                vinculo.DefinirFinanceiro(v.Pagador, v.Pagador);
                // Quem já tem acesso vê o filho novo sem esperar a fila de confirmação (D53).
                if (ficha.ComAcesso) vinculo.ConfirmarNaCriacao(agora, usuarioAtual.Id, autor);
                alunos.AdicionarVinculo(vinculo);
                if (v.Pagador) pagador = ficha.Id;
            }

            alunos.AdicionarMatricula(new Matricula
            {
                Matricula_ID = Guid.NewGuid(), Aluno_ID = aluno.Aluno_ID, Turma_ID = d.TurmaId, DataInicio = d.Inicio, Mensalidade = d.Mensalidade
            });

            if (declaracao && pagador is { } autorizou)
                foreach (var (finalidade, resposta) in new[] { (FinalidadeConsentimento.TratamentoDados, d.ConsentimentoDados), (FinalidadeConsentimento.UsoImagem, d.ConsentimentoImagem) })
                    if (resposta is { } concedido)
                        alunos.AdicionarConsentimento(new ConsentimentoAluno
                        {
                            ConsentimentoAluno_ID = Guid.NewGuid(), Aluno_ID = aluno.Aluno_ID, Finalidade = finalidade, Concedido = concedido,
                            VersaoTermo = TermosConsentimento.Obter(finalidade).Versao, Responsavel_ID = autorizou, Origem = OrigemConsentimento.Importacao,
                            RegistradoEm = agora, RegistradoPor_ID = usuarioAtual.Id, RegistradoPorNome = TextoHelper.Truncar(autor, ConsentimentoAluno.TamanhoAutor)!
                        });

            var turma = turmas[d.TurmaId];
            registro.Registrar(ServicoAlunos.Entidade, aluno.Aluno_ID, "Matrícula", null,
                $"Ativo em {turma.Nome} desde {d.Inicio:dd/MM/yyyy}, mensalidade {ServicoAlunos.Moeda(d.Mensalidade)}" +
                (d.Mensalidade != turma.Mensalidade ? $" (a da turma é {ServicoAlunos.Moeda(turma.Mensalidade)})" : string.Empty) +
                $". Responsáveis: {string.Join("; ", linha.Vinculos.Select(v => fichas[v.Chave].Nome + (v.Pagador ? " (paga a mensalidade)" : string.Empty)))}.",
                $"Carga inicial: {lote.NomeArquivo}, linha {linha.Numero}.");
        }

        lote.Confirmar(agora, usuarioAtual.Id, autor, plano.AlunosNovos, plano.ResponsaveisNovos, plano.ResponsaveisReaproveitados, plano.NaoImportadas);
        var resumo = $"{plano.AlunosNovos} aluno(s) com matrícula, {plano.ResponsaveisNovos} responsável(is) novo(s), " +
                     $"{plano.ResponsaveisReaproveitados} reaproveitado(s) e {plano.Vinculos} vínculo(s). " +
                     (plano.NaoImportadas == 0 ? "Todas as linhas entraram." : $"{plano.NaoImportadas} linha(s) ficaram de fora.");
        registro.Registrar(Entidade, lote.ImportacaoLote_ID, "Confirmação", "Aberto", resumo,
            plano.TemConsentimento ? "A gestão declarou que as autorizações marcadas na planilha foram dadas pelos responsáveis." : null);

        try
        {
            await repositorio.SalvarAsync(ct);
        }
        catch (ConflitoDeConcorrenciaException)
        {
            return ResultadoOperacao.Erro("A escolinha mudou enquanto a importação era gravada (outro envio ou um cadastro igual): confira de novo.");
        }
        financeiro.CadastroMudou();
        return ResultadoOperacao.Ok($"Importação concluída: {resumo}", lote.ImportacaoLote_ID);
    }

    /// <summary>
    /// O job diário (P53, na escolinha do contexto): o lote aberto há mais de <see cref="ImportacaoLote.DiasAberto"/> dias é
    /// descartado, e a planilha cifrada sai do banco, mesmo que ninguém envie outra planilha.
    /// </summary>
    public async Task<int> DescartarEsquecidosAsync(CancellationToken ct = default)
    {
        var descartados = await MarcarEsquecidosAsync(relogio.Agora(), ct);
        if (descartados > 0) await repositorio.SalvarAsync(ct);
        return descartados;
    }

    private async Task<int> MarcarEsquecidosAsync(DateTime agora, CancellationToken ct)
    {
        var vencidos = await repositorio.AbertosAntesDeAsync(agora.AddDays(-ImportacaoLote.DiasAberto), ct);
        foreach (var vencido in vencidos)
        {
            vencido.Descartar(agora, null, "Sistema");
            registro.Registrar(Entidade, vencido.ImportacaoLote_ID, "Descarte", "Aberto",
                $"Descartado sem confirmação depois de {ImportacaoLote.DiasAberto} dias (a planilha foi apagada).");
        }
        return vencidos.Count;
    }

    public async Task<ResultadoOperacao> DescartarAsync(Guid id, CancellationToken ct = default)
    {
        if (await AbertoAsync(id, ct) is not { } aberto) return ResultadoOperacao.Erro(LoteNaoEncontrado);
        aberto.Lote.Descartar(relogio.Agora(), usuarioAtual.Id, usuarioAtual.Nome ?? "Gestão");
        registro.Registrar(Entidade, aberto.Lote.ImportacaoLote_ID, "Descarte", "Aberto", "Descartado pela gestão (a planilha foi apagada).");
        await repositorio.SalvarAsync(ct);
        return ResultadoOperacao.Ok($"Lote {aberto.Lote.NomeArquivo} descartado. Nada entrou no cadastro.");
    }

    public async Task<LoteNaLista?> LoteAsync(Guid id, CancellationToken ct = default) =>
        PodeImportar ? await repositorio.ResumoAsync(id, ct) : null;

    /// <summary>O modelo para baixar, com as turmas ativas da escolinha nas instruções.</summary>
    public async Task<byte[]?> ModeloAsync(CancellationToken ct = default) =>
        PodeImportar ? planilhas.Modelo((await repositorio.BaseAsync(ct)).Turmas.Select(t => t.Nome).Order().ToList()) : null;

    private sealed record LoteAberto(ImportacaoLote Lote, PlanilhaLida Planilha);

    /// <summary>O lote aberto com a planilha decifrada; <c>null</c> sem permissão, fora da escolinha, encerrado ou ilegível.</summary>
    private async Task<LoteAberto?> AbertoAsync(Guid id, CancellationToken ct)
    {
        if (!PodeImportar || await repositorio.ObterAsync(id, ct) is not { Aberta: true, Conteudo: { } conteudo } lote) return null;
        return protetor.Desproteger(conteudo) is { } json && JsonSerializer.Deserialize<PlanilhaLida>(json, Json) is { } planilha
            ? new LoteAberto(lote, planilha)
            : null;
    }

    private static Dictionary<CampoImportacao, int> Mapa(ImportacaoLote lote) =>
        JsonSerializer.Deserialize<Dictionary<CampoImportacao, int>>(lote.Mapeamento, Json) ?? [];

    private static DecisoesImportacao Decisoes(ImportacaoLote lote) =>
        JsonSerializer.Deserialize<DecisoesImportacao>(lote.Decisoes, Json) ?? new DecisoesImportacao();

    private static IReadOnlyList<CampoImportacao> Faltando(IReadOnlyDictionary<CampoImportacao, int> mapa) =>
        CatalogoCamposImportacao.Todos.Where(c => c.Obrigatorio && !mapa.ContainsKey(c.Campo)).Select(c => c.Campo).ToList();
}
