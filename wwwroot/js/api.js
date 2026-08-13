
const API_URL = "/api";

async function apiFetch(endpoint, options = {}) {

    const headers = { ...options.headers };

    if (options.body && !(options.body instanceof FormData)) {
        headers["Content-Type"] ??= "application/json";
    }

    const response = await fetch(API_URL + endpoint, {
        ...options,
        headers,
        credentials: "same-origin"
    });

    if (!response.ok) {

        let mensaje = response.status === 401
            ? "La sesión venció o no está iniciada."
            : "Ocurrió un error.";

        const contenido = await response.text();

        try {
            const data = contenido ? JSON.parse(contenido) : {};

            mensaje =
                data.mensaje ||
                data.error ||
                data.title ||
                mensaje;

        }
        catch {
            mensaje = contenido || mensaje;

        }

        throw new Error(mensaje);

    }

    if (response.status === 204) {
        return null;
    }

    return await response.json();
}
