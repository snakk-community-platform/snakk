(function(): void {
    'use strict';

    if ((window as any)._snakkFollowingInit) return;
    (window as any)._snakkFollowingInit = true;

    document.addEventListener('click', async function(e: MouseEvent) {
        const btn = (e.target as HTMLElement).closest<HTMLElement>('[data-unfollow-type]');
        if (!btn) return;

        const type = btn.dataset.unfollowType;
        const id   = btn.dataset.unfollowId;
        if (!type || !id) return;

        const urlMap: Record<string, string> = {
            space:      `/bff/spaces/${id}/follow`,
            discussion: `/bff/discussions/${id}/follow`,
            user:       `/bff/users/${id}/follow`,
        };

        const url = urlMap[type];
        if (!url) return;

        (btn as HTMLButtonElement).disabled = true;
        try {
            const res = await fetch(url, { method: 'POST', credentials: 'include' });
            if (res.ok) {
                const data = await res.json() as { isFollowing: boolean };
                if (!data.isFollowing) {
                    const card = btn.closest<HTMLElement>('[data-following-item]');
                    card?.remove();
                } else {
                    (btn as HTMLButtonElement).disabled = false;
                }
            } else {
                (btn as HTMLButtonElement).disabled = false;
            }
        } catch (_) {
            (btn as HTMLButtonElement).disabled = false;
        }
    });
})();
