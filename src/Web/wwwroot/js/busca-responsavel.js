// Reaproveitar um responsável já cadastrado (CA-1): busca por nome, CPF ou celular e escolha do cadastro existente.
// <div data-esc-responsavel data-esc-busca-url="/Responsaveis/Buscar">
//   <input type="hidden" data-esc-responsavel-id name="Responsaveis[0].ResponsavelId">
//   <div data-esc-responsavel-busca> <input data-esc-busca-termo> <ul data-esc-busca-resultados> </div>
//   <div data-esc-responsavel-escolhido> <span data-esc-escolhido-nome> <span data-esc-escolhido-detalhe> <button data-esc-responsavel-trocar hidden> </div>
//   <div data-esc-responsavel-novo> (os campos do cadastro novo) </div>
// </div>
// Sem JS, a busca não aparece: o CPF igual reaproveita sozinho no servidor, e celular ou nome igual viram sugestão.
(function () {
  "use strict";

  var CLASSE_OPCAO = "flex w-full flex-col items-start gap-0.5 rounded-md px-3 py-2 text-start hover:bg-accent";

  /** Mostra o cadastro escolhido ou a busca + os campos do novo. */
  function alternar(bloco, temEscolhido) {
    var painelEscolhido = bloco.querySelector("[data-esc-responsavel-escolhido]");
    var painelNovo = bloco.querySelector("[data-esc-responsavel-novo]");
    var painelBusca = bloco.querySelector("[data-esc-responsavel-busca]");
    if (painelEscolhido) painelEscolhido.hidden = !temEscolhido;
    if (painelBusca) painelBusca.hidden = temEscolhido;
    if (painelNovo) {
      painelNovo.hidden = temEscolhido;
      // Campo desabilitado não é validado nem enviado: escolhido o cadastro, os campos do novo não contam.
      painelNovo.querySelectorAll("input, select, textarea").forEach(function (c) { c.disabled = temEscolhido; });
    }
  }

  function aplicarEscolha(bloco, escolhido) {
    var campoId = bloco.querySelector("[data-esc-responsavel-id]");
    var painelEscolhido = bloco.querySelector("[data-esc-responsavel-escolhido]");
    if (campoId) {
      campoId.value = escolhido ? escolhido.id : "";
      campoId.disabled = false;
    }
    if (painelEscolhido && escolhido) {
      painelEscolhido.querySelector("[data-esc-escolhido-nome]").textContent = escolhido.nome;
      painelEscolhido.querySelector("[data-esc-escolhido-detalhe]").textContent = detalhe(escolhido);
    }
    alternar(bloco, !!escolhido);
  }

  function detalhe(r) {
    var partes = [];
    if (r.telefone) partes.push(formatarTelefone(r.telefone));
    if (r.cpf) partes.push("CPF " + r.cpf);
    if (r.alunos && r.alunos.length) partes.push("responsável por " + r.alunos.join(", "));
    return partes.join(" · ");
  }

  function formatarTelefone(e164) {
    var n = e164.indexOf("+55") === 0 ? e164.slice(3) : e164;
    if (n.length === 11) return "(" + n.slice(0, 2) + ") " + n.slice(2, 7) + "-" + n.slice(7);
    if (n.length === 10) return "(" + n.slice(0, 2) + ") " + n.slice(2, 6) + "-" + n.slice(6);
    return e164;
  }

  function desenhar(bloco, lista, resultados) {
    lista.innerHTML = "";
    if (!resultados.length) {
      var vazio = document.createElement("li");
      vazio.className = "px-3 py-2 text-xs text-secondary-foreground";
      vazio.textContent = "Nenhum responsável encontrado: preencha os dados abaixo para cadastrar.";
      lista.appendChild(vazio);
      return;
    }
    resultados.forEach(function (r) {
      var item = document.createElement("li");
      var botao = document.createElement("button");
      botao.type = "button";
      botao.className = CLASSE_OPCAO;
      var nome = document.createElement("span");
      nome.className = "text-sm font-medium text-mono";
      nome.textContent = r.nome;
      var info = document.createElement("span");
      info.className = "text-xs text-secondary-foreground";
      info.textContent = detalhe(r);
      botao.appendChild(nome);
      botao.appendChild(info);
      botao.addEventListener("click", function () {
        lista.innerHTML = "";
        aplicarEscolha(bloco, r);
      });
      item.appendChild(botao);
      lista.appendChild(item);
    });
  }

  function iniciar(bloco) {
    var termo = bloco.querySelector("[data-esc-busca-termo]");
    var lista = bloco.querySelector("[data-esc-busca-resultados]");
    var url = bloco.getAttribute("data-esc-busca-url");
    var painelBusca = bloco.querySelector("[data-esc-responsavel-busca]");
    if (painelBusca) painelBusca.hidden = false;

    // Cadastro já escolhido (voltou do servidor): o texto já vem desenhado; só alterna os painéis.
    var campoId = bloco.querySelector("[data-esc-responsavel-id]");
    alternar(bloco, !!(campoId && campoId.value));

    var espera = null;
    var ultima = "";
    if (termo && lista && url) {
      termo.addEventListener("input", function () {
        var valor = termo.value.trim();
        clearTimeout(espera);
        if (valor.length < 3) {
          lista.innerHTML = "";
          return;
        }
        espera = setTimeout(function () {
          ultima = valor;
          window.esc.fetch(url + "?termo=" + encodeURIComponent(valor))
            .then(function (resposta) { return resposta.ok ? resposta.json() : []; })
            .then(function (resultados) {
              if (valor === ultima) desenhar(bloco, lista, resultados || []);
            })
            .catch(function () { lista.innerHTML = ""; });
        }, 300);
      });
      // Enter na busca não envia o formulário nem avança a etapa do assistente.
      termo.addEventListener("keydown", function (evento) {
        if (evento.key !== "Enter") return;
        evento.preventDefault();
        evento.stopPropagation();
      });
    }

    var trocar = bloco.querySelector("[data-esc-responsavel-trocar]");
    if (trocar) trocar.hidden = false;
    if (trocar) trocar.addEventListener("click", function () {
      aplicarEscolha(bloco, null);
      if (termo) {
        termo.value = "";
        termo.focus();
      }
    });
  }

  function iniciarTodos() {
    document.querySelectorAll("[data-esc-responsavel]").forEach(iniciar);
  }

  if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", iniciarTodos);
  else iniciarTodos();
})();
