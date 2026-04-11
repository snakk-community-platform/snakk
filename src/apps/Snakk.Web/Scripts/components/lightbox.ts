/**
 * Lightbox — full-screen image viewer with prev/next navigation.
 * Opens when clicking images that have a data-full attribute.
 */

(function() {
    'use strict';

    let overlay: HTMLElement | null = null;
    let contentEl: HTMLElement | null = null;
    let imgEl: HTMLImageElement | null = null;
    let images: string[] = [];
    let blurs: (string | null)[] = [];
    let currentIdx = 0;
    let savedScrollY = 0;

    function open(urls: string[], startIdx: number, blurUrls?: (string | null)[]): void {
        images = urls;
        blurs = blurUrls || [];
        currentIdx = startIdx;

        if (!overlay) create();
        overlay!.classList.add('lightbox-open');

        // Lock scrolling but keep the scrollbar visible. Pinning the body
        // with position: fixed collapses html's scroll height to viewport
        // size; html.lightbox-lock then forces overflow-y: scroll so the
        // scrollbar track keeps rendering. The thumb fills the whole track
        // and can't move — visually present, functionally inert.
        savedScrollY = window.scrollY;
        document.body.style.position = 'fixed';
        document.body.style.top = `-${savedScrollY}px`;
        document.body.style.left = '0';
        document.body.style.right = '0';
        document.documentElement.classList.add('lightbox-lock');

        show(currentIdx);
    }

    function close(): void {
        overlay?.classList.remove('lightbox-open');
        document.documentElement.classList.remove('lightbox-lock');
        document.body.style.position = '';
        document.body.style.top = '';
        document.body.style.left = '';
        document.body.style.right = '';

        // Force a synchronous reflow so the document regains its full height
        // before we scroll. Without this, scrollTo runs against the still-
        // collapsed (position: fixed) layout and the browser clamps the
        // target to 0 — which is why closing the lightbox jumped to top.
        // eslint-disable-next-line @typescript-eslint/no-unused-expressions
        document.body.offsetHeight;

        // 'instant' defeats any global scroll-behavior: smooth so the restore
        // is a hard jump rather than an animation through 0.
        window.scrollTo({ top: savedScrollY, left: 0, behavior: 'instant' as ScrollBehavior });
    }

    function show(idx: number): void {
        currentIdx = idx;
        if (!imgEl) return;

        imgEl.classList.remove('lightbox-loaded');
        imgEl.src = images[idx]!;

        // Apply the blur-data-uri for this slide as an ambient backdrop behind
        // the image. Fills the whole lightbox surface so wide images no longer
        // sit on pure black and the user sees something during load.
        if (contentEl) {
            const blur = blurs[idx] || null;
            if (blur) {
                contentEl.style.setProperty('--lightbox-blur', `url("${blur}")`);
                contentEl.classList.add('lightbox-has-blur');
            } else {
                contentEl.style.removeProperty('--lightbox-blur');
                contentEl.classList.remove('lightbox-has-blur');
            }
        }

        // Update arrow visibility
        const prev = overlay?.querySelector('.lightbox-prev') as HTMLElement | null;
        const next = overlay?.querySelector('.lightbox-next') as HTMLElement | null;
        if (prev) prev.style.display = images.length > 1 ? '' : 'none';
        if (next) next.style.display = images.length > 1 ? '' : 'none';

        // Counter
        const counter = overlay?.querySelector('.lightbox-counter');
        if (counter) counter.textContent = `${idx + 1} / ${images.length}`;

        // Preload adjacent images
        preload(idx - 1);
        preload(idx + 1);
    }

    function preload(idx: number): void {
        if (idx < 0 || idx >= images.length) return;
        const p = new window.Image();
        p.src = images[idx]!;
    }

    function prev(): void {
        show(currentIdx > 0 ? currentIdx - 1 : images.length - 1);
    }

    function next(): void {
        show(currentIdx < images.length - 1 ? currentIdx + 1 : 0);
    }

    function create(): void {
        overlay = document.createElement('div');
        overlay.className = 'lightbox-overlay';

        const chevronLeft = '<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="15 18 9 12 15 6"/></svg>';
        const chevronRight = '<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="9 6 15 12 9 18"/></svg>';
        const closeIcon = '<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg>';

        overlay.innerHTML =
            '<div class="lightbox-content">' +
                '<img class="lightbox-img" />' +
            '</div>' +
            '<button type="button" class="lightbox-close" aria-label="Close">' + closeIcon + '</button>' +
            '<button type="button" class="lightbox-arrow lightbox-prev" aria-label="Previous">' + chevronLeft + '</button>' +
            '<button type="button" class="lightbox-arrow lightbox-next" aria-label="Next">' + chevronRight + '</button>' +
            '<div class="lightbox-counter"></div>';

        contentEl = overlay.querySelector('.lightbox-content') as HTMLElement;
        imgEl = overlay.querySelector('.lightbox-img') as HTMLImageElement;
        imgEl.addEventListener('load', () => imgEl!.classList.add('lightbox-loaded'));

        // Close on overlay click (not on image)
        overlay.addEventListener('click', (e) => {
            if (e.target === overlay || (e.target as HTMLElement).classList.contains('lightbox-content')) close();
        });

        overlay.querySelector('.lightbox-close')!.addEventListener('click', close);
        overlay.querySelector('.lightbox-prev')!.addEventListener('click', prev);
        overlay.querySelector('.lightbox-next')!.addEventListener('click', next);

        document.body.appendChild(overlay);
    }

    // Keyboard navigation
    document.addEventListener('keydown', (e) => {
        if (!overlay?.classList.contains('lightbox-open')) return;
        if (e.key === 'Escape') close();
        else if (e.key === 'ArrowLeft') prev();
        else if (e.key === 'ArrowRight') next();
    });

    // Preload a full image on hover (before the user clicks)
    function preloadUrl(url: string): void {
        if (!url) return;
        const p = new window.Image();
        p.src = url;
    }

    // Expose for use by images code
    (window as any).SnakkLightbox = { open, preloadUrl };
})();
