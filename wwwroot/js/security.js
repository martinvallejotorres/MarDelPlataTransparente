function escaparHtml(valor) {
    return String(valor ?? "")
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;")
        .replaceAll('"', "&quot;")
        .replaceAll("'", "&#039;");
}

function urlLocalSegura(valor) {
    if (!valor) return "";

    try {
        const url = new URL(valor, window.location.origin);
        return url.origin === window.location.origin ? url.pathname : "";
    }
    catch {
        return "";
    }
}
