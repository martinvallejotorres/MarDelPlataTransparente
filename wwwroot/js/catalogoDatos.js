document.addEventListener("DOMContentLoaded", inicializarSelectorCategoriasDatos);

const categoriasDatosDisponibles = new Set([
    "infraestructura-obras-publicas",
    "administracion-publica",
    "movilidad-transporte",
    "medio-ambiente"
]);

const textosCategoriasDatos = {
    "infraestructura-obras-publicas": {
        titulo: "Explorá las obras públicas",
        descripcion: "Consultá obras relevadas por año, su estado, presupuesto y ubicación."
    },
    "administracion-publica": {
        titulo: "Explorá la Administración Pública",
        descripcion: "Analizá la planta de personal municipal y consultá los límites de las delegaciones."
    },
    "movilidad-transporte": {
        titulo: "Explorá la Movilidad y el Transporte",
        descripcion: "Consultá pasajeros, kilómetros, frecuencias, flota, recorridos y paradas del transporte público."
    },
    "medio-ambiente": {
        titulo: "Explorá el Medio Ambiente",
        descripcion: "Analizá residuos, recuperación de materiales, controles de agua y playas, y activá las capas ambientales oficiales."
    }
};

async function inicializarSelectorCategoriasDatos() {
    const selector = document.getElementById("categoriaDatosSelector");
    if (!selector) return;

    try {
        const response = await fetch("/api/datos-publicos/categorias");
        if (response.ok) {
            const resultado = await response.json();
            const seleccionActual = selector.value;
            selector.replaceChildren(...(resultado.categorias || []).map(categoria => {
                const option = document.createElement("option");
                option.value = categoria.id;
                option.textContent = categoria.nombre +
                    (categoriasDatosDisponibles.has(categoria.id) ? "" : " — próximamente");
                option.disabled = !categoriasDatosDisponibles.has(categoria.id);
                return option;
            }));
            selector.value = seleccionActual;
        }
    }
    catch (error) {
        console.warn("No se pudo actualizar el selector de categorías:", error);
    }

    selector.addEventListener("change", () => mostrarCategoriaDatos(selector.value));
    mostrarCategoriaDatos(selector.value);
}

function mostrarCategoriaDatos(categoria) {
    const paneles = {
        "infraestructura-obras-publicas": document.getElementById("panelCategoriaObras"),
        "administracion-publica": document.getElementById("panelCategoriaAdministracion"),
        "movilidad-transporte": document.getElementById("panelCategoriaMovilidad"),
        "medio-ambiente": document.getElementById("panelCategoriaMedioAmbiente")
    };
    Object.entries(paneles).forEach(([id, panel]) => {
        if (panel) panel.hidden = id !== categoria;
    });

    const textos = textosCategoriasDatos[categoria] || textosCategoriasDatos["infraestructura-obras-publicas"];
    const titulo = document.getElementById("tituloCategoriaDatos");
    const descripcion = document.getElementById("descripcionCategoriaDatos");
    if (titulo) titulo.textContent = textos.titulo;
    if (descripcion) descripcion.textContent = textos.descripcion;

    if (categoria === "administracion-publica" && typeof cargarAdministracionPublica === "function") {
        cargarAdministracionPublica();
    }
    if (categoria === "movilidad-transporte" && typeof cargarMovilidadTransporte === "function") {
        cargarMovilidadTransporte();
    }
    if (categoria === "medio-ambiente" && typeof cargarMedioAmbiente === "function") {
        cargarMedioAmbiente();
    }
}
