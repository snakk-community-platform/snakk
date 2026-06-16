(function () {
    'use strict';

    const modal = document.getElementById('relogin-modal') as HTMLDialogElement | null;
    if (!modal) return;

    if (modal.hasAttribute('data-auto-open')) {
        modal.showModal();
        const url = new URL(window.location.href);
        url.searchParams.delete('session-expired');
        history.replaceState(null, '', url.toString());
        setTimeout(() => {
            (modal.querySelector('#relogin-password') as HTMLInputElement | null)?.focus();
        }, 50);
    }

    document.addEventListener('snakk:session:expired', () => {
        if (!modal.open) modal.showModal();
        (modal.querySelector('#relogin-password') as HTMLInputElement | null)?.focus();
    });

    const form      = document.getElementById('relogin-form') as HTMLFormElement | null;
    const errorEl   = document.getElementById('relogin-error') as HTMLElement | null;
    const submitBtn = document.getElementById('relogin-submit') as HTMLButtonElement | null;
    const strings = {
        wrongPassword: document.querySelector<HTMLMetaElement>('meta[name="snakk-relogin-wrong-password"]')?.content ?? '',
        generic:       document.querySelector<HTMLMetaElement>('meta[name="snakk-relogin-generic"]')?.content ?? '',
    };

    form?.addEventListener('submit', async (e) => {
        e.preventDefault();
        if (!form) return;

        const data     = new FormData(form);
        const email    = data.get('email') as string;
        const password = data.get('password') as string;

        if (errorEl) errorEl.classList.add('hidden');
        if (submitBtn) submitBtn.disabled = true;

        try {
            const res = await fetch('/bff/auth/login', {
                method:  'POST',
                headers: { 'Content-Type': 'application/json' },
                body:    JSON.stringify({ email, password }),
            });

            if (res.ok) {
                // The hint cookie is HttpOnly and cleared server-side by the relogin handler.
                window.location.href = modal.getAttribute('data-return-path') || '/';
                return;
            }

            if (errorEl) {
                errorEl.textContent = res.status === 401
                    ? (strings.wrongPassword ?? 'Incorrect password.')
                    : (strings.generic       ?? 'Something went wrong.');
                errorEl.classList.remove('hidden');
            }
        } catch {
            if (errorEl) {
                errorEl.textContent = strings.generic ?? 'Something went wrong.';
                errorEl.classList.remove('hidden');
            }
        } finally {
            if (submitBtn) submitBtn.disabled = false;
            (modal.querySelector('#relogin-password') as HTMLInputElement | null)?.focus();
        }
    });
})();
