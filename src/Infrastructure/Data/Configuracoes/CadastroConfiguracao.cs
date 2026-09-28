using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Domain.Alunos;
using MinhaEscolinha.Domain.Comum;
using MinhaEscolinha.Domain.Configuracao;
using MinhaEscolinha.Domain.Equipe;
using MinhaEscolinha.Domain.Turmas;

namespace MinhaEscolinha.Infrastructure.Data.Configuracoes;

// Fatia S1 — cadastro: equipe, locais, turmas, sessões, alunos, responsáveis, matrículas e consentimentos.
// Todo índice único começa por Escolinha_ID (X01); nada aqui apaga em cascata a partir da escolinha.

internal static class MapeamentoComum
{
    public static void MapearEndereco(ComplexPropertyBuilder<Endereco> endereco)
    {
        endereco.Property(x => x.Logradouro).HasColumnName("Logradouro").HasMaxLength(Endereco.TamanhoLogradouro);
        endereco.Property(x => x.Numero).HasColumnName("Numero").HasMaxLength(Endereco.TamanhoNumero);
        endereco.Property(x => x.Complemento).HasColumnName("Complemento").HasMaxLength(Endereco.TamanhoComplemento);
        endereco.Property(x => x.Bairro).HasColumnName("Bairro").HasMaxLength(Endereco.TamanhoBairro);
        endereco.Property(x => x.Cidade).HasColumnName("Cidade").HasMaxLength(Endereco.TamanhoCidade);
        endereco.Property(x => x.Uf).HasColumnName("Uf").HasMaxLength(Endereco.TamanhoUf).IsUnicode(false).IsFixedLength();
        endereco.Property(x => x.Cep).HasColumnName("Cep").HasMaxLength(Endereco.TamanhoCep).IsUnicode(false).IsFixedLength();
    }
}

public class ProfessorConfiguracao : IEntityTypeConfiguration<Professor>
{
    public void Configure(EntityTypeBuilder<Professor> builder)
    {
        builder.ToTable("Professor");

        builder.HasKey(p => p.Professor_ID).HasName("PK_Professor");
        builder.Property(p => p.Professor_ID).HasDefaultValueSql("NEWSEQUENTIALID()").ValueGeneratedOnAdd();
        builder.HasOne<Escolinha>().WithMany().HasForeignKey(p => p.Escolinha_ID)
            .HasConstraintName("FK_Professor_Escolinha").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Pessoa>().WithMany().HasForeignKey(p => p.Pessoa_ID)
            .HasConstraintName("FK_Professor_Pessoa").OnDelete(DeleteBehavior.Restrict);

        builder.Property(p => p.Nome).HasMaxLength(Professor.TamanhoNome).IsRequired();
        builder.Property(p => p.NomeBusca).HasMaxLength(Professor.TamanhoNome).IsRequired();
        builder.Property(p => p.Cpf).HasMaxLength(11).IsUnicode(false).IsFixedLength();
        builder.Property(p => p.Telefone).HasMaxLength(Professor.TamanhoTelefone).IsUnicode(false);
        builder.Property(p => p.Email).HasMaxLength(Professor.TamanhoEmail);
        builder.Ignore(p => p.TemDisponibilidadeDeclarada);

        builder.HasMany(p => p.Disponibilidade).WithOne().HasForeignKey(f => f.Professor_ID)
            .HasConstraintName("FK_FaixaDisponibilidade_Professor").OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(p => p.Indisponibilidades).WithOne().HasForeignKey(i => i.Professor_ID)
            .HasConstraintName("FK_IndisponibilidadeProfessor_Professor").OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => new { p.Escolinha_ID, p.Cpf }).IsUnique().HasFilter("[Cpf] IS NOT NULL")
            .HasDatabaseName("UQ_Professor_Escolinha_Cpf");
        builder.HasIndex(p => new { p.Escolinha_ID, p.Pessoa_ID }).IsUnique().HasFilter("[Pessoa_ID] IS NOT NULL")
            .HasDatabaseName("UQ_Professor_Escolinha_Pessoa");
        builder.HasIndex(p => new { p.Escolinha_ID, p.NomeBusca }).HasDatabaseName("IX_Professor_Escolinha_NomeBusca");
    }
}

