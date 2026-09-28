using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MinhaEscolinha.Domain.Alunos;
using MinhaEscolinha.Domain.Configuracao;
using MinhaEscolinha.Domain.Equipe;
using MinhaEscolinha.Domain.Eventos;
using MinhaEscolinha.Domain.Turmas;

namespace MinhaEscolinha.Infrastructure.Data.Configuracoes;

public class EventoConfiguracao : IEntityTypeConfiguration<Evento>
{
    public void Configure(EntityTypeBuilder<Evento> builder)
    {
        builder.ToTable("Evento", t =>
        {
            t.HasCheckConstraint("CK_Evento_Tipo", "[Tipo] IN (10)");
            t.HasCheckConstraint("CK_Evento_Situacao", "[Situacao] IN (10, 20, 30, 40, 50, 60)");
            t.HasCheckConstraint("CK_Evento_Duracao", $"[DuracaoMinutos] BETWEEN {Evento.DuracaoMinima} AND {Evento.DuracaoMaxima}");
        });

        builder.HasKey(e => e.Evento_ID).HasName("PK_Evento");
        builder.Property(e => e.Evento_ID).HasDefaultValueSql("NEWSEQUENTIALID()").ValueGeneratedOnAdd();
        builder.HasOne<Escolinha>().WithMany().HasForeignKey(e => e.Escolinha_ID)
            .HasConstraintName("FK_Evento_Escolinha").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Modalidade>().WithMany().HasForeignKey(e => e.Modalidade_ID)
            .HasConstraintName("FK_Evento_Modalidade").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Local>().WithMany().HasForeignKey(e => e.Local_ID)
            .HasConstraintName("FK_Evento_Local").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Professor>().WithMany().HasForeignKey(e => e.Professor_ID)
            .HasConstraintName("FK_Evento_Professor").OnDelete(DeleteBehavior.Restrict);

        builder.Property(e => e.Adversario).HasMaxLength(Evento.TamanhoAdversario);
        builder.Property(e => e.Logistica).HasMaxLength(Evento.TamanhoLogistica);
        builder.Property(e => e.Motivo).HasMaxLength(Evento.TamanhoMotivo);
        builder.Property(e => e.EncerradoPorNome).HasMaxLength(Evento.TamanhoAutor);
        builder.Property(e => e.Versao).IsRowVersion();
        builder.Ignore(e => e.Fim);
        builder.Ignore(e => e.Aberto);
        builder.Ignore(e => e.Encerrado);
        builder.Ignore(e => e.IdsDasTurmas);

        builder.HasMany(e => e.Turmas).WithOne().HasForeignKey(t => t.Evento_ID)
            .HasConstraintName("FK_EventoTurma_Evento").OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.Escolinha_ID, e.Inicio }).HasDatabaseName("IX_Evento_Escolinha_Inicio");
        builder.HasIndex(e => new { e.Escolinha_ID, e.Professor_ID, e.Inicio }).HasDatabaseName("IX_Evento_Escolinha_Professor_Inicio");
    }
}

public class EventoTurmaConfiguracao : IEntityTypeConfiguration<EventoTurma>
{
    public void Configure(EntityTypeBuilder<EventoTurma> builder)
    {
        builder.ToTable("EventoTurma");
        builder.HasKey(t => t.EventoTurma_ID).HasName("PK_EventoTurma");
        builder.Property(t => t.EventoTurma_ID).HasDefaultValueSql("NEWSEQUENTIALID()").ValueGeneratedOnAdd();
        builder.HasOne<Escolinha>().WithMany().HasForeignKey(t => t.Escolinha_ID)
            .HasConstraintName("FK_EventoTurma_Escolinha").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Turma>().WithMany().HasForeignKey(t => t.Turma_ID)
            .HasConstraintName("FK_EventoTurma_Turma").OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => new { t.Escolinha_ID, t.Evento_ID, t.Turma_ID }).IsUnique().HasDatabaseName("UQ_EventoTurma_Escolinha_Evento_Turma");
        builder.HasIndex(t => new { t.Escolinha_ID, t.Turma_ID }).HasDatabaseName("IX_EventoTurma_Escolinha_Turma");
    }
}

