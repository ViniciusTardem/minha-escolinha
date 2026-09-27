using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Domain.Configuracao;

namespace MinhaEscolinha.Infrastructure.Data.Configuracoes;

public class PessoaConfiguracao : IEntityTypeConfiguration<Pessoa>
{
    public void Configure(EntityTypeBuilder<Pessoa> builder)
    {
        builder.ToTable("Pessoa");

        builder.HasKey(p => p.Pessoa_ID).HasName("PK_Pessoa");
        builder.Property(p => p.Pessoa_ID).HasDefaultValueSql("NEWSEQUENTIALID()").ValueGeneratedOnAdd();

        builder.Property(p => p.Nome).HasMaxLength(Pessoa.TamanhoNome).IsRequired();
        builder.Property(p => p.Cpf).HasMaxLength(11).IsUnicode(false).IsFixedLength();

        builder.HasIndex(p => p.Cpf).IsUnique().HasFilter("[Cpf] IS NOT NULL").HasDatabaseName("UQ_Pessoa_Cpf");
    }
}

public class CredencialConfiguracao : IEntityTypeConfiguration<Credencial>
{
    public void Configure(EntityTypeBuilder<Credencial> builder)
    {
        builder.ToTable("Credencial");

        builder.HasKey(c => c.Pessoa_ID).HasName("PK_Credencial");
        builder.Property(c => c.Pessoa_ID).ValueGeneratedNever();
        builder.HasOne(c => c.Pessoa).WithOne(p => p.Credencial)
            .HasForeignKey<Credencial>(c => c.Pessoa_ID)
            .HasConstraintName("FK_Credencial_Pessoa")
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(c => c.Login).HasMaxLength(Credencial.TamanhoLogin).IsRequired();
        builder.Property(c => c.SenhaHash).HasMaxLength(100).IsUnicode(false).IsRequired();

        builder.HasIndex(c => c.Login).IsUnique().HasDatabaseName("UQ_Credencial_Login");
    }
}

public class VinculoAtuacaoConfiguracao : IEntityTypeConfiguration<VinculoAtuacao>
{
    public void Configure(EntityTypeBuilder<VinculoAtuacao> builder)
    {
        builder.ToTable("VinculoAtuacao");

        builder.HasKey(v => v.VinculoAtuacao_ID).HasName("PK_VinculoAtuacao");
        builder.Property(v => v.VinculoAtuacao_ID).HasDefaultValueSql("NEWSEQUENTIALID()").ValueGeneratedOnAdd();

        builder.HasOne<Escolinha>().WithMany().HasForeignKey(v => v.Escolinha_ID)
            .HasConstraintName("FK_VinculoAtuacao_Escolinha").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(v => v.Pessoa).WithMany().HasForeignKey(v => v.Pessoa_ID)
            .HasConstraintName("FK_VinculoAtuacao_Pessoa").OnDelete(DeleteBehavior.Restrict);

        builder.Property(v => v.MotivoRevogacao).HasMaxLength(VinculoAtuacao.TamanhoMotivo);

        builder.HasIndex(v => new { v.Escolinha_ID, v.Pessoa_ID, v.Papel }).IsUnique()
            .HasDatabaseName("UQ_VinculoAtuacao_Escolinha_Pessoa_Papel");
        builder.HasIndex(v => new { v.Pessoa_ID, v.Situacao }).HasDatabaseName("IX_VinculoAtuacao_Pessoa_Situacao");
    }
}
