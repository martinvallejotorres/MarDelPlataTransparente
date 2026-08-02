document.addEventListener("DOMContentLoaded", () => {

    cargarPerfil();

    document
        .getElementById("btnCerrarSesionPerfil")
        .addEventListener("click", cerrarSesionPerfil);

});


async function cargarPerfil() {

    const token = localStorage.getItem("token");

    // Si no está logueado, no debería poder entrar al perfil
    if (!token) {

        window.location.href = "/";
        return;

    }


    try {

        const usuario = await apiFetch(
            "/usuarios/me"
        );

        const reclamos = await apiFetch(
            "/usuarios/mis-reclamos"
        );


        document
            .getElementById("perfilNombre")
            .textContent =
            usuario.nombre ?? "Usuario";


        document
            .getElementById("perfilNombreDetalle")
            .textContent =
            usuario.nombre ?? "-";


        document
            .getElementById("perfilEmail")
            .textContent =
            usuario.email ?? "-";


        document
            .getElementById("perfilApoyos")
            .textContent =
            usuario.apoyosRealizados ?? 0;


        document
            .getElementById("perfilReclamos")
            .textContent =
            reclamos.length;


        // Mostrar rol
        const rol = usuario.roles?.includes("Administrador")
            ? "Administrador"
            : "Usuario";

        document
            .getElementById("perfilRol")
            .textContent = rol;

    }
    catch (error) {

        console.error(
            "Error cargando perfil:",
            error
        );

        // Si el JWT expiró o ya no es válido
        localStorage.removeItem("token");
        localStorage.removeItem("usuario");

        window.location.href = "/";

    }

}


function cerrarSesionPerfil() {

    localStorage.removeItem("token");
    localStorage.removeItem("usuario");

    window.location.href = "/";

}