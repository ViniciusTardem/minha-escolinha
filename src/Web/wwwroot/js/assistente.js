/* Formulário em etapas e select que depende de outro — sem script inline (CSP). Portado do AlternativaDepartamentoPessoal.

   Assistente: <form data-esc-assistente> com <section data-esc-passo data-esc-passo-titulo="..."> por etapa e uma lista
   <ol data-esc-passos></ol> (preenchida aqui). Mostra uma etapa por vez; "Continuar" valida os campos da etapa antes de avançar.
   Sem JS, todas as etapas aparecem (o formulário continua funcionando).
   Botões: data-esc-passo-anterior, data-esc-passo-proximo e o submit final (data-esc-passo-final: só aparece na última).
   Abre na etapa ?passo=N (1-based) ou na primeira com erro ([data-esc-passo-erro] ou campo com aria-invalid="true", que o
   servidor marca quando o erro vem com o campo) e põe o foco no campo com erro.

   Select dependente: <select data-esc-depende="#origem" data-esc-sugestao="Texto"> com <option data-grupo="valor-da-origem">.
   Mostra só as opções do grupo escolhido na origem; se a escolha sumiu, tenta a opção com o texto da sugestão. */
(function () {
  "use strict";

  function iniciarAssistente(form) {
    var passos = Array.prototype.slice.call(form.querySelectorAll("[data-esc-passo]"));
    if (passos.length < 2) return;
    var lista = form.querySelector("[data-esc-passos]");
    var anterior = form.querySelector("[data-esc-passo-anterior]");
    var proximo = form.querySelector("[data-esc-passo-proximo]");
    var final = form.querySelector("[data-esc-passo-final]");
    var atual = 0;

    if (lista) {
      lista.innerHTML = "";
      passos.forEach(function (p, i) {
        var li = document.createElement("li");
        var b = document.createElement("button");
        b.type = "button";
        b.className = "esc-passo";
        b.setAttribute("data-esc-passo-ir", String(i));
        var n = document.createElement("span");
        n.className = "esc-passo-numero";
        n.textContent = String(i + 1);
        var t = document.createElement("span");
        t.className = "esc-passo-titulo";
        t.textContent = p.getAttribute("data-esc-passo-titulo") || "Etapa " + (i + 1);
        b.appendChild(n);
        b.appendChild(t);
        li.appendChild(b);
        lista.appendChild(li);
      });
    }

    function valido(i) {
      var campos = passos[i].querySelectorAll("input, select, textarea");
      for (var k = 0; k < campos.length; k++) {
        if (!campos[k].checkValidity()) { campos[k].reportValidity(); return false; }
      }
      return true;
    }

    function mostrar(i) {
      atual = Math.max(0, Math.min(passos.length - 1, i));
      passos.forEach(function (p, k) { p.hidden = k !== atual; });
      if (lista) {
        lista.querySelectorAll("[data-esc-passo-ir]").forEach(function (b, k) {
          b.classList.toggle("esc-passo-atual", k === atual);
          b.classList.toggle("esc-passo-feito", k < atual);
          if (k === atual) b.setAttribute("aria-current", "step"); else b.removeAttribute("aria-current");
        });
      }
      if (anterior) anterior.hidden = atual === 0;
      if (proximo) proximo.hidden = atual === passos.length - 1;
      if (final) final.hidden = atual !== passos.length - 1;
    }

    form.addEventListener("click", function (e) {
      var alvo = e.target instanceof Element ? e.target : null;
      if (!alvo) return;
      if (alvo.closest("[data-esc-passo-anterior]")) { mostrar(atual - 1); return; }
      if (alvo.closest("[data-esc-passo-proximo]")) { if (valido(atual)) mostrar(atual + 1); return; }
      var ir = alvo.closest("[data-esc-passo-ir]");
      if (ir) {
        var destino = parseInt(ir.getAttribute("data-esc-passo-ir"), 10);
        // Voltar é livre; avançar confere as etapas do caminho.
        for (var k = atual; k < destino; k++) { if (!valido(k)) { mostrar(k); return; } }
        mostrar(destino);
      }
    });

    // Enter num campo avança em vez de enviar antes da hora.
    form.addEventListener("keydown", function (e) {
      if (e.key !== "Enter" || !(e.target instanceof HTMLInputElement) || atual === passos.length - 1) return;
      e.preventDefault();
      if (valido(atual)) mostrar(atual + 1);
    });

    // Envio com campo inválido numa etapa escondida: abre a etapa do problema.
    form.addEventListener("invalid", function (e) {
      var passo = e.target instanceof Element ? e.target.closest("[data-esc-passo]") : null;
      var i = passos.indexOf(passo);
      if (i >= 0 && i !== atual) mostrar(i);
    }, true);

    var pedido = parseInt(new URLSearchParams(window.location.search).get("passo") || "", 10);
    var comErro = passos.findIndex(function (p) { return p.querySelector("[data-esc-passo-erro], [aria-invalid='true']"); });
    mostrar(!isNaN(pedido) ? pedido - 1 : comErro >= 0 ? comErro : 0);
    form.classList.add("esc-assistente-ativo");
    var campoComErro = passos[atual].querySelector("[aria-invalid='true']");
    if (campoComErro) campoComErro.focus();
  }

  function filtrar(select) {
    var origem = document.querySelector(select.getAttribute("data-esc-depende"));
    if (!origem) return;
    var grupo = origem.value;
    var escolhidaSumiu = false;
    Array.prototype.forEach.call(select.options, function (o) {
      var g = o.getAttribute("data-grupo");
      if (g === null) return; // "— nenhum —" e afins valem sempre
      var visivel = g === grupo;
      o.hidden = !visivel;
      o.disabled = !visivel;
      if (!visivel && o.selected) escolhidaSumiu = true;
    });
    if (escolhidaSumiu || !select.value) {
      var sugestao = (select.getAttribute("data-esc-sugestao") || "").trim().toLowerCase();
      var achada = Array.prototype.find.call(select.options, function (o) {
        return !o.disabled && o.getAttribute("data-grupo") !== null && o.textContent.trim().toLowerCase() === sugestao;
      });
      select.value = achada ? achada.value : "";
    }
  }

  document.addEventListener("change", function (e) {
    var origem = e.target;
    if (!(origem instanceof HTMLElement) || !origem.id) return;
    document.querySelectorAll('[data-esc-depende="#' + origem.id + '"]').forEach(filtrar);
  });

  function iniciar() {
    document.querySelectorAll("form[data-esc-assistente]").forEach(iniciarAssistente);
    document.querySelectorAll("select[data-esc-depende]").forEach(filtrar);
  }

  if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", iniciar);
  else iniciar();
})();
