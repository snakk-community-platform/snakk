/**
 * Lightbox — full-screen image viewer with prev/next navigation.
 * Opens when clicking gallery images that have a data-full attribute.
 */

(function() {
    'use strict';

    let overlay: HTMLElement | null = null;
    let imgEl: HTMLImageElement | null = null;
    let images: string[] = [];
    let currentIdx = 0;

    function open(urls: string[], startIdx: number): void {
        images = urls;
        currentIdx = startIdx;

        if (!overlay) create();
        overlay!.classList.add('lightbox-open');
        document.body.style.overflow = 'hidden';
        show(currentIdx);
    }

    function close(): void {
        overlay?.classList.remove('lightbox-open');
        document.body.style.overflow = '';
    }

    function show(idx: number): void {
        currentIdx = idx;
        if (!imgEl) return;

        imgEl.classList.remove('lightbox-loaded');
        imgEl.src = images[idx]!;

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

    // Expose for use by gallery code
    (window as any).SnakkLightbox = { open, preloadUrl };
})();
