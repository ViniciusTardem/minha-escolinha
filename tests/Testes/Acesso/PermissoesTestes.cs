using MinhaEscolinha.Application.Acesso;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Testes.Infraestrutura;

namespace MinhaEscolinha.Testes.Acesso;

/// <summary>A matriz de permissões da spec §7, célula a célula (BL-M01-03).</summary>
public class MatrizPermissoesTestes
{
    private const NivelAcesso T = NivelAcesso.Total, P = NivelAcesso.Parcial, L = NivelAcesso.Leitura, X = NivelAcesso.Nenhum;

    /// <summary>A tabela da spec, transcrita: Recurso | Gestor | Professor | Responsável.</summary>
    public static readonly TheoryData<Recurso, NivelAcesso, NivelAcesso, NivelAcesso> Spec = new()
    {
        { Recurso.CadastroAlunosResponsaveis, T, P, P },
        { Recurso.DocumentosObservacoesMedicas, T, P, P },
        { Recurso.RegistroPresenca, T, P, L },
        { Recurso.ConfiguracaoEscolinha, T, X, X },
        { Recurso.FinanceiroEscolinha, T, X, X },
        { Recurso.RemuneracaoPropriaProfessor, T, P, X },
        { Recurso.PagamentosProprioFilho, T, X, P },
        { Recurso.EventosConvocacao, T, P, X },
        { Recurso.ResponderConvocacao, P, P, P },
        { Recurso.CadastroProfessores, T, X, X }
    };

    [Theory]
    [MemberData(nameof(Spec))]
    public void Cada_celula_da_matriz_segue_a_spec(Recurso recurso, NivelAcesso gestor, NivelAcesso professor, NivelAcesso responsavel)
    {
        Assert.Equal(gestor, MatrizPermissoes.Nivel(recurso, PapelAtuacao.Gestor));
        Assert.Equal(professor, MatrizPermissoes.Nivel(recurso, PapelAtuacao.Professor));
        Assert.Equal(responsavel, MatrizPermissoes.Nivel(recurso, PapelAtuacao.Responsavel));
    }

    [Fact]
    public void Todo_recurso_do_enum_esta_na_spec_transcrita()
    {
        var naSpec = Spec.Select(linha => (Recurso)linha[0]).ToHashSet();
        Assert.Equal(Enum.GetValues<Recurso>().ToHashSet(), naSpec);
    }

    [Fact]
    public void Papel_ou_recurso_desconhecido_nega()
    {
        Assert.Equal(NivelAcesso.Nenhum, MatrizPermissoes.Nivel((Recurso)999, PapelAtuacao.Gestor));
        Assert.Equal(NivelAcesso.Nenhum, MatrizPermissoes.Nivel(Recurso.ConfiguracaoEscolinha, (PapelAtuacao)999));
    }

    [Theory]
    [InlineData(NivelAcesso.Total, Operacao.Alterar, false, true)]
    [InlineData(NivelAcesso.Parcial, Operacao.Alterar, true, true)]
    [InlineData(NivelAcesso.Parcial, Operacao.Ler, false, false)]
    [InlineData(NivelAcesso.Leitura, Operacao.Ler, true, true)]
    [InlineData(NivelAcesso.Leitura, Operacao.Alterar, true, false)]
    [InlineData(NivelAcesso.Nenhum, Operacao.Ler, true, false)]
    public void O_nivel_permite_o_que_a_legenda_diz(NivelAcesso nivel, Operacao operacao, bool noEscopo, bool esperado) =>
        Assert.Equal(esperado, MatrizPermissoes.Permite(nivel, operacao, noEscopo));

    [Fact]
    public void Papeis_acumulam_pelo_maior_nivel()
    {
        PapelAtuacao[] papeis = [PapelAtuacao.Professor, PapelAtuacao.Responsavel];

        Assert.Equal(NivelAcesso.Parcial, MatrizPermissoes.MaiorNivel(Recurso.RegistroPresenca, papeis)); // P do professor vence o L
        Assert.Equal(NivelAcesso.Parcial, MatrizPermissoes.MaiorNivel(Recurso.PagamentosProprioFilho, papeis)); // só o responsável tem
        Assert.Equal(NivelAcesso.Nenhum, MatrizPermissoes.MaiorNivel(Recurso.FinanceiroEscolinha, papeis));
    }

    [Fact]
    public void So_o_recurso_de_saude_e_documentos_e_sensivel()
    {
        Assert.True(MatrizPermissoes.Sensivel(Recurso.DocumentosObservacoesMedicas));
        Assert.All(Enum.GetValues<Recurso>().Where(r => r != Recurso.DocumentosObservacoesMedicas),
            r => Assert.False(MatrizPermissoes.Sensivel(r)));
    }
}

/// <summary>O motor de permissões com o contexto e o escopo de cada papel.</summary>
public class AutorizacaoTestes
{
    private static readonly Guid Escolinha = Guid.NewGuid();
    private static readonly AlvoEscopo AlunoDaMinhaTurma = new(TipoAlvoEscopo.Aluno, Guid.NewGuid());
    private static readonly AlvoEscopo AlunoDeOutraTurma = new(TipoAlvoEscopo.Aluno, Guid.NewGuid());
    private static readonly AlvoEscopo MeuFilho = new(TipoAlvoEscopo.Aluno, Guid.NewGuid());

    /// <summary>Escopo falso: o professor tem um aluno na turma dele; o responsável tem um filho verificado.</summary>
    private sealed class EscopoFalso : IResolvedorEscopo
    {
        public Task<bool> NoEscopoAsync(PapelAtuacao papel, AlvoEscopo alvo, Guid pessoaId, CancellationToken ct = default) =>
            Task.FromResult(papel switch
            {
                PapelAtuacao.Professor => alvo == AlunoDaMinhaTurma,
                PapelAtuacao.Responsavel => alvo == MeuFilho,
                _ => false
            });
    }

    private static Autorizacao Com(params PapelAtuacao[] papeis)
    {
        var contexto = new ContextoEscolinha();
        contexto.Definir(Escolinha, papeis);
        return new Autorizacao(contexto, new UsuarioDeTeste { Id = Guid.NewGuid() }, new EscopoFalso());
    }

    [Fact]
    public async Task Gestor_tem_acesso_total_sem_depender_do_escopo()
    {
        var gestor = Com(PapelAtuacao.Gestor);

        Assert.True(gestor.Pode(Recurso.FinanceiroEscolinha, Operacao.Alterar));
        Assert.True(await gestor.PodeAsync(Recurso.DocumentosObservacoesMedicas, Operacao.Ler, AlunoDeOutraTurma));
    }

    [Fact]
    public async Task Professor_ve_dado_de_saude_so_dos_alunos_da_propria_turma()
    {
        var professor = Com(PapelAtuacao.Professor);

        Assert.True(await professor.PodeAsync(Recurso.DocumentosObservacoesMedicas, Operacao.Ler, AlunoDaMinhaTurma));
        Assert.False(await professor.PodeAsync(Recurso.DocumentosObservacoesMedicas, Operacao.Ler, AlunoDeOutraTurma));
        Assert.False(professor.Pode(Recurso.FinanceiroEscolinha, Operacao.Ler));
        Assert.False(professor.Pode(Recurso.ConfiguracaoEscolinha, Operacao.Ler));
    }

    [Fact]
    public async Task Responsavel_so_le_a_presenca_do_proprio_filho()
    {
        var responsavel = Com(PapelAtuacao.Responsavel);

        Assert.True(await responsavel.PodeAsync(Recurso.RegistroPresenca, Operacao.Ler, MeuFilho));
        Assert.False(await responsavel.PodeAsync(Recurso.RegistroPresenca, Operacao.Alterar, MeuFilho));
        Assert.False(await responsavel.PodeAsync(Recurso.RegistroPresenca, Operacao.Ler, AlunoDaMinhaTurma));
    }

    [Fact]
    public async Task Quem_acumula_papeis_tem_o_escopo_de_cada_um()
    {
        var professorEResponsavel = Com(PapelAtuacao.Professor, PapelAtuacao.Responsavel);

        Assert.True(await professorEResponsavel.PodeAsync(Recurso.CadastroAlunosResponsaveis, Operacao.Alterar, AlunoDaMinhaTurma));
        Assert.True(await professorEResponsavel.PodeAsync(Recurso.CadastroAlunosResponsaveis, Operacao.Alterar, MeuFilho));
        Assert.False(await professorEResponsavel.PodeAsync(Recurso.CadastroAlunosResponsaveis, Operacao.Alterar, AlunoDeOutraTurma));
    }

    [Fact]
    public async Task Sem_escolinha_no_contexto_nada_e_permitido()
    {
        var semEscolinha = new Autorizacao(new ContextoEscolinha(), new UsuarioDeTeste { Id = Guid.NewGuid() }, new EscopoFalso());

        Assert.False(semEscolinha.Pode(Recurso.ConfiguracaoEscolinha, Operacao.Ler));
        Assert.False(await semEscolinha.PodeAsync(Recurso.CadastroAlunosResponsaveis, Operacao.Ler, MeuFilho));
    }

