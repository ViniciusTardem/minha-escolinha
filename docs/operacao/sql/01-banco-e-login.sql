/*
  Banco da Minha Escolinha (dono único: este sistema) e o login da aplicação. Portado do AlternativaDepartamentoPessoal.
  As tabelas (inclusive as do Hangfire) são criadas pela própria aplicação na primeira subida (migrations automáticas).

  Quem roda: DBA, no servidor do banco. Troque <SENHA_FORTE> antes (nunca versione a senha).
  Connection string (variável de ambiente do site): ConnectionStrings__ConexaoPadrao
    Server=<servidor>;Database=escolinhaesporte;User Id=escolinha_app;Password=<SENHA_FORTE>;
    Encrypt=True;TrustServerCertificate=<conforme o servidor>;Application Name=MinhaEscolinha
  E a trava de ambiente: GuardaAmbiente__BancosProducao__0__Banco = escolinhaesporte
  (opcional: GuardaAmbiente__BancosProducao__0__Servidor = <servidor>).

  A ordenação é a mesma do banco de desenvolvimento e do de testes (a padrão do SQL Server instalado em pt-BR/en-US):
  sem diferenciar maiúsculas, diferenciando acentos.
*/

USE [master];
GO
IF DB_ID(N'escolinhaesporte') IS NULL
    CREATE DATABASE [escolinhaesporte] COLLATE SQL_Latin1_General_CP1_CI_AS;
GO
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'escolinha_app')
    CREATE LOGIN [escolinha_app] WITH PASSWORD = N'<SENHA_FORTE>', CHECK_POLICY = ON, DEFAULT_DATABASE = [escolinhaesporte];
GO

USE [escolinhaesporte];
GO
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'escolinha_app')
    CREATE USER [escolinha_app] FOR LOGIN [escolinha_app];
GO
-- A aplicação cria e altera as próprias tabelas (migrations do EF e schema do Hangfire): dona deste banco, e só dele.
ALTER ROLE [db_owner] ADD MEMBER [escolinha_app];
GO

-- Leitura de versão das linhas sem bloquear (o painel lê enquanto os jobs gravam). É também como o banco de testes roda:
-- o EF liga ao criar o banco. A concorrência de escrita continua pela versão de linha (rowversion).
ALTER DATABASE [escolinhaesporte] SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;
GO
-- O SQL Express cria o banco com AUTO_CLOSE ligado; o Hangfire consulta o banco sem parar.
ALTER DATABASE [escolinhaesporte] SET AUTO_CLOSE OFF;
GO

-- Conferência: o banco, a ordenação, as opções e o papel do login.
SELECT d.name, d.collation_name, d.compatibility_level, d.is_read_committed_snapshot_on, d.is_auto_close_on
FROM sys.databases d WHERE d.name = N'escolinhaesporte';
SELECT u.name AS usuario, r.name AS papel
FROM sys.database_role_members m
JOIN sys.database_principals r ON r.principal_id = m.role_principal_id
JOIN sys.database_principals u ON u.principal_id = m.member_principal_id
WHERE u.name = N'escolinha_app';
GO
