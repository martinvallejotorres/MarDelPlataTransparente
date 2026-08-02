function mostrarToast(titulo, mensaje, tipo = "success") {

    const toast = document.getElementById("appToast");

    document.getElementById("toastTitulo").textContent = titulo;
    document.getElementById("toastMensaje").textContent = mensaje;

    toast.classList.remove(
        "text-bg-success",
        "text-bg-danger",
        "text-bg-warning",
        "text-bg-primary"
    );

    switch (tipo) {

        case "error":

            toast.classList.add("text-bg-danger");
            break;

        case "warning":

            toast.classList.add("text-bg-warning");
            break;

        case "info":

            toast.classList.add("text-bg-primary");
            break;

        default:

            toast.classList.add("text-bg-success");
            break;
    }

    const bsToast = new bootstrap.Toast(toast, {
        delay: 3500
    });

    bsToast.show();

}