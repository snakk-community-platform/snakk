/**
 * Restores spoiler/adult-content reveal state from sessionStorage after HTMX
 * swaps (boosted navigations via _LayoutPartial and load-more partial inserts).
 *
 * COLD FIRST PAINT is handled by a synchronous inline copy of this logic in
 * _Layout.cshtml (inside #main-content) so revealed spoilers don't flash before
 * the deferred bundle runs — keep the two in sync. htmx:afterSettle fires in the
 * same task as the DOM insertion, before the next paint, so no flash on swaps.
 */
(function () {
    'use strict';

    if ((window as any).SnakkSpoilerRestore) return;
    (window as any).SnakkSpoilerRestore = true;

    function restore(): void {
        try {
            const kp = 'snakk:spoiler-revealed:';
            const sel = '.sn-images-spoiler[data-discussion-id],.images-display.images-spoiler[data-discussion-id]';
            document.querySelectorAll<HTMLElement>(sel).forEach((el) => {
                const id = el.dataset.discussionId;
                if (id && sessionStorage.getItem(kp + id) === '1') el.classList.add('revealed');
            });

            const akp = 'snakk:adult-revealed:';
            document.querySelectorAll<HTMLElement>('.sn-link-card-thumb-adult-blur[data-discussion-id]').forEach((el) => {
                const id = el.dataset.discussionId;
                if (!id) return;
                if (sessionStorage.getItem(akp + id) === '1') {
                    el.classList.add('revealed');
                    const img = el.querySelector('.sn-link-card-img-adult-blur');
                    if (img) img.classList.add('revealed');
                }
            });
        } catch { /* sessionStorage unavailable — nothing to restore */ }
    }

    // Idempotent re-run on full loads (inline script already handled first paint)
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', restore);
    } else {
        restore();
    }

    // afterSwap fires synchronously at DOM insertion (before paint); afterSettle
    // fires ~20ms later (htmx defaultSettleDelay) and catches settle-phase mutations.
    // restore() is idempotent, so running on both is safe and flash-free.
    document.addEventListener('htmx:afterSwap', restore);
    document.addEventListener('htmx:afterSettle', restore);
})();
