# Roteiro de homologação — Minha Escolinha

> Para quem valida o sistema antes da produção: a equipe LDNA e a gestão da escolinha piloto. Marque cada item; o que
> falhar vira anotação com print. Os critérios de aceite da spec (CA-1 a CA-21) estão indicados onde são provados.
>
> **Ambiente:** servidor com `ASPNETCORE_ENVIRONMENT = Homologacao`, HTTPS público e o certificado do Data Protection
> ([`RUNBOOK.md`](RUNBOOK.md) §4). Asaas na **Sandbox** (§6.1 do runbook) e WhatsApp com os **números de teste**
> (`WhatsApp__NumerosTeste`, §6.2): fora de Production, os demais números são suprimidos, com o motivo.
> **Só dados de teste:** a sandbox da Asaas manda e-mail e SMS de verdade.

## 1. Plataforma e acesso (S0)

- [ ] Entrar com o administrador do `Seed__Administrador__*` → troca obrigatória de senha.
- [ ] Plataforma › Nova escolinha, com o 1º gestor: a senha temporária aparece uma vez; o gestor entra e troca a senha.
- [ ] Criar uma segunda escolinha: o gestor de uma não vê nada da outra, nem colando o endereço de uma ficha (404).
- [ ] Conceder créditos de mensagem à escolinha piloto (Plataforma › Escolinhas).
- [ ] 5 senhas erradas → bloqueio temporário com aviso.
- [ ] "Esqueci a senha" com um login de e-mail → o e-mail chega (com `Email__RedirecionarPara` fora de Production) → o link cria a senha nova e não vale uma segunda vez (D107).
- [ ] Na ficha de um responsável que entra pelo CPF, "Link de nova senha" → o link aparece uma vez → a pessoa cria a senha e entra.
- [ ] Quem atua em duas escolinhas troca pelo menu da pessoa, num toque (BL-M01-02).
- [ ] Cadastrar um aluno com o 1º responsável com e-mail e os termos "ainda não coletado" → o e-mail chega → o link pede o CPF → aceitar um termo e recusar o outro → a ficha mostra "Respondido" com as duas respostas → o link não abre mais (D111).
- [ ] Um responsável sem e-mail: a ficha mostra o link para copiar; "Enviar de novo" invalida o anterior.

## 2. Configuração da escolinha (S0)

- [ ] Dados, modalidade futsal com o perfil Coletiva, locais.
- [ ] Parâmetros: mudar um valor → vale na hora, sem reiniciar; valor fora da faixa é recusado; o histórico mostra antes → depois, quem e quando (CA-18).
- [ ] Conta de recebimento: vincular a chave **Sandbox** (pede a senha; mostra só o final da chave). No painel da sandbox, em Integrações › Webhooks, aparece o endereço `https://<site>/api/v1/webhooks/asaas/<escolinha>`.

## 3. Cadastro (S1) e carga inicial (S7)

- [ ] Professores com disponibilidade; turmas com horários → as sessões aparecem na agenda.
- [ ] Importação: baixar o modelo ou enviar a planilha da escolinha → mapear as colunas → a conferência mostra aptos, avisos e bloqueados com o motivo, sem dado de saúde → confirmar.
- [ ] Irmãos da planilha com o mesmo responsável: um responsável só, com os dois filhos (CA-1).
- [ ] Cadastrar à mão o 2º filho de um responsável que já existe: o sistema reaproveita o responsável pelo CPF (CA-1).
- [ ] Ficha do aluno: a saúde só aparece para a gestão e o professor da turma; os consentimentos com a data.
- [ ] Desligar um aluno exige data e motivo; as mensalidades futuras são canceladas (CA-2); ele sai da chamada e das próximas convocações, e o histórico fica (CA-3).

## 4. Convites e mensagens (S2)

- [ ] Convites: convidar um responsável e um professor com números de teste → chega o link no WhatsApp → a pessoa ativa com o CPF e cria a senha.
- [ ] Vínculos: a gestão confirma o vínculo do responsável com o filho; antes disso o portal não mostra nenhum dado do aluno.
- [ ] Mensagens: cada envio com a situação (enviada, entregue, lida) e a linha do tempo.
- [ ] Mandar algo na janela de silêncio: a mensagem espera o fim da janela (exceto cancelamento ou alteração nas próximas 24 h).
- [ ] Nenhuma mensagem tem o aluno como destinatário (CA-19); só saem os gatilhos do catálogo (CA-20).
- [ ] Créditos zerados: as mensagens essenciais ficam retidas e o painel avisa; ao conceder créditos, saem sozinhas.