public class FaixaDisponibilidadeConfiguracao : IEntityTypeConfiguration<FaixaDisponibilidade>
{
    public void Configure(EntityTypeBuilder<FaixaDisponibilidade> builder)
    {
        builder.ToTable("FaixaDisponibilidade", t => t.HasCheckConstraint("CK_FaixaDisponibilidade_Horario", "[Fim] > [Inicio]"));

        builder.HasKey(f => f.FaixaDisponibilidade_ID).HasName("PK_FaixaDisponibilidade");
        builder.Property(f => f.FaixaDisponibilidade_ID).HasDefaultValueSql("NEWSEQUENTIALID()").ValueGeneratedOnAdd();
        builder.HasOne<Escolinha>().WithMany().HasForeignKey(f => f.Escolinha_ID)
            .HasConstraintName("FK_FaixaDisponibilidade_Escolinha").OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(f => new { f.Escolinha_ID, f.Professor_ID }).HasDatabaseName("IX_FaixaDisponibilidade_Escolinha_Professor");
    }
}

public class IndisponibilidadeProfessorConfiguracao : IEntityTypeConfiguration<IndisponibilidadeProfessor>
{
    public void Configure(EntityTypeBuilder<IndisponibilidadeProfessor> builder)
    {
        builder.ToTable("IndisponibilidadeProfessor", t => t.HasCheckConstraint("CK_IndisponibilidadeProfessor_Periodo", "[Fim] > [Inicio]"));

        builder.HasKey(i => i.IndisponibilidadeProfessor_ID).HasName("PK_IndisponibilidadeProfessor");
        builder.Property(i => i.IndisponibilidadeProfessor_ID).HasDefaultValueSql("NEWSEQUENTIALID()").ValueGeneratedOnAdd();
        builder.HasOne<Escolinha>().WithMany().HasForeignKey(i => i.Escolinha_ID)
            .HasConstraintName("FK_IndisponibilidadeProfessor_Escolinha").OnDelete(DeleteBehavior.Restrict);

        builder.Property(i => i.Motivo).HasMaxLength(IndisponibilidadeProfessor.TamanhoMotivo);
        builder.Property(i => i.RegistradaPorNome).HasMaxLength(150).IsRequired();

        builder.HasIndex(i => new { i.Escolinha_ID, i.Professor_ID, i.Inicio }).HasDatabaseName("IX_IndisponibilidadeProfessor_Escolinha_Professor_Inicio");
    }
}

public class LocalConfiguracao : IEntityTypeConfiguration<Local>
{
    public void Configure(EntityTypeBuilder<Local> builder)
    {
        builder.ToTable("Local", t => t.HasCheckConstraint("CK_Local_Capacidade", "[Capacidade] IS NULL OR [Capacidade] > 0"));

        builder.HasKey(l => l.Local_ID).HasName("PK_Local");
        builder.Property(l => l.Local_ID).HasDefaultValueSql("NEWSEQUENTIALID()").ValueGeneratedOnAdd();
        builder.HasOne<Escolinha>().WithMany().HasForeignKey(l => l.Escolinha_ID)
            .HasConstraintName("FK_Local_Escolinha").OnDelete(DeleteBehavior.Restrict);

        builder.Property(l => l.Nome).HasMaxLength(Local.TamanhoNome).IsRequired();
        builder.Property(l => l.Observacao).HasMaxLength(Local.TamanhoObservacao);
        builder.ComplexProperty(l => l.Endereco, MapeamentoComum.MapearEndereco);

        builder.HasIndex(l => new { l.Escolinha_ID, l.Nome }).IsUnique().HasDatabaseName("UQ_Local_Escolinha_Nome");
    }
}

