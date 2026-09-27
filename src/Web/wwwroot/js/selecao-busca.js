// Select com busca (o "select2" do sistema, sem jQuery): <select data-esc-selecao-busca> ganha um botão com o valor escolhido
// e um painel com campo de busca e a lista. O <select> original continua sendo a fonte (formulário, filtros, evento "change").
// Sem script inline (CSP). Portado do AlternativaDepartamentoPessoal.
(function () {
  "use strict";

  function normalizar(t) { return (t || "").normalize("NFD").replace(/[̀-ͯ]/g, "").toLowerCase().trim(); }

  var aberto = null; // { select, painel, busca, lista, indice }

  function rotulo(select) {
    var op = select.options[select.selectedIndex];
    return op ? op.textContent : "";
  }

  function montar(select) {
    if (select.getAttribute("data-esc-selecao-pronto")) return;
    select.setAttribute("data-esc-selecao-pronto", "1");
    select.classList.add("esc-selecao-original");
    select.tabIndex = -1;
    select.setAttribute("aria-hidden", "true");

    var caixa = document.createElement("div");
    caixa.className = "esc-selecao";
    select.parentNode.insertBefore(caixa, select);
    caixa.appendChild(select);

    var botao = document.createElement("button");
    botao.type = "button";
    botao.className = "kt-input esc-selecao-botao";
    botao.setAttribute("aria-haspopup", "listbox");
    botao.setAttribute("aria-expanded", "false");
    if (select.id) {
      var label = document.querySelector('label[for="' + select.id + '"]');
      if (label) { label.setAttribute("for", select.id + "_busca"); botao.id = select.id + "_busca"; }
    }
    var texto = document.createElement("span");
    texto.className = "esc-selecao-texto";
    var seta = document.createElement("i");
    seta.className = "ki-filled ki-down text-xs text-muted-foreground";
    botao.appendChild(texto);
    botao.appendChild(seta);
    caixa.appendChild(botao);
    atualizarBotao(select);

    select.addEventListener("change", function () { atualizarBotao(select); });
    new MutationObserver(function () { atualizarBotao(select); }).observe(select, { childList: true });
  }

  function atualizarBotao(select) {
    var caixa = select.closest(".esc-selecao");
    var texto = caixa && caixa.querySelector(".esc-selecao-texto");
    if (texto) {
      texto.textContent = rotulo(select);
      texto.classList.toggle("text-muted-foreground", !select.value);
    }
  }

  function abrir(select) {
    fechar();
    var caixa = select.closest(".esc-selecao");
    var painel = document.createElement("div");
    painel.className = "esc-selecao-painel";
    var rotuloBusca = document.createElement("label");
    rotuloBusca.className = "kt-input kt-input-sm";
    var lupa = document.createElement("i");
    lupa.className = "ki-filled ki-magnifier";
    var busca = document.createElement("input");
    busca.type = "search";
    busca.autocomplete = "off";
    rotuloBusca.appendChild(lupa);
    rotuloBusca.appendChild(busca);
    var lista = document.createElement("ul");
    lista.className = "esc-selecao-lista";
    lista.setAttribute("role", "listbox");
    painel.appendChild(rotuloBusca);
    painel.appendChild(lista);
    caixa.appendChild(painel);
    busca.placeholder = select.getAttribute("data-esc-busca-placeholder") || "Buscar…";
    busca.setAttribute("aria-label", busca.placeholder);
    aberto = { select: select, painel: painel, busca: busca, lista: lista, indice: -1 };
    caixa.querySelector(".esc-selecao-botao").setAttribute("aria-expanded", "true");
    listar("");
    busca.focus();
  }

  function fechar(focarBotao) {
    if (!aberto) return;
    var caixa = aberto.select.closest(".esc-selecao");
    var botao = caixa.querySelector(".esc-selecao-botao");
    botao.setAttribute("aria-expanded", "false");
    aberto.painel.remove();
    aberto = null;
    if (focarBotao) botao.focus();
  }

  function listar(termo) {
    var t = normalizar(termo);
    var lista = aberto.lista;
    lista.innerHTML = "";
    var itens = Array.prototype.filter.call(aberto.select.options, function (op) {
      return !t || normalizar(op.textContent).indexOf(t) !== -1;
    });
    if (!itens.length) {
      var vazio = document.createElement("li");
      vazio.className = "esc-selecao-vazio";
      vazio.textContent = "Nada encontrado";
      lista.appendChild(vazio);
      aberto.indice = -1;
      return;
    }
    itens.forEach(function (op) {
      var li = document.createElement("li");
      li.className = "esc-selecao-opcao";
      li.setAttribute("role", "option");
      li.setAttribute("data-valor", op.value);
      li.setAttribute("aria-selected", op.value === aberto.select.value ? "true" : "false");
      li.textContent = op.textContent;
      lista.appendChild(li);
    });
    var atual = itens.findIndex(function (op) { return op.value === aberto.select.value; });
    marcar(atual >= 0 ? atual : 0);
  }

  function marcar(indice) {
    var opcoes = aberto.lista.querySelectorAll(".esc-selecao-opcao");
    if (!opcoes.length) return;
    aberto.indice = Math.max(0, Math.min(indice, opcoes.length - 1));
    opcoes.forEach(function (li, i) { li.classList.toggle("esc-selecao-ativa", i === aberto.indice); });
    opcoes[aberto.indice].scrollIntoView({ block: "nearest" });
  }

  function escolher(valor) {
    var select = aberto.select;
    fechar(true);
    if (select.value === valor) return;
    select.value = valor;
    select.dispatchEvent(new Event("change", { bubbles: true }));
  }

  document.addEventListener("click", function (e) {
    var alvo = e.target instanceof Element ? e.target : null;
    if (!alvo) return;
    var botao = alvo.closest(".esc-selecao-botao");
    if (botao) {
      var select = botao.closest(".esc-selecao").querySelector("select");
      if (aberto && aberto.select === select) fechar(); else abrir(select);
      return;
    }
    var opcao = alvo.closest(".esc-selecao-opcao");
    if (opcao && aberto) { escolher(opcao.getAttribute("data-valor")); return; }
    if (aberto && !alvo.closest(".esc-selecao-painel")) fechar();
  });

  document.addEventListener("input", function (e) {
    if (aberto && e.target === aberto.busca) listar(aberto.busca.value);
  });

  document.addEventListener("keydown", function (e) {
    if (!aberto) {
      var botao = e.target instanceof Element ? e.target.closest(".esc-selecao-botao") : null;
      if (botao && (e.key === "ArrowDown" || e.key === "Enter" || e.key === " ")) {
        e.preventDefault();
        abrir(botao.closest(".esc-selecao").querySelector("select"));
      }
      return;
    }
    if (e.key === "Escape") { e.preventDefault(); e.stopPropagation(); fechar(true); }
    else if (e.key === "ArrowDown") { e.preventDefault(); marcar(aberto.indice + 1); }
    else if (e.key === "ArrowUp") { e.preventDefault(); marcar(aberto.indice - 1); }
    else if (e.key === "Enter") {
      e.preventDefault();
      var ativa = aberto.lista.querySelectorAll(".esc-selecao-opcao")[aberto.indice];
      if (ativa) escolher(ativa.getAttribute("data-valor"));
    } else if (e.key === "Tab") { fechar(); }
  }, true);

  function aplicar(raiz) {
    Array.prototype.forEach.call((raiz || document).querySelectorAll("select[data-esc-selecao-busca]"), montar);
  }

  function atualizar(raiz) {
    Array.prototype.forEach.call((raiz || document).querySelectorAll("select[data-esc-selecao-pronto]"), atualizarBotao);
  }

  window.escSelecaoBusca = { aplicar: aplicar, atualizar: atualizar };
  if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", function () { aplicar(); });
  else aplicar();
})();
