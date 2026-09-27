/* Página da compra de créditos (Views/Creditos/Compra.cshtml): enquanto a compra não termina, pergunta a situação a
   cada 3 s, por até 2 min, e recarrega quando ela mudar. Depois disso mostra o aviso de que pode fechar a página:
   a confirmação chega sozinha (webhook do Stripe ou varredura). */
(function () {
  "use strict";

  var marcador = document.querySelector("[data-esc-compra-url]");
  if (!marcador || !window.esc || !window.esc.fetch) return;

  var url = marcador.getAttribute("data-esc-compra-url");
  var situacaoInicial = marcador.getAttribute("data-esc-compra-situacao");
  var tentativas = 0;
  var maximo = 40;

  function mostrarAviso() {
    var aviso = document.querySelector("[data-esc-compra-aviso]");
    if (aviso) aviso.hidden = false;
  }

  function consultar() {
    tentativas++;
    window.esc.fetch(url)
      .then(function (resposta) { return resposta.ok ? resposta.json() : null; })
      .then(function (dados) {
        if (dados && dados.situacao !== situacaoInicial) {
          window.location.reload();
          return;
        }
        if (tentativas < maximo) window.setTimeout(consultar, 3000);
        else mostrarAviso();
      })
      .catch(function () {
        if (tentativas < maximo) window.setTimeout(consultar, 5000);
        else mostrarAviso();
      });
  }

  window.setTimeout(consultar, 2000);
})();