    [Fact]
    public async Task O_resolvedor_real_pergunta_ao_banco_do_professor_e_so_libera_ao_responsavel_o_vinculo_verificado()
    {
        var consultas = new ConsultasEscopoFalsas();
        var resolvedor = new ResolvedorEscopoCadastros(consultas);
        var pessoa = Guid.NewGuid();
        var turma = Guid.NewGuid();
        consultas.TurmasDoProfessor[pessoa] = [turma];
        consultas.AlunosDoProfessor[pessoa] = [AlunoDaMinhaTurma.Id];

        Assert.True(await resolvedor.NoEscopoAsync(PapelAtuacao.Professor, new AlvoEscopo(TipoAlvoEscopo.Turma, turma), pessoa));
        Assert.True(await resolvedor.NoEscopoAsync(PapelAtuacao.Professor, AlunoDaMinhaTurma, pessoa));
        Assert.False(await resolvedor.NoEscopoAsync(PapelAtuacao.Professor, AlunoDeOutraTurma, pessoa));
        Assert.False(await resolvedor.NoEscopoAsync(PapelAtuacao.Professor, new AlvoEscopo(TipoAlvoEscopo.Cobranca, Guid.NewGuid()), pessoa));
        // Responsável: só o filho com vínculo confirmado pelo gestor (S2, BL-M01-06); pendente é bloqueio total.
        Assert.False(await resolvedor.NoEscopoAsync(PapelAtuacao.Responsavel, AlunoDaMinhaTurma, pessoa));
        consultas.VerificadosDoResponsavel[pessoa] = [AlunoDaMinhaTurma.Id];
        Assert.True(await resolvedor.NoEscopoAsync(PapelAtuacao.Responsavel, AlunoDaMinhaTurma, pessoa));
        Assert.False(await resolvedor.NoEscopoAsync(PapelAtuacao.Responsavel, AlunoDeOutraTurma, pessoa));
        Assert.False(await resolvedor.NoEscopoAsync(PapelAtuacao.Responsavel, new AlvoEscopo(TipoAlvoEscopo.Turma, turma), pessoa));
    }

    private sealed class ConsultasEscopoFalsas : IConsultasEscopo
    {
        public Dictionary<Guid, List<Guid>> TurmasDoProfessor { get; } = [];
        public Dictionary<Guid, List<Guid>> AlunosDoProfessor { get; } = [];
        public Dictionary<Guid, List<Guid>> VerificadosDoResponsavel { get; } = [];
        public Dictionary<Guid, List<Guid>> EventosDoProfessor { get; } = [];

        public Task<bool> ProfessorDoEventoAsync(Guid pessoaId, Guid eventoId, CancellationToken ct = default) =>
            Task.FromResult(EventosDoProfessor.GetValueOrDefault(pessoaId)?.Contains(eventoId) == true);

        public Task<bool> ProfessorDaTurmaAsync(Guid pessoaId, Guid turmaId, CancellationToken ct = default) =>
            Task.FromResult(TurmasDoProfessor.GetValueOrDefault(pessoaId)?.Contains(turmaId) == true);

        public Task<bool> ProfessorDoAlunoAsync(Guid pessoaId, Guid alunoId, CancellationToken ct = default) =>
            Task.FromResult(AlunosDoProfessor.GetValueOrDefault(pessoaId)?.Contains(alunoId) == true);

        public Task<bool> ProfessorDaSessaoAsync(Guid pessoaId, Guid sessaoId, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> ProprioProfessorAsync(Guid pessoaId, Guid professorId, CancellationToken ct = default) => Task.FromResult(false);

        public Task<IReadOnlyList<Guid>> TurmasDoProfessorAsync(Guid pessoaId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Guid>>(TurmasDoProfessor.GetValueOrDefault(pessoaId) ?? []);

        public Task<bool> ResponsavelVerificadoDoAlunoAsync(Guid pessoaId, Guid alunoId, CancellationToken ct = default) =>
            Task.FromResult(VerificadosDoResponsavel.GetValueOrDefault(pessoaId)?.Contains(alunoId) == true);

        public Task<bool> ProprioResponsavelAsync(Guid pessoaId, Guid responsavelId, CancellationToken ct = default) => Task.FromResult(false);
    }

    [Fact]
    public void O_contexto_nao_troca_de_escolinha_no_mesmo_escopo()
    {
        var contexto = new ContextoEscolinha();
        contexto.Definir(Escolinha, [PapelAtuacao.Gestor]);

        Assert.Throws<InvalidOperationException>(() => contexto.Definir(Guid.NewGuid(), [PapelAtuacao.Gestor]));
    }
}
