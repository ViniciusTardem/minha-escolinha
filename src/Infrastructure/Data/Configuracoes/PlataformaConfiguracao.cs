using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MinhaEscolinha.Domain.Configuracao;
using MinhaEscolinha.Domain.Integracao;
using MinhaEscolinha.Domain.Plataforma;

namespace MinhaEscolinha.Infrastructure.Data.Configuracoes;

public class PacoteCreditosConfiguracao : IEntityTypeConfiguration<PacoteCreditos>
{
    public void Configure(EntityTypeBuilder<PacoteCreditos> builder)
    {
        builder.ToTable("PacoteCreditos", t =>
        {
            t.HasCheckConstraint("CK_PacoteCreditos_Creditos", "[Creditos] > 0");
            t.HasCheckConstraint("CK_PacoteCreditos_Preco", "[Preco] >= 1");
        });

        builder.HasKey(p => p.PacoteCreditos_ID).HasName("PK_PacoteCreditos");
        builder.Property(p => p.PacoteCreditos_ID).HasDefaultValueSql("NEWSEQUENTIALID()").ValueGeneratedOnAdd();

        builder.Property(p => p.Nome).HasMaxLength(PacoteCreditos.TamanhoNome).IsRequired();
        builder.Property(p => p.Preco).HasPrecision(10, 2);

        builder.HasIndex(p => new { p.Ativo, p.Ordem }).HasDatabaseName("IX_PacoteCreditos_Ativo_Ordem");
    }
}

public class CompraCreditosConfiguracao : IEntityTypeConfiguration<CompraCreditos>
{
    public void Configure(EntityTypeBuilder<CompraCreditos> builder)
    {
        builder.ToTable("CompraCreditos", t =>
        {
            t.HasCheckConstraint("CK_CompraCreditos_Creditos", "[Creditos] > 0");
            t.HasCheckConstraint("CK_CompraCreditos_Valor", "[Valor] > 0");
        });

        builder.HasKey(c => c.CompraCreditos_ID).HasName("PK_CompraCreditos");
        // O id nasce na aplicação (vai para o Stripe antes da 2ª gravação); o padrão cobre inserção manual.
        builder.Property(c => c.CompraCreditos_ID).HasDefaultValueSql("NEWSEQUENTIALID()").ValueGeneratedOnAdd();
        builder.HasOne<Escolinha>().WithMany().HasForeignKey(c => c.Escolinha_ID)
            .HasConstraintName("FK_CompraCreditos_Escolinha").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PacoteCreditos>().WithMany().HasForeignKey(c => c.PacoteCreditos_ID)
            .HasConstraintName("FK_CompraCreditos_PacoteCreditos").OnDelete(DeleteBehavior.Restrict);

        builder.Property(c => c.NomePacote).HasMaxLength(PacoteCreditos.TamanhoNome).IsRequired();
        builder.Property(c => c.Valor).HasPrecision(10, 2);
        builder.Property(c => c.SessaoCheckoutId).HasMaxLength(CompraCreditos.TamanhoReferenciaExterna).IsUnicode(false);
        builder.Property(c => c.PagamentoId).HasMaxLength(CompraCreditos.TamanhoReferenciaExterna).IsUnicode(false);
        builder.Property(c => c.MeioPagamento).HasMaxLength(CompraCreditos.TamanhoMeio).IsUnicode(false);
        builder.Property(c => c.Erro).HasMaxLength(CompraCreditos.TamanhoErro);
        builder.Property(c => c.AutorNome).HasMaxLength(CompraCreditos.TamanhoAutor).IsRequired();
        builder.Property(c => c.Versao).IsRowVersion();

        builder.HasIndex(c => c.SessaoCheckoutId).IsUnique().HasFilter("[SessaoCheckoutId] IS NOT NULL")
            .HasDatabaseName("UQ_CompraCreditos_SessaoCheckout");
        builder.HasIndex(c => new { c.Escolinha_ID, c.IniciadaEm }).HasDatabaseName("IX_CompraCreditos_Escolinha_IniciadaEm");
        builder.HasIndex(c => new { c.Escolinha_ID, c.Situacao }).HasDatabaseName("IX_CompraCreditos_Escolinha_Situacao");
    }
}

public class WebhookRecebidoConfiguracao : IEntityTypeConfiguration<WebhookRecebido>
{
    public void Configure(EntityTypeBuilder<WebhookRecebido> builder)
    {
        builder.ToTable("WebhookRecebido");

        builder.HasKey(w => w.WebhookRecebido_ID).HasName("PK_WebhookRecebido");
        builder.Property(w => w.WebhookRecebido_ID).HasDefaultValueSql("NEWSEQUENTIALID()").ValueGeneratedOnAdd();

        builder.Property(w => w.Origem).HasMaxLength(WebhookRecebido.TamanhoOrigem).IsUnicode(false).IsRequired();
        builder.Property(w => w.EventoExterno).HasMaxLength(WebhookRecebido.TamanhoEvento).IsUnicode(false).IsRequired();
        builder.Property(w => w.Tipo).HasMaxLength(WebhookRecebido.TamanhoTipo).IsUnicode(false);
        builder.Property(w => w.Corpo).IsRequired();
        builder.Property(w => w.Ip).HasMaxLength(WebhookRecebido.TamanhoIp).IsUnicode(false);
        builder.Property(w => w.Observacao).HasMaxLength(WebhookRecebido.TamanhoObservacao);

        builder.HasIndex(w => new { w.Origem, w.EventoExterno }).IsUnique().HasDatabaseName("UQ_WebhookRecebido_Origem_Evento");
        builder.HasIndex(w => new { w.Origem, w.RecebidoEm }).HasDatabaseName("IX_WebhookRecebido_Origem_RecebidoEm");
    }
}
