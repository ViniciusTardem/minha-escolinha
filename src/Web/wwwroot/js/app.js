// Comportamentos globais da Minha Escolinha. Sem script inline (CSP): tudo por atributo data-* e delegação de eventos.
// Base portada do app.js do AlternativaDepartamentoPessoal (prefixo dp → esc), mais as abas genéricas, o controle
// segmentado/chips e o selo de conexão das telas de celular.
(function () {
  "use strict";

  // ── Toasts vindos do TempData (Views/Shared/_Mensagens.cshtml) ──
  function mostrarMensagens() {
    var el = document.querySelector("[data-esc-mensagens]");
    if (!el) return;
    toast(el.getAttribute("data-sucesso"), "success");
    toast(el.getAttribute("data-erro"), "destructive");
  }

  // O KTToast monta a mensagem como HTML (concatena no innerHTML). As mensagens levam nomes digitados na tela
  // (escolinha, modalidade...), então o texto é sempre escapado antes.
  function escaparHtml(texto) {
    return String(texto).replace(/[&<>"']/g, function (c) {
      return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c];
    });
  }

  function toast(mensagem, variante) {
    if (!mensagem) return;
    if (window.KTToast && typeof window.KTToast.show === "function") {
      window.KTToast.show({ message: escaparHtml(mensagem), variant: variante });
    } else {
      window.alert(mensagem);
    }
  }

  // ── Confirmação antes de enviar: <form data-esc-confirmar="Texto da pergunta"> ──
  document.addEventListener("submit", function (evento) {
    var form = evento.target;
    if (!(form instanceof HTMLFormElement)) return;
    var pergunta = form.getAttribute("data-esc-confirmar");
    if (pergunta && !window.confirm(pergunta)) {
      evento.preventDefault();
      return;
    }
    // Evita duplo envio: desabilita os botões de submit quando o envio segue de fato. Roda depois de todos os
    // ouvintes; se algum cancelou o envio (formulário por AJAX, esc.fetch), os botões continuam ativos.
    setTimeout(function () {
      if (evento.defaultPrevented) return;
      form.querySelectorAll("button[type=submit]").forEach(function (b) {
        b.disabled = true;
        b.setAttribute("data-esc-enviando", "");
      });
    }, 0);
  });

  // Voltar pelo histórico devolve a página do cache como estava: reabilita o que o envio desabilitou.
  window.addEventListener("pageshow", function (evento) {
    if (!evento.persisted) return;
    document.querySelectorAll("button[data-esc-enviando]").forEach(function (b) {
      b.disabled = false;
      b.removeAttribute("data-esc-enviando");
    });
  });

  // ── Filtro que vale na hora: <input data-esc-enviar-ao-mudar> envia o formulário ao mudar ──
  document.addEventListener("change", function (evento) {
    var campo = evento.target;
    if (campo instanceof HTMLElement && campo.hasAttribute("data-esc-enviar-ao-mudar") && campo.form) campo.form.requestSubmit();
  });

  // ── Copiar texto: <button data-esc-copiar="#seletor"> ──
  document.addEventListener("click", function (evento) {
    var botao = evento.target instanceof Element ? evento.target.closest("[data-esc-copiar]") : null;
    if (!botao) return;
    var alvo = document.querySelector(botao.getAttribute("data-esc-copiar"));
    if (!alvo || !navigator.clipboard) return;
    navigator.clipboard.writeText(alvo.textContent.trim()).then(function () {
      toast("Copiado.", "success");
    });
  });

  // ── Escolha única por aria-pressed: controle segmentado (P/F da chamada) e chips (filhos no portal) ──
  // <div data-esc-escolha> <button aria-pressed="false" data-valor="x">…</button> … </div>
  document.addEventListener("click", function (evento) {
    var opcao = evento.target instanceof Element ? evento.target.closest("[data-esc-escolha] > [aria-pressed]") : null;
    if (!opcao) return;
    var grupo = opcao.parentElement;
    grupo.querySelectorAll(":scope > [aria-pressed]").forEach(function (o) {
      o.setAttribute("aria-pressed", o === opcao ? "true" : "false");
    });
    grupo.dispatchEvent(new CustomEvent("esc:escolha", { bubbles: true, detail: { valor: opcao.getAttribute("data-valor") } }));
  });

  // ── Abas genéricas na mesma página: <button class="esc-aba" data-esc-aba="#painel" role="tab" aria-selected> ──
  document.addEventListener("click", function (evento) {
    var aba = evento.target instanceof Element ? evento.target.closest("[data-esc-aba]") : null;
    if (!aba) return;
    var lista = aba.closest("[role=tablist]");
    if (!lista) return;
    lista.querySelectorAll("[data-esc-aba]").forEach(function (outra) {
      var ativa = outra === aba;
      outra.setAttribute("aria-selected", ativa ? "true" : "false");
      var painel = document.querySelector(outra.getAttribute("data-esc-aba"));
      if (painel) painel.hidden = !ativa;
    });
  });

  // ── Selo de conexão (telas de celular): <span data-esc-conexao data-pendentes="0"> ──
  function atualizarConexao() {
    document.querySelectorAll("[data-esc-conexao]").forEach(function (selo) {
      var online = navigator.onLine;
      var pendentes = parseInt(selo.getAttribute("data-pendentes") || "0", 10);
      selo.classList.toggle("esc-conexao-offline", !online);
      var texto = selo.querySelector("[data-esc-conexao-texto]");
      var icone = selo.querySelector("i");
      if (icone) icone.className = online ? "ki-filled ki-wifi" : "ki-filled ki-cloud-change";
      if (texto) texto.textContent = online
        ? (pendentes > 0 ? "Enviando " + pendentes : "Online")
        : "Sem sinal" + (pendentes > 0 ? " · " + pendentes + " pendente" + (pendentes > 1 ? "s" : "") : "");
    });
  }
  window.addEventListener("online", atualizarConexao);
  window.addEventListener("offline", atualizarConexao);

  // ── fetch com antiforgery e tratamento de sessão expirada (401) ──
  function tokenAntiforgery() {
    var meta = document.querySelector('meta[name="csrf-token"]');
    return meta ? meta.getAttribute("content") : "";
  }

  window.esc = window.esc || {};
  window.esc.toast = toast;
  window.esc.fetch = function (url, opcoes) {
    opcoes = opcoes || {};
    var cabecalhos = new Headers(opcoes.headers || {});
    cabecalhos.set("X-Requested-With", "fetch");
    if (!cabecalhos.has("Accept")) cabecalhos.set("Accept", "application/json");
    var metodo = (opcoes.method || "GET").toUpperCase();
    if (metodo !== "GET" && metodo !== "HEAD") cabecalhos.set("RequestVerificationToken", tokenAntiforgery());
    opcoes.headers = cabecalhos;
    opcoes.credentials = "same-origin";

    return fetch(url, opcoes).then(function (resposta) {
      if (resposta.status === 401) {
        toast("Sua sessão expirou. Entre novamente.", "warning");
        window.location.href = "/Conta/Login?returnUrl=" + encodeURIComponent(window.location.pathname + window.location.search);
      }
      return resposta;
    });
  };

  // Depois de injetar HTML por AJAX, os data-kt-* novos só funcionam depois de reinicializar o KTUI.
  window.esc.reinicializarComponentes = function () {
    if (window.KTComponents && typeof window.KTComponents.init === "function") window.KTComponents.init();
    if (window.escSelecaoBusca) window.escSelecaoBusca.aplicar();
  };

  // ── Busca rápida (Ctrl+K ou /): módulos do menu ──
  var CLASSE_OPCAO = "flex items-center gap-3 rounded-md px-3 py-2 hover:bg-accent";
  var CLASSE_DESTAQUE = "bg-accent";

  function normalizar(texto) {
    return (texto || "").normalize("NFD").replace(/[̀-ͯ]/g, "").toLowerCase().trim();
  }

  function modalBusca() { return document.getElementById("modal_busca"); }

  function abrirBusca() {
    var modal = modalBusca();
    if (!modal) return;
    var instancia = window.KTModal && window.KTModal.getInstance ? window.KTModal.getInstance(modal) : null;
    if (instancia) instancia.show();
    else modal.classList.add("open");
    prepararBusca();
  }

  function prepararBusca() {
    var campo = document.getElementById("busca_termo");
    if (!campo) return;
    campo.value = "";
    filtrarBusca("");
    setTimeout(function () { campo.focus(); }, 50);
  }

  function filtrarBusca(termo) {
    var lista = document.getElementById("busca_resultados");
    if (!lista) return;
    var alvo = normalizar(termo);

    lista.innerHTML = "";
    var modulos = document.querySelectorAll("#sidebar_menu a[data-esc-busca]");
    var encontrados = 0;
    modulos.forEach(function (link) {
      var titulo = link.getAttribute("data-esc-busca");
      var resumo = link.getAttribute("data-esc-busca-resumo") || "";
      if (alvo && normalizar(titulo + " " + resumo).indexOf(alvo) === -1) return;
      encontrados++;

      var item = document.createElement("li");
      var a = document.createElement("a");
      a.className = CLASSE_OPCAO;
      a.href = link.getAttribute("href");
      a.setAttribute("role", "option");
      a.setAttribute("data-esc-busca-opcao", "");

      var icone = document.createElement("i");
      icone.className = "ki-filled " + (link.getAttribute("data-esc-busca-icone") || "ki-element-11") + " text-lg text-muted-foreground";
      var textos = document.createElement("span");
      textos.className = "flex flex-col min-w-0";
      var t = document.createElement("span");
      t.className = "text-sm font-medium text-mono";
      t.textContent = titulo;
      var r = document.createElement("span");
      r.className = "text-xs text-secondary-foreground truncate";
      r.textContent = resumo;
      textos.appendChild(t);
      textos.appendChild(r);
      a.appendChild(icone);
      a.appendChild(textos);
      item.appendChild(a);
      lista.appendChild(item);
    });

    var vazio = document.getElementById("busca_vazio");
    if (vazio) {
      vazio.classList.toggle("hidden", encontrados > 0);
      vazio.classList.toggle("flex", encontrados === 0);
    }
    destacar(0);
  }

  function opcoesVisiveis() {
    return Array.prototype.filter.call(
      document.querySelectorAll("#modal_busca [data-esc-busca-opcao]"),
      function (el) { return !el.closest("[hidden]"); });
  }

  function destacar(indice) {
    var opcoes = opcoesVisiveis();
    opcoes.forEach(function (el, i) {
      el.classList.toggle(CLASSE_DESTAQUE, i === indice);
      el.setAttribute("aria-selected", i === indice ? "true" : "false");
    });
    if (opcoes[indice]) opcoes[indice].scrollIntoView({ block: "nearest" });
  }

  function indiceDestacado() {
    return opcoesVisiveis().findIndex(function (el) { return el.classList.contains(CLASSE_DESTAQUE); });
  }

  document.addEventListener("input", function (evento) {
    if (evento.target && evento.target.id === "busca_termo") filtrarBusca(evento.target.value);
  });

  document.addEventListener("click", function (evento) {
    var gatilho = evento.target instanceof Element ? evento.target.closest('[data-kt-modal-toggle="#modal_busca"]') : null;
    if (gatilho) prepararBusca();
  });

  document.addEventListener("keydown", function (evento) {
    var alvo = evento.target;
    var digitando = alvo instanceof HTMLElement &&
      (alvo.isContentEditable || /^(INPUT|TEXTAREA|SELECT)$/.test(alvo.tagName));

    if ((evento.ctrlKey || evento.metaKey) && evento.key.toLowerCase() === "k") {
      if (!modalBusca()) return;
      evento.preventDefault();
      abrirBusca();
      return;
    }
    if (evento.key === "/" && !digitando && modalBusca()) {
      evento.preventDefault();
      abrirBusca();
      return;
    }

    if (!(alvo instanceof HTMLElement) || alvo.id !== "busca_termo") return;
    var total = opcoesVisiveis().length;
    if (!total) return;
    var atual = indiceDestacado();
    if (evento.key === "ArrowDown") {
      evento.preventDefault();
      destacar((atual + 1) % total);
    } else if (evento.key === "ArrowUp") {
      evento.preventDefault();
      destacar((atual - 1 + total) % total);
    } else if (evento.key === "Enter") {
      evento.preventDefault();
      var escolhido = opcoesVisiveis()[atual < 0 ? 0 : atual];
      if (escolhido) escolhido.click();
    }
  });

  function iniciar() {
    mostrarMensagens();
    atualizarConexao();
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", iniciar);
  } else {
    iniciar();
  }
})();
