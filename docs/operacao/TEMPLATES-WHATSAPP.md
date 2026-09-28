# Templates de WhatsApp — revisão antes da submissão à Meta (P02)

> Para quem valida os textos (o responsável pela LDNA) e para quem submete os templates no WhatsApp Manager da conta da
> LDNA (P14, P34). Situação em 28/09/2026: os **22 templates** (11 gatilhos, nos tons Cordial e Direto) estão como **rascunho**, todos na
> categoria **utilidade**, com os **textos decididos** pelo responsável (P02, D97). Nada foi submetido.
>
> **Onde ficam os textos:** a tela Plataforma › Templates de WhatsApp (equipe LDNA) é a fonte da verdade; os textos
> iniciais vêm de `TemplatesPadrao` (`src/Domain/Mensageria/TemplateMensagem.cs`). Aprovado um texto na Meta, marque o
> template como "Aprovado" na tela: só o aprovado sai pela API.

## 1. O que decidir

1. **A categoria:** **utilidade** para os 22 (§2).
2. **Os textos:** decididos (§4). Os que terminavam numa variável ganharam uma frase fixa no fim (§3.1).
3. **O que submeter primeiro:** para a prova com o número de teste (P34), `convite_acesso_v1`, `treino_cancelado_v1`,
   `convocacao_v1` e `convocacao_lembrete_v1`. Os demais antes da produção. Com o tom Direto na escolinha, submeta também as versões `_direto_v1`.

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

**Resolvido em 28/09/2026 (D97).** Os avisos de treino e de jogo terminavam em "Aviso para: {{n}}.". Agora fecham com uma
frase fixa ("Qualquer dúvida, fale com a equipe da escolinha."), e as mensalidades com "Se já pagou, desconsidere esta
mensagem.". Nenhum dos 22 termina numa variável. Isso também aumenta o texto fixo (§3.2).

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

## 4. Os 22 templates (decididos em 28/09/2026, D97)

Todos em `pt_BR`, categoria utilidade, pelo número oficial da LDNA (P14). O `{{2}}` é sempre o nome da escolinha, como sujeito (D48). Cada gatilho tem o tom **Cordial** e o **Direto**; o parâmetro `mensagem.tom` escolhe o da escolinha (o alerta de faltas usa o `presenca.alerta.tom`). Nenhum texto termina numa variável (§3.1).

