let actualizandoHome = false;


async function actualizarHome() {

    // Evitar que se superpongan actualizaciones
    if (actualizandoHome) {
        return;
    }


    // No gastar requests si la pestaña está oculta
    if (document.hidden) {
        return;
    }


    actualizandoHome = true;


    try {

        await Promise.all([

            cargarReclamos(),

            cargarPulsoCiudad(),

            cargarDashboard()

        ]);

    }
    catch (error) {

        console.error(
            "Error actualizando la página:",
            error
        );

    }
    finally {

        actualizandoHome = false;

    }

}


// Primera carga
document.addEventListener(
    "DOMContentLoaded",
    actualizarHome
);


// Actualización automática cada 15 segundos
setInterval(
    actualizarHome,
    15000
);