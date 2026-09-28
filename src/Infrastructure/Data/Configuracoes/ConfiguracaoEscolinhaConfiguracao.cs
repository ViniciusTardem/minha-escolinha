using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MinhaEscolinha.Domain.Auditoria;
using MinhaEscolinha.Domain.Comum;
using MinhaEscolinha.Domain.Configuracao;
using MinhaEscolinha.Domain.Configuracao.Parametros;

namespace MinhaEscolinha.Infrastructure.Data.Configuracoes;

public class EscolinhaConfiguracao : IEntityTypeConfiguration<Escolinha>
{
    public void Configure(EntityTypeBuilder<Escolinha> builder)
    {
        builder.ToTable("Escolinha");

        builder.HasKey(e => e.Escolinha_ID).HasName("PK_Escolinha");
        // Criada com o id definido pela aplicação (o contexto já nasce na escolinha nova); o padrão cobre inserção manual.
        builder.Property(e => e.Escolinha_ID).HasDefaultValueSql("NEWSEQUENTIALID()").ValueGeneratedOnAdd();

        builder.Property(e => e.Nome).HasMaxLength(Escolinha.TamanhoNome).IsRequired();
        builder.Property(e => e.RazaoSocial).HasMaxLength(Escolinha.TamanhoRazaoSocial);
        builder.Property(e => e.Cnpj).HasMaxLength(14).IsUnicode(false).IsFixedLength();
        builder.Property(e => e.Telefone).HasMaxLength(Escolinha.TamanhoTelefone).IsUnicode(false);
        builder.Property(e => e.Email).HasMaxLength(Escolinha.TamanhoEmail);

        builder.ComplexProperty(e => e.Endereco, endereco =>
        {
            endereco.Property(x => x.Logradouro).HasColumnName("Logradouro").HasMaxLength(Endereco.TamanhoLogradouro);
            endereco.Property(x => x.Numero).HasColumnName("Numero").HasMaxLength(Endereco.TamanhoNumero);
            endereco.Property(x => x.Complemento).HasColumnName("Complemento").HasMaxLength(Endereco.TamanhoComplemento);
            endereco.Property(x => x.Bairro).HasColumnName("Bairro").HasMaxLength(Endereco.TamanhoBairro);
            endereco.Property(x => x.Cidade).HasColumnName("Cidade").HasMaxLength(Endereco.TamanhoCidade);
            endereco.Property(x => x.Uf).HasColumnName("Uf").HasMaxLength(Endereco.TamanhoUf).IsUnicode(false).IsFixedLength();
            endereco.Property(x => x.Cep).HasColumnName("Cep").HasMaxLength(Endereco.TamanhoCep).IsUnicode(false).IsFixedLength();
        });
    }
}

public class ModalidadeConfiguracao : IEntityTypeConfiguration<Modalidade>
{
    public void Configure(EntityTypeBuilder<Modalidade> builder)
    {
        builder.ToTable("Modalidade");

        builder.HasKey(m => m.Modalidade_ID).HasName("PK_Modalidade");
        builder.Property(m => m.Modalidade_ID).HasDefaultValueSql("NEWSEQUENTIALID()").ValueGeneratedOnAdd();
        builder.HasOne<Escolinha>().WithMany().HasForeignKey(m => m.Escolinha_ID)
            .HasConstraintName("FK_Modalidade_Escolinha").OnDelete(DeleteBehavior.Restrict);

        builder.Property(m => m.Nome).HasMaxLength(Modalidade.TamanhoNome).IsRequired();
        builder.Ignore(m => m.Recursos);

        builder.HasIndex(m => new { m.Escolinha_ID, m.Nome }).IsUnique().HasDatabaseName("UQ_Modalidade_Escolinha_Nome");
    }
}

public class ParametroEscolinhaConfiguracao : IEntityTypeConfiguration<ParametroEscolinha>
{
    public void Configure(EntityTypeBuilder<ParametroEscolinha> builder)
    {
        builder.ToTable("ParametroEscolinha");

        builder.HasKey(p => p.ParametroEscolinha_ID).HasName("PK_ParametroEscolinha");
        builder.Property(p => p.ParametroEscolinha_ID).HasDefaultValueSql("NEWSEQUENTIALID()").ValueGeneratedOnAdd();
        builder.HasOne<Escolinha>().WithMany().HasForeignKey(p => p.Escolinha_ID)
            .HasConstraintName("FK_ParametroEscolinha_Escolinha").OnDelete(DeleteBehavior.Restrict);

        builder.Property(p => p.Chave).HasMaxLength(ParametroEscolinha.TamanhoChave).IsUnicode(false).IsRequired();
        builder.Property(p => p.Valor).HasMaxLength(ParametroEscolinha.TamanhoValor);

        builder.HasIndex(p => new { p.Escolinha_ID, p.Chave }).IsUnique().HasDatabaseName("UQ_ParametroEscolinha_Escolinha_Chave");
    }
}

