using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Domain.Alunos;
using MinhaEscolinha.Domain.Configuracao;
using MinhaEscolinha.Domain.Mensageria;

namespace MinhaEscolinha.Infrastructure.Data.Configuracoes;

public class MensagemConfiguracao : IEntityTypeConfiguration<Mensagem>
{
    public void Configure(EntityTypeBuilder<Mensagem> builder)
    {
        builder.ToTable("Mensagem", t =>
        {
            // Destinatário é responsável ou professor: nunca o aluno (R4), também no banco.
            t.HasCheckConstraint("CK_Mensagem_TipoDestinatario", "[TipoDestinatario] IN (10, 20)");
            t.HasCheckConstraint("CK_Mensagem_Canal", "[Canal] IN (10, 20)");
            t.HasCheckConstraint("CK_Mensagem_Creditos", "[CreditosConsumidos] >= 0");
        });

        builder.HasKey(m => m.Mensagem_ID).HasName("PK_Mensagem");
        builder.Property(m => m.Mensagem_ID).HasDefaultValueSql("NEWSEQUENTIALID()").ValueGeneratedOnAdd();
        builder.HasOne<Escolinha>().WithMany().HasForeignKey(m => m.Escolinha_ID)
            .HasConstraintName("FK_Mensagem_Escolinha").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Aluno>().WithMany().HasForeignKey(m => m.Aluno_ID)
            .HasConstraintName("FK_Mensagem_Aluno").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Pessoa>().WithMany().HasForeignKey(m => m.Pessoa_ID)
            .HasConstraintName("FK_Mensagem_Pessoa").OnDelete(DeleteBehavior.Restrict);

        builder.Property(m => m.Chave).HasMaxLength(Mensagem.TamanhoChave).IsUnicode(false).IsRequired();
        builder.Property(m => m.TipoOrigem).HasMaxLength(Mensagem.TamanhoTipoOrigem).IsUnicode(false).IsRequired();
        builder.Property(m => m.Etapa).HasMaxLength(Mensagem.TamanhoEtapa).IsUnicode(false);
        builder.Property(m => m.DestinatarioNome).HasMaxLength(Mensagem.TamanhoNome).IsRequired();
        builder.Property(m => m.Telefone).HasMaxLength(Mensagem.TamanhoTelefone).IsUnicode(false);
        builder.Property(m => m.Template).HasMaxLength(Mensagem.TamanhoTemplate).IsUnicode(false).IsRequired();
        builder.Property(m => m.Variaveis).HasMaxLength(Mensagem.TamanhoVariaveis).IsRequired();
        builder.Property(m => m.Conteudo).HasMaxLength(Mensagem.TamanhoConteudo).IsRequired();
        builder.Property(m => m.Motivo).HasMaxLength(Mensagem.TamanhoMotivo);
        builder.Property(m => m.IdExterno).HasMaxLength(Mensagem.TamanhoIdExterno).IsUnicode(false);
        builder.Property(m => m.Versao).IsRowVersion();

        builder.HasMany(m => m.Eventos).WithOne().HasForeignKey(e => e.Mensagem_ID)
            .HasConstraintName("FK_EventoMensagem_Mensagem").OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(m => m.Respostas).WithOne().HasForeignKey(r => r.Mensagem_ID)
            .HasConstraintName("FK_RespostaMensagem_Mensagem").OnDelete(DeleteBehavior.Cascade);

        // A mesma ocorrência do gatilho não gera duas mensagens para a mesma pessoa no mesmo canal.
        builder.HasIndex(m => new { m.Escolinha_ID, m.Chave }).IsUnique().HasDatabaseName("UQ_Mensagem_Escolinha_Chave");
        builder.HasIndex(m => new { m.Escolinha_ID, m.Canal, m.Situacao, m.AgendadaPara }).HasDatabaseName("IX_Mensagem_Escolinha_Fila");
        builder.HasIndex(m => new { m.Escolinha_ID, m.CriadaEm }).HasDatabaseName("IX_Mensagem_Escolinha_CriadaEm");
        builder.HasIndex(m => new { m.Escolinha_ID, m.Pessoa_ID, m.Canal }).HasDatabaseName("IX_Mensagem_Escolinha_Pessoa");
        builder.HasIndex(m => new { m.Escolinha_ID, m.Aluno_ID }).HasDatabaseName("IX_Mensagem_Escolinha_Aluno");
        builder.HasIndex(m => new { m.Escolinha_ID, m.TipoOrigem, m.Origem_ID }).HasDatabaseName("IX_Mensagem_Escolinha_Origem");
    }
}

