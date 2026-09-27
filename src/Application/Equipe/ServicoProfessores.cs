using FluentValidation;
using MinhaEscolinha.Application.Acesso;
using MinhaEscolinha.Application.Alunos;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Configuracao;
using MinhaEscolinha.Application.Turmas;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Domain.Agenda;
using MinhaEscolinha.Domain.Comum;
using MinhaEscolinha.Domain.Equipe;
using MinhaEscolinha.Domain.Turmas;

namespace MinhaEscolinha.Application.Equipe;

/// <summary>
/// Cadastro de professor e equipe (BL-M08-01), reaproveitando a pessoa que já está na escolinha, e a escala de
/// disponibilidade com as indisponibilidades pontuais (BL-M08-04).
/// </summary>
public class ServicoProfessores(
    IEquipeRepositorio repositorio,
    IAcessoRepositorio acesso,
    IAlunosRepositorio alunos,
    ITurmasRepositorio turmas,
    RegistroDeAlteracoes registro,
    IRelogioBrasil relogio,
    IUsuarioAtual usuarioAtual,
    IValidator<ProfessorRequisicao> validador,
    IValidator<DisponibilidadeRequisicao> validadorDisponibilidade,
    IValidator<IndisponibilidadeRequisicao> validadorIndisponibilidade)
{
    public const string Entidade = nameof(Professor);

    public async Task<IReadOnlyList<ProfessorResumo>> ListarAsync(CancellationToken ct = default)
    {
        var professores = await repositorio.ListarProfessoresAsync(ct);
        var porProfessor = (await turmas.ListarTurmasAsync(EscopoTurmas.Tudo, incluirEncerradas: false, ct))
            .GroupBy(t => t.Professor_ID).ToDictionary(g => g.Key, g => g.Select(t => t.Nome).OrderBy(n => n).ToList());
        return professores.Select(p => new ProfessorResumo(p.Professor_ID, p.Nome, p.Telefone, p.Email, p.Pessoa_ID is not null,
            porProfessor.GetValueOrDefault(p.Professor_ID) ?? [], ResumoDisponibilidade.De(p.Disponibilidade))).ToList();
    }

    /// <summary>Quem pode ter o cadastro reaproveitado: pessoas com acesso a esta escolinha e responsáveis, que ainda não são professores.</summary>
    public async Task<IReadOnlyList<CandidatoProfessor>> CandidatosAsync(CancellationToken ct = default)
    {
        var professores = await repositorio.ListarProfessoresAsync(ct);
        var pessoasProfessoras = professores.Where(p => p.Pessoa_ID is not null).Select(p => p.Pessoa_ID!.Value).ToHashSet();
        var cpfsProfessores = professores.Where(p => p.Cpf is not null).Select(p => p.Cpf!).ToHashSet();

        var comAcesso = (await acesso.PessoasDaEscolinhaAsync(ct))
            .Where(p => !pessoasProfessoras.Contains(p.PessoaId) && (p.Cpf is null || !cpfsProfessores.Contains(p.Cpf)))
            .Select(p => new CandidatoProfessor(OrigemCadastroProfessor.PessoaComAcesso, p.PessoaId, p.Nome,
                string.Join(", ", p.Papeis.Select(Rotulo))));
        var responsaveis = (await alunos.TodosResponsaveisAsync(ct))
            .Where(r => r.Cpf is null || !cpfsProfessores.Contains(r.Cpf))
            .Select(r => new CandidatoProfessor(OrigemCadastroProfessor.Responsavel, r.Id, r.Nome,
                "Responsável" + (r.Alunos.Count > 0 ? $" por {string.Join(", ", r.Alunos)}" : string.Empty)));

        return comAcesso.Concat(responsaveis).ToList();
    }

    /// <summary>O formulário do professor novo, já preenchido com o cadastro reaproveitado (sem redigitar, R11).</summary>
    public async Task<ProfessorRequisicao?> PrepararAsync(OrigemCadastroProfessor origem, Guid? origemId, CancellationToken ct = default)
    {
        switch (origem)
        {
            case OrigemCadastroProfessor.PessoaComAcesso when origemId is { } pessoaId:
                var pessoa = (await acesso.PessoasDaEscolinhaAsync(ct)).FirstOrDefault(p => p.PessoaId == pessoaId);
                if (pessoa is null) return null;
                var credencial = await acesso.ObterCredencialAsync(pessoaId, ct);
                return new ProfessorRequisicao
                {
                    Origem = origem, OrigemId = pessoaId, OrigemNome = pessoa.Nome, Nome = pessoa.Nome, Cpf = Cpf.Formatar(pessoa.Cpf),
                    Email = credencial?.Login
                };
            case OrigemCadastroProfessor.Responsavel when origemId is { } responsavelId:
                if (await alunos.ObterResponsavelAsync(responsavelId, ct) is not { } r) return null;
                return new ProfessorRequisicao
                {
                    Origem = origem, OrigemId = responsavelId, OrigemNome = r.Nome, Nome = r.Nome, Cpf = Cpf.Formatar(r.Cpf),
                    Telefone = Cadastros.TelefoneParaCampo(r.Telefone), Email = r.Email
                };
            default:
                return new ProfessorRequisicao();
        }
    }

    public async Task<ProfessorRequisicao?> ParaEdicaoAsync(Guid id, CancellationToken ct = default) =>
        await repositorio.ObterProfessorAsync(id, ct) is { } p
            ? new ProfessorRequisicao
            {
                Id = p.Professor_ID, Nome = p.Nome, Cpf = Cpf.Formatar(p.Cpf), Telefone = Cadastros.TelefoneParaCampo(p.Telefone), Email = p.Email
            }
            : null;

    public async Task<ResultadoOperacao> CriarAsync(ProfessorRequisicao r, CancellationToken ct = default)
    {
        r.Id = null;
        var validacao = await validador.ValidateAsync(r, ct);
        if (!validacao.IsValid) return ResultadoOperacao.Invalido(validacao);

        var cpf = Cpf.Normalizar(r.Cpf);
        Guid? pessoaId = null;
        switch (r.Origem)
        {
            case OrigemCadastroProfessor.PessoaComAcesso:
                // Só quem já atua nesta escolinha: o id vem do formulário e nunca pode ligar alguém de outra escolinha.
                var pessoa = (await acesso.PessoasDaEscolinhaAsync(ct)).FirstOrDefault(p => p.PessoaId == r.OrigemId);
                if (pessoa is null) return ResultadoOperacao.Erro("Essa pessoa não atua nesta escolinha.");
                pessoaId = pessoa.PessoaId;
                cpf = pessoa.Cpf ?? cpf;
                break;
            case OrigemCadastroProfessor.Responsavel:
                if (await alunos.ObterResponsavelAsync(r.OrigemId!.Value, ct) is not { } responsavel)
                    return ResultadoOperacao.Erro("Responsável não encontrado.");
                pessoaId = responsavel.Pessoa_ID;
                cpf ??= responsavel.Cpf;
                break;
        }

        if (pessoaId is { } jaTem && await repositorio.ProfessorDaPessoaAsync(jaTem, ct) is { } existente)
            return ResultadoOperacao.Erro($"{existente.Nome} já está na equipe de professores desta escolinha.");
        if (cpf is not null && await repositorio.CpfEmUsoAsync(cpf, null, ct))
            return ResultadoOperacao.Erro("Já existe um professor com este CPF nesta escolinha.", nameof(ProfessorRequisicao.Cpf));

        var professor = new Professor
        {
            Professor_ID = Guid.NewGuid(),
            Cpf = cpf,
            Telefone = Telefone.ParaE164(r.Telefone),
            Email = Cadastros.Vazio(r.Email)?.ToLowerInvariant(),
            Pessoa_ID = pessoaId
        };
        professor.DefinirNome(r.Nome);
        repositorio.AdicionarProfessor(professor);

        if (pessoaId is { } comAcesso) await GarantirPapelProfessorAsync(comAcesso, ct);
        registro.Registrar(Entidade, professor.Professor_ID, "Criação", null, Descrever(professor),
            r.Origem == OrigemCadastroProfessor.PessoaNova ? null : $"Cadastro reaproveitado de {r.OrigemNome ?? "pessoa existente"}.");

        await repositorio.SalvarAsync(ct);
        var acessoTexto = pessoaId is null ? " O acesso ao sistema chega com o convite (em breve)." : " O acesso ao sistema é o que a pessoa já tem.";
        return ResultadoOperacao.Ok($"{professor.Nome} entrou na equipe de professores.{acessoTexto}", professor.Professor_ID);
    }

    public async Task<ResultadoOperacao> EditarAsync(ProfessorRequisicao r, CancellationToken ct = default)
    {
        var validacao = await validador.ValidateAsync(r, ct);
        if (!validacao.IsValid) return ResultadoOperacao.Invalido(validacao);
        if (r.Id is not { } id || await repositorio.ObterProfessorAsync(id, ct) is not { } professor)
            return ResultadoOperacao.Erro("Professor não encontrado.");

        var cpf = Cpf.Normalizar(r.Cpf);
        if (cpf is not null && await repositorio.CpfEmUsoAsync(cpf, id, ct))
            return ResultadoOperacao.Erro("Já existe um professor com este CPF nesta escolinha.", nameof(ProfessorRequisicao.Cpf));
        if (professor.Pessoa_ID is not null && professor.Cpf is not null && cpf != professor.Cpf)
            return ResultadoOperacao.Erro("O CPF de quem já tem acesso não muda por aqui.", nameof(ProfessorRequisicao.Cpf));

        var antes = Descrever(professor);
        professor.DefinirNome(r.Nome);
        professor.Cpf = cpf;
        professor.Telefone = Telefone.ParaE164(r.Telefone);
        professor.Email = Cadastros.Vazio(r.Email)?.ToLowerInvariant();
        if (!registro.SeMudou(Entidade, id, "Cadastro", antes, Descrever(professor))) return ResultadoOperacao.Ok("Nada mudou.");

        await repositorio.SalvarAsync(ct);
        return ResultadoOperacao.Ok($"Cadastro de {professor.Nome} atualizado.");
    }

    public async Task<FichaProfessor?> ObterFichaAsync(Guid id, CancellationToken ct = default)
    {
        if (await repositorio.ObterProfessorAsync(id, ct) is not { } p) return null;
        var agora = relogio.Agora();
        var doProfessor = (await turmas.ListarTurmasAsync(EscopoTurmas.Tudo, incluirEncerradas: true, ct))
            .Where(t => t.Professor_ID == id).OrderBy(t => t.Situacao).ThenBy(t => t.Nome).ToList();
        var ativas = doProfessor.Where(t => t.Ativa).ToList();

        // Os alertas das próximas sessões: as turmas ativas dele entre si, a disponibilidade e as indisponibilidades.
        var conflitos = new List<ConflitoAgenda>();
        foreach (var turma in ativas)
        {
            var outras = ativas.Where(t => t.Turma_ID != turma.Turma_ID)
                .SelectMany(t => t.Horarios.Select(h => new HorarioOcupado(t.Nome, h.Semanal))).ToList();
            var horarios = turma.Horarios.Select(h => h.Semanal).ToList();
            var hoje = DateOnly.FromDateTime(agora);
            var de = turma.DataInicio > hoje ? turma.DataInicio : hoje;
            var ocorrencias = GeradorSessoes.Ocorrencias(horarios, de, GeradorSessoes.FimDoHorizonte(hoje)).Where(o => o.Inicio >= agora);
            conflitos.AddRange(DetectorConflitos.ParaHorarios(p, horarios, outras, ocorrencias)
                .Select(c => c with { Descricao = $"{turma.Nome} — {c.Descricao}" }));
        }

        return new FichaProfessor(p.Professor_ID, p.Nome, p.Cpf, p.Telefone, p.Email, p.Pessoa_ID is not null,
            doProfessor.Select(t => new TurmaDoProfessor(t.Turma_ID, t.Nome, t.ResumoHorarios(), t.Situacao)).ToList(),
            p.Disponibilidade.OrderBy(f => DiasSemana.Posicao(f.DiaSemana)).ThenBy(f => f.Inicio).ToList(),
            p.Indisponibilidades.Where(i => i.Fim >= agora).OrderBy(i => i.Inicio).ToList(),
            conflitos.DistinctBy(c => c.Descricao).ToList());
    }

    public async Task<DisponibilidadeRequisicao?> DisponibilidadeParaEdicaoAsync(Guid id, CancellationToken ct = default)
    {
        if (await repositorio.ObterProfessorAsync(id, ct) is not { } p) return null;
        var faixas = p.Disponibilidade.OrderBy(f => DiasSemana.Posicao(f.DiaSemana)).ThenBy(f => f.Inicio)
            .Select(f => new FaixaRequisicao { DiaSemana = f.DiaSemana, Inicio = f.Inicio, Fim = f.Fim }).ToList();
        return new DisponibilidadeRequisicao { ProfessorId = id, Faixas = faixas };
    }

    /// <summary>Troca a escala semanal inteira (BL-M08-04). Lista vazia = disponibilidade não informada (sem alerta de faixa).</summary>
    public async Task<ResultadoOperacao> SalvarDisponibilidadeAsync(DisponibilidadeRequisicao r, CancellationToken ct = default)
    {
        var validacao = await validadorDisponibilidade.ValidateAsync(r, ct);
        if (!validacao.IsValid) return ResultadoOperacao.Invalido(validacao);
        if (await repositorio.ObterProfessorAsync(r.ProfessorId, ct) is not { } professor) return ResultadoOperacao.Erro("Professor não encontrado.");

        var antes = ResumoDisponibilidade.De(professor.Disponibilidade);
        foreach (var faixa in professor.Disponibilidade.ToList())
        {
            professor.Disponibilidade.Remove(faixa);
            repositorio.RemoverFaixa(faixa);
        }
        foreach (var f in r.Faixas.Where(f => !f.Vazia))
        {
            var faixa = new FaixaDisponibilidade
            {
                FaixaDisponibilidade_ID = Guid.NewGuid(), Professor_ID = professor.Professor_ID, DiaSemana = f.DiaSemana,
                Inicio = f.Inicio!.Value, Fim = f.Fim!.Value
            };
            repositorio.AdicionarFaixa(faixa);
            professor.Disponibilidade.Add(faixa);
        }

        if (!registro.SeMudou(Entidade, professor.Professor_ID, "Disponibilidade", antes, ResumoDisponibilidade.De(professor.Disponibilidade)))
            return ResultadoOperacao.Ok("Nada mudou.");
        await repositorio.SalvarAsync(ct);
        return ResultadoOperacao.Ok($"Disponibilidade de {professor.Nome} atualizada.");
    }

    public async Task<ResultadoOperacao> AdicionarIndisponibilidadeAsync(IndisponibilidadeRequisicao r, CancellationToken ct = default)
    {
        var validacao = await validadorIndisponibilidade.ValidateAsync(r, ct);
        if (!validacao.IsValid) return ResultadoOperacao.Invalido(validacao);
        if (await repositorio.ObterProfessorAsync(r.ProfessorId, ct) is not { } professor) return ResultadoOperacao.Erro("Professor não encontrado.");
        var agora = relogio.Agora();
        if (r.Fim!.Value <= agora) return ResultadoOperacao.Erro("A indisponibilidade já passou.", nameof(IndisponibilidadeRequisicao.Fim));

        var indisponibilidade = new IndisponibilidadeProfessor
        {
            IndisponibilidadeProfessor_ID = Guid.NewGuid(),
            Professor_ID = professor.Professor_ID,
            Inicio = r.Inicio!.Value,
            Fim = r.Fim.Value,
            Motivo = TextoHelper.Truncar(Cadastros.Vazio(r.Motivo), IndisponibilidadeProfessor.TamanhoMotivo),
            RegistradaEm = agora,
            RegistradaPorNome = TextoHelper.Truncar(usuarioAtual.Nome ?? "Sistema", 150)!
        };
        repositorio.AdicionarIndisponibilidade(indisponibilidade);
        professor.Indisponibilidades.Add(indisponibilidade);
        registro.Registrar(Entidade, professor.Professor_ID, "Indisponibilidade", null,
            $"{indisponibilidade.Inicio:dd/MM/yyyy HH:mm} a {indisponibilidade.Fim:dd/MM/yyyy HH:mm}", indisponibilidade.Motivo);
        await repositorio.SalvarAsync(ct);

        var atingidas = await turmas.ListarSessoesAsync(DateOnly.FromDateTime(indisponibilidade.Inicio), DateOnly.FromDateTime(indisponibilidade.Fim), ct);
        var conflito = atingidas.Count(s => s.Professor_ID == professor.Professor_ID && s.Situacao == SituacaoSessao.Prevista
                                           && s.Inicio < indisponibilidade.Fim && indisponibilidade.Inicio < s.Fim);
        return ResultadoOperacao.Ok(conflito == 0
            ? "Indisponibilidade registrada."
            : $"Indisponibilidade registrada. {conflito} treino(s) previsto(s) caem nesse período: confira a agenda (cancele ou combine a substituição).");
    }

    public async Task<ResultadoOperacao> RemoverIndisponibilidadeAsync(Guid professorId, Guid indisponibilidadeId, CancellationToken ct = default)
    {
        if (await repositorio.ObterProfessorAsync(professorId, ct) is not { } professor) return ResultadoOperacao.Erro("Professor não encontrado.");
        if (professor.Indisponibilidades.FirstOrDefault(i => i.IndisponibilidadeProfessor_ID == indisponibilidadeId) is not { } item)
            return ResultadoOperacao.Erro("Indisponibilidade não encontrada.");

        professor.Indisponibilidades.Remove(item);
        repositorio.RemoverIndisponibilidade(item);
        registro.Registrar(Entidade, professorId, "Indisponibilidade", $"{item.Inicio:dd/MM/yyyy HH:mm} a {item.Fim:dd/MM/yyyy HH:mm}",
            "Retirada", item.Motivo);
        await repositorio.SalvarAsync(ct);
        return ResultadoOperacao.Ok("Indisponibilidade retirada.");
    }

    /// <summary>A pessoa com acesso passa a atuar também como professora (os papéis acumulam, BL-M01-01).</summary>
    private async Task GarantirPapelProfessorAsync(Guid pessoaId, CancellationToken ct)
    {
        var vinculos = await acesso.VinculosNaEscolinhaAsync(pessoaId, ct);
        var professor = vinculos.FirstOrDefault(v => v.Papel == PapelAtuacao.Professor);
        if (professor is null)
            acesso.AdicionarVinculo(new VinculoAtuacao { Pessoa_ID = pessoaId, Papel = PapelAtuacao.Professor });
        else if (!professor.Ativo)
            professor.Reativar();
        else
            return;
        registro.Registrar(nameof(VinculoAtuacao), null, "Papel", null, "Professor", "Cadastro de professor reaproveitando a pessoa.");
    }

    private static string Descrever(Professor p) =>
        $"{p.Nome} · CPF {(p.Cpf is null ? "não informado" : Cpf.Formatar(p.Cpf))} · {p.Telefone ?? "sem celular"} · {p.Email ?? "sem e-mail"}";

    private static string Rotulo(PapelAtuacao papel) => papel switch
    {
        PapelAtuacao.Gestor => "Gestor",
        PapelAtuacao.Professor => "Professor",
        _ => "Responsável"
    };
}