| Gatilho | Tom | Template | Texto | Exemplos das variáveis, na ordem |
|---|---|---|---|---|
| Faltas atingidas | Cordial | `faltas_atingidas_cordial_v1` | Olá, {{1}}! {{2}} informa: {{3}} teve {{4}} em {{5}} nos treinos da turma {{6}}. Está tudo bem? Se precisar, fale com a equipe da escolinha. | Maria · Escolinha Piloto Londrina · Pedro · 3 faltas · setembro · Sub-11 |
|  | Direto | `faltas_atingidas_direto_v1` | Atenção, {{1}}: {{2}} informa que {{3}} já soma {{4}} em {{5}} na turma {{6}}. Por favor, entre em contato com a equipe da escolinha. | o mesmo |
| Treino cancelado | Cordial | `treino_cancelado_v1` | Olá, {{1}}! {{2}} informa: o treino da turma {{3}} marcado para {{4}} foi cancelado. Motivo: {{5}}. Este aviso é sobre {{6}}. Qualquer dúvida, fale com a equipe da escolinha. | Maria · Escolinha Piloto Londrina · Sub-11 · sábado, 03/10 às 09:00 · chuva forte · Pedro e Ana |
|  | Direto | `treino_cancelado_direto_v1` | Atenção, {{1}}: {{2}} cancelou o treino da turma {{3}} de {{4}}. Motivo: {{5}}. Aviso sobre {{6}}. Dúvidas, fale com a equipe da escolinha. | o mesmo |
| Treino remarcado | Cordial | `treino_remarcado_v1` | Olá, {{1}}! {{2}} informa que o treino da turma {{3}} mudou de horário. Antes: {{4}}. Agora: {{5}}. Este aviso é sobre {{6}}. Qualquer dúvida, fale com a equipe da escolinha. | Maria · Escolinha Piloto Londrina · Sub-11 · sábado, 03/10 às 09:00 · domingo, 04/10 às 10:00 · Pedro |
|  | Direto | `treino_remarcado_direto_v1` | Atenção, {{1}}: {{2}} mudou o horário do treino da turma {{3}}. Antes: {{4}}. Agora: {{5}}. Aviso sobre {{6}}. Dúvidas, fale com a equipe da escolinha. | o mesmo |
| Lembrete antes do vencimento | Cordial | `mensalidade_a_vencer_v1` | Olá, {{1}}! {{2}} lembra que a mensalidade de {{3}}, no valor de {{4}}, vence em {{5}}. Para pagar por Pix, boleto ou cartão, acesse a fatura: {{6}}. Se já pagou, desconsidere esta mensagem. | Maria · Escolinha Piloto Londrina · Pedro · R$ 150,00 · 10/10/2026 · https://www.asaas.com/i/080225913252 |
|  | Direto | `mensalidade_a_vencer_direto_v1` | Atenção, {{1}}: {{2}} informa que a mensalidade de {{3}}, de {{4}}, vence em {{5}}. Fatura para pagamento: {{6}}. Se já pagou, desconsidere esta mensagem. | o mesmo |
| Mensalidade vencida (régua) | Cordial | `mensalidade_vencida_cordial_v1` | Olá, {{1}}! {{2}} informa que a mensalidade de {{3}}, no valor de {{4}}, com vencimento em {{5}}, ainda consta em aberto. Para pagar por Pix, boleto ou cartão, acesse a fatura: {{6}}. Se já pagou, desconsidere esta mensagem. | Maria · Escolinha Piloto Londrina · Pedro · R$ 150,00 · 10/10/2026 · https://www.asaas.com/i/080225913252 |
|  | Direto | `mensalidade_vencida_direto_v1` | Atenção, {{1}}: {{2}} informa que a mensalidade de {{3}}, de {{4}}, está vencida desde {{5}}. Regularize pela fatura: {{6}}. Se já pagou, desconsidere esta mensagem. | o mesmo |
| Pagamento confirmado | Cordial | `pagamento_confirmado_v1` | Olá, {{1}}! {{2}} confirma o recebimento do pagamento da mensalidade de {{3}}, no valor de {{4}}. Agradecemos, e até o próximo treino! | Maria · Escolinha Piloto Londrina · Pedro · R$ 150,00 |
|  | Direto | `pagamento_confirmado_direto_v1` | Pagamento confirmado, {{1}}: {{2}} recebeu o pagamento da mensalidade de {{3}}, de {{4}}. Agradecemos. | o mesmo |
| Convocação (botões Vai e Não vai) | Cordial | `convocacao_v1` | Olá, {{1}}! {{2}} convocou {{3}} para o {{4}}, {{5}}, no local {{6}}. Como chegar: {{7}}. Toque em Vai ou Não vai para confirmar a presença. | Maria · Escolinha Piloto Londrina · Pedro · jogo da Sub-11 contra Tigre FC · sábado, 03/10 às 09:00 · Ginásio Moringão · https://www.google.com/maps/search/?api=1&query=... |
|  | Direto | `convocacao_direto_v1` | Convocação, {{1}}: {{2}} chamou {{3}} para o {{4}}, {{5}}, no local {{6}}. Como chegar: {{7}}. Responda pelos botões: Vai ou Não vai. | o mesmo |
| Lembrete a quem não respondeu (botões Vai e Não vai) | Cordial | `convocacao_lembrete_v1` | Olá, {{1}}! {{2}} ainda aguarda a resposta da convocação de {{3}} para o {{4}}, {{5}}. Toque em Vai ou Não vai para confirmar a presença. | Maria · Escolinha Piloto Londrina · Pedro · jogo da Sub-11 contra Tigre FC · sábado, 03/10 às 09:00 |
|  | Direto | `convocacao_lembrete_direto_v1` | Lembrete, {{1}}: {{2}} precisa da resposta da convocação de {{3}} para o {{4}}, {{5}}. Responda pelos botões: Vai ou Não vai. | o mesmo |
| Jogo alterado | Cordial | `jogo_alterado_v1` | Atenção, {{1}}: {{2}} informa que o {{3}} mudou. Novo horário: {{4}}, no local {{5}}. Como chegar: {{6}}. Este aviso é sobre {{7}}. Qualquer dúvida, fale com a equipe da escolinha. | Maria · Escolinha Piloto Londrina · jogo da Sub-11 contra Tigre FC · sábado, 03/10 às 10:00 · Ginásio Moringão · https://www.google.com/maps/search/?api=1&query=... · Pedro e Ana |
|  | Direto | `jogo_alterado_direto_v1` | Mudança de jogo, {{1}}: {{2}} alterou o {{3}}. Agora: {{4}}, no local {{5}}. Como chegar: {{6}}. Aviso sobre {{7}}. Dúvidas, fale com a equipe da escolinha. | o mesmo |
| Jogo cancelado ou adiado | Cordial | `jogo_cancelado_v1` | Atenção, {{1}}: {{2}} informa que o {{3}}, marcado para {{4}}, foi {{5}}. Motivo: {{6}}. Este aviso é sobre {{7}}. Qualquer dúvida, fale com a equipe da escolinha. | Maria · Escolinha Piloto Londrina · jogo da Sub-11 contra Tigre FC · sábado, 03/10 às 09:00 · cancelado · o adversário desistiu · Pedro e Ana |
|  | Direto | `jogo_cancelado_direto_v1` | Aviso de jogo, {{1}}: {{2}} informa que o {{3}} de {{4}} foi {{5}}. Motivo: {{6}}. Aviso sobre {{7}}. Dúvidas, fale com a equipe da escolinha. | o mesmo |
| Convite de acesso | Cordial | `convite_acesso_v1` | Olá, {{1}}! {{2}} convidou você para acompanhar tudo pelo Minha Escolinha. Ative o seu acesso por este link: {{3}} (vale até {{4}}). Se não reconhece este convite, ignore esta mensagem. | Maria · Escolinha Piloto Londrina · https://&lt;site&gt;/Convite/Xb7kQ2 · 05/10/2026 |
|  | Direto | `convite_acesso_direto_v1` | Convite de acesso, {{1}}: {{2}} liberou o seu acesso ao Minha Escolinha. Ative pelo link {{3}}, válido até {{4}}. Se não reconhece este convite, ignore esta mensagem. | o mesmo |

## 5. Depois da aprovação

1. Na tela Templates, marcar cada template aprovado como "Aprovado". Se a Meta reprovar, marcar "Rejeitado" com o motivo
   na observação, corrigir o texto na versão seguinte e submeter de novo.
2. Texto mudado depois de aprovado é template novo, com o nome na versão seguinte (`_v2`). A tela guarda um template
   por gatilho e tom, e o envio procura o template pelo nome gravado na mensagem: sem template aprovado com esse nome,
   a mensagem é encerrada sem envio, com o motivo em Mensagens. Por isso:
   - submeta primeiro a versão nova na Meta e, quando ela for aprovada, troque na tela o nome e o texto, já como
     "Aprovado";
   - a tela guarda o nome anterior, e o envio aceita os dois nomes até a fila esvaziar (P55, D97).
3. Seguir a prova com o número de teste: runbook §6.2 e roteiro de homologação §4 e §7.
