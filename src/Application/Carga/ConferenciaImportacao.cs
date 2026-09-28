using System.Globalization;
using MinhaEscolinha.Application.Alunos;
using MinhaEscolinha.Domain.Alunos;
using MinhaEscolinha.Domain.Comum;
using MinhaEscolinha.Domain.Turmas;

namespace MinhaEscolinha.Application.Carga;

/// <summary>A leitura de uma célula: o texto como a escolinha digitou vira o valor do cadastro.</summary>
public static class LeituraCelulas
{
    private static readonly CultureInfo PtBr = new("pt-BR");

    private static readonly string[] FormatosData =
        ["d/M/yyyy", "dd/MM/yyyy", "d/M/yy", "dd/MM/yy", "yyyy-MM-dd", "d-M-yyyy", "dd-MM-yyyy", "d.M.yyyy", "dd.MM.yyyy"];

    public static string? Texto(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    /// <summary>"12/04/2015", "12/4/15", "2015-04-12" ou o número de série do Excel (célula de data sem formato).</summary>
    public static bool LerData(string? valor, out DateOnly data)
    {
        data = default;
        if (Texto(valor) is not { } texto) return false;
        if (DateOnly.TryParseExact(texto, FormatosData, PtBr, DateTimeStyles.None, out data)) return true;
        if (double.TryParse(texto, NumberStyles.Float, CultureInfo.InvariantCulture, out var serie) && serie is > 1 and < 80000
            && Math.Abs(serie - Math.Floor(serie)) < double.Epsilon)
        {
            data = DateOnly.FromDateTime(DateTime.FromOADate(serie));
            return true;
        }
        return false;
    }

    /// <summary>O CPF válido, só com os dígitos. Célula numérica perde os zeros à esquerda: com 9 ou 10 dígitos, completa até 11.</summary>
    public static string? LerCpf(string? valor)
    {
        var digitos = TextoHelper.SomenteDigitos(valor);
        if (digitos.Length is >= 9 and < 11) digitos = digitos.PadLeft(11, '0');
        return digitos.Length == 0 ? null : Cpf.Normalizar(digitos);
    }

    /// <summary>"sim", "s", "x", "autoriza"... → verdadeiro; "não", "n", "recusa"... → falso; o resto → não reconhecido.</summary>
    public static bool? LerSimNao(string? valor) => CatalogoCamposImportacao.NormalizarCabecalho(valor) switch
    {
        "sim" or "s" or "x" or "autoriza" or "autorizado" or "autorizada" or "concedido" or "ok" or "1" => true,
        "nao" or "n" or "nao autoriza" or "recusa" or "recusado" or "recusada" or "0" => false,
        _ => null
    };

    /// <summary>O parentesco da planilha; vazio ou não reconhecido vira "Outro" (nada é deduzido do nome da pessoa).</summary>
    public static Parentesco LerParentesco(string? valor, out bool reconhecido)
    {
        reconhecido = true;
        switch (CatalogoCamposImportacao.NormalizarCabecalho(valor))
        {
            case "mae": return Parentesco.Mae;
            case "pai": return Parentesco.Pai;
            case "avo" or "avos": return Parentesco.Avo;
            case "tutor" or "tutora" or "tutor legal" or "tutora legal" or "responsavel legal": return Parentesco.Tutor;
            case "" or "outro" or "outra" or "tio" or "tia" or "irmao" or "irma" or "padrasto" or "madrasta": return Parentesco.Outro;
            default:
                reconhecido = false;
                return Parentesco.Outro;
        }
    }

    /// <summary>A mesma regra de e-mail do cadastro: um "@" no meio e nenhum espaço.</summary>
    public static bool EmailValido(string email) =>
        email.Count(c => c == '@') == 1 && !email.StartsWith('@') && !email.EndsWith('@') && !email.Any(char.IsWhiteSpace);
}

/// <summary>
/// A conferência da planilha (BL-M12-02 a BL-M12-05): cada linha vira apta, com aviso ou bloqueada, com o motivo; o mesmo
/// responsável em várias linhas (irmãos) vira um cadastro só, e o que já existe na escolinha é reaproveitado pelo CPF
/// (CA-1). Mesmo celular ou mesmo nome sem o mesmo CPF só sugere, e a gestão decide (D33). Nada é gravado aqui: a mesma
/// conta é refeita na confirmação, com a base relida (regra de ouro 4). Função pura, sem banco.
/// </summary>
public static class ConferenciaImportacao
{
    /// <summary>Um responsável lido de uma linha, antes de virar o cadastro (novo ou reaproveitado).</summary>
    private sealed record Lido(int Ordem, string Nome, string NomeBusca, string? Cpf, string Telefone, string? Email, Parentesco Parentesco)
    {
        public string NomeCelular => $"{NomeBusca}|{Telefone}";
    }