public class EventoMensagemConfiguracao : IEntityTypeConfiguration<EventoMensagem>
{
    public void Configure(EntityTypeBuilder<EventoMensagem> builder)
    {
        builder.ToTable("EventoMensagem");
        builder.HasKey(e => e.EventoMensagem_ID).HasName("PK_EventoMensagem");
        builder.Property(e => e.EventoMensagem_ID).HasDefaultValueSql("NEWSEQUENTIALID()").ValueGeneratedOnAdd();
        builder.HasOne<Escolinha>().WithMany().HasForeignKey(e => e.Escolinha_ID)
            .HasConstraintName("FK_EventoMensagem_Escolinha").OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Detalhe).HasMaxLength(EventoMensagem.TamanhoDetalhe);
        builder.HasIndex(e => new { e.Escolinha_ID, e.Mensagem_ID, e.Em }).HasDatabaseName("IX_EventoMensagem_Escolinha_Mensagem");
    }
}

public class RespostaMensagemConfiguracao : IEntityTypeConfiguration<RespostaMensagem>
{
    public void Configure(EntityTypeBuilder<RespostaMensagem> builder)
    {
        builder.ToTable("RespostaMensagem");
        builder.HasKey(r => r.RespostaMensagem_ID).HasName("PK_RespostaMensagem");
        builder.Property(r => r.RespostaMensagem_ID).HasDefaultValueSql("NEWSEQUENTIALID()").ValueGeneratedOnAdd();
        builder.HasOne<Escolinha>().WithMany().HasForeignKey(r => r.Escolinha_ID)
            .HasConstraintName("FK_RespostaMensagem_Escolinha").OnDelete(DeleteBehavior.Restrict);
        builder.Property(r => r.Texto).HasMaxLength(RespostaMensagem.TamanhoTexto);
        builder.Property(r => r.Payload).HasMaxLength(RespostaMensagem.TamanhoPayload).IsUnicode(false);
        builder.Property(r => r.IdExterno).HasMaxLength(Mensagem.TamanhoIdExterno).IsUnicode(false);
        builder.Property(r => r.Observacao).HasMaxLength(RespostaMensagem.TamanhoObservacao);
        builder.HasIndex(r => new { r.Escolinha_ID, r.Mensagem_ID }).HasDatabaseName("IX_RespostaMensagem_Escolinha_Mensagem");
    }
}

public class TemplateMensagemConfiguracao : IEntityTypeConfiguration<TemplateMensagem>
{
    public void Configure(EntityTypeBuilder<TemplateMensagem> builder)
    {
        builder.ToTable("TemplateMensagem");
        builder.HasKey(t => t.TemplateMensagem_ID).HasName("PK_TemplateMensagem");
        builder.Property(t => t.TemplateMensagem_ID).HasDefaultValueSql("NEWSEQUENTIALID()").ValueGeneratedOnAdd();
        builder.Property(t => t.NomeNaMeta).HasMaxLength(TemplateMensagem.TamanhoNome).IsUnicode(false).IsRequired();
        builder.Property(t => t.NomeAnterior).HasMaxLength(TemplateMensagem.TamanhoNome).IsUnicode(false);
        builder.Property(t => t.Idioma).HasMaxLength(TemplateMensagem.TamanhoIdioma).IsUnicode(false).IsRequired();
        builder.Property(t => t.Corpo).HasMaxLength(TemplateMensagem.TamanhoCorpo).IsRequired();
        builder.Property(t => t.Variaveis).HasMaxLength(TemplateMensagem.TamanhoVariaveis).IsUnicode(false).IsRequired();
        builder.Property(t => t.Botoes).HasMaxLength(TemplateMensagem.TamanhoBotoes);
        builder.Property(t => t.Observacao).HasMaxLength(TemplateMensagem.TamanhoObservacao);

        // Um template por gatilho e tom; o nome na Meta também é único.
        builder.HasIndex(t => new { t.Gatilho, t.Tom }).IsUnique().HasDatabaseName("UQ_TemplateMensagem_Gatilho_Tom");
        builder.HasIndex(t => t.NomeNaMeta).IsUnique().HasDatabaseName("UQ_TemplateMensagem_NomeNaMeta");
    }
}

