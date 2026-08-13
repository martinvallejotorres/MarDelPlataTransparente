(() => {
    try {
        const base64 = document.body.dataset.googleAuth;
        const bytes = Uint8Array.from(atob(base64), caracter => caracter.charCodeAt(0));
        const payload = JSON.parse(new TextDecoder().decode(bytes));

        window.opener?.postMessage(payload, window.location.origin);
    }
    finally {
        window.close();
    }
})();