public class TurmaConfiguracao : IEntityTypeConfiguration<Turma>
{
    public void Configure(EntityTypeBuilder<Turma> builder)
    {
        builder.ToTable("Turma", t =>
        {
            t.HasCheckConstraint("CK_Turma_Capacidade", "[Capacidade] > 0");
            t.HasCheckConstraint("CK_Turma_Mensalidade", "[Mensalidade] >= 0");
            t.HasCheckConstraint("CK_Turma_MinimoConfirmados", "[MinimoConfirmados] > 0");
            t.HasCheckConstraint("CK_Turma_Periodicidade", "[PeriodicidadeMeses] IN (1, 3, 6, 12)");
            t.HasCheckConstraint("CK_Turma_MetaFrequencia", "[MetaFrequenciaPercentual] IS NULL OR [MetaFrequenciaPercentual] BETWEEN 1 AND 100");
        });

        builder.HasKey(t => t.Turma_ID).HasName("PK_Turma");
        builder.Property(t => t.Turma_ID).HasDefaultValueSql("NEWSEQUENTIALID()").ValueGeneratedOnAdd();
        builder.HasOne<Escolinha>().WithMany().HasForeignKey(t => t.Escolinha_ID)
            .HasConstraintName("FK_Turma_Escolinha").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Modalidade>().WithMany().HasForeignKey(t => t.Modalidade_ID)
            .HasConstraintName("FK_Turma_Modalidade").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(t => t.Local).WithMany().HasForeignKey(t => t.Local_ID)
            .HasConstraintName("FK_Turma_Local").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(t => t.Professor).WithMany().HasForeignKey(t => t.Professor_ID)
            .HasConstraintName("FK_Turma_Professor").OnDelete(DeleteBehavior.Restrict);

        builder.Property(t => t.Nome).HasMaxLength(Turma.TamanhoNome).IsRequired();
        builder.Property(t => t.Categoria).HasMaxLength(Turma.TamanhoCategoria);
        builder.Property(t => t.Mensalidade).HasPrecision(10, 2);
        builder.Property(t => t.MotivoEncerramento).HasMaxLength(Turma.TamanhoMotivo);
        builder.Property(t => t.Versao).IsRowVersion();
        builder.Ignore(t => t.Ativa);

        builder.HasMany(t => t.Horarios).WithOne().HasForeignKey(h => h.Turma_ID)
            .HasConstraintName("FK_HorarioTurma_Turma").OnDelete(DeleteBehavior.Cascade);

        // Nome único entre as turmas ativas (uma turma encerrada libera o nome).
        builder.HasIndex(t => new { t.Escolinha_ID, t.Nome }).IsUnique().HasFilter("[Situacao] = 10")
            .HasDatabaseName("UQ_Turma_Escolinha_Nome_Ativa");
        builder.HasIndex(t => new { t.Escolinha_ID, t.Professor_ID }).HasDatabaseName("IX_Turma_Escolinha_Professor");
    }
}

public class HorarioTurmaConfiguracao : IEntityTypeConfiguration<HorarioTurma>
{
    public void Configure(EntityTypeBuilder<HorarioTurma> builder)
    {
        builder.ToTable("HorarioTurma", t => t.HasCheckConstraint("CK_HorarioTurma_Horario", "[Fim] > [Inicio]"));

        builder.HasKey(h => h.HorarioTurma_ID).HasName("PK_HorarioTurma");
        builder.Property(h => h.HorarioTurma_ID).HasDefaultValueSql("NEWSEQUENTIALID()").ValueGeneratedOnAdd();
        builder.HasOne<Escolinha>().WithMany().HasForeignKey(h => h.Escolinha_ID)
            .HasConstraintName("FK_HorarioTurma_Escolinha").OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(h => h.Semanal);

        builder.HasIndex(h => new { h.Escolinha_ID, h.Turma_ID }).HasDatabaseName("IX_HorarioTurma_Escolinha_Turma");
    }
}