public class ConviteAcessoConfiguracao : IEntityTypeConfiguration<ConviteAcesso>
{
    public void Configure(EntityTypeBuilder<ConviteAcesso> builder)
    {
        builder.ToTable("ConviteAcesso", t =>
            t.HasCheckConstraint("CK_ConviteAcesso_Ficha",
                "([Tipo] = 10 AND [Responsavel_ID] IS NOT NULL AND [Professor_ID] IS NULL) OR ([Tipo] = 20 AND [Professor_ID] IS NOT NULL AND [Responsavel_ID] IS NULL)"));

        builder.HasKey(c => c.ConviteAcesso_ID).HasName("PK_ConviteAcesso");
        builder.Property(c => c.ConviteAcesso_ID).HasDefaultValueSql("NEWSEQUENTIALID()").ValueGeneratedOnAdd();
        builder.HasOne<Escolinha>().WithMany().HasForeignKey(c => c.Escolinha_ID)
            .HasConstraintName("FK_ConviteAcesso_Escolinha").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Responsavel>().WithMany().HasForeignKey(c => c.Responsavel_ID)
            .HasConstraintName("FK_ConviteAcesso_Responsavel").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Domain.Equipe.Professor>().WithMany().HasForeignKey(c => c.Professor_ID)
            .HasConstraintName("FK_ConviteAcesso_Professor").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Pessoa>().WithMany().HasForeignKey(c => c.Pessoa_ID)
            .HasConstraintName("FK_ConviteAcesso_Pessoa").OnDelete(DeleteBehavior.Restrict);

        builder.Property(c => c.Nome).HasMaxLength(Responsavel.TamanhoNome).IsRequired();
        builder.Property(c => c.Telefone).HasMaxLength(Mensagem.TamanhoTelefone).IsUnicode(false).IsRequired();
        builder.Property(c => c.TokenHash).HasMaxLength(ConviteAcesso.TamanhoHash).IsUnicode(false).IsFixedLength().IsRequired();
        builder.Property(c => c.EmitidoPorNome).HasMaxLength(Responsavel.TamanhoNome).IsRequired();
        builder.Property(c => c.MotivoEncerramento).HasMaxLength(ConviteAcesso.TamanhoMotivo);

        builder.HasIndex(c => new { c.Escolinha_ID, c.TokenHash }).IsUnique().HasDatabaseName("UQ_ConviteAcesso_Escolinha_TokenHash");
        // A ativação anônima acha o convite pelo hash, antes de saber a escolinha.
        builder.HasIndex(c => c.TokenHash).HasDatabaseName("IX_ConviteAcesso_TokenHash");
        builder.HasIndex(c => new { c.Escolinha_ID, c.Responsavel_ID }).HasDatabaseName("IX_ConviteAcesso_Escolinha_Responsavel");
        builder.HasIndex(c => new { c.Escolinha_ID, c.Professor_ID }).HasDatabaseName("IX_ConviteAcesso_Escolinha_Professor");
    }
}

public class RastreioWhatsAppConfiguracao : IEntityTypeConfiguration<RastreioWhatsApp>
{
    public void Configure(EntityTypeBuilder<RastreioWhatsApp> builder)
    {
        builder.ToTable("RastreioWhatsApp");
        builder.HasKey(r => r.RastreioWhatsApp_ID).HasName("PK_RastreioWhatsApp");
        builder.Property(r => r.RastreioWhatsApp_ID).HasDefaultValueSql("NEWSEQUENTIALID()").ValueGeneratedOnAdd();
        builder.Property(r => r.IdExterno).HasMaxLength(RastreioWhatsApp.TamanhoIdExterno).IsUnicode(false).IsRequired();
        builder.Property(r => r.ChaveTelefone).HasMaxLength(RastreioWhatsApp.TamanhoChaveTelefone).IsUnicode(false).IsRequired();

        builder.HasIndex(r => r.IdExterno).IsUnique().HasDatabaseName("UQ_RastreioWhatsApp_IdExterno");
        builder.HasIndex(r => new { r.ChaveTelefone, r.EnviadoEm }).HasDatabaseName("IX_RastreioWhatsApp_Telefone_EnviadoEm");
    }
}
