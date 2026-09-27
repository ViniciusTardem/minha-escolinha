// Linhas repetíveis de formulário (horários da turma, faixas de disponibilidade do professor).
// <div data-esc-linhas data-esc-linhas-prefixo="Horarios">
//   <ul data-esc-linhas-lista> <li data-esc-linha> ... <button data-esc-linhas-remover hidden> </li> </ul>
//   <template data-esc-linhas-modelo> <li data-esc-linha> ... name="Horarios[__i__].Campo" ... </li> </template>
//   <button data-esc-linhas-adicionar hidden>
// </div>
// Sem JS, as linhas que o servidor desenhou (inclusive a linha em branco extra) funcionam; os botões ficam ocultos.
// Cada mudança renumera os índices (Horarios[0], Horarios[1]...) para o model binding receber a lista inteira.
(function () {
  "use strict";

  function escaparRegex(texto) {
    return texto.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
  }

  function renumerar(bloco) {
    var prefixo = bloco.getAttribute("data-esc-linhas-prefixo");
    var p = escaparRegex(prefixo);
    var nome = new RegExp("^" + p + "\\[(\\d+|__i__)\\]");
    var id = new RegExp("^" + p + "_(\\d+|__i__)__");
    var linhas = bloco.querySelectorAll("[data-esc-linhas-lista] > [data-esc-linha]");
    linhas.forEach(function (linha, i) {
      linha.querySelectorAll("[name],[id],label[for]").forEach(function (campo) {
        // O ASP.NET marca campo de data/hora com um oculto "__Invariant" cujo valor é o nome do campo: acompanha a renumeração.
        if (campo.getAttribute("name") === "__Invariant") campo.value = campo.value.replace(nome, prefixo + "[" + i + "]");
        else if (campo.hasAttribute("name")) campo.setAttribute("name", campo.getAttribute("name").replace(nome, prefixo + "[" + i + "]"));
        if (campo.hasAttribute("id")) campo.setAttribute("id", campo.getAttribute("id").replace(id, prefixo + "_" + i + "__"));
        if (campo.hasAttribute("for")) campo.setAttribute("for", campo.getAttribute("for").replace(id, prefixo + "_" + i + "__"));
      });
    });
  }

  function limpar(linha) {
    linha.querySelectorAll("input:not([type=hidden]), select, textarea").forEach(function (campo) {
      if (campo.tagName === "SELECT") campo.selectedIndex = 0;
      else campo.value = "";
      campo.removeAttribute("aria-invalid");
    });
    linha.querySelectorAll("input[type=hidden][data-esc-linhas-id]").forEach(function (campo) { campo.value = ""; });
  }

  document.addEventListener("click", function (evento) {
    var alvo = evento.target instanceof Element ? evento.target : null;
    if (!alvo) return;

    var adicionar = alvo.closest("[data-esc-linhas-adicionar]");
    if (adicionar) {
      var bloco = adicionar.closest("[data-esc-linhas]");
      var modelo = bloco && bloco.querySelector("template[data-esc-linhas-modelo]");
      var lista = bloco && bloco.querySelector("[data-esc-linhas-lista]");
      if (!modelo || !lista) return;
      var nova = modelo.content.firstElementChild.cloneNode(true);
      nova.querySelectorAll("[data-esc-linhas-remover]").forEach(function (b) { b.hidden = false; });
      lista.appendChild(nova);
      renumerar(bloco);
      var primeiro = nova.querySelector("input, select");
      if (primeiro) primeiro.focus();
      return;
    }

    var remover = alvo.closest("[data-esc-linhas-remover]");
    if (remover) {
      var blocoRemover = remover.closest("[data-esc-linhas]");
      var linha = remover.closest("[data-esc-linha]");
      if (!blocoRemover || !linha) return;
      var restantes = blocoRemover.querySelectorAll("[data-esc-linhas-lista] > [data-esc-linha]").length;
      // A última linha fica (em branco): a lista nunca some da tela.
      if (restantes <= 1) limpar(linha);
      else linha.remove();
      renumerar(blocoRemover);
    }
  });

  function iniciar() {
    document.querySelectorAll("[data-esc-linhas]").forEach(function (bloco) {
      bloco.querySelectorAll("[data-esc-linhas-adicionar], [data-esc-linhas-remover]").forEach(function (b) { b.hidden = false; });
      renumerar(bloco);
    });
  }

  if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", iniciar);
  else iniciar();
})();