public class SessaoTreinoConfiguracao : IEntityTypeConfiguration<SessaoTreino>
{
    public void Configure(EntityTypeBuilder<SessaoTreino> builder)
    {
        builder.ToTable("SessaoTreino", t => t.HasCheckConstraint("CK_SessaoTreino_Horario", "[Fim] > [Inicio]"));

        builder.HasKey(s => s.SessaoTreino_ID).HasName("PK_SessaoTreino");
        builder.Property(s => s.SessaoTreino_ID).HasDefaultValueSql("NEWSEQUENTIALID()").ValueGeneratedOnAdd();
        builder.HasOne<Escolinha>().WithMany().HasForeignKey(s => s.Escolinha_ID)
            .HasConstraintName("FK_SessaoTreino_Escolinha").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.Turma).WithMany().HasForeignKey(s => s.Turma_ID)
            .HasConstraintName("FK_SessaoTreino_Turma").OnDelete(DeleteBehavior.Restrict);
        // Retirar um horário da turma não apaga o histórico das sessões dele: a referência fica nula.
        builder.HasOne<HorarioTurma>().WithMany().HasForeignKey(s => s.HorarioTurma_ID)
            .HasConstraintName("FK_SessaoTreino_HorarioTurma").OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(s => s.Local).WithMany().HasForeignKey(s => s.Local_ID)
            .HasConstraintName("FK_SessaoTreino_Local").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.Professor).WithMany().HasForeignKey(s => s.Professor_ID)
            .HasConstraintName("FK_SessaoTreino_Professor").OnDelete(DeleteBehavior.Restrict);

        builder.Property(s => s.MotivoCancelamento).HasMaxLength(SessaoTreino.TamanhoMotivo);
        builder.Property(s => s.CanceladaPorNome).HasMaxLength(SessaoTreino.TamanhoAutor);
        builder.HasOne<SessaoTreino>().WithMany().HasForeignKey(s => s.RemarcadaDe_ID).OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_SessaoTreino_RemarcadaDe");
        builder.HasIndex(s => s.RemarcadaDe_ID).HasDatabaseName("IX_SessaoTreino_RemarcadaDe").HasFilter("[RemarcadaDe_ID] IS NOT NULL");
        builder.Ignore(s => s.PodeCancelar);

        // A chave da geração: um horário gera uma sessão por data (rodar a geração de novo não duplica).
        builder.HasIndex(s => new { s.Escolinha_ID, s.HorarioTurma_ID, s.Data }).IsUnique().HasFilter("[HorarioTurma_ID] IS NOT NULL")
            .HasDatabaseName("UQ_SessaoTreino_Escolinha_Horario_Data");
        builder.HasIndex(s => new { s.Escolinha_ID, s.Turma_ID, s.Data }).HasDatabaseName("IX_SessaoTreino_Escolinha_Turma_Data");
        builder.HasIndex(s => new { s.Escolinha_ID, s.Professor_ID, s.Inicio }).HasDatabaseName("IX_SessaoTreino_Escolinha_Professor_Inicio");
        builder.HasIndex(s => new { s.Escolinha_ID, s.Local_ID, s.Inicio }).HasDatabaseName("IX_SessaoTreino_Escolinha_Local_Inicio");
    }
}

public class AlunoConfiguracao : IEntityTypeConfiguration<Aluno>
{
    public void Configure(EntityTypeBuilder<Aluno> builder)
    {
        builder.ToTable("Aluno");

        builder.HasKey(a => a.Aluno_ID).HasName("PK_Aluno");
        builder.Property(a => a.Aluno_ID).HasDefaultValueSql("NEWSEQUENTIALID()").ValueGeneratedOnAdd();
        builder.HasOne<Escolinha>().WithMany().HasForeignKey(a => a.Escolinha_ID)
            .HasConstraintName("FK_Aluno_Escolinha").OnDelete(DeleteBehavior.Restrict);

        builder.Property(a => a.Nome).HasMaxLength(Aluno.TamanhoNome).IsRequired();
        builder.Property(a => a.NomeBusca).HasMaxLength(Aluno.TamanhoNome).IsRequired();
        builder.Property(a => a.Cpf).HasMaxLength(11).IsUnicode(false).IsFixedLength();
        builder.Property(a => a.Categoria).HasMaxLength(Aluno.TamanhoCategoria);
        builder.ComplexProperty(a => a.Endereco, MapeamentoComum.MapearEndereco);
        builder.Property(a => a.MotivoDesligamento).HasMaxLength(Aluno.TamanhoMotivo);
        builder.Property(a => a.ContatoEmergenciaNome).HasMaxLength(Aluno.TamanhoNome);
        builder.Property(a => a.ContatoEmergenciaTelefone).HasMaxLength(Aluno.TamanhoTelefone).IsUnicode(false);
        builder.Property(a => a.ContatoEmergenciaParentesco).HasMaxLength(Aluno.TamanhoParentesco);
        builder.Property(a => a.Alergias).HasMaxLength(Aluno.TamanhoSaude);
        builder.Property(a => a.Restricoes).HasMaxLength(Aluno.TamanhoSaude);
        builder.Property(a => a.ObservacoesMedicas).HasMaxLength(Aluno.TamanhoObservacoesMedicas);
        builder.Property(a => a.Versao).IsRowVersion();
        builder.Ignore(a => a.Treina);

        builder.HasIndex(a => new { a.Escolinha_ID, a.Cpf }).IsUnique().HasFilter("[Cpf] IS NOT NULL").HasDatabaseName("UQ_Aluno_Escolinha_Cpf");
        builder.HasIndex(a => new { a.Escolinha_ID, a.NomeBusca }).HasDatabaseName("IX_Aluno_Escolinha_NomeBusca");
        builder.HasIndex(a => new { a.Escolinha_ID, a.Situacao }).HasDatabaseName("IX_Aluno_Escolinha_Situacao");
    }
}

