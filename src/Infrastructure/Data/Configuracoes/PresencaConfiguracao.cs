using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MinhaEscolinha.Domain.Alunos;
using MinhaEscolinha.Domain.Configuracao;
using MinhaEscolinha.Domain.Presencas;
using MinhaEscolinha.Domain.Turmas;

namespace MinhaEscolinha.Infrastructure.Data.Configuracoes;

public class PresencaConfiguracao : IEntityTypeConfiguration<Presenca>
{
    public void Configure(EntityTypeBuilder<Presenca> builder)
    {
        builder.ToTable("Presenca", t => t.HasCheckConstraint("CK_Presenca_Situacao", "[Situacao] IN (10, 20, 30)"));

        builder.HasKey(p => p.Presenca_ID).HasName("PK_Presenca");
        builder.Property(p => p.Presenca_ID).HasDefaultValueSql("NEWSEQUENTIALID()").ValueGeneratedOnAdd();
        builder.HasOne<Escolinha>().WithMany().HasForeignKey(p => p.Escolinha_ID)
            .HasConstraintName("FK_Presenca_Escolinha").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SessaoTreino>().WithMany().HasForeignKey(p => p.SessaoTreino_ID)
            .HasConstraintName("FK_Presenca_SessaoTreino").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Aluno>().WithMany().HasForeignKey(p => p.Aluno_ID)
            .HasConstraintName("FK_Presenca_Aluno").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Turma>().WithMany().HasForeignKey(p => p.Turma_ID)
            .HasConstraintName("FK_Presenca_Turma").OnDelete(DeleteBehavior.Restrict);

        builder.Property(p => p.Justificativa).HasMaxLength(Presenca.TamanhoJustificativa);
        builder.Property(p => p.Dispositivo).HasMaxLength(Presenca.TamanhoDispositivo);
        builder.Property(p => p.MarcadaPorNome).HasMaxLength(Presenca.TamanhoNome);
        builder.Property(p => p.Versao).IsRowVersion();

        // Uma presença por aluno em cada sessão; a contagem das faltas e a frequência leem por aluno, turma e data.
        builder.HasIndex(p => new { p.Escolinha_ID, p.SessaoTreino_ID, p.Aluno_ID }).IsUnique()
            .HasDatabaseName("UQ_Presenca_Escolinha_Sessao_Aluno");
        builder.HasIndex(p => new { p.Escolinha_ID, p.Aluno_ID, p.Turma_ID, p.SessaoInicio })
            .HasDatabaseName("IX_Presenca_Escolinha_Aluno_Turma_Inicio");
        builder.HasIndex(p => new { p.Escolinha_ID, p.SessaoInicio }).HasDatabaseName("IX_Presenca_Escolinha_Inicio");
    }
}

public class MarcacaoPresencaConfiguracao : IEntityTypeConfiguration<MarcacaoPresenca>
{
    public void Configure(EntityTypeBuilder<MarcacaoPresenca> builder)
    {
        builder.ToTable("MarcacaoPresenca", t => t.HasCheckConstraint("CK_MarcacaoPresenca_Resultado", "[Resultado] IN (10, 20)"));

        builder.HasKey(m => m.MarcacaoPresenca_ID).HasName("PK_MarcacaoPresenca");
        builder.Property(m => m.MarcacaoPresenca_ID).HasDefaultValueSql("NEWSEQUENTIALID()").ValueGeneratedOnAdd();
        builder.HasOne<Escolinha>().WithMany().HasForeignKey(m => m.Escolinha_ID)
            .HasConstraintName("FK_MarcacaoPresenca_Escolinha").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SessaoTreino>().WithMany().HasForeignKey(m => m.SessaoTreino_ID)
            .HasConstraintName("FK_MarcacaoPresenca_SessaoTreino").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Aluno>().WithMany().HasForeignKey(m => m.Aluno_ID)
            .HasConstraintName("FK_MarcacaoPresenca_Aluno").OnDelete(DeleteBehavior.Restrict);

        builder.Property(m => m.Justificativa).HasMaxLength(Presenca.TamanhoJustificativa);
        builder.Property(m => m.Dispositivo).HasMaxLength(Presenca.TamanhoDispositivo);
        builder.Property(m => m.PessoaNome).HasMaxLength(Presenca.TamanhoNome);
        builder.Property(m => m.Observacao).HasMaxLength(MarcacaoPresenca.TamanhoObservacao);

        // O id gerado no aparelho é único: o reenvio da mesma marcação é reconhecido (CA-21).
        builder.HasIndex(m => new { m.Escolinha_ID, m.Marcacao }).IsUnique().HasDatabaseName("UQ_MarcacaoPresenca_Escolinha_Marcacao");
        builder.HasIndex(m => new { m.Escolinha_ID, m.SessaoTreino_ID, m.Aluno_ID }).HasDatabaseName("IX_MarcacaoPresenca_Escolinha_Sessao_Aluno");
    }
}

public class AlertaFaltasConfiguracao : IEntityTypeConfiguration<AlertaFaltas>
{
    public void Configure(EntityTypeBuilder<AlertaFaltas> builder)
    {
        builder.ToTable("AlertaFaltas", t =>
        {
            t.HasCheckConstraint("CK_AlertaFaltas_Situacao", "[Situacao] IN (10, 20, 30)");
            t.HasCheckConstraint("CK_AlertaFaltas_Faltas", "[Faltas] >= 1 AND [Limite] >= 1");
        });

        builder.HasKey(a => a.AlertaFaltas_ID).HasName("PK_AlertaFaltas");
        builder.Property(a => a.AlertaFaltas_ID).HasDefaultValueSql("NEWSEQUENTIALID()").ValueGeneratedOnAdd();
        builder.HasOne<Escolinha>().WithMany().HasForeignKey(a => a.Escolinha_ID)
            .HasConstraintName("FK_AlertaFaltas_Escolinha").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Aluno>().WithMany().HasForeignKey(a => a.Aluno_ID)
            .HasConstraintName("FK_AlertaFaltas_Aluno").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Turma>().WithMany().HasForeignKey(a => a.Turma_ID)
            .HasConstraintName("FK_AlertaFaltas_Turma").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SessaoTreino>().WithMany().HasForeignKey(a => a.SessaoTreino_ID)
            .HasConstraintName("FK_AlertaFaltas_SessaoTreino").OnDelete(DeleteBehavior.Restrict);

        builder.Property(a => a.Periodo).HasMaxLength(AlertaFaltas.TamanhoPeriodo);
        builder.Property(a => a.Motivo).HasMaxLength(AlertaFaltas.TamanhoMotivo);
        builder.Ignore(a => a.Descricao);

        // A janela anti-duplicidade procura o último alerta disparado do aluno.
        builder.HasIndex(a => new { a.Escolinha_ID, a.Aluno_ID, a.Situacao, a.AvaliadoEm })
            .HasDatabaseName("IX_AlertaFaltas_Escolinha_Aluno_Situacao_Avaliado");
    }
}