## 5. Chamada na quadra (S3)

Num **Android** e num **iPhone**, com o professor logado no celular:
- [ ] Abrir a lista do dia com sinal → modo avião → reabrir a chamada → marcar presença e falta → o selo mostra "Sem sinal" com as pendentes.
- [ ] Voltar o sinal → a fila sincroniza sozinha; o painel mostra as marcações da quadra (CA-21).
- [ ] No computador, o teste de navegador contra o servidor (`ESC_E2E_URL` com o endereço https, D108) passa.
- [ ] Com "Faltas para disparar o alerta" em 3: a 3ª falta no mês manda o alerta ao responsável na hora (CA-4); outra falta dentro da janela anti-duplicidade não manda um segundo (CA-5).
- [ ] A aba Presença da ficha mostra qual alerta saiu, quando e para quem (CA-6).
- [ ] Sair do sistema no celular: a lista guardada no aparelho é apagada.

## 6. Mensalidades (S4)

- [ ] Mensalidades › Atualizar agora → as do mês aparecem; confirmar a primeira de cada matrícula.
- [ ] A emissão vai para a sandbox, com a referência da mensalidade; o lembrete sai nos dias da régua.
- [ ] Pagar sem dinheiro (`confirmPaymentReceipt`, §6.1 do runbook) → a mensalidade passa a paga sozinha e a régua para (CA-7); a família não recebe mais cobrança dela (CA-8).
- [ ] Pagar na fatura com o cartão de teste da sandbox (`5162 3062 1937 8829`, CVV `318`) e estornar no painel da sandbox → a mensalidade vai para Divergências, sem conciliar; a gestão resolve com explicação (CA-9).
- [ ] Forçar o vencimento (`expirePayment`) → no dia seguinte, a rotina manda a cobrança "depois do vencimento".
- [ ] Pagador sem CPF: a mensalidade fica impedida, com o motivo na linha.
- [ ] O professor não vê nada do financeiro.

## 7. Convocações (S5)

- [ ] Montar o fim de semana: 4 jogos com 12 convocados cada, num fluxo só, e disparar (CA-10).
- [ ] Cada responsável recebe uma mensagem por convocado, com data, hora, local com o link do mapa e a pergunta (CA-11).
- [ ] Tocar em "Vai" no celular de teste → o painel do jogo aberto muda sozinho, sem recarregar (CA-12).
- [ ] Responsável com dois filhos convocados: as duas mensagens dizem o nome de cada filho e o jogo (CA-13).
- [ ] Cancelar um jogo → todas as famílias são avisadas e as convocações ficam canceladas (CA-14).
- [ ] Registrar uma resposta por fora; esperar o lembrete a quem não respondeu; no início do jogo, os pendentes viram "sem resposta".

## 8. Portal do responsável (S6)

No celular, com o responsável de dois filhos:
- [ ] Início com os dois filhos numa conta só (CA-15).
- [ ] Pagamentos: o total do mês e o detalhamento por filho, antes e depois de pagar (CA-16).
- [ ] Copiar o Pix e pagar na sandbox, ou abrir a fatura → a mensalidade concilia sozinha.
- [ ] Jogos: responder pelo portal → o painel do professor muda; a mesma pergunta também chegou pelo WhatsApp (CA-17).
- [ ] Presenças: só leitura, com as faltas justificadas em separado.

## 9. Operação

- [ ] `/health` responde Healthy; o monitor externo chama a cada 5 minutos (keep-alive).
- [ ] `/jobs` só abre para a equipe da plataforma; os recorrentes da §7 do runbook estão lá, no horário de Brasília.
- [ ] Reiniciar o site: ninguém é deslogado, os jobs voltam sozinhos e a chave Asaas da escolinha continua valendo.
- [ ] Logs: nenhum CPF inteiro, nenhuma chave nem token.
- [ ] Backup do banco restaurado num servidor de teste, com o mesmo certificado do Data Protection: o sistema sobe e a chave Asaas funciona.

## 10. Antes da produção

- [ ] Todos os itens acima marcados, e os que falharam corrigidos e refeitos.
- [ ] Revisão jurídica dos termos de consentimento, com o WhatsApp no termo de dados (P29, P15).
- [ ] Os templates aprovados pela Meta, com a categoria validada (P02); o número de produção da LDNA na Cloud API (P14).
- [ ] A conta Asaas de produção da escolinha, com a chave Pix cadastrada.
- [ ] Os valores provisórios dos parâmetros confirmados com a escolinha (P05).
- [ ] O certificado do Data Protection de produção guardado fora do servidor, com a senha em lugar seguro.