public class ResponsavelConfiguracao : IEntityTypeConfiguration<Responsavel>
{
    public void Configure(EntityTypeBuilder<Responsavel> builder)
    {
        builder.ToTable("Responsavel");

        builder.HasKey(r => r.Responsavel_ID).HasName("PK_Responsavel");
        builder.Property(r => r.Responsavel_ID).HasDefaultValueSql("NEWSEQUENTIALID()").ValueGeneratedOnAdd();
        builder.HasOne<Escolinha>().WithMany().HasForeignKey(r => r.Escolinha_ID)
            .HasConstraintName("FK_Responsavel_Escolinha").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Pessoa>().WithMany().HasForeignKey(r => r.Pessoa_ID)
            .HasConstraintName("FK_Responsavel_Pessoa").OnDelete(DeleteBehavior.Restrict);

        builder.Property(r => r.Nome).HasMaxLength(Responsavel.TamanhoNome).IsRequired();
        builder.Property(r => r.NomeBusca).HasMaxLength(Responsavel.TamanhoNome).IsRequired();
        builder.Property(r => r.Cpf).HasMaxLength(11).IsUnicode(false).IsFixedLength();
        builder.Property(r => r.Telefone).HasMaxLength(Responsavel.TamanhoTelefone).IsUnicode(false);
        builder.Property(r => r.Email).HasMaxLength(Responsavel.TamanhoEmail);
        builder.ComplexProperty(r => r.Endereco, MapeamentoComum.MapearEndereco);

        builder.HasMany(r => r.Vinculos).WithOne(v => v.Responsavel).HasForeignKey(v => v.Responsavel_ID)
            .HasConstraintName("FK_VinculoAlunoResponsavel_Responsavel").OnDelete(DeleteBehavior.Restrict);

        // O CPF deduplica sozinho (CA-1); telefone e nome só sugerem.
        builder.HasIndex(r => new { r.Escolinha_ID, r.Cpf }).IsUnique().HasFilter("[Cpf] IS NOT NULL")
            .HasDatabaseName("UQ_Responsavel_Escolinha_Cpf");
        builder.HasIndex(r => new { r.Escolinha_ID, r.Pessoa_ID }).IsUnique().HasFilter("[Pessoa_ID] IS NOT NULL")
            .HasDatabaseName("UQ_Responsavel_Escolinha_Pessoa");
        builder.HasIndex(r => new { r.Escolinha_ID, r.Telefone }).HasDatabaseName("IX_Responsavel_Escolinha_Telefone");
        builder.HasIndex(r => new { r.Escolinha_ID, r.NomeBusca }).HasDatabaseName("IX_Responsavel_Escolinha_NomeBusca");
    }
}

public class VinculoAlunoResponsavelConfiguracao : IEntityTypeConfiguration<VinculoAlunoResponsavel>
{
    public void Configure(EntityTypeBuilder<VinculoAlunoResponsavel> builder)
    {
        builder.ToTable("VinculoAlunoResponsavel", t =>
            t.HasCheckConstraint("CK_VinculoAlunoResponsavel_Pagador", "[Pagador] = 0 OR [RecebeCobranca] = 1"));

        builder.HasKey(v => v.VinculoAlunoResponsavel_ID).HasName("PK_VinculoAlunoResponsavel");
        builder.Property(v => v.VinculoAlunoResponsavel_ID).HasDefaultValueSql("NEWSEQUENTIALID()").ValueGeneratedOnAdd();
        builder.HasOne<Escolinha>().WithMany().HasForeignKey(v => v.Escolinha_ID)
            .HasConstraintName("FK_VinculoAlunoResponsavel_Escolinha").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(v => v.Aluno).WithMany().HasForeignKey(v => v.Aluno_ID)
            .HasConstraintName("FK_VinculoAlunoResponsavel_Aluno").OnDelete(DeleteBehavior.Restrict);

        // "Um pagador por aluno" fica no serviço (ServicoResponsaveis), sem índice único filtrado: trocar o pagador entre
        // dois vínculos são duas atualizações no mesmo SaveChanges, e o EF não ordena as linhas pela condição do filtro.
        builder.HasIndex(v => new { v.Escolinha_ID, v.Aluno_ID, v.Responsavel_ID }).IsUnique()
            .HasDatabaseName("UQ_VinculoAlunoResponsavel_Escolinha_Aluno_Responsavel");
        builder.HasIndex(v => new { v.Escolinha_ID, v.Responsavel_ID }).HasDatabaseName("IX_VinculoAlunoResponsavel_Escolinha_Responsavel");

        // Verificação do vínculo (S2, BL-M01-06): a fila do gestor procura os pendentes.
        builder.Property(v => v.VerificadoPorNome).HasMaxLength(Responsavel.TamanhoNome);
        builder.Property(v => v.MotivoRecusa).HasMaxLength(VinculoAlunoResponsavel.TamanhoMotivoRecusa);
        builder.HasIndex(v => new { v.Escolinha_ID, v.Verificacao }).HasDatabaseName("IX_VinculoAlunoResponsavel_Escolinha_Verificacao");
    }
}

