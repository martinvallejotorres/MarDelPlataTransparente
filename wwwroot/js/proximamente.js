const botonesPeriodo =
    document.querySelectorAll(
        ".futuro-tabs button"
    );

botonesPeriodo.forEach(boton => {

    boton.addEventListener(
        "click",
        () => {

            botonesPeriodo.forEach(b =>
                b.classList.remove("activo")
            );

            boton.classList.add("activo");


            const periodo =
                boton.dataset.periodo;


            const obra1 =
                document.querySelector(".obra-1");

            const obra2 =
                document.querySelector(".obra-2");

            const obra3 =
                document.querySelector(".obra-3");


            if (obra1) {
                obra1.style.opacity =
                    periodo === "ejecucion"
                        ? "1"
                        : ".18";
            }


            if (obra2) {
                obra2.style.opacity =
                    periodo === "finalizadas"
                        ? "1"
                        : ".18";
            }


            if (obra3) {
                obra3.style.opacity =
                    periodo === "proximas"
                        ? "1"
                        : ".18";
            }

        }
    );

});


// ==========================
// Selector de información
// ==========================

const selector =
    document.getElementById(
        "futuroSelector"
    );

const dropdown =
    document.getElementById(
        "futuroDropdown"
    );

const selectorTexto =
    document.getElementById(
        "futuroSelectorTexto"
    );

const selectorIcono =
    document.getElementById(
        "futuroSelectorIcono"
    );


if (
    selector &&
    dropdown &&
    selectorTexto &&
    selectorIcono
) {

    selector.addEventListener(
        "click",
        () => {

            dropdown.classList.toggle(
                "abierto"
            );

        }
    );


    dropdown
        .querySelectorAll("button")
        .forEach(opcion => {

            opcion.addEventListener(
                "click",
                () => {

                    const texto =
                        opcion.dataset.opcion;

                    const icono =
                        opcion.dataset.icono;


                    selectorTexto.textContent =
                        texto;


                    selectorIcono.className =
                        `fa-solid ${icono}`;


                    dropdown.classList.remove(
                        "abierto"
                    );

                }
            );

        });


    document.addEventListener(
        "click",
        e => {

            if (
                !selector.contains(e.target) &&
                !dropdown.contains(e.target)
            ) {

                dropdown.classList.remove(
                    "abierto"
                );

            }

        }
    );

}