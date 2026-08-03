
const API_URL = "/api";

async function apiFetch(endpoint, options = {}) {

    const token = localStorage.getItem("token");
    const headers = {
        "Content-Type": "application/json",
        ...options.headers
    };

    if (token) {
        headers["Authorization"] = `Bearer ${token}`;
    }
    const response = await fetch(API_URL + endpoint, {
        ...options,
        headers
    });

    if (!response.ok) {

        let mensaje = "Ocurrió un error.";

        try {

            const data = await response.json();

            mensaje =
                data.mensaje ||
                data.title ||
                JSON.stringify(data);

        }
        catch {

            mensaje = await response.text();

        }

        throw new Error(mensaje);

    }

    return await response.json();
}