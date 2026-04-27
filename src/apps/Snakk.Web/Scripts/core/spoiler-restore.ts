/**
 * Synchronously restores image-spoiler reveal state from sessionStorage
 * before the browser paints. Prevents the overlay flash / layout jump on
 * revisit.
 *
 * MUST be loaded as an external, non-deferred script at the end of
 * #main-content so it runs after all markup is parsed but before first
 * paint. Runs again automatically on every HTMX swap because HTMX
 * re-executes inline scripts inside the swap target.
 *
 * Counterpart writers:
 *   - pages/images-carousel.ts       (list preview reveal)
 *   - components/discussion-type-widgets.ts (detail page reveal)
 *
 * Keep this as a standalone file — do NOT inline it, CSP strict-dynamic
 * setups will block inline scripts.
 */
(function() {
    'use strict';

    const KEY_PREFIX = 'snakk:spoiler-revealed:';
    const selectors = [
        '.sn-images-spoiler[data-discussion-id]',       // list preview
        '.images-display.images-spoiler[data-discussion-id]', // detail page
    ].join(',');

    document.querySelectorAll<HTMLElement>(selectors).forEach(el => {
        const id = el.dataset.discussionId;
        if (!id) return;
        if (sessionStorage.getItem(KEY_PREFIX + id) === '1') {
            el.classList.add('revealed');
        }
    });

    const ADULT_KEY_PREFIX = 'snakk:adult-revealed:';
    document.querySelectorAll<HTMLElement>('.sn-link-card-thumb-adult-blur[data-discussion-id]').forEach(el => {
        const id = el.dataset.discussionId;
        if (!id) return;
        if (sessionStorage.getItem(ADULT_KEY_PREFIX + id) === '1') {
            el.classList.add('revealed');
            const img = el.querySelector('.sn-link-card-img-adult-blur');
            if (img) img.classList.add('revealed');
        }
    });
})();
