function mostrarToast(titulo, mensaje, tipo = "info") {

    const toastElement =
        document.getElementById("toastGlobal");

    const toastTitulo =
        document.getElementById("toastTitulo");

    const toastMensaje =
        document.getElementById("toastMensaje");

    if (!toastElement ||
        !toastTitulo ||
        !toastMensaje) {

        console.warn(
            "No se encontró el toast en esta página."
        );

        return;
    }

    toastTitulo.textContent = titulo;
    toastMensaje.textContent = mensaje;

    toastElement.classList.remove(
        "text-bg-success",
        "text-bg-danger",
        "text-bg-warning",
        "text-bg-info"
    );

    switch (tipo) {

        case "success":
            toastElement.classList.add(
                "text-bg-success"
            );
            break;

        case "danger":
        case "error":
            toastElement.classList.add(
                "text-bg-danger"
            );
            break;

        case "warning":
            toastElement.classList.add(
                "text-bg-warning"
            );
            break;

        default:
            toastElement.classList.add(
                "text-bg-info"
            );
            break;
    }

    const toast =
        bootstrap.Toast.getOrCreateInstance(
            toastElement
        );

    toast.show();
}