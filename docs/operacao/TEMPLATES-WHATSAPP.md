# Templates de WhatsApp — revisão antes da submissão à Meta (P02)

> Para quem valida os textos (o responsável pela LDNA) e para quem submete os templates no WhatsApp Manager da conta da
> LDNA (P14, P34). Situação em 28/09/2026: os 13 templates estão como **rascunho**, todos na categoria **utilidade**, com
> textos provisórios. Nada foi submetido.
>
> **Onde ficam os textos:** a tela Plataforma › Templates de WhatsApp (equipe LDNA) é a fonte da verdade; os textos
> iniciais vêm de `TemplatesPadrao` (`src/Domain/Mensageria/TemplateMensagem.cs`). Aprovado um texto na Meta, marque o
> template como "Aprovado" na tela: só o aprovado sai pela API.

## 1. O que decidir

1. **A categoria:** a proposta é **utilidade** para os 13 (§2).
2. **Os textos:** ler cada um na §4. Quatro deles terminam numa variável e podem ser reprovados (§3.1).
3. **O que submeter primeiro:** para a prova com o número de teste (P34), `convite_acesso_v1`, `treino_cancelado_v1`,
   `convocacao_v1` e `convocacao_lembrete_v1`. Os demais antes da produção. O `treino_remarcado_v1` pode esperar: nada o
   dispara hoje (P33).

## 2. Por que utilidade

Pela [classificação da Meta](https://developers.facebook.com/documentation/business-messaging/whatsapp/templates/template-categorization):
- **utilidade** é a mensagem que acompanha uma relação ou transação que já existe, como o lembrete de pagamento e o aviso
  sobre a conta. É o caso de todos: o treino e o jogo de quem está matriculado, as faltas, as mensalidades, o pagamento
  e o convite para o acesso ao serviço contratado;
- **autenticação** é só o código de uso único (OTP). O convite leva um link, não um código;
- **marketing** é o que promove ou vende. Nenhum texto tem oferta, e não pode passar a ter: a Meta reclassifica como
  marketing o template de utilidade com conteúdo promocional, com aviso de 1 dia, e o marketing custa mais.

## 3. O que a Meta costuma reprovar, nestes textos

A [revisão de templates](https://developers.facebook.com/documentation/business-messaging/whatsapp/templates/template-review)
lista, entre os motivos de reprovação: a variável no início ou no fim do texto ("dangling parameters"), variáveis demais
para o tamanho da mensagem e a falta do exemplo de cada variável.

### 3.1 Terminam numa variável

`treino_cancelado_v1`, `treino_remarcado_v1`, `jogo_alterado_v1` e `jogo_cancelado_v1` terminam em "Aviso para: {{n}}.",
só com o ponto depois da variável. **Proposta:** fechar com uma frase fixa, por exemplo "Qualquer dúvida, fale com a
equipe." Isso também aumenta o texto fixo (§3.2). Como nada foi submetido ainda, o texto muda na própria tela Templates,
sem trocar o nome. O texto novo precisa usar as mesmas variáveis, cada uma uma vez e em ordem (a tela confere).

### 3.2 Muitas variáveis para o tamanho

A Meta não publica a proporção. Pela quantidade de variáveis e de texto fixo, o risco é:
- **maior:** `convocacao_v1`, `jogo_alterado_v1` e `jogo_cancelado_v1` (7 variáveis), `treino_remarcado_v1` e
  `treino_cancelado_v1` (6);
- **médio:** as faltas, as mensalidades e o `pagamento_confirmado_v1`;
- **baixo:** o `convite_acesso_v1`.

Se a Meta reprovar por isso, a saída é mais texto fixo, e não menos informação: o código monta os valores, e o texto
só os encaixa.

### 3.3 Exemplo de cada variável

A submissão pede um exemplo por variável. A §4 traz exemplos no formato que o sistema monta. Troque `<site>` pelo
endereço de produção.

## 4. Os 13 templates

Todos em `pt_BR`, categoria utilidade. O `{{2}}` é sempre o nome da escolinha, como sujeito (D48).

| Template (gatilho) | Texto | Exemplos das variáveis, na ordem | Atenção |
|---|---|---|---|
| `convite_acesso_v1` (convite de acesso) | Olá, {{1}}! {{2}} convidou você para acompanhar tudo pelo Minha Escolinha. Ative o seu acesso por este link: {{3}} (vale até {{4}}). Se não reconhece este convite, ignore esta mensagem. | Maria · Escolinha Piloto Londrina · https://&lt;site&gt;/Convite/Xb7kQ2 · 05/10/2026 | — |
| `treino_cancelado_v1` (treino cancelado) | Olá, {{1}}! {{2}} informa: o treino da turma {{3}} de {{4}} foi cancelado. Motivo: {{5}}. Aviso para: {{6}}. | Maria · Escolinha Piloto Londrina · Sub-11 · sábado, 03/10 às 09:00 · chuva forte · Pedro e Ana | Termina na variável (§3.1); 6 variáveis |
| `treino_remarcado_v1` (treino remarcado) | Olá, {{1}}! {{2}} informa: o treino da turma {{3}} de {{4}} mudou para {{5}}. Aviso para: {{6}}. | Maria · Escolinha Piloto Londrina · Sub-11 · sábado, 03/10 às 09:00 · domingo, 04/10 às 10:00 · Pedro | Termina na variável; 6 variáveis; nada o dispara hoje (P33) |
| `faltas_atingidas_cordial_v1` (faltas, tom cordial) | Olá, {{1}}! {{2}} informa: {{3}} teve {{4}} em {{5}} nos treinos da turma {{6}}. Está tudo bem? Se precisar, fale com a equipe. | Maria · Escolinha Piloto Londrina · Pedro · 3 faltas · setembro · Sub-11 | — |
| `faltas_atingidas_direto_v1` (faltas, tom direto) | Atenção, {{1}}: {{2}} informa que {{3}} já soma {{4}} em {{5}} na turma {{6}}. Por favor, entre em contato com a equipe. | Maria · Escolinha Piloto Londrina · Pedro · 3 faltas · setembro · Sub-11 | — |
| `mensalidade_a_vencer_v1` (lembrete antes do vencimento) | Olá, {{1}}! {{2}} informa: a mensalidade de {{3}} ({{4}}) vence em {{5}}. Para pagar, acesse {{6}} e escolha Pix, boleto ou cartão. | Maria · Escolinha Piloto Londrina · Pedro · R$ 150,00 · 10/10/2026 · https://www.asaas.com/i/080225913252 | O link é a fatura da Asaas da escolinha |
| `mensalidade_vencida_cordial_v1` (cobrança, tom cordial) | Olá, {{1}}! {{2}} informa: a mensalidade de {{3}} ({{4}}), com vencimento em {{5}}, ainda está em aberto. Para pagar, acesse {{6}} e escolha Pix, boleto ou cartão. | Maria · Escolinha Piloto Londrina · Pedro · R$ 150,00 · 10/10/2026 · https://www.asaas.com/i/080225913252 | — |
| `mensalidade_vencida_direto_v1` (cobrança, tom direto) | Atenção, {{1}}: {{2}} informa que a mensalidade de {{3}} ({{4}}) está vencida desde {{5}}. Regularize pelo link {{6}} o quanto antes. | Maria · Escolinha Piloto Londrina · Pedro · R$ 150,00 · 10/10/2026 · https://www.asaas.com/i/080225913252 | Tom firme, sem ameaça (a Meta reprova linguagem ameaçadora) |
| `pagamento_confirmado_v1` (pagamento confirmado) | Olá, {{1}}! {{2}} confirma o pagamento da mensalidade de {{3}} ({{4}}). Agradecemos! | Maria · Escolinha Piloto Londrina · Pedro · R$ 150,00 | Texto curto para 4 variáveis |
| `convocacao_v1` (convocação), botões "Vai" e "Não vai" | Olá, {{1}}! {{2}} convoca {{3}} para o {{4}}: {{5}}, no local {{6}} ({{7}}). Confirma a presença? | Maria · Escolinha Piloto Londrina · Pedro · jogo da Sub-11 contra o Tigre · sábado, 03/10 às 09:00 · Ginásio Moringão · https://www.google.com/maps/search/?api=1&query=... | 7 variáveis (§3.2); os botões são de resposta rápida |
| `convocacao_lembrete_v1` (lembrete a quem não respondeu), botões "Vai" e "Não vai" | Olá, {{1}}! {{2}} ainda espera a resposta da convocação de {{3}} para o {{4}}: {{5}}. Confirma a presença? | Maria · Escolinha Piloto Londrina · Pedro · jogo da Sub-11 contra o Tigre · sábado, 03/10 às 09:00 | — |
| `jogo_alterado_v1` (jogo alterado) | Atenção, {{1}}: {{2}} informa que o {{3}} mudou. Agora: {{4}}, no local {{5}} ({{6}}). Aviso para: {{7}}. | Maria · Escolinha Piloto Londrina · jogo da Sub-11 contra o Tigre · sábado, 03/10 às 10:00 · Ginásio Moringão · https://www.google.com/maps/search/?api=1&query=... · Pedro e Ana | Termina na variável; 7 variáveis |
| `jogo_cancelado_v1` (jogo cancelado ou adiado) | Atenção, {{1}}: {{2}} informa que o {{3}} de {{4}} foi {{5}}. Motivo: {{6}}. Aviso para: {{7}}. | Maria · Escolinha Piloto Londrina · jogo da Sub-11 contra o Tigre · sábado, 03/10 às 09:00 · cancelado · o adversário desistiu · Pedro e Ana | Termina na variável; 7 variáveis. O {{5}} é "cancelado" ou "adiado (a nova data será avisada)" |

## 5. Depois da aprovação

1. Na tela Templates, marcar cada template aprovado como "Aprovado". Se a Meta reprovar, marcar "Rejeitado" com o motivo
   na observação, corrigir o texto na versão seguinte e submeter de novo.
2. Texto mudado depois de aprovado é template novo, com o nome na versão seguinte (`_v2`). A tela guarda um template
   por gatilho e tom, e o envio procura o template pelo nome gravado na mensagem: sem template aprovado com esse nome,
   a mensagem é encerrada sem envio, com o motivo em Mensagens. Por isso:
   - submeta primeiro a versão nova na Meta e, quando ela for aprovada, troque na tela o nome e o texto, já como
     "Aprovado";
   - faça a troca fora de um disparo: as mensagens que ainda estão na fila com o nome antigo não saem.
3. Seguir a prova com o número de teste: runbook §6.2 e roteiro de homologação §4 e §7.
