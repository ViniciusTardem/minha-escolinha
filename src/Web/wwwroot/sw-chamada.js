/* Service worker da chamada (S3, X07): escopo /Chamada, registrado pelo js/chamada.js. Guarda no aparelho as páginas da
   chamada e os arquivos do layout da quadra, para a chamada abrir sem sinal (R10, CA-21).
   - Páginas: primeiro a rede (com prazo curto, porque o sinal da quadra é ruim); sem rede, a cópia guardada; sem cópia,
     a página "sem sinal".
   - Arquivos (CSS, JS, fontes): a cópia guardada, atualizada em segundo plano.
   - Nunca guarda: o token antiforgery, os envios (POST) e nada fora do próprio site.
   - As páginas não trazem motivo de saúde; o logout apaga esta cópia (js/app.js). */
"use strict";

var VERSAO = "esc-chamada-v1";
var SEM_SINAL = "/Chamada/SemSinal";
var FONTES = [
  "/assets/vendors/keenicons/fonts/keenicons-filled.woff?nz57rx",
  "/fonts/inter/inter-latin-wght-normal.woff2",
  "/fonts/inter/inter-latin-ext-wght-normal.woff2"
];
var PRAZO_REDE = 4000;

function guardar(cache, url) {
  return fetch(url, { credentials: "same-origin" })
    .then(function (resposta) {
      if (resposta.ok && !resposta.redirected) return cache.put(url, resposta);
    })
    .catch(function () { /* sem rede agora: fica para a próxima */ });
}

self.addEventListener("install", function (evento) {
  evento.waitUntil(
    caches.open(VERSAO)
      .then(function (cache) { return Promise.all([SEM_SINAL].concat(FONTES).map(function (url) { return guardar(cache, url); })); })
      .then(function () { return self.skipWaiting(); })
  );
});

self.addEventListener("activate", function (evento) {
  evento.waitUntil(
    caches.keys()
      .then(function (chaves) {
        return Promise.all(chaves
          .filter(function (chave) { return chave.indexOf("esc-chamada-") === 0 && chave !== VERSAO; })
          .map(function (chave) { return caches.delete(chave); }));
      })
      .then(function () { return self.clients.claim(); })
  );
});

/* A página da lista pede para guardar as sessões do dia e os arquivos que ela usa. */
self.addEventListener("message", function (evento) {
  var dados = evento.data || {};
  if (!Array.isArray(dados.guardar)) return;
  evento.waitUntil(caches.open(VERSAO).then(function (cache) {
    return Promise.all(dados.guardar
      .filter(function (url) { return typeof url === "string" && url.charAt(0) === "/" && url.indexOf("//") !== 0; })
      .map(function (url) { return guardar(cache, url); }));
  }));
});

function comPrazo(promessa, ms) {
  return new Promise(function (ok, erro) {
    var relogio = setTimeout(function () { erro(new Error("prazo")); }, ms);
    promessa.then(function (r) { clearTimeout(relogio); ok(r); }, function (e) { clearTimeout(relogio); erro(e); });
  });
}

function pagina(requisicao) {
  var url = requisicao.url;
  return caches.open(VERSAO).then(function (cache) {
    return comPrazo(fetch(requisicao), PRAZO_REDE)
      .then(function (resposta) {
        if (resposta.ok && !resposta.redirected) cache.put(url, resposta.clone());
        return resposta;
      })
      .catch(function () {
        return cache.match(url, { ignoreVary: true }).then(function (guardada) {
          return guardada || cache.match(SEM_SINAL).then(function (semSinal) { return semSinal || Response.error(); });
        });
      });
  });
}

function arquivo(requisicao) {
  return caches.open(VERSAO).then(function (cache) {
    return cache.match(requisicao, { ignoreVary: true }).then(function (guardado) {
      var daRede = fetch(requisicao)
        .then(function (resposta) {
          if (resposta.ok) cache.put(requisicao, resposta.clone());
          return resposta;
        })
        .catch(function () { return null; });
      return guardado || daRede.then(function (resposta) { return resposta || Response.error(); });
    });
  });
}

self.addEventListener("fetch", function (evento) {
  var requisicao = evento.request;
  if (requisicao.method !== "GET") return;
  var url = new URL(requisicao.url);
  if (url.origin !== self.location.origin || url.pathname === "/Chamada/Token") return;

  var aceita = requisicao.headers.get("Accept") || "";
  if (requisicao.mode === "navigate" || (url.pathname.indexOf("/Chamada") === 0 && aceita.indexOf("text/html") >= 0)) {
    evento.respondWith(pagina(requisicao));
    return;
  }
  if (/^\/(assets|css|js|fonts|lib)\//.test(url.pathname) || url.pathname === "/favicon.svg") evento.respondWith(arquivo(requisicao));
});
