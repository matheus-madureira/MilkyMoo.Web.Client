// A regra @page não aceita var(): a medida escolhida na tela é escrita numa <style> dedicada.
let sheet;

/**
 * Define o tamanho da folha usado pelo diálogo de impressão do browser.
 * @param {number} widthMm largura da etiqueta, em milímetros.
 * @param {number} heightMm altura da etiqueta, em milímetros.
 */
export function setPageSize(widthMm, heightMm) {
    sheet ??= document.head.appendChild(document.createElement("style"));
    sheet.textContent = `@page { size: ${widthMm}mm ${heightMm}mm; margin: 0; }`;
}

/** Abre o diálogo de impressão do browser. */
export function print() {
    window.print();
}
