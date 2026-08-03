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

    const email =
        document.getElementById("loginEmail").value;

    const password =
        document.getElementById("loginPassword").value;

    try {

        const respuesta =
            await apiFetch("/auth/login", {

                method: "POST",

                body: JSON.stringify({
                    email,
                    password
                })

            });


        guardarSesion(respuesta);

        await obtenerPerfil();


        const modal =
            bootstrap.Modal.getInstance(
                document.getElementById("modalLogin")
            );

        if (modal) {
            modal.hide();
        }


        actualizarNavbar();


        // ==========================
        // Volver al reclamo pendiente
        // ==========================

        const reclamoPendienteId =
            sessionStorage.getItem(
                "reclamoPendienteApoyo"
            );


        if (reclamoPendienteId) {

            sessionStorage.removeItem(
                "reclamoPendienteApoyo"
            );


            const id =
                Number(reclamoPendienteId);


            setTimeout(() => {

                irAReclamo(id);

            }, 300);

        }

    }
    catch (error) {

        console.error(
            "ERROR LOGIN:",
            error
        );

        mostrarToast(
            "Error al intentar ingresar. Verifica tus credenciales."
        );

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
    boton.onclick = null;

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
            <a class="dropdown-item" href="/perfil.html">
                <i class="fa-solid fa-user me-2"></i>
                Mi perfil
            </a>
        </li>

        <li>
            <a class="dropdown-item"href="mis-reclamos.html">

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

function mostrarRegistro() {

    document
        .getElementById("vistaLogin")
        .classList.add("d-none");

    document
        .getElementById("vistaRegistro")
        .classList.remove("d-none");

    document
        .getElementById("authTitulo")
        .textContent = "Crear cuenta";
}


function mostrarLogin() {

    document
        .getElementById("vistaRegistro")
        .classList.add("d-none");

    document
        .getElementById("vistaLogin")
        .classList.remove("d-none");

    document
        .getElementById("authTitulo")
        .textContent = "Iniciar sesión";
}


async function registrar() {

    const nombre = document
        .getElementById("registroNombre")
        .value
        .trim();

    const email = document
        .getElementById("registroEmail")
        .value
        .trim();

    const password = document
        .getElementById("registroPassword")
        .value;

    const confirmacion = document
        .getElementById("registroPasswordConfirmacion")
        .value;


    if (nombre.length < 2) {

        mostrarToast(
            "Nombre inválido",
            "Ingresá tu nombre.",
            "warning"
        );

        return;
    }


    if (!email) {

        mostrarToast(
            "Email inválido",
            "Ingresá un email.",
            "warning"
        );

        return;
    }


    if (password.length < 6) {

        mostrarToast(
            "Contraseña inválida",
            "La contraseña debe tener al menos 6 caracteres.",
            "warning"
        );

        return;
    }


    if (password !== confirmacion) {

        mostrarToast(
            "Las contraseñas no coinciden",
            "Volvé a escribirlas.",
            "warning"
        );

        return;
    }


    try {

        await apiFetch("/auth/register", {

            method: "POST",

            body: JSON.stringify({
                nombre,
                email,
                password
            })

        });


        mostrarToast(
            "Cuenta creada",
            "Tu cuenta fue creada correctamente.",
            "success"
        );


        document.getElementById("loginEmail").value =
            email;

        document.getElementById("loginPassword").value =
            password;


        mostrarLogin();


        // Loguear automáticamente
        await login();

    }

    catch (error) {

        console.error(error);

        mostrarToast(
            "No se pudo crear la cuenta",
            error.message,
            "error"
        );

    }

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

    document
        .getElementById("mostrarRegistro")
        .addEventListener("click", mostrarRegistro);

    document
        .getElementById("mostrarLogin")
        .addEventListener("click", mostrarLogin);

    document
        .getElementById("btnRegistrar")
        .addEventListener("click", registrar);


    document
        .getElementById("btnGoogle")
        .addEventListener(
            "click",
            loginGoogle
        );
});


// ===============================
// LOGIN GOOGLE
// ===============================
function loginGoogle() {

    const popup = window.open(
        "/api/auth/google",
        "googleLogin",
        "width=500,height=650"
    );


    if (!popup) {

        mostrarToast(
            "Google",
            "El navegador bloqueó la ventana de inicio de sesión.",
            "warning"
        );

    }

}


window.addEventListener("message", async (event) => {

    if (event.origin !== window.location.origin) {
        return;
    }


    const mensaje = event.data;


    if (
        !mensaje ||
        mensaje.tipo !== "google-auth"
    ) {
        return;
    }


    if (mensaje.error) {

        mostrarToast(
            "No se pudo iniciar sesión",
            mensaje.error,
            "error"
        );

        return;
    }


    if (!mensaje.resultado) {
        return;
    }


    // ==========================
    // Guardar sesión
    // ==========================

    guardarSesion(
        mensaje.resultado
    );


    await obtenerPerfil();


    actualizarNavbar();


    // ==========================
    // Cerrar modal login
    // ==========================

    bootstrap.Modal
        .getInstance(
            document.getElementById("modalLogin")
        )
        ?.hide();


    // ==========================
    // Volver al reclamo pendiente
    // ==========================

    const reclamoPendienteId =
        sessionStorage.getItem(
            "reclamoPendienteApoyo"
        );


    if (reclamoPendienteId) {

        sessionStorage.removeItem(
            "reclamoPendienteApoyo"
        );


        const id =
            Number(reclamoPendienteId);


        setTimeout(() => {

            irAReclamo(id);

        }, 300);

    }


    // ==========================
    // Toast
    // ==========================

    mostrarToast(
        "Sesión iniciada",
        "Ingresaste correctamente con Google.",
        "success"
    );

});