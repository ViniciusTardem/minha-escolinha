// Tema claro/escuro do Metronic (KTUI). Carregado no <head> — antes do body pintar — para não piscar.
// Em arquivo (e não inline) por causa da CSP script-src 'self'. Portado do AlternativaDepartamentoPessoal.
(function () {
  var padrao = "light";
  var raiz = document.documentElement;
  var modo = padrao;
  try {
    modo = localStorage.getItem("kt-theme") || raiz.getAttribute("data-kt-theme-mode") || padrao;
  } catch (e) {
    modo = raiz.getAttribute("data-kt-theme-mode") || padrao;
  }
  if (modo === "system") {
    modo = window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
  }
  raiz.classList.add(modo);
})();
