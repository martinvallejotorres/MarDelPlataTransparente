// ===============================
// SESIÓN
// ===============================

function guardarSesion(data) {

    localStorage.setItem(
        "token",
        data.token
    );

    localStorage.setItem(
        "usuario",
        JSON.stringify(data.usuario)
    );
}


function obtenerUsuario() {

    const usuario = localStorage.getItem("usuario");

    if (!usuario)
        return null;

    return JSON.parse(usuario);
}


function cerrarSesion() {

    localStorage.removeItem("token");
    localStorage.removeItem("usuario");

    location.reload();
}

async function obtenerPerfil() {

    try {

        const perfil = await apiFetch("/usuarios/me");

        localStorage.setItem(
            "perfil",
            JSON.stringify(perfil)
        );

        return perfil;

    }
    catch {

        localStorage.removeItem("perfil");

        return null;
    }
}

function obtenerPerfilGuardado() {

    const perfil = localStorage.getItem("perfil");

    if (!perfil)
        return null;

    return JSON.parse(perfil);
}


// ===============================
// LOGIN
// ===============================

async function login() {

    const email = document.getElementById("loginEmail").value;

    const password = document.getElementById("loginPassword").value;

    try {

        const respuesta = await apiFetch("/auth/login", {

            method: "POST",
           
            body: JSON.stringify({

                email,
                password

            })
        });

       
        guardarSesion(respuesta);
        await obtenerPerfil();



        bootstrap.Modal.getInstance(
            document.getElementById("modalLogin")
        ).hide();


        actualizarNavbar();
    }

    catch (error) {

        console.error("ERROR LOGIN:", error);

        alert(error.message);

    }

}



// ===============================
// NAVBAR
// ===============================

function actualizarNavbar() {

    const boton = document.getElementById("btnLogin");
    const menu = document.getElementById("menuUsuario");

    const usuario = obtenerUsuario();
    const perfil = obtenerPerfilGuardado();

    // =====================
    // Usuario NO logueado
    // =====================

    if (!usuario) {

        boton.classList.remove("btn-primary");
        boton.classList.add("btn-outline-primary");

        boton.innerHTML = `
            <i class="fa-regular fa-user me-2"></i>
            Iniciar sesión
        `;

        boton.removeAttribute("data-bs-toggle");

        boton.onclick = () => {

            new bootstrap.Modal(
                document.getElementById("modalLogin")
            ).show();

        };

        menu.innerHTML = "";

        return;
    }


    // =====================
    // Usuario logueado
    // =====================

    boton.classList.remove("btn-outline-primary");
    boton.classList.add("btn-primary");

    boton.innerHTML = `
        <i class="fa-solid fa-user me-2"></i>
        ${usuario.nombre}
    `;

    boton.setAttribute("data-bs-toggle", "dropdown");

    const esAdmin =
        perfil &&
        perfil.roles &&
        perfil.roles.includes("Administrador");


    menu.innerHTML = `
        <li>
            <a class="dropdown-item" href="#">
                <i class="fa-regular fa-user me-2"></i>
                Mi perfil
            </a>
        </li>

        <li>
            <a class="dropdown-item" href="#">
                <i class="fa-regular fa-folder me-2"></i>
                Mis reclamos
            </a>
        </li>

        ${esAdmin ? `
        <li><hr class="dropdown-divider"></li>

        <li>
            <a class="dropdown-item" href="admin.html">
                <i class="fa-solid fa-shield-halved me-2"></i>
                Panel Administrador
            </a>
        </li>
        ` : ""}

        <li><hr class="dropdown-divider"></li>

        <li>
            <a class="dropdown-item text-danger"
               href="#"
               id="cerrarSesion">

                <i class="fa-solid fa-right-from-bracket me-2"></i>

                Cerrar sesión

            </a>
        </li>
    `;


    document
        .getElementById("cerrarSesion")
        .addEventListener("click", cerrarSesion);

}



// ===============================
// INICIO
// ===============================

document.addEventListener("DOMContentLoaded", async () => {

    if (obtenerUsuario()) {
        await obtenerPerfil();
    }

    actualizarNavbar();

    document
        .getElementById("btnEntrar")
        .addEventListener("click", login);

});


