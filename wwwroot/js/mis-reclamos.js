async function cargarMisReclamos() {

    try {

        const reclamos = await apiFetch("/usuarios/mis-reclamos");

        const contenedor =
            document.getElementById("listaReclamos");

        contenedor.innerHTML = "";

        if (reclamos.length === 0) {

            contenedor.innerHTML = `
                <div class="col-12">

                    <div class="alert alert-info">

                        Todavía no creaste ningún reclamo.

                    </div>

                </div>
            `;

            return;

        }

        reclamos.forEach(r => {

            contenedor.innerHTML += `

                <div class="col-md-6">

                    <div class="card shadow-sm h-100">

                        <div class="card-body">

                            <h5>

                                ${r.titulo}

                            </h5>

                            <p class="text-muted">

                                📍 ${r.zona}

                            </p>

                            <p>

                                Estado:

                                <strong>

                                    ${r.estado}

                                </strong>

                            </p>

                            <p>

                                👍 ${r.apoyos} apoyos

                            </p>

                            <small class="text-secondary">

                                ${new Date(r.fecha).toLocaleString()}

                            </small>

                        </div>

                    </div>

                </div>

            `;

        });

    }

    catch (error) {

        console.error(error);

    }

}

document.addEventListener(
    "DOMContentLoaded",
    cargarMisReclamos
);