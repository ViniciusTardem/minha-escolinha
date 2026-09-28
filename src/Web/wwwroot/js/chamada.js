/* Chamada na quadra (S3, BL-M05-01): cada toque vai para uma fila no aparelho (IndexedDB), com um id próprio e a ordem
   do aparelho, e sincroniza quando há sinal (R10, CA-21). O servidor aplica cada marcação uma vez só: o reenvio é
   reconhecido, e o registro da quadra prevalece. As páginas da chamada ficam guardadas pelo service worker
   (/sw-chamada.js, escopo /Chamada), para abrir sem sinal. docs/design/GUIA-DESIGN-SYSTEM.md §6.29. */
(function () {
  "use strict";

  var BANCO = "esc-chamada";
  var LOJA = "fila";
  var CHAVE_DISPOSITIVO = "esc.chamada.dispositivo";
  var CHAVE_SEQUENCIA = "esc.chamada.sequencia";
  var MAXIMO_POR_ENVIO = 500;
  var INTERVALO_TENTATIVA = 30000;
  var SITUACOES = { Presente: 10, Falta: 20, Justificada: 30 };

  var enviando = false;
  var agendado = null;
  var sessaoAvisada = false;
  var outraPessoaAvisada = false;

  function toast(texto, variante) {
    if (window.esc && typeof window.esc.toast === "function") window.esc.toast(texto, variante);
  }

  // ── O aparelho e a ordem das marcações nele (a ordem decide entre dois toques do mesmo aparelho) ──
  function novoId() {
    if (window.crypto && typeof window.crypto.randomUUID === "function") return window.crypto.randomUUID();
    var b = new Uint8Array(16);
    window.crypto.getRandomValues(b);
    b[6] = (b[6] & 0x0f) | 0x40;
    b[8] = (b[8] & 0x3f) | 0x80;
    var h = Array.prototype.map.call(b, function (x) { return (x + 0x100).toString(16).slice(1); }).join("");
    return h.slice(0, 8) + "-" + h.slice(8, 12) + "-" + h.slice(12, 16) + "-" + h.slice(16, 20) + "-" + h.slice(20);
  }

  function lerLocal(chave) {
    try { return window.localStorage.getItem(chave); } catch (e) { return null; }
  }

  function gravarLocal(chave, valor) {
    try { window.localStorage.setItem(chave, valor); } catch (e) { /* sem armazenamento: segue só na memória */ }
  }

  var dispositivo = lerLocal(CHAVE_DISPOSITIVO);
  if (!dispositivo) {
    dispositivo = novoId();
    gravarLocal(CHAVE_DISPOSITIVO, dispositivo);
  }
  var ultimaSequencia = parseInt(lerLocal(CHAVE_SEQUENCIA) || "0", 10) || 0;

  function proximaSequencia() {
    ultimaSequencia = Math.max(Date.now(), ultimaSequencia + 1);
    gravarLocal(CHAVE_SEQUENCIA, String(ultimaSequencia));
    return ultimaSequencia;
  }

  // ── A fila no IndexedDB (na memória, se o navegador não deixar: some ao recarregar a página) ──
  var bancoAberto = null;
  var memoria = [];

  function abrirBanco() {
    if (bancoAberto) return bancoAberto;
    bancoAberto = new Promise(function (ok, erro) {
      if (!window.indexedDB) { erro(new Error("sem IndexedDB")); return; }
      var pedido = window.indexedDB.open(BANCO, 1);
      pedido.onupgradeneeded = function () {
        if (!pedido.result.objectStoreNames.contains(LOJA)) pedido.result.createObjectStore(LOJA, { keyPath: "id" });
      };
      pedido.onsuccess = function () { ok(pedido.result); };
      pedido.onerror = function () { erro(pedido.error); };
    });
    return bancoAberto;
  }

  function transacao(modo, fazer) {
    return abrirBanco().then(function (db) {
      return new Promise(function (ok, erro) {
        var tx = db.transaction(LOJA, modo);
        var pedido = fazer(tx.objectStore(LOJA));
        tx.oncomplete = function () { ok(pedido ? pedido.result : undefined); };
        tx.onerror = function () { erro(tx.error); };
        tx.onabort = function () { erro(tx.error); };
      });
    });
  }

  function guardar(marcacao) {
    return transacao("readwrite", function (loja) { loja.put(marcacao); }).catch(function () {
      if (memoria.length === 0) toast("Este navegador não guarda a fila: não recarregue a página enquanto estiver sem sinal.", "warning");
      memoria.push(marcacao);
    });
  }

  function todas() {
    return transacao("readonly", function (loja) { return loja.getAll(); })
      .then(function (lista) { return (lista || []).concat(memoria); })
      .catch(function () { return memoria.slice(); });
  }

  function remover(ids) {
    var conjunto = {};
    ids.forEach(function (id) { conjunto[id] = true; });
    memoria = memoria.filter(function (m) { return !conjunto[m.id]; });
    return transacao("readwrite", function (loja) { ids.forEach(function (id) { loja.delete(id); }); }).catch(function () { });
  }

  // ── Selo de conexão: "Sem sinal · 3 pendentes" ──
  function atualizarPendentes(quantidade) {
    var definir = function (n) {
      document.querySelectorAll("[data-esc-conexao]").forEach(function (selo) { selo.setAttribute("data-pendentes", String(n)); });
      if (window.esc && typeof window.esc.atualizarConexao === "function") window.esc.atualizarConexao();
      return n;
    };
    if (typeof quantidade === "number") return Promise.resolve(definir(quantidade));
    return todas().then(function (fila) { return definir(fila.length); });
  }

  // ── Envio: token novo a cada vez (a página guardada pode ter um velho) ──
  function avisarSessao() {
    if (sessaoAvisada) return;
    sessaoAvisada = true;
    toast("Sua sessão expirou. Entre de novo: as marcações continuam guardadas no aparelho e vão depois do login.", "warning");
  }

  function buscarToken() {
    return fetch("/Chamada/Token", {
      credentials: "same-origin",
      cache: "no-store",
      headers: { "Accept": "application/json", "X-Requested-With": "fetch" }
    }).then(function (resposta) {
      if (resposta.status === 401) { avisarSessao(); throw new Error("sessão"); }
      if (!resposta.ok) throw new Error("token");
      return resposta.json();
    }).then(function (dados) { return dados; });
  }

  // P38: no aparelho compartilhado, cada marcação guarda quem marcou e só vai com essa pessoa logada. A marcação sem dono
  // (guardada antes desta versão) vai com quem estiver logado, como antes.
  function daPessoa(marcacao, pessoa) {
    return !marcacao.pessoa || !pessoa || marcacao.pessoa === pessoa;
  }

  function enviar() {
    if (enviando) return Promise.resolve({ ok: false });
    if (!navigator.onLine) return atualizarPendentes().then(function () { return { ok: false, semSinal: true }; });
    enviando = true;

    return todas().then(function (fila) {
      if (fila.length === 0) return { ok: true };
      var lote = [];
      return buscarToken().then(function (dados) {
        var token = dados.token;
        lote = fila.filter(function (m) { return daPessoa(m, dados.pessoa); }).slice(0, MAXIMO_POR_ENVIO);
        var deOutra = fila.length - fila.filter(function (m) { return daPessoa(m, dados.pessoa); }).length;
        if (deOutra > 0 && !outraPessoaAvisada) {
          outraPessoaAvisada = true;
          toast(deOutra + " marcação(ões) de outra pessoa ficam no aparelho e vão quando ela entrar de novo.", "info");
        }
        if (lote.length === 0) return null;
        return fetch("/Chamada/Sincronizar", {
          method: "POST",
          credentials: "same-origin",
          cache: "no-store",
          headers: {
            "Content-Type": "application/json",
            "Accept": "application/json",
            "X-Requested-With": "fetch",
            "RequestVerificationToken": token
          },
          body: JSON.stringify({ marcacoes: lote })
        });
      }).then(function (resposta) {
        if (resposta === null) return { ok: false };
        if (resposta.status === 401) { avisarSessao(); return { ok: false }; }
        if (!resposta.ok) return { ok: false };
        return resposta.json().then(function (dados) {
          var processadas = [];
          var recusadas = [];
          (dados.marcacoes || []).forEach(function (r) {
            processadas.push(r.id);
            if (r.resultado === "recusada") recusadas.push(r);
          });
          return remover(processadas).then(function () {
            if (recusadas.length > 0) toast(recusadas.length + " marcação(ões) recusada(s): " + (recusadas[0].mensagem || ""), "warning");
            if (dados.alertas > 0) toast(dados.alertas + " aviso(s) de faltas na fila para as famílias.", "info");
            return { ok: true };
          });
        });
      });
    }).catch(function () {
      return { ok: false };
    }).then(function (resultado) {
      enviando = false;
      return atualizarPendentes().then(function (restantes) {
        if (restantes > 0 && resultado.ok) agendarEnvio(300);
        resultado.restantes = restantes;
        return resultado;
      });
    });
  }

  function agendarEnvio(ms) {
    if (agendado) clearTimeout(agendado);
    agendado = setTimeout(function () { agendado = null; enviar(); }, ms);
  }

  // ── A tela da sessão ──
  var raiz = document.querySelector("[data-esc-chamada]");
  var sessaoId = raiz ? raiz.getAttribute("data-sessao") : null;
  var podeMarcar = !!raiz && raiz.getAttribute("data-pode-marcar") === "true";

  function linhaDoAluno(alunoId) {
    return raiz ? raiz.querySelector('[data-aluno="' + alunoId + '"]') : null;
  }

  function nomeDaSituacao(valor) {
    for (var nome in SITUACOES) if (SITUACOES[nome] === valor) return nome;
    return "";
  }

  function mostrar(linha, situacao, justificativa, saude) {
    if (!linha) return;
    linha.setAttribute("data-situacao", situacao);
    linha.querySelectorAll("[data-esc-marcar]").forEach(function (botao) {
      botao.setAttribute("aria-pressed", botao.getAttribute("data-esc-marcar") === situacao ? "true" : "false");
    });
    var painel = linha.querySelector("[data-esc-justificativa]");
    if (!painel) return;
    painel.hidden = situacao !== "Justificada";
    var campo = painel.querySelector("[data-esc-motivo]");
    var caixa = painel.querySelector("[data-esc-saude]");
    if (campo && justificativa !== undefined) campo.value = justificativa || "";
    if (caixa && saude !== undefined) caixa.checked = !!saude;
    var botaoGuardar = painel.querySelector("[data-esc-guardar-motivo]");
    if (botaoGuardar) botaoGuardar.disabled = true;
  }

  function contar() {
    var contador = raiz ? raiz.querySelector("[data-esc-chamada-contador]") : null;
    if (contador) contador.textContent = String(raiz.querySelectorAll('[data-aluno]:not([data-situacao=""])').length);
  }

  function marcar(alunoId, situacao, justificativa, saude) {
    var marcacao = {
      id: novoId(),
      pessoa: raiz.getAttribute("data-pessoa") || null,
      sessaoId: sessaoId,
      alunoId: alunoId,
      situacao: SITUACOES[situacao],
      justificativa: justificativa || null,
      saude: !!saude,
      dispositivo: dispositivo,
      sequencia: proximaSequencia(),
      marcadaEm: new Date().toISOString()
    };
    mostrar(linhaDoAluno(alunoId), situacao, justificativa, saude);
    contar();
    return guardar(marcacao).then(function () {
      atualizarPendentes();
      agendarEnvio(700);
    });
  }

  function semMarcacao() {
    return raiz ? raiz.querySelectorAll('[data-aluno][data-situacao=""]') : [];
  }

  if (raiz) {
    // O que ainda está na fila vale na tela (a página guardada mostra o que o servidor tinha antes).
    todas().then(function (fila) {
      fila.filter(function (m) { return m.sessaoId === sessaoId; })
        .sort(function (a, b) { return a.sequencia - b.sequencia; })
        .forEach(function (m) { mostrar(linhaDoAluno(m.alunoId), nomeDaSituacao(m.situacao), m.justificativa, m.saude); });
      contar();
      atualizarPendentes(fila.length);
    });

    raiz.addEventListener("click", function (evento) {
      var botao = evento.target.closest("[data-esc-marcar]");
      if (botao) {
        var linha = botao.closest("[data-aluno]");
        var situacao = botao.getAttribute("data-esc-marcar");
        if (!podeMarcar || botao.disabled || !linha || linha.getAttribute("data-situacao") === situacao) return;
        marcar(linha.getAttribute("data-aluno"), situacao);
        return;
      }

      var guardarMotivo = evento.target.closest("[data-esc-guardar-motivo]");
      if (guardarMotivo && podeMarcar) {
        var linhaMotivo = guardarMotivo.closest("[data-aluno]");
        var texto = linhaMotivo.querySelector("[data-esc-motivo]").value.trim();
        if (!texto) return;
        marcar(linhaMotivo.getAttribute("data-aluno"), "Justificada", texto, linhaMotivo.querySelector("[data-esc-saude]").checked)
          .then(function () { toast("Motivo guardado.", "success"); });
      }
    });

    var liberarGuardar = function (evento) {
      var painel = evento.target.closest("[data-esc-justificativa]");
      if (!painel) return;
      var campo = painel.querySelector("[data-esc-motivo]");
      var botaoGuardar = painel.querySelector("[data-esc-guardar-motivo]");
      if (campo && botaoGuardar) botaoGuardar.disabled = campo.value.trim().length === 0;
    };
    raiz.addEventListener("input", liberarGuardar);
    raiz.addEventListener("change", liberarGuardar);

    var todosPresentes = document.querySelector("[data-esc-todos-presentes]");
    if (todosPresentes) todosPresentes.addEventListener("click", function () {
      if (!podeMarcar) return;
      var linhas = semMarcacao();
      if (linhas.length === 0) { toast("Todos já estão marcados.", "info"); return; }
      var promessas = [];
      linhas.forEach(function (l) { promessas.push(marcar(l.getAttribute("data-aluno"), "Presente")); });
      Promise.all(promessas).then(function () { toast(promessas.length + " aluno(s) marcado(s) presente(s).", "success"); });
    });

    var concluir = document.querySelector("[data-esc-concluir]");
    if (concluir) concluir.addEventListener("click", function () {
      var faltam = semMarcacao().length;
      if (faltam > 0 && !window.confirm(faltam + " aluno(s) sem marcação. Concluir a chamada assim mesmo?")) return;
      concluir.disabled = true;
      enviar().then(function (resultado) {
        if (resultado.restantes > 0) {
          toast("Sem sinal: " + resultado.restantes + " marcação(ões) guardada(s) no aparelho. Elas vão sozinhas quando o sinal voltar.", "warning");
          setTimeout(function () { window.location.href = "/Chamada"; }, 1800);
          return;
        }
        window.location.href = "/Chamada";
      });
    });
  }

  // ── Quando sincronizar: ao abrir, ao voltar o sinal, ao voltar para a página e de tempos em tempos ──
  window.addEventListener("online", function () { enviar(); });
  document.addEventListener("visibilitychange", function () { if (document.visibilityState === "visible") enviar(); });
  setInterval(function () { if (navigator.onLine) todas().then(function (fila) { if (fila.length > 0) enviar(); }); }, INTERVALO_TENTATIVA);
  enviar();

  // ── Service worker: guarda estas páginas e as da lista do dia para abrir sem sinal ──
  if ("serviceWorker" in navigator) {
    navigator.serviceWorker.register("/sw-chamada.js", { scope: "/Chamada" })
      .then(function () { return navigator.serviceWorker.ready; })
      .then(function (registro) {
        if (!registro.active) return;
        var urls = [window.location.pathname];
        document.querySelectorAll('link[rel="stylesheet"][href], link[rel="icon"][href], script[src]').forEach(function (el) {
          urls.push(el.getAttribute("href") || el.getAttribute("src"));
        });
        document.querySelectorAll("[data-esc-chamada-sessao]").forEach(function (a) { urls.push(a.getAttribute("href")); });
        registro.active.postMessage({ guardar: urls });
      })
      .catch(function () { /* sem service worker: a chamada funciona enquanto a página estiver aberta */ });
  }
})();
