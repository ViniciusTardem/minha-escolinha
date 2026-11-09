# Minha Escolinha

Sistema de gestão para escolinhas esportivas, começando por futsal e futebol em Londrina. Produto da LDNA S/A.

Ele automatiza as três tarefas que mais consomem o dia do gestor, nesta ordem de prioridade:

1. **Convocar para jogos.**
2. **Cobrar a mensalidade.**
3. **Controlar presença e evasão.**

## Para quem é

| Público | Como usa |
|---|---|
| Gestor | Painel web |
| Professor | Chamada e convocações na quadra, pelo celular |
| Responsável | Portal web e WhatsApp, sem instalar nada |
| Equipe LDNA | Área da plataforma (escolinhas, créditos, templates) |

## Estado atual

O piloto da Fase 1 está construído: **81 entregáveis em 8 fatias (S0 a S7)** e os **21 critérios de aceite** têm teste ou demonstração no simulador.

| Fatia | Conteúdo |
|---|---|
| S0 | Conta da escolinha, identidade, permissões, parâmetros |
| S1 | Turmas, alunos, responsáveis, equipe |
| S2 | Mensageria (WhatsApp) e acesso das famílias |
| S3 | Chamada na quadra (inclusive sem sinal) e risco de evasão |
| S4 | Mensalidades, régua de cobrança e conciliação (Asaas) |
| S5 | Eventos e convocações com painel em tempo real |
| S6 | Portal do responsável |
| S7 | Carga inicial por planilha |

**Ainda não está em produção.** Asaas, WhatsApp, Stripe e e-mail rodam em modo simulado. Faltam as contas reais, um servidor com HTTPS, a revisão jurídica dos termos e a planilha da escolinha piloto. O detalhe está em [`docs/PLANO.md`](docs/PLANO.md).

## Regras do produto

- Pagamento só pela Asaas (R1).
- Mensagens só por um motor único, pelo WhatsApp oficial, e nunca para o aluno menor (R2, R3, R4).
- Dados de saúde só para gestor, professor da turma e o responsável do aluno (R5).
- Regra de negócio sensível é parâmetro por escolinha, nunca constante no código (R7).
- Sinalizar, não bloquear (R9).

As 12 regras (R1 a R12) estão em [`CLAUDE.md`](CLAUDE.md), seção 4.

## Tecnologia

- .NET 10, ASP.NET Core MVC com Razor, Entity Framework Core
- SQL Server 2019 Express
- Hangfire (jobs), SignalR (tempo real)
- Metronic 9, KTUI e Tailwind 4
- xUnit e Playwright (testes)

Solução `MinhaEscolinha.sln`, em camadas que dependem para dentro:

```
src/Domain/          regras puras, sem pacotes
src/Application/     casos de uso, DTOs, validadores, interfaces
src/Infrastructure/  EF Core, repositórios, Hangfire, integrações
src/Web/             MVC, Razor, front
tests/Testes/        xUnit
docs/                plano, backlog, decisões, operação, design
```

Cada escolinha é isolada das outras por um filtro global no banco (multi-escolinha).

## Como rodar

Pré-requisitos: SDK do .NET 10, SQL Server (Express serve) e Node.js.

```powershell
# Segredos locais (nunca em arquivo versionado)
dotnet user-secrets set "ConnectionStrings:ConexaoPadrao" "<connection string>" --project src/Web
dotnet user-secrets set "Seed:Administrador:Login" "<e-mail>" --project src/Web
dotnet user-secrets set "Seed:Administrador:Nome" "<nome>" --project src/Web
dotnet user-secrets set "Seed:Administrador:Senha" "<senha temporária>" --project src/Web

dotnet build MinhaEscolinha.sln
dotnet test tests/Testes
dotnet run --project src/Web --launch-profile https   # https://localhost:7186
```

No primeiro start o app aplica as migrations e cria o primeiro administrador da plataforma. As integrações iniciam em modo **Simulado**, com telas de apoio em `/dev/whatsapp` e `/dev/asaas`, que só existem em Development.

Front (em `src/Web`): `npm install` e `npm run build`. O resultado vai para o git, porque o servidor não roda npm.

Todos os comandos, inclusive os de cada integração, estão em [`CLAUDE.md`](CLAUDE.md), seção 7.

## Documentação

| Documento | Para quê |
|---|---|
| [`CLAUDE.md`](CLAUDE.md) | Regras, convenções e comandos. Leia antes de mexer no código |
| [`docs/PLANO.md`](docs/PLANO.md) | Onde estamos e os próximos passos |
| [`docs/BACKLOG.md`](docs/BACKLOG.md) | Progresso item a item |
| [`docs/DECISOES-EM-ABERTO.md`](docs/DECISOES-EM-ABERTO.md) | Decisões, desvios e pendências |
| [`docs/HERANCA.md`](docs/HERANCA.md) | O que veio do projeto AlternativaDepartamentoPessoal |
| [`docs/operacao/RUNBOOK.md`](docs/operacao/RUNBOOK.md) | Publicação e operação no servidor |
| [`docs/design/GUIA-DESIGN-SYSTEM.md`](docs/design/GUIA-DESIGN-SYSTEM.md) | Guia visual |

## Avisos

- O projeto está no .NET 10 LTS (suporte até 10/11/2028); o suporte do 9 acaba em 10/11/2026. Veja a D113 em `docs/DECISOES-EM-ABERTO.md`. O CI (D114) passou em 09/11/2026 (build 0 aviso, 450 testes); faltam, na máquina do responsável, o teste no SQL Express, o CA-21 e o publish.
- Em produção, o app exige o certificado do Data Protection. Sem ele, não sobe.
- Cobrança real e WhatsApp real para números que não são de teste só com aprovação explícita.

## Licença

Projeto privado da LDNA S/A. Todos os direitos reservados.