    private sealed class Rascunho(LinhaLida linha)
    {
        public LinhaLida Linha { get; } = linha;
        public List<string> Bloqueios { get; } = [];
        public List<string> Avisos { get; } = [];
        public List<Lido> Lidos { get; } = [];
        public List<VinculoPlanejado> Vinculos { get; } = [];
        public string Aluno { get; set; } = "(sem nome)";
        public string? Turma { get; set; }
        public DadosDaLinha? Dados { get; set; }
        public bool Parecido { get; set; }
        public bool Importar { get; set; }
        public bool Saude { get; set; }
        public bool Contato { get; set; }
    }

    private sealed class Grupo(string chave, Lido primeiro, string? cpf, int linha)
    {
        public string Chave { get; } = chave;
        public Lido Primeiro { get; } = primeiro;
        public string? Cpf { get; } = cpf;
        public int PrimeiraLinha { get; } = linha;
        public List<int> Linhas { get; } = [linha];
    }

    public static PlanoImportacao Planejar(PlanilhaLida planilha, IReadOnlyDictionary<CampoImportacao, int> mapa, DecisoesImportacao decisoes,
        BaseExistente existente, DateOnly hoje)
    {
        var turmasPorNome = existente.Turmas.GroupBy(t => Chave(t.Nome)).ToDictionary(g => g.Key, g => g.First());
        var turmasPorId = existente.Turmas.ToDictionary(t => t.Id);
        var alunosPorCpf = existente.Alunos.Where(a => a.Cpf is not null).GroupBy(a => a.Cpf!).ToDictionary(g => g.Key, g => g.First());
        var responsaveisPorCpf = existente.Responsaveis.Where(r => r.Cpf is not null).GroupBy(r => r.Cpf!).ToDictionary(g => g.Key, g => g.First());
        var mapeouDados = mapa.ContainsKey(CampoImportacao.ConsentimentoDados);

        var semCorrespondencia = new Dictionary<string, (string Valor, int Linhas)>();
        var cpfsDeAluno = new Dictionary<string, int>();
        var alunosDaPlanilha = new Dictionary<string, int>();
        var rascunhos = planilha.Linhas.Select(l => new Rascunho(l)).ToList();

        foreach (var r in rascunhos)
        {
            var linha = r.Linha;
            string? Celula(CampoImportacao campo) =>
                mapa.TryGetValue(campo, out var i) && i < linha.Celulas.Count ? LeituraCelulas.Texto(linha.Celulas[i]) : null;
            void Tamanho(string? valor, int maximo, string rotulo)
            {
                if (valor is not null && valor.Length > maximo) r.Bloqueios.Add($"{rotulo} passa de {maximo} caracteres.");
            }

            // Aluno.
            var nome = Celula(CampoImportacao.AlunoNome);
            if (nome is null) r.Bloqueios.Add("Sem o nome do aluno.");
            else r.Aluno = nome;
            Tamanho(nome, Aluno.TamanhoNome, "O nome do aluno");

            var nascimentoTexto = Celula(CampoImportacao.AlunoNascimento);
            var nascimento = default(DateOnly);
            if (nascimentoTexto is null) r.Bloqueios.Add("Sem a data de nascimento.");
            else if (!LeituraCelulas.LerData(nascimentoTexto, out nascimento) || nascimento < RegrasAluno.NascimentoMinimo || nascimento > hoje)
                r.Bloqueios.Add($"Data de nascimento inválida: \"{nascimentoTexto}\".");

            string? cpfAluno = null;
            if (Celula(CampoImportacao.AlunoCpf) is { } cpfAlunoTexto)
            {
                cpfAluno = LeituraCelulas.LerCpf(cpfAlunoTexto);
                if (cpfAluno is null) r.Bloqueios.Add("CPF do aluno inválido.");
                else if (alunosPorCpf.TryGetValue(cpfAluno, out var mesmoCpf)) r.Bloqueios.Add($"Já existe o aluno {mesmoCpf.Nome} com este CPF.");
                else if (cpfsDeAluno.TryGetValue(cpfAluno, out var anterior)) r.Bloqueios.Add($"CPF do aluno repetido na linha {anterior}.");
                else cpfsDeAluno[cpfAluno] = linha.Numero;
            }

            var categoria = Celula(CampoImportacao.AlunoCategoria);
            Tamanho(categoria, Aluno.TamanhoCategoria, "A categoria");

            if (nome is not null && r.Bloqueios.Count == 0)
            {
                var nomeBusca = TextoHelper.NormalizarParaBusca(nome);
                var chaveAluno = $"{nomeBusca}|{nascimento:yyyyMMdd}";
                if (alunosDaPlanilha.TryGetValue(chaveAluno, out var repetido)) r.Bloqueios.Add($"Aluno repetido na linha {repetido}.");
                else
                {
                    alunosDaPlanilha[chaveAluno] = linha.Numero;
                    r.Parecido = existente.Alunos.Any(a => a.NomeBusca == nomeBusca && a.Nascimento == nascimento);
                }
            }

            // Matrícula: a turma pelo nome, ou pela escolha da gestão no quadro de turmas.
            var turmaTexto = Celula(CampoImportacao.Turma);
            TurmaExistente? turma = null;
            if (turmaTexto is null) r.Bloqueios.Add("Sem a turma.");
            else if (!turmasPorNome.TryGetValue(Chave(turmaTexto), out turma))
            {
                var chave = Chave(turmaTexto);
                var visto = semCorrespondencia.GetValueOrDefault(chave);
                semCorrespondencia[chave] = (visto.Valor ?? turmaTexto, visto.Linhas + 1);
                if (decisoes.Turmas.TryGetValue(chave, out var escolhida) && turmasPorId.TryGetValue(escolhida, out var dePara)) turma = dePara;
                else r.Bloqueios.Add($"A turma \"{turmaTexto}\" não existe: escolha a turma correspondente no quadro de turmas.");
            }
            r.Turma = turma?.Nome ?? turmaTexto;

            var inicio = hoje;
            if (Celula(CampoImportacao.InicioNaTurma) is { } inicioTexto && !LeituraCelulas.LerData(inicioTexto, out inicio))
                r.Bloqueios.Add($"Início na turma inválido: \"{inicioTexto}\".");

            decimal? mensalidade = null;
            if (Celula(CampoImportacao.Mensalidade) is { } mensalidadeTexto)
            {
                if (Moeda.TentarLer(mensalidadeTexto, out var valor) && valor >= 0 && valor <= Turma.MensalidadeMaxima) mensalidade = valor;
                else r.Bloqueios.Add($"Mensalidade inválida: \"{mensalidadeTexto}\".");
            }

            // Saúde: só o tamanho, nunca o conteúdo na mensagem (BL-M12-05).
            var alergias = Celula(CampoImportacao.Alergias);
            var restricoes = Celula(CampoImportacao.Restricoes);
            var observacoes = Celula(CampoImportacao.ObservacoesMedicas);
            Tamanho(alergias, Aluno.TamanhoSaude, "Alergias");
            Tamanho(restricoes, Aluno.TamanhoSaude, "Restrições");
            Tamanho(observacoes, Aluno.TamanhoObservacoesMedicas, "Observações médicas");
            r.Saude = alergias is not null || restricoes is not null || observacoes is not null;

            var contatoNome = Celula(CampoImportacao.EmergenciaNome);
            var contatoParentesco = Celula(CampoImportacao.EmergenciaParentesco);
            Tamanho(contatoNome, Aluno.TamanhoNome, "O contato de emergência");
            Tamanho(contatoParentesco, Aluno.TamanhoParentesco, "O parentesco do contato de emergência");
            string? contatoTelefone = null;
            if (Celula(CampoImportacao.EmergenciaTelefone) is { } telefoneEmergencia)
            {
                contatoTelefone = Telefone.ParaE164(telefoneEmergencia);
                if (contatoTelefone is null) r.Avisos.Add("Telefone de emergência inválido: fica em branco.");
            }
            r.Contato = contatoNome is not null || contatoTelefone is not null;

            // Consentimentos (R6): a declaração da gestão vale para o que a planilha marcou.
            var dados = Consentimento(Celula(CampoImportacao.ConsentimentoDados), "tratamento de dados", r.Avisos);
            if (mapeouDados && Celula(CampoImportacao.ConsentimentoDados) is null)
                r.Avisos.Add("Sem a autorização de tratamento de dados: fica pendente na ficha.");
            var imagem = Consentimento(Celula(CampoImportacao.ConsentimentoImagem), "uso de imagem", r.Avisos);

            // Responsáveis: o 1º paga a mensalidade (D34) e é obrigatório; o 2º, incompleto, fica de fora com aviso.
            if (LerResponsavel(1, Celula(CampoImportacao.Responsavel1Nome), Celula(CampoImportacao.Responsavel1Cpf),
                    Celula(CampoImportacao.Responsavel1Celular), Celula(CampoImportacao.Responsavel1Email),
                    Celula(CampoImportacao.Responsavel1Parentesco), obrigatorio: true, r) is { } r1)
                r.Lidos.Add(r1);
            if (LerResponsavel(2, Celula(CampoImportacao.Responsavel2Nome), Celula(CampoImportacao.Responsavel2Cpf),
                    Celula(CampoImportacao.Responsavel2Celular), Celula(CampoImportacao.Responsavel2Email),
                    Celula(CampoImportacao.Responsavel2Parentesco), obrigatorio: false, r) is { } r2)
                r.Lidos.Add(r2);

            if (r.Bloqueios.Count == 0)
                r.Dados = new DadosDaLinha(nome!, nascimento, cpfAluno, categoria, turma!.Id, inicio, mensalidade ?? turma.Mensalidade,
                    alergias, restricoes, observacoes, contatoNome, contatoTelefone, contatoParentesco, dados, imagem);
        }

        // O mesmo responsável na planilha: pelo CPF, ou pelo nome com o celular de uma linha que trouxe o CPF (irmãos, CA-1).
        var cpfPorNomeCelular = rascunhos.SelectMany(r => r.Lidos).Where(l => l.Cpf is not null)
            .GroupBy(l => l.NomeCelular).ToDictionary(g => g.Key, g => g.First().Cpf!);
        var grupos = new Dictionary<string, Grupo>();
        foreach (var r in rascunhos)
            foreach (var lido in r.Lidos)
            {
                var cpf = lido.Cpf ?? cpfPorNomeCelular.GetValueOrDefault(lido.NomeCelular);
                var chave = cpf is not null ? $"cpf:{cpf}" : $"nome:{lido.NomeCelular}";
                if (r.Vinculos.Any(v => v.Chave == chave))
                {
                    r.Avisos.Add("O responsável 2 é o mesmo do responsável 1: fica um só.");
                    continue;
                }
                if (!grupos.TryGetValue(chave, out var grupo)) grupos[chave] = new Grupo(chave, lido, cpf, r.Linha.Numero);
                else
                {
                    if (!grupo.Linhas.Contains(r.Linha.Numero)) grupo.Linhas.Add(r.Linha.Numero);
                    if (grupo.Primeiro.NomeBusca != lido.NomeBusca)
                        r.Avisos.Add($"O CPF de {lido.Nome} é o mesmo de {grupo.Primeiro.Nome} (linha {grupo.PrimeiraLinha}): fica o cadastro de {grupo.Primeiro.Nome}.");
                }
                r.Vinculos.Add(new VinculoPlanejado(chave, lido.Parentesco, Pagador: lido.Ordem == 1));
                if (lido.Ordem == 1 && cpf is null)
                    r.Avisos.Add("O responsável 1 está sem CPF: a mensalidade não vai para a Asaas até o CPF ser informado na ficha.");
            }

        // Os responsáveis contra o cadastro: o mesmo CPF reaproveita; celular ou nome iguais só sugerem.
        var planejados = new List<ResponsavelPlanejado>();
        var avisoPorChave = new Dictionary<string, string>();
        foreach (var g in grupos.Values)
        {
            var p = g.Primeiro;
            if (g.Cpf is not null && responsaveisPorCpf.TryGetValue(g.Cpf, out var mesmoCpf))
            {
                planejados.Add(new ResponsavelPlanejado(g.Chave, p.Nome, g.Cpf, p.Telefone, p.Email, mesmoCpf.Id, mesmoCpf.Nome,
                    mesmoCpf.PessoaId is not null, true, [], g.Linhas));
                continue;
            }

            var parecidos = existente.Responsaveis
                .Where(e => e.Telefone == p.Telefone || e.NomeBusca == p.NomeBusca)
                .Where(e => g.Cpf is null || e.Cpf is null || e.Cpf == g.Cpf)
                .ToList();
            ResponsavelExistente? escolhido;
            if (decisoes.Responsaveis.TryGetValue(g.Chave, out var decisao))
                escolhido = decisao is { } id ? parecidos.FirstOrDefault(e => e.Id == id) : null;
            else
                // Sem decisão, só o mesmo nome com o mesmo celular aponta para o cadastro existente; o resto vira cadastro novo.
                escolhido = parecidos.FirstOrDefault(e => e.NomeBusca == p.NomeBusca && e.Telefone == p.Telefone);
            if (parecidos.Count > 0)
                avisoPorChave[g.Chave] = escolhido is null
                    ? $"{p.Nome} tem cadastro parecido na escolinha (mesmo celular ou nome): vai como cadastro novo; confira no quadro de responsáveis."
                    : $"{p.Nome} vai usar o cadastro de {escolhido.Nome}; confira no quadro de responsáveis.";
            planejados.Add(new ResponsavelPlanejado(g.Chave, p.Nome, g.Cpf, p.Telefone, p.Email, escolhido?.Id, escolhido?.Nome,
                escolhido?.PessoaId is not null, false, parecidos, g.Linhas));
        }

        foreach (var r in rascunhos)
        {
            foreach (var v in r.Vinculos)
                if (avisoPorChave.TryGetValue(v.Chave, out var aviso)) r.Avisos.Add(aviso);
            if (r.Parecido)
                r.Avisos.Add(decisoes.AlunosNovos.Contains(r.Linha.Numero)
                    ? "Já existe um aluno com este nome e esta data de nascimento: a gestão confirmou que é outra criança."
                    : "Já existe um aluno com este nome e esta data de nascimento: só entra se a gestão confirmar que é outra criança.");
            r.Importar = r.Bloqueios.Count == 0 && (!r.Parecido || decisoes.AlunosNovos.Contains(r.Linha.Numero));
        }

        // A turma cheia sinaliza e não bloqueia (R9, D38).
        foreach (var turma in existente.Turmas)
        {
            var entrando = rascunhos.Where(x => x.Importar && x.Dados!.TurmaId == turma.Id).ToList();
            if (entrando.Count > 0 && turma.Ocupadas + entrando.Count > turma.Capacidade)
                foreach (var x in entrando)
                    x.Avisos.Add($"A turma {turma.Nome} passa da capacidade ({turma.Ocupadas + entrando.Count} de {turma.Capacidade} vagas).");
        }

        var linhas = rascunhos.Select(r => new LinhaPlanejada(r.Linha.Numero, r.Aluno, r.Turma,
                r.Bloqueios.Count > 0 ? ClasseLinha.Bloqueada : r.Avisos.Count > 0 || r.Parecido ? ClasseLinha.ComAviso : ClasseLinha.Apta,
                r.Bloqueios, r.Avisos, r.Importar, r.Parecido, r.Saude, r.Contato, r.Vinculos, r.Dados))
            .ToList();
        var turmas = semCorrespondencia
            .Select(t => new TurmaDaPlanilha(t.Key, t.Value.Valor, t.Value.Linhas,
                decisoes.Turmas.TryGetValue(t.Key, out var id) && turmasPorId.ContainsKey(id) ? id : null))
            .OrderBy(t => t.Valor, StringComparer.Create(CultureInfo.GetCultureInfo("pt-BR"), true))
            .ToList();
        return new PlanoImportacao(linhas, planejados, turmas);
    }