public class MatriculaConfiguracao : IEntityTypeConfiguration<Matricula>
{
    public void Configure(EntityTypeBuilder<Matricula> builder)
    {
        builder.ToTable("Matricula", t =>
        {
            t.HasCheckConstraint("CK_Matricula_Mensalidade", "[Mensalidade] >= 0");
            t.HasCheckConstraint("CK_Matricula_Periodo", "[DataFim] IS NULL OR [DataFim] >= [DataInicio]");
        });

        builder.HasKey(m => m.Matricula_ID).HasName("PK_Matricula");
        builder.Property(m => m.Matricula_ID).HasDefaultValueSql("NEWSEQUENTIALID()").ValueGeneratedOnAdd();
        builder.HasOne<Escolinha>().WithMany().HasForeignKey(m => m.Escolinha_ID)
            .HasConstraintName("FK_Matricula_Escolinha").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(m => m.Aluno).WithMany().HasForeignKey(m => m.Aluno_ID)
            .HasConstraintName("FK_Matricula_Aluno").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(m => m.Turma).WithMany().HasForeignKey(m => m.Turma_ID)
            .HasConstraintName("FK_Matricula_Turma").OnDelete(DeleteBehavior.Restrict);

        builder.Property(m => m.Mensalidade).HasPrecision(10, 2);
        builder.Property(m => m.MotivoEncerramento).HasMaxLength(Matricula.TamanhoMotivo);
        builder.Ignore(m => m.Ativa);

        // Uma matrícula ativa por aluno em cada turma (a encerrada fica no histórico).
        builder.HasIndex(m => new { m.Escolinha_ID, m.Aluno_ID, m.Turma_ID }).IsUnique().HasFilter("[Situacao] = 10")
            .HasDatabaseName("UQ_Matricula_Escolinha_Aluno_Turma_Ativa");
        builder.HasIndex(m => new { m.Escolinha_ID, m.Turma_ID, m.Situacao }).HasDatabaseName("IX_Matricula_Escolinha_Turma_Situacao");
    }
}

public class ConsentimentoAlunoConfiguracao : IEntityTypeConfiguration<ConsentimentoAluno>
{
    public void Configure(EntityTypeBuilder<ConsentimentoAluno> builder)
    {
        builder.ToTable("ConsentimentoAluno");

        builder.HasKey(c => c.ConsentimentoAluno_ID).HasName("PK_ConsentimentoAluno");
        builder.Property(c => c.ConsentimentoAluno_ID).HasDefaultValueSql("NEWSEQUENTIALID()").ValueGeneratedOnAdd();
        builder.HasOne<Escolinha>().WithMany().HasForeignKey(c => c.Escolinha_ID)
            .HasConstraintName("FK_ConsentimentoAluno_Escolinha").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Aluno>().WithMany().HasForeignKey(c => c.Aluno_ID)
            .HasConstraintName("FK_ConsentimentoAluno_Aluno").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.Responsavel).WithMany().HasForeignKey(c => c.Responsavel_ID)
            .HasConstraintName("FK_ConsentimentoAluno_Responsavel").OnDelete(DeleteBehavior.Restrict);

        builder.Property(c => c.VersaoTermo).HasMaxLength(ConsentimentoAluno.TamanhoVersao).IsUnicode(false).IsRequired();
        builder.Property(c => c.RegistradoPorNome).HasMaxLength(ConsentimentoAluno.TamanhoAutor).IsRequired();
        builder.Property(c => c.RevogadoPorNome).HasMaxLength(ConsentimentoAluno.TamanhoAutor);
        builder.Property(c => c.MotivoRevogacao).HasMaxLength(ConsentimentoAluno.TamanhoMotivo);
        builder.Ignore(c => c.Estado);

        builder.HasIndex(c => new { c.Escolinha_ID, c.Aluno_ID, c.Finalidade, c.RegistradoEm })
            .HasDatabaseName("IX_ConsentimentoAluno_Escolinha_Aluno_Finalidade_Em");
    }
}
