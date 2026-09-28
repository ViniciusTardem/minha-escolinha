using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MinhaEscolinha.Domain.Alunos;
using MinhaEscolinha.Domain.Configuracao;
using MinhaEscolinha.Domain.Financeiro;
using MinhaEscolinha.Domain.Turmas;

namespace MinhaEscolinha.Infrastructure.Data.Configuracoes;

public class CobrancaConfiguracao : IEntityTypeConfiguration<Cobranca>
{
    public void Configure(EntityTypeBuilder<Cobranca> builder)
    {
        builder.ToTable("Cobranca", t =>
        {
            t.HasCheckConstraint("CK_Cobranca_Situacao", "[Situacao] IN (10, 20, 30, 40, 50, 60, 70)");
            t.HasCheckConstraint("CK_Cobranca_Sinal", "[Sinal] IN (0, 10, 20, 30, 40, 50, 60)");
            t.HasCheckConstraint("CK_Cobranca_Valor", "[Valor] > 0");
        });

        builder.HasKey(c => c.Cobranca_ID).HasName("PK_Cobranca");
        builder.Property(c => c.Cobranca_ID).HasDefaultValueSql("NEWSEQUENTIALID()").ValueGeneratedOnAdd();
        builder.HasOne<Escolinha>().WithMany().HasForeignKey(c => c.Escolinha_ID)
            .HasConstraintName("FK_Cobranca_Escolinha").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Aluno>().WithMany().HasForeignKey(c => c.Aluno_ID)
            .HasConstraintName("FK_Cobranca_Aluno").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Matricula>().WithMany().HasForeignKey(c => c.Matricula_ID)
            .HasConstraintName("FK_Cobranca_Matricula").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Turma>().WithMany().HasForeignKey(c => c.Turma_ID)
            .HasConstraintName("FK_Cobranca_Turma").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Responsavel>().WithMany().HasForeignKey(c => c.Pagador_ID)
            .HasConstraintName("FK_Cobranca_Pagador").OnDelete(DeleteBehavior.Restrict);

        builder.Property(c => c.Valor).HasPrecision(12, 2);
        builder.Property(c => c.ValorPago).HasPrecision(12, 2);
        builder.Property(c => c.MultaPercentual).HasPrecision(5, 2);
        builder.Property(c => c.JurosPercentualMes).HasPrecision(5, 2);
        builder.Property(c => c.ConfirmadaPorNome).HasMaxLength(Cobranca.TamanhoNome);
        builder.Property(c => c.PagadorNome).HasMaxLength(Cobranca.TamanhoNome);
        builder.Property(c => c.IdAsaas).HasMaxLength(Cobranca.TamanhoIdAsaas).IsUnicode(false);
        builder.Property(c => c.UrlFatura).HasMaxLength(Cobranca.TamanhoUrl).IsUnicode(false);
        builder.Property(c => c.PixCopiaECola).HasMaxLength(Cobranca.TamanhoPix); // o nome do recebedor pode ter acento
        builder.Property(c => c.ErroEmissao).HasMaxLength(Cobranca.TamanhoErro);
        builder.Property(c => c.Motivo).HasMaxLength(Cobranca.TamanhoMotivo);
        builder.Property(c => c.EncerradaPorNome).HasMaxLength(Cobranca.TamanhoNome);
        builder.Property(c => c.UltimaEtapa).HasMaxLength(Cobranca.TamanhoEtapa).IsUnicode(false);
        builder.Property(c => c.SinalDetalhe).HasMaxLength(Cobranca.TamanhoMotivo);
        builder.Property(c => c.SinalResolucao).HasMaxLength(Cobranca.TamanhoMotivo);
        builder.Property(c => c.SinalResolvidoPorNome).HasMaxLength(Cobranca.TamanhoNome);
        builder.Property(c => c.Versao).IsRowVersion();

        // Uma mensalidade por matrícula e mês: a projeção e a emissão repetidas não duplicam.
        builder.HasIndex(c => new { c.Escolinha_ID, c.Matricula_ID, c.Competencia }).IsUnique()
            .HasDatabaseName("UQ_Cobranca_Escolinha_Matricula_Competencia");
        builder.HasIndex(c => new { c.Escolinha_ID, c.Situacao, c.Vencimento }).HasDatabaseName("IX_Cobranca_Escolinha_Situacao_Vencimento");
        builder.HasIndex(c => new { c.Escolinha_ID, c.Competencia }).HasDatabaseName("IX_Cobranca_Escolinha_Competencia");
        builder.HasIndex(c => new { c.Escolinha_ID, c.Aluno_ID }).HasDatabaseName("IX_Cobranca_Escolinha_Aluno");
        builder.HasIndex(c => new { c.Escolinha_ID, c.IdAsaas }).HasDatabaseName("IX_Cobranca_Escolinha_IdAsaas")
            .HasFilter("[IdAsaas] IS NOT NULL");
        builder.HasIndex(c => new { c.Escolinha_ID, c.Sinal }).HasDatabaseName("IX_Cobranca_Escolinha_Sinal").HasFilter("[Sinal] <> 0");
    }
}

public class ClienteAsaasConfiguracao : IEntityTypeConfiguration<ClienteAsaas>
{
    public void Configure(EntityTypeBuilder<ClienteAsaas> builder)
    {
        builder.ToTable("ClienteAsaas");

        builder.HasKey(c => c.ClienteAsaas_ID).HasName("PK_ClienteAsaas");
        builder.Property(c => c.ClienteAsaas_ID).HasDefaultValueSql("NEWSEQUENTIALID()").ValueGeneratedOnAdd();
        builder.HasOne<Escolinha>().WithMany().HasForeignKey(c => c.Escolinha_ID)
            .HasConstraintName("FK_ClienteAsaas_Escolinha").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Responsavel>().WithMany().HasForeignKey(c => c.Responsavel_ID)
            .HasConstraintName("FK_ClienteAsaas_Responsavel").OnDelete(DeleteBehavior.Restrict);

        builder.Property(c => c.IdAsaas).HasMaxLength(ClienteAsaas.TamanhoId).IsUnicode(false).IsRequired();

        // O pagador é um cliente por conta: a troca de conta cria o cliente de novo na conta nova.
        builder.HasIndex(c => new { c.Escolinha_ID, c.Responsavel_ID, c.Conta }).IsUnique()
            .HasDatabaseName("UQ_ClienteAsaas_Escolinha_Responsavel_Conta");
    }
}
