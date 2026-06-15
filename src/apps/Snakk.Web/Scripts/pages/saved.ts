(function(): void {
    'use strict';

    if ((window as any)._snakkSavedInit) return;
    (window as any)._snakkSavedInit = true;

    document.addEventListener('click', async function(e: MouseEvent) {
        const btn = (e.target as HTMLElement).closest<HTMLElement>('[data-action="unsave-from-list"]');
        if (!btn) return;

        const type = btn.dataset.unsaveType;
        const id   = btn.dataset.unsaveId;
        if (!type || !id) return;

        const url = type === 'discussion'
            ? `/bff/discussions/${id}/save`
            : `/bff/posts/${id}/save`;

        (btn as HTMLButtonElement).disabled = true;
        try {
            const res = await fetch(url, { method: 'POST', credentials: 'include' });
            if (res.ok) {
                const card = btn.closest('article');
                if (card) card.remove();
            }
        } catch (_) {
            (btn as HTMLButtonElement).disabled = false;
        }
    });
})();
