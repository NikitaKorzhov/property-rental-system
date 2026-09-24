// Generic handler for the "populate a modal from a partial view, re-render the same
// partial with validation errors on failure, close + refresh a target on success" pattern.
// A trigger element needs data-modal-url="<GET action returning a partial>".
// The form that partial renders needs data-refresh-url/data-refresh-target to say what to
// reload after a successful save; a controller action signals success via the
// X-Form-Success response header (see FormSuccess() in the controllers).
(function () {
    const modalEl = document.getElementById('crudModal');
    if (!modalEl) return;

    const modal = new bootstrap.Modal(modalEl);
    const content = document.getElementById('crudModalContent');

    document.body.addEventListener('click', async (e) => {
        const trigger = e.target.closest('[data-modal-url]');
        if (!trigger) return;
        e.preventDefault();

        const response = await fetch(trigger.dataset.modalUrl, {
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        });
        content.innerHTML = await response.text();
        modal.show();
    });

    modalEl.addEventListener('submit', async (e) => {
        const form = e.target.closest('form');
        if (!form) return;
        e.preventDefault();

        const response = await fetch(form.action, {
            method: 'POST',
            body: new FormData(form),
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        });

        if (response.headers.get('X-Form-Success') === 'true') {
            modal.hide();
            const refreshUrl = form.dataset.refreshUrl;
            const refreshTarget = form.dataset.refreshTarget;
            if (refreshUrl && refreshTarget) {
                const listResponse = await fetch(refreshUrl);
                document.querySelector(refreshTarget).innerHTML = await listResponse.text();
            } else {
                location.reload();
            }
        } else {
            // Validation failed (or the model was invalid) — the server re-rendered the
            // same partial with error messages; swap it back into the modal in place.
            content.innerHTML = await response.text();
        }
    });
})();
