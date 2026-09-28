using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MinhaEscolinha.Domain.Carga;
using MinhaEscolinha.Domain.Configuracao;

namespace MinhaEscolinha.Infrastructure.Data.Configuracoes;

public class ImportacaoLoteConfiguracao : IEntityTypeConfiguration<ImportacaoLote>
{
    public void Configure(EntityTypeBuilder<ImportacaoLote> builder)
    {
        builder.ToTable("ImportacaoLote", t =>
        {
            t.HasCheckConstraint("CK_ImportacaoLote_Situacao", "[Situacao] IN (10, 20, 30)");
            // O conteúdo da planilha só existe enquanto o lote está aberto (R5: nada de saúde guardado fora da ficha).
            t.HasCheckConstraint("CK_ImportacaoLote_Conteudo", "[Situacao] = 10 OR [Conteudo] IS NULL");
        });

        builder.HasKey(l => l.ImportacaoLote_ID).HasName("PK_ImportacaoLote");
        builder.Property(l => l.ImportacaoLote_ID).HasDefaultValueSql("NEWSEQUENTIALID()").ValueGeneratedOnAdd();
        builder.HasOne<Escolinha>().WithMany().HasForeignKey(l => l.Escolinha_ID)
            .HasConstraintName("FK_ImportacaoLote_Escolinha").OnDelete(DeleteBehavior.Restrict);

        builder.Property(l => l.NomeArquivo).HasMaxLength(ImportacaoLote.TamanhoArquivo).IsRequired();
        builder.Property(l => l.Conteudo).IsUnicode(false);
        builder.Property(l => l.Mapeamento).IsUnicode(false).IsRequired();
        builder.Property(l => l.Decisoes).IsUnicode(false).IsRequired();
        builder.Property(l => l.EnviadoPorNome).HasMaxLength(ImportacaoLote.TamanhoNome).IsRequired();
        builder.Property(l => l.EncerradoPorNome).HasMaxLength(ImportacaoLote.TamanhoNome);
        builder.Property(l => l.Versao).IsRowVersion();
        builder.Ignore(l => l.Aberta);

        builder.HasIndex(l => new { l.Escolinha_ID, l.EnviadoEm }).HasDatabaseName("IX_ImportacaoLote_Escolinha_EnviadoEm");
    }
}
