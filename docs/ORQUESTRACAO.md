# Orquestração do trabalho com agentes

Como as demandas são divididas entre sessões e agentes. As definições ficam em [`.claude/agents/`](../.claude/agents/). Quem coordena é a sessão principal (o orquestrador), que decide, delega e junta o resultado.

## Papéis

| Agente | Função | Modelo | Esforço | Edita? |
|---|---|---|---|---|
| Orquestrador (sessão principal) | Divide a demanda, escolhe o agente, junta os resultados, fala com o responsável | o da sessão | o da sessão | Só o trivial |
| `escolinha-arquiteto` | Plano por demanda: arquivos, regras R1–R12, riscos, testes | Opus | high | Não |
| `escolinha-implementador` | Código cirúrgico com os testes das regras | Sonnet | high | Sim |
| `escolinha-revisor` | Revisão contra R1–R12, regras de ouro, isolamento e segurança | Opus | high | Não |
| `escolinha-qa` | Build, testes, classes CSS, pacotes vulneráveis | Haiku | medium | Não |
| `escolinha-docs` | BACKLOG, PLANO, DECISOES, RUNBOOK, README | Sonnet | medium | Só documentação |

O critério dos modelos: o que exige julgamento (planejar e revisar) usa o mais forte; o que escreve código segue o padrão existente e usa o intermediário; o que só roda comandos e conta usa o mais leve. Nenhum agente passa de esforço `high`.

## Fluxo de uma demanda

1. **Entender e planejar.** Demanda pequena e clara dispensa o arquiteto. Demanda que toca regra, banco, mensagem ou permissão passa por ele.
2. **Implementar.** Um implementador por item, na ordem da fatia. Itens que não mexem nos mesmos arquivos podem rodar em paralelo, cada um em seu worktree.
3. **Verificar.** O QA roda build, testes e verificações. O revisor lê o diff e devolve bloqueios, importantes e menores.
4. **Corrigir.** O implementador resolve os bloqueios e os importantes. O QA repete.
5. **Documentar.** O agente de documentação atualiza BACKLOG, PLANO, DECISOES e RUNBOOK na mesma entrega.
6. **Entregar.** Relatório ao responsável. **Commit e push só com autorização.**

## Limites que valem para todos

- Nada de WhatsApp real, cobrança real ou migration destrutiva sem aprovação humana explícita.
- Dependência ou padrão novo só com registro em `docs/DECISOES-EM-ABERTO.md`.
- Nenhum item fora do escopo da Fase 1 (spec §1.5).
- Agente nenhum faz commit ou push.
