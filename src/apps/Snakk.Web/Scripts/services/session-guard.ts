(function () {
    'use strict';

    const STORAGE_KEY = 'snakk:session:expires';

    function updateExpiry(): void {
        const meta = document.querySelector<HTMLMetaElement>('meta[name="snakk-session-expires"]');
        if (!meta?.content) return;
        const val = parseInt(meta.content, 10);
        if (!isNaN(val)) {
            try { localStorage.setItem(STORAGE_KEY, String(val)); } catch {}
        }
    }

    function checkExpiry(): void {
        let stored: string | null = null;
        try { stored = localStorage.getItem(STORAGE_KEY); } catch {}
        if (!stored) return;
        const expiresAtUtc = parseInt(stored, 10);
        if (isNaN(expiresAtUtc)) return;
        const nowUtc = Math.floor(Date.now() / 1000);
        if (nowUtc >= expiresAtUtc) {
            document.dispatchEvent(new CustomEvent('snakk:session:expired', {
                detail: { reason: 'inactivity' }
            }));
        }
    }

    function scheduleExpiryTimer(): void {
        let stored: string | null = null;
        try { stored = localStorage.getItem(STORAGE_KEY); } catch {}
        if (!stored) return;
        const expiresAtUtc = parseInt(stored, 10);
        if (isNaN(expiresAtUtc)) return;
        const msUntilExpiry = expiresAtUtc * 1000 - Date.now();
        if (msUntilExpiry <= 0) return;
        setTimeout(checkExpiry, msUntilExpiry);
    }

    updateExpiry();
    scheduleExpiryTimer();

    document.addEventListener('visibilitychange', () => {
        if (document.visibilityState === 'visible') checkExpiry();
    });
})();