public class ContaAsaasEscolinhaConfiguracao : IEntityTypeConfiguration<ContaAsaasEscolinha>
{
    public void Configure(EntityTypeBuilder<ContaAsaasEscolinha> builder)
    {
        builder.ToTable("ContaAsaasEscolinha");

        builder.HasKey(c => c.ContaAsaasEscolinha_ID).HasName("PK_ContaAsaasEscolinha");
        builder.Property(c => c.ContaAsaasEscolinha_ID).HasDefaultValueSql("NEWSEQUENTIALID()").ValueGeneratedOnAdd();
        builder.HasOne<Escolinha>().WithMany().HasForeignKey(c => c.Escolinha_ID)
            .HasConstraintName("FK_ContaAsaasEscolinha_Escolinha").OnDelete(DeleteBehavior.Restrict);

        builder.Property(c => c.ChaveApiProtegida).HasMaxLength(2000).IsUnicode(false).IsRequired();
        builder.Property(c => c.FinalChave).HasMaxLength(ContaAsaasEscolinha.TamanhoFinal).IsUnicode(false).IsRequired();
        // A conta vinculada antes do S4 ganha um identificador na migração.
        builder.Property(c => c.Identificador).HasDefaultValueSql("NEWSEQUENTIALID()");
        builder.Property(c => c.TokenWebhookProtegido).HasMaxLength(2000).IsUnicode(false);
        builder.Property(c => c.WebhookErro).HasMaxLength(ContaAsaasEscolinha.TamanhoErroWebhook);

        builder.HasIndex(c => c.Escolinha_ID).IsUnique().HasDatabaseName("UQ_ContaAsaasEscolinha_Escolinha");
    }
}

public class ContaCreditosConfiguracao : IEntityTypeConfiguration<ContaCreditos>
{
    public void Configure(EntityTypeBuilder<ContaCreditos> builder)
    {
        builder.ToTable("ContaCreditos", t => t.HasCheckConstraint("CK_ContaCreditos_Saldo", "[Saldo] >= 0"));

        builder.HasKey(c => c.ContaCreditos_ID).HasName("PK_ContaCreditos");
        builder.Property(c => c.ContaCreditos_ID).HasDefaultValueSql("NEWSEQUENTIALID()").ValueGeneratedOnAdd();
        builder.HasOne<Escolinha>().WithMany().HasForeignKey(c => c.Escolinha_ID)
            .HasConstraintName("FK_ContaCreditos_Escolinha").OnDelete(DeleteBehavior.Restrict);

        builder.Property(c => c.Plano).HasMaxLength(ContaCreditos.TamanhoPlano).IsRequired();
        builder.Property(c => c.Pacote).HasMaxLength(ContaCreditos.TamanhoPlano);
        builder.Property(c => c.Versao).IsRowVersion();

        builder.HasIndex(c => c.Escolinha_ID).IsUnique().HasDatabaseName("UQ_ContaCreditos_Escolinha");
    }
}

public class MovimentoCreditoConfiguracao : IEntityTypeConfiguration<MovimentoCredito>
{
    public void Configure(EntityTypeBuilder<MovimentoCredito> builder)
    {
        builder.ToTable("MovimentoCredito");

        builder.HasKey(m => m.MovimentoCredito_ID).HasName("PK_MovimentoCredito");
        builder.Property(m => m.MovimentoCredito_ID).HasDefaultValueSql("NEWSEQUENTIALID()").ValueGeneratedOnAdd();
        builder.HasOne<Escolinha>().WithMany().HasForeignKey(m => m.Escolinha_ID)
            .HasConstraintName("FK_MovimentoCredito_Escolinha").OnDelete(DeleteBehavior.Restrict);

        builder.Property(m => m.Motivo).HasMaxLength(MovimentoCredito.TamanhoMotivo).IsRequired();
        builder.Property(m => m.AutorNome).HasMaxLength(MovimentoCredito.TamanhoAutor).IsRequired();

        builder.HasIndex(m => new { m.Escolinha_ID, m.Em }).HasDatabaseName("IX_MovimentoCredito_Escolinha_Em");
    }
}

public class RegistroAlteracaoConfiguracao : IEntityTypeConfiguration<RegistroAlteracao>
{
    public void Configure(EntityTypeBuilder<RegistroAlteracao> builder)
    {
        builder.ToTable("RegistroAlteracao");

        builder.HasKey(r => r.RegistroAlteracao_ID).HasName("PK_RegistroAlteracao");
        builder.Property(r => r.RegistroAlteracao_ID).HasDefaultValueSql("NEWSEQUENTIALID()").ValueGeneratedOnAdd();
        builder.HasOne<Escolinha>().WithMany().HasForeignKey(r => r.Escolinha_ID)
            .HasConstraintName("FK_RegistroAlteracao_Escolinha").OnDelete(DeleteBehavior.Restrict);

        builder.Property(r => r.Entidade).HasMaxLength(RegistroAlteracao.TamanhoEntidade).IsUnicode(false).IsRequired();
        builder.Property(r => r.Campo).HasMaxLength(RegistroAlteracao.TamanhoCampo).IsRequired();
        builder.Property(r => r.ValorAnterior).HasMaxLength(RegistroAlteracao.TamanhoValor);
        builder.Property(r => r.ValorNovo).HasMaxLength(RegistroAlteracao.TamanhoValor);
        builder.Property(r => r.Motivo).HasMaxLength(RegistroAlteracao.TamanhoMotivo);
        builder.Property(r => r.AutorNome).HasMaxLength(RegistroAlteracao.TamanhoAutor).IsRequired();
        builder.Property(r => r.Ip).HasMaxLength(RegistroAlteracao.TamanhoIp).IsUnicode(false);

        builder.HasIndex(r => new { r.Escolinha_ID, r.Entidade, r.Em }).HasDatabaseName("IX_RegistroAlteracao_Escolinha_Entidade_Em");
    }
}
