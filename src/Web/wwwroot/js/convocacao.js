// Jogos e convocações (S5), na quadra.
// - Painel de confirmações em tempo real (BL-M07-09, CA-12), no padrão do DP: o servidor avisa pelo SignalR que as
//   convocações de um jogo mudaram, e a tela busca o painel de novo (a autorização fica na página). Rede de segurança:
//   busca de novo a cada minuto com a aba visível, ao voltar para a aba e depois de reconectar.
// - Montagem do fim de semana (BL-M07-08): o contador de escolhidos de cada jogo e o total na pergunta do disparo.
(function () {
  "use strict";

  var painel = document.querySelector("[data-esc-painel-jogo]");
  if (painel) {
    var evento = (painel.getAttribute("data-evento") || "").toLowerCase();
    var url = painel.getAttribute("data-url");
    var espera = null;

    var atualizar = function () {
      clearTimeout(espera);
      espera = setTimeout(function () {
        window.esc.fetch(url)
          .then(function (resposta) { return resposta.ok ? resposta.text() : null; })
          .then(function (html) {
            if (html === null) return;
            painel.innerHTML = html;
            window.esc.reinicializarComponentes();
          })
          .catch(function () { /* sem sinal: a próxima tentativa refaz */ });
      }, 250);
    };

    if (window.signalR) {
      var conexao = new window.signalR.HubConnectionBuilder()
        .withUrl("/hubs/convocacoes")
        .withAutomaticReconnect()
        .build();
      conexao.on("ConvocacoesAlteradas", function (dados) {
        var ids = (dados && dados.eventos) || [];
        for (var i = 0; i < ids.length; i++) {
          if (String(ids[i]).toLowerCase() === evento) { atualizar(); return; }
        }
      });
      conexao.onreconnected(atualizar);
      conexao.start().catch(function () { /* sem tempo real: a busca periódica cobre */ });
    }

    setInterval(function () { if (document.visibilityState === "visible") atualizar(); }, 60000);
    document.addEventListener("visibilitychange", function () { if (document.visibilityState === "visible") atualizar(); });
  }

  var montagem = document.querySelector("[data-esc-convocacao]");
  if (montagem) {
    var contar = function () {
      var total = 0;
      montagem.querySelectorAll("[data-esc-jogo]").forEach(function (jogo) {
        var escolhidos = jogo.querySelectorAll("input[data-esc-escolha-aluno]:checked:not(:disabled)").length;
        var alvo = jogo.querySelector("[data-esc-escolhidos]");
        if (alvo) alvo.textContent = String(escolhidos);
        total += escolhidos;
      });
      var disparar = document.querySelector("[data-esc-disparar]");
      if (disparar) {
        disparar.setAttribute("data-esc-confirmar", total === 0
          ? "Nenhuma convocação escolhida para disparar. Salvar a escolha mesmo assim?"
          : "Disparar " + total + " convocação(ões)? As famílias recebem pelo WhatsApp.");
      }
    };
    montagem.addEventListener("change", contar);
    contar();
  }

  // "Novo jogo" na barra inferior: abre o formulário da própria tela.
  document.addEventListener("click", function (e) {
    var link = e.target instanceof Element ? e.target.closest("[data-esc-abrir-novo]") : null;
    if (!link) return;
    var novo = document.getElementById("novo-jogo");
    if (!novo) return;
    e.preventDefault();
    novo.open = true;
    novo.scrollIntoView({ behavior: "smooth", block: "start" });
  });

  // Voltou com erro no jogo novo: mostra o formulário aberto.
  var aberto = document.querySelector("#novo-jogo[open]");
  if (aberto) aberto.scrollIntoView({ block: "start" });
})();
