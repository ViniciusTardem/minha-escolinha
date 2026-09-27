using MinhaEscolinha.Application.Alunos;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Equipe;
using MinhaEscolinha.Application.Turmas;
using MinhaEscolinha.Domain.Alunos;
using MinhaEscolinha.Domain.Comum;

namespace MinhaEscolinha.Web.Infraestrutura.Desenvolvimento;

/// <summary>
/// Massa de demonstração (plano §5.3, spec §13), a parte do cadastro: 3 turmas e 60 alunos em 38 famílias — irmãos na
/// mesma escolinha (e em turmas diferentes), famílias com dois responsáveis, responsável sem CPF, trials (alguns vencidos),
/// inativos e um desligado. Tudo pelos serviços de verdade: validação, deduplicação e auditoria valem como na tela.
/// Determinística (mesmos nomes a cada escolinha) e de uma vez só por escolinha. Só em Development (/dev/massa).
/// </summary>
public class MassaDemonstracao(
    ServicoLocais locais,
    ServicoProfessores professores,
    ServicoTurmas turmas,
    ServicoAlunos alunos,
    IRelogioBrasil relogio)
{
    public const string TurmaMarcadora = "Sub-13 · noite";

    private static readonly string[] Sobrenomes =
    [
        "Almeida", "Barbosa", "Cardoso", "Dias", "Esteves", "Ferreira", "Gomes", "Hoffmann", "Ishikawa", "Jardim", "Klein", "Lopes",
        "Machado", "Nogueira", "Oliveira", "Pereira", "Queiroz", "Ribeiro", "Santos", "Teixeira", "Umeda", "Vieira", "Wagner",
        "Xavier", "Yamamoto", "Zanetti", "Araújo", "Batista", "Campos", "Duarte", "Freitas", "Guimarães", "Honda", "Rezende",
        "Moura", "Nunes", "Pacheco", "Siqueira"
    ];

    private static readonly string[] Maes =
    [
        "Ana", "Beatriz", "Camila", "Daniela", "Elaine", "Fernanda", "Gabriela", "Helena", "Isabela", "Juliana", "Karina", "Luciana",
        "Mariana", "Natália", "Patrícia", "Renata", "Sabrina", "Tatiane", "Vanessa", "Aline"
    ];

    private static readonly string[] Pais =
    [
        "André", "Bruno", "Carlos", "Diego", "Eduardo", "Fábio", "Gustavo", "Henrique", "Igor", "Jorge", "Leandro", "Marcelo",
        "Rafael", "Rodrigo", "Thiago"
    ];

    private static readonly string[] Criancas =
    [
        "Arthur", "Bernardo", "Davi", "Enzo", "Gael", "Heitor", "Isaac", "João", "Lorenzo", "Miguel", "Noah", "Pedro", "Samuel",
        "Theo", "Vicente", "Alice", "Cecília", "Helena", "Laura", "Lívia", "Manuela", "Maria", "Sofia", "Valentina", "Benício",
        "Caio", "Felipe", "Gustavo", "Lucas", "Matheus", "Nicolas", "Otávio", "Rafael", "Tomás", "Vitor", "Yuri", "Luísa", "Clara",
        "Heloísa", "Isis", "Júlia", "Lara", "Melissa", "Olívia", "Pietra", "Rebeca", "Sara", "Yasmin", "Antônio", "Emanuel",
        "Guilherme", "Joaquim", "Leonardo", "Murilo", "Raul", "Anthony", "Benjamin", "Daniel", "Henry", "Lucca"
    ];

    public async Task<ResultadoOperacao> GerarAsync(CancellationToken ct = default)
    {
        if ((await turmas.ListarAsync(true, null, ct)).Any(t => t.Nome == TurmaMarcadora))
            return ResultadoOperacao.Erro("A massa de demonstração já foi gerada nesta escolinha.");

        var hoje = DateOnly.FromDateTime(relogio.Hoje());
        var avisos = new List<string>();

        var localId = (await locais.ListarAsync(ct)).FirstOrDefault()?.Id
                      ?? Guid(await locais.SalvarAsync(new LocalRequisicao { Nome = "Quadra da massa", Cidade = "Londrina", Uf = "PR" }, ct));
        var rafael = await ProfessorAsync("Rafael Martins", "43 99120-0001", ct);
        var juliana = await ProfessorAsync("Juliana Prado", "43 99120-0002", ct);

        var turmaIds = new[]
        {
            await TurmaAsync("Sub-9 · sábado de manhã", "Sub-9", localId, juliana, 120m, [(DayOfWeek.Saturday, 9, 0, 10, 30)], ct),
            await TurmaAsync("Sub-11 · terça e quinta à tarde", "Sub-11", localId, rafael, 140m,
                [(DayOfWeek.Tuesday, 16, 0, 17, 0), (DayOfWeek.Thursday, 16, 0, 17, 0)], ct),
            await TurmaAsync(TurmaMarcadora, "Sub-13", localId, rafael, 160m,
                [(DayOfWeek.Monday, 19, 0, 20, 30), (DayOfWeek.Wednesday, 19, 0, 20, 30)], ct)
        };

        var criados = new List<Guid>();
        var reaproveitados = 0;
        var k = 0;
        for (var familia = 0; familia < Sobrenomes.Length; familia++)
        {
            var filhos = familia < 20 ? 1 : familia < 34 ? 2 : 3;
            var sobrenome = Sobrenomes[familia];
            var mae = $"{Maes[familia % Maes.Length]} {sobrenome}";
            var pai = familia % 3 == 0 ? $"{Pais[familia % Pais.Length]} {sobrenome}" : null;
            var cpfMae = familia % 4 == 3 ? null : CpfValido(123_456_000 + familia * 7_919);

            for (var f = 0; f < filhos; f++, k++)
            {
                var t = (familia + f) % 3;
                var ano = t switch { 0 => 2017 + (k % 2), 1 => 2015 + (k % 2), _ => 2013 + (k % 2) };
                var nascimento = new DateOnly(ano, 1 + k % 12, 1 + k * 7 % 28);
                var trial = k % 12 == 5;
                var inicio = trial ? hoje.AddDays(-10) : new DateOnly(2026, 2, 2).AddDays(k * 3 % 200);
                if (inicio > hoje) inicio = hoje;

                var r = NovoAlunoRequisicao.Vazia(hoje);
                r.Nome = $"{Criancas[k % Criancas.Length]} {sobrenome}";
                r.DataNascimento = nascimento;
                r.Categoria = t switch { 0 => "Sub-9", 1 => "Sub-11", _ => "Sub-13" };
                r.TurmaId = turmaIds[t];
                r.DataInicio = inicio;
                r.EmTrial = trial;
                r.TrialAte = trial ? hoje.AddDays(k % 10 - 3) : null;
                if (r.TrialAte < inicio) r.TrialAte = inicio;
                r.Responsaveis[0] = new ResponsavelDoAlunoRequisicao
                {
                    Nome = mae, Cpf = cpfMae, Telefone = Celular(familia, 1), Parentesco = Parentesco.Mae,
                    Pagador = true, RecebeCobranca = true, RecebeNotificacao = true
                };
                r.Responsaveis[1] = pai is null
                    ? new ResponsavelDoAlunoRequisicao { Parentesco = Parentesco.Pai }
                    : new ResponsavelDoAlunoRequisicao { Nome = pai, Telefone = Celular(familia, 2), Parentesco = Parentesco.Pai, RecebeNotificacao = true };
                if (k % 7 == 3) r.Alergias = "Amendoim";
                if (k % 9 == 4) r.Restricoes = "Asma leve: bombinha na mochila";
                r.ContatoEmergenciaNome = $"Avó {sobrenome}";
                r.ContatoEmergenciaTelefone = Celular(familia, 3);
                var dados = r.Consentimentos.Single(c => c.Finalidade == FinalidadeConsentimento.TratamentoDados);
                dados.Resposta = k % 7 == 6 ? RespostaConsentimento.NaoColetado : RespostaConsentimento.Concedido;
                var imagem = r.Consentimentos.Single(c => c.Finalidade == FinalidadeConsentimento.UsoImagem);
                imagem.Resposta = (k % 20) switch { < 12 => RespostaConsentimento.Concedido, < 17 => RespostaConsentimento.Recusado, _ => RespostaConsentimento.NaoColetado };

                var (resultado, reaproveitou) = await CriarAlunoAsync(r, ct);
                if (!resultado.Sucesso)
                {
                    avisos.Add($"{r.Nome}: {resultado.Mensagem}");
                    continue;
                }
                reaproveitados += reaproveitou;
                criados.Add((Guid)resultado.Dados!);
            }
        }

        // Pausas e um desligamento (a máquina de situação de verdade).
        foreach (var indice in new[] { 7, 23 }.Where(i => i < criados.Count))
            await alunos.MudarSituacaoAsync(new MudarSituacaoRequisicao
            {
                AlunoId = criados[indice], Para = SituacaoAluno.Inativo, Data = hoje, Motivo = "Pausa: lesão no tornozelo"
            }, ct);
        if (criados.Count > 40)
            await alunos.MudarSituacaoAsync(new MudarSituacaoRequisicao
            {
                AlunoId = criados[40], Para = SituacaoAluno.Desistente, Data = hoje, Motivo = "Mudou de cidade"
            }, ct);

        return ResultadoOperacao.Ok(
            $"Massa gerada: 3 turmas e {criados.Count} alunos; {reaproveitados} cadastro(s) de responsável reaproveitado(s) entre irmãos." +
            (avisos.Count == 0 ? string.Empty : $" Avisos: {string.Join(" | ", avisos)}"));
    }

    /// <summary>
    /// Cria o aluno como o gestor faria: com CPF igual, o responsável é reaproveitado sozinho; sem CPF, a sugestão de
    /// cadastro parecido é aceita quando o nome é o mesmo (o "Usar este cadastro" da tela).
    /// </summary>
    private async Task<(ResultadoOperacao Resultado, int Reaproveitados)> CriarAlunoAsync(NovoAlunoRequisicao r, CancellationToken ct)
    {
        var reaproveitados = 0;
        for (var tentativa = 0; tentativa < 4; tentativa++)
        {
            var resultado = await alunos.CriarAsync(r, ct);
            if (resultado.Sucesso)
                return (resultado, reaproveitados + (resultado.Mensagem.Contains("reaproveitado", StringComparison.Ordinal) ? 1 : 0));
            if (resultado.Dados is not DuplicidadeResponsavel duplicidade) return (resultado, reaproveitados);

            var pedido = r.Responsaveis[duplicidade.Indice];
            var mesmo = duplicidade.Candidatos.FirstOrDefault(c =>
                TextoHelper.NormalizarParaBusca(c.Nome) == TextoHelper.NormalizarParaBusca(pedido.Nome));
            if (mesmo is null)
            {
                pedido.ConfirmarNovo = true;
                continue;
            }

            r.Responsaveis[duplicidade.Indice] = new ResponsavelDoAlunoRequisicao
            {
                ResponsavelId = mesmo.Id, Parentesco = pedido.Parentesco, Pagador = pedido.Pagador,
                RecebeCobranca = pedido.RecebeCobranca, RecebeNotificacao = pedido.RecebeNotificacao
            };
            reaproveitados++;
        }
        return (ResultadoOperacao.Erro("Não deu para resolver os responsáveis."), reaproveitados);
    }

    private async Task<Guid> ProfessorAsync(string nome, string telefone, CancellationToken ct)
    {
        if ((await professores.ListarAsync(ct)).FirstOrDefault(p => p.Nome == nome) is { } existente) return existente.Id;
        return Guid(await professores.CriarAsync(new ProfessorRequisicao { Nome = nome, Telefone = telefone }, ct));
    }

    private async Task<Guid> TurmaAsync(string nome, string categoria, Guid local, Guid professor, decimal mensalidade,
        (DayOfWeek Dia, int H1, int M1, int H2, int M2)[] horarios, CancellationToken ct)
    {
        var r = await turmas.NovaAsync(ct);
        // Com mais de uma modalidade a turma nova vem sem: a massa é de futsal (ou a primeira que houver).
        var modalidades = (await turmas.OpcoesAsync(ct)).Modalidades;
        r.ModalidadeId ??= (modalidades.FirstOrDefault(m => m.Nome.Contains("Futsal", StringComparison.OrdinalIgnoreCase)) ?? modalidades[0]).Id;
        r.Nome = nome;
        r.Categoria = categoria;
        r.LocalId = local;
        r.ProfessorId = professor;
        r.Mensalidade = mensalidade;
        r.DataInicio = new DateOnly(2026, 2, 2);
        r.ConfirmarConflitos = true;
        r.Horarios = horarios.Select(h => new HorarioRequisicao
        {
            DiaSemana = h.Dia, Inicio = new TimeOnly(h.H1, h.M1), Fim = new TimeOnly(h.H2, h.M2)
        }).ToList();
        return Guid(await turmas.CriarAsync(r, ct));
    }

    private static Guid Guid(ResultadoOperacao resultado) =>
        resultado is { Sucesso: true, Dados: Guid id } ? id : throw new InvalidOperationException($"Massa: {resultado.Mensagem}");

    /// <summary>Celular de Londrina determinístico: (43) 9XXXX-XXXX.</summary>
    private static string Celular(int familia, int quem) => $"43 9{(20_000_000 + familia * 104_729 + quem * 7_919) % 90_000_000 + 10_000_000:00000000}";

    /// <summary>Um CPF válido a partir de 9 dígitos (calcula os verificadores).</summary>
    internal static string CpfValido(int base9)
    {
        var digitos = (base9 % 1_000_000_000).ToString("000000000");
        int Verificador(string parcial)
        {
            var soma = 0;
            for (var i = 0; i < parcial.Length; i++) soma += (parcial[i] - '0') * (parcial.Length + 1 - i);
            var resto = soma % 11;
            return resto < 2 ? 0 : 11 - resto;
        }
        digitos += Verificador(digitos);
        digitos += Verificador(digitos);
        return digitos;
    }
}