public class ConvocacaoConfiguracao : IEntityTypeConfiguration<Convocacao>
{
    public void Configure(EntityTypeBuilder<Convocacao> builder)
    {
        builder.ToTable("Convocacao", t =>
        {
            t.HasCheckConstraint("CK_Convocacao_Situacao", "[Situacao] IN (5, 10, 20, 30, 40, 50)");
            t.HasCheckConstraint("CK_Convocacao_Origem", "[Origem] IS NULL OR [Origem] IN (10, 20, 30)");
        });

        builder.HasKey(c => c.Convocacao_ID).HasName("PK_Convocacao");
        builder.Property(c => c.Convocacao_ID).HasDefaultValueSql("NEWSEQUENTIALID()").ValueGeneratedOnAdd();
        builder.HasOne<Escolinha>().WithMany().HasForeignKey(c => c.Escolinha_ID)
            .HasConstraintName("FK_Convocacao_Escolinha").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Evento>().WithMany().HasForeignKey(c => c.Evento_ID)
            .HasConstraintName("FK_Convocacao_Evento").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Aluno>().WithMany().HasForeignKey(c => c.Aluno_ID)
            .HasConstraintName("FK_Convocacao_Aluno").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Turma>().WithMany().HasForeignKey(c => c.Turma_ID)
            .HasConstraintName("FK_Convocacao_Turma").OnDelete(DeleteBehavior.Restrict);

        builder.Property(c => c.RespondidoPorNome).HasMaxLength(Convocacao.TamanhoNome);
        builder.Property(c => c.OutraResposta).HasMaxLength(Convocacao.TamanhoOutraResposta);
        builder.Property(c => c.MotivoCancelamento).HasMaxLength(Convocacao.TamanhoMotivo);
        builder.Property(c => c.Versao).IsRowVersion();
        builder.Ignore(c => c.Pendente);
        builder.Ignore(c => c.Ativa);
        builder.Ignore(c => c.Disparada);
        builder.Ignore(c => c.PodeRegistrarPorFora);

        builder.HasMany(c => c.Historico).WithOne().HasForeignKey(h => h.Convocacao_ID)
            .HasConstraintName("FK_HistoricoConvocacao_Convocacao").OnDelete(DeleteBehavior.Cascade);

        // Uma convocação por aluno e evento (spec §5: uma por convocado).
        builder.HasIndex(c => new { c.Escolinha_ID, c.Evento_ID, c.Aluno_ID }).IsUnique().HasDatabaseName("UQ_Convocacao_Escolinha_Evento_Aluno");
        builder.HasIndex(c => new { c.Escolinha_ID, c.Aluno_ID }).HasDatabaseName("IX_Convocacao_Escolinha_Aluno");
    }
}

public class HistoricoConvocacaoConfiguracao : IEntityTypeConfiguration<HistoricoConvocacao>
{
    public void Configure(EntityTypeBuilder<HistoricoConvocacao> builder)
    {
        builder.ToTable("HistoricoConvocacao");
        builder.HasKey(h => h.HistoricoConvocacao_ID).HasName("PK_HistoricoConvocacao");
        builder.Property(h => h.HistoricoConvocacao_ID).HasDefaultValueSql("NEWSEQUENTIALID()").ValueGeneratedOnAdd();
        builder.HasOne<Escolinha>().WithMany().HasForeignKey(h => h.Escolinha_ID)
            .HasConstraintName("FK_HistoricoConvocacao_Escolinha").OnDelete(DeleteBehavior.Restrict);
        builder.Property(h => h.Detalhe).HasMaxLength(HistoricoConvocacao.TamanhoDetalhe);
        builder.HasIndex(h => new { h.Escolinha_ID, h.Convocacao_ID, h.Em }).HasDatabaseName("IX_HistoricoConvocacao_Escolinha_Convocacao");
    }
}