    /// <summary>A chave de comparação do nome da turma: só letras e números, minúsculas e sem acento ("Sub-13" = "sub 13").</summary>
    public static string Chave(string texto) => CatalogoCamposImportacao.NormalizarCabecalho(texto);

    private static bool? Consentimento(string? valor, string rotulo, List<string> avisos)
    {
        if (valor is null) return null;
        var resposta = LeituraCelulas.LerSimNao(valor);
        if (resposta is null) avisos.Add($"Autorização de {rotulo} \"{valor}\" não reconhecida (use sim ou não): fica pendente na ficha.");
        return resposta;
    }

    private static Lido? LerResponsavel(int ordem, string? nome, string? cpfTexto, string? celular, string? email, string? parentescoTexto,
        bool obrigatorio, Rascunho r)
    {
        if (nome is null && cpfTexto is null && celular is null)
        {
            if (obrigatorio) r.Bloqueios.Add("Sem o responsável 1: toda comunicação passa por ele.");
            return null;
        }

        var problemas = new List<string>();
        if (nome is null) problemas.Add($"sem o nome do responsável {ordem}");
        else if (nome.Length > Responsavel.TamanhoNome) problemas.Add($"o nome do responsável {ordem} passa de {Responsavel.TamanhoNome} caracteres");
        var telefone = celular is null ? null : Telefone.ParaE164(celular);
        if (telefone is null) problemas.Add($"o celular do responsável {ordem} está {(celular is null ? "vazio" : "inválido")} (é o canal com a família)");
        var cpf = cpfTexto is null ? null : LeituraCelulas.LerCpf(cpfTexto);
        if (cpfTexto is not null && cpf is null) problemas.Add($"o CPF do responsável {ordem} é inválido");

        if (problemas.Count > 0)
        {
            var texto = string.Join("; ", problemas);
            texto = char.ToUpper(texto[0], CultureInfo.GetCultureInfo("pt-BR")) + texto[1..];
            if (obrigatorio) r.Bloqueios.Add($"{texto}.");
            else r.Avisos.Add($"{texto}: o responsável {ordem} fica de fora; complete na ficha.");
            return null;
        }

        string? emailValido = null;
        if (email is not null)
        {
            if (email.Length <= Responsavel.TamanhoEmail && LeituraCelulas.EmailValido(email)) emailValido = email.ToLowerInvariant();
            else r.Avisos.Add($"O e-mail do responsável {ordem} é inválido: fica em branco.");
        }
        var parentesco = LeituraCelulas.LerParentesco(parentescoTexto, out var reconhecido);
        if (!reconhecido) r.Avisos.Add($"Parentesco do responsável {ordem} \"{parentescoTexto}\" não reconhecido: fica \"Outro\".");

        return new Lido(ordem, nome!, TextoHelper.NormalizarParaBusca(nome), cpf, telefone!, emailValido, parentesco);
    }
}
