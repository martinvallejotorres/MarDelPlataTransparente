let graficoCategorias;
let graficoEstados;
let graficoEvolucion;

async function cargarDashboard() {

    try {

        const datos = await apiFetch("/reclamos/dashboard");

        document.getElementById("kpiReclamos").textContent =
            datos.totalReclamos;

        document.getElementById("kpiApoyos").textContent =
            datos.totalApoyos;

        document.getElementById("kpiBarrios").textContent =
            datos.totalBarrios;

        document.getElementById("kpiHoy").textContent =
            datos.reclamosHoy;

        crearGraficoCategorias(datos.categorias);

        crearGraficoEstados(datos.estados);

        crearRankingBarrios(datos.barrios);

        crearReclamosMasApoyados(datos.masApoyados);

        crearGraficoEvolucion(datos.evolucion30Dias);

    }

    catch (error) {

        console.error(error);

    }

}

function crearGraficoCategorias(datos) {

    if (graficoCategorias) {

        graficoCategorias.destroy();

    }

    graficoCategorias = new Chart(

        document.getElementById("graficoCategorias"),

        {

            type: "bar",

            data: {

                labels: datos.map(x => x.categoria),

                datasets: [{

                    label: "Cantidad",

                    data: datos.map(x => x.cantidad)

                }]

            },

            options: {

                responsive: true,

                plugins: {

                    legend: {

                        display: false

                    }

                }

            }

        }

    );

}


function crearGraficoEstados(datos) {

    if (graficoEstados) {
        graficoEstados.destroy();
    }

    const coloresEstado = {
        "Recibido": "#6c757d",
        "En revisión": "#0dcaf0",
        "En proceso": "#ffc107",
        "Solucionado": "#198754",
        "Rechazado": "#dc3545"
    };

    const colores = datos.map(x =>
        coloresEstado[x.estado] ?? "#6c757d"
    );

    graficoEstados = new Chart(
        document.getElementById("graficoEstados"),
        {
            type: "doughnut",

            data: {
                labels: datos.map(x => x.estado),

                datasets: [{
                    data: datos.map(x => x.cantidad),
                    backgroundColor: colores,
                    borderWidth: 2
                }]
            },

            options: {
                responsive: true,
                cutout: "65%",
                plugins: {
                    legend: {
                        position: "top"
                    }
                }
            }
        }
    );
}


function crearRankingBarrios(barrios) {

    const contenedor =
        document.getElementById("rankingBarrios");

    contenedor.innerHTML = "";

    if (!barrios || barrios.length === 0) {

        contenedor.innerHTML = `
            <p class="text-muted mb-0">
                Todavía no hay datos suficientes.
            </p>
        `;

        return;
    }

    const maximo = Math.max(
        ...barrios.map(x => x.cantidad)
    );

    barrios.forEach((barrio, index) => {

        const porcentaje =
            maximo > 0
                ? (barrio.cantidad / maximo) * 100
                : 0;

        contenedor.innerHTML += `
            <div class="ranking-barrio">

                <div class="d-flex justify-content-between align-items-center mb-2">

                    <div>

                        <span class="ranking-posicion">
                            ${index + 1}
                        </span>

                        <strong>
                            ${barrio.barrio}
                        </strong>

                    </div>

                    <span class="badge text-bg-light">

                        ${barrio.cantidad}
                        ${barrio.cantidad === 1 ? "reclamo" : "reclamos"}

                    </span>

                </div>

                <div class="progress ranking-progress">

                    <div
                        class="progress-bar"
                        role="progressbar"
                        style="width: ${porcentaje}%">
                    </div>

                </div>

            </div>
        `;
    });
}


function crearReclamosMasApoyados(reclamos) {

    const contenedor =
        document.getElementById("reclamosMasApoyados");

    contenedor.innerHTML = "";

    if (!reclamos || reclamos.length === 0) {

        contenedor.innerHTML = `
            <p class="text-muted">
                Todavía no hay reclamos.
            </p>
        `;

        return;
    }

    reclamos.forEach(reclamo => {

        const imagen = reclamo.fotoUrl
            ? `
                <img
                    src="${reclamo.fotoUrl}"
                    class="card-img-top"
                    alt="Imagen del reclamo">
              `
            : "";


        contenedor.innerHTML += `
            <div class="col-lg-4 col-md-6">

                <div class="card h-100 shadow-sm reclamo-destacado"
                     onclick="irAReclamo(${reclamo.id})">

                    ${imagen}

                    <div class="card-body">

                        <span class="badge text-bg-light mb-2">
                            ${reclamo.tipo}
                        </span>

                        <h5 class="fw-bold">
                            ${reclamo.titulo || "Sin título"}
                        </h5>

                        <div class="text-muted mb-3">

                            <i class="fa-solid fa-location-dot me-1"></i>

                            ${reclamo.zona ?? "Sin zona"}

                        </div>

                        <div class="fw-semibold">

                            <i class="fa-solid fa-thumbs-up me-1"></i>

                            ${reclamo.apoyos ?? 0}
                            ${(reclamo.apoyos ?? 0) === 1
                ? "apoyo"
                : "apoyos"}

                        </div>

                    </div>

                </div>

            </div>
        `;

    });

}


function irAReclamo(id) {

    const reclamo = reclamosMapa.find(
        r => r.id === id
    );

    if (!reclamo) {

        console.error(
            "No se encontró el reclamo:",
            id
        );

        return;
    }

    document
        .getElementById("map")
        .scrollIntoView({
            behavior: "smooth",
            block: "center"
        });

    map.flyTo(
        [
            reclamo.latitud,
            reclamo.longitud
        ],
        17,
        {
            animate: true,
            duration: 1.5
        }
    );

    setTimeout(() => {

        abrirDetalle(reclamo);

    }, 1200);
}


function crearGraficoEvolucion(datos) {

    if (graficoEvolucion) {
        graficoEvolucion.destroy();
    }

    const contexto =
        document.getElementById("graficoEvolucion");

    graficoEvolucion = new Chart(contexto, {

        type: "line",

        data: {

            labels: datos.map(x => {

                const fecha = new Date(
                    x.fecha + "T00:00:00"
                );

                return fecha.toLocaleDateString(
                    "es-AR",
                    {
                        day: "2-digit",
                        month: "short"
                    }
                );

            }),

            datasets: [{
                label: "Reclamos",
                data: datos.map(x => x.cantidad),
                tension: 0.35,
                fill: true
            }]

        },

        options: {

            responsive: true,
            maintainAspectRatio: false,

            scales: {

                y: {
                    beginAtZero: true,
                    ticks: {
                        precision: 0
                    }
                }

            },

            plugins: {

                legend: {
                    display: false
                }

            }

        }

    });
}