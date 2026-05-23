/**
 * Lightbox — full-screen image viewer with prev/next navigation.
 * Opens when clicking images that have a data-full attribute.
 */

(function() {
    'use strict';

    let overlay: HTMLElement | null = null;
    let imgEl: HTMLImageElement | null = null;
    let lqipEl: HTMLImageElement | null = null;
    let imgWrap: HTMLElement | null = null;
    let images: string[] = [];
    let blurs: (string | null)[] = [];
    let currentIdx = 0;
    let savedScrollY = 0;
    let isZoomed = false;

    let pinchScale = 1;
    let pinchTx = 0;
    let pinchTy = 0;
    let lastPinchDist = 0;
    let lastPinchMidX = 0;
    let lastPinchMidY = 0;
    let isPinching = false;
    let wasPinching = false;

    function touchDist(a: Touch, b: Touch): number {
        const dx = a.clientX - b.clientX, dy = a.clientY - b.clientY;
        return Math.sqrt(dx * dx + dy * dy);
    }

    function applyPinchTransform(): void {
        if (!imgWrap) return;
        imgWrap.style.transform = `translate(${pinchTx}px, ${pinchTy}px) scale(${pinchScale})`;
    }

    function resetPinchTransform(): void {
        pinchScale = 1;
        pinchTx = 0;
        pinchTy = 0;
        if (imgWrap) imgWrap.style.transform = '';
    }

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
        if (isZoomed) zoomOut();
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
        if (isZoomed) zoomOut();
        resetPinchTransform();
        currentIdx = idx;
        if (!imgEl) return;

        imgEl.classList.remove('lightbox-loaded');
        imgEl.src = images[idx]!;

        // Apply the blur-data-uri for this slide as both an ambient backdrop
        // (fullscreen blur via ::before) and a sized LQIP overlay rendered at
        // the image's true aspect ratio, sitting in exactly the same area the
        // real image will occupy until it loads.
        const blur = blurs[idx] || null;
        if (lqipEl) {
            if (blur) lqipEl.src = blur;
            else lqipEl.removeAttribute('src');
        }
        if (overlay) {
            if (blur) {
                overlay.style.setProperty('--lightbox-blur', `url("${blur}")`);
                overlay.classList.add('lightbox-has-blur');
            } else {
                overlay.style.removeProperty('--lightbox-blur');
                overlay.classList.remove('lightbox-has-blur');
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

    function zoomIn(): void {
        isZoomed = true;
        overlay?.classList.add('lightbox-zoomed');
    }

    function zoomOut(): void {
        isZoomed = false;
        overlay?.classList.remove('lightbox-zoomed');
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

        const chevronLeft = '<span class="icon icon-chevron-left" style="width:20px;height:20px" aria-hidden="true"></span>';
        const chevronRight = '<span class="icon icon-chevron-right" style="width:20px;height:20px" aria-hidden="true"></span>';
        const closeIcon = '<span class="icon icon-x" style="width:20px;height:20px" aria-hidden="true"></span>';

        overlay.innerHTML =
            '<div class="lightbox-content">' +
                '<div class="lightbox-img-wrap">' +
                    '<img class="lightbox-lqip" alt="" aria-hidden="true" />' +
                    '<img class="lightbox-img" />' +
                '</div>' +
            '</div>' +
            '<button type="button" class="lightbox-close" aria-label="Close">' + closeIcon + '</button>' +
            '<button type="button" class="lightbox-arrow lightbox-prev" aria-label="Previous">' + chevronLeft + '</button>' +
            '<button type="button" class="lightbox-arrow lightbox-next" aria-label="Next">' + chevronRight + '</button>' +
            '<div class="lightbox-counter"></div>';

        imgEl = overlay.querySelector('.lightbox-img') as HTMLImageElement;
        lqipEl = overlay.querySelector('.lightbox-lqip') as HTMLImageElement;
        imgWrap = overlay.querySelector('.lightbox-img-wrap') as HTMLElement;
        imgEl.addEventListener('load', () => imgEl!.classList.add('lightbox-loaded'));
        imgEl.addEventListener('click', (e) => {
            if (wasPinching) return;
            e.stopPropagation();
            isZoomed ? zoomOut() : zoomIn();
        });

        // Close on overlay click (not on image)
        overlay.addEventListener('click', (e) => {
            const t = e.target as HTMLElement;
            if (t === overlay ||
                t.classList.contains('lightbox-content') ||
                t.classList.contains('lightbox-img-wrap')) close();
        });

        overlay.querySelector('.lightbox-close')!.addEventListener('click', close);
        overlay.querySelector('.lightbox-prev')!.addEventListener('click', prev);
        overlay.querySelector('.lightbox-next')!.addEventListener('click', next);

        // Pinch-to-zoom: intercept 2-finger gestures on the image area only,
        // applying a CSS transform to imgWrap so chrome (arrows, close, counter)
        // is unaffected. touch-action: none in SCSS prevents browser-level zoom.
        const content = overlay.querySelector('.lightbox-content') as HTMLElement;

        content.addEventListener('touchstart', (e: TouchEvent) => {
            if (e.touches.length === 2) {
                isPinching = true;
                lastPinchDist = touchDist(e.touches[0]!, e.touches[1]!);
                lastPinchMidX = (e.touches[0]!.clientX + e.touches[1]!.clientX) / 2;
                lastPinchMidY = (e.touches[0]!.clientY + e.touches[1]!.clientY) / 2;
            }
        }, { passive: true });

        content.addEventListener('touchmove', (e: TouchEvent) => {
            if (e.touches.length < 2 || !isPinching) return;
            e.preventDefault();
            const t0 = e.touches[0]!, t1 = e.touches[1]!;
            const newDist = touchDist(t0, t1);
            const ds = newDist / lastPinchDist;
            pinchScale = Math.min(Math.max(pinchScale * ds, 1), 6);

            const newMidX = (t0.clientX + t1.clientX) / 2;
            const newMidY = (t0.clientY + t1.clientY) / 2;
            pinchTx += newMidX - lastPinchMidX;
            pinchTy += newMidY - lastPinchMidY;

            lastPinchDist = newDist;
            lastPinchMidX = newMidX;
            lastPinchMidY = newMidY;

            applyPinchTransform();
        }, { passive: false });

        const onPinchEnd = () => {
            if (!isPinching) return;
            isPinching = false;
            wasPinching = true;
            setTimeout(() => { wasPinching = false; }, 300);
            if (pinchScale < 1.05) resetPinchTransform();
        };
        content.addEventListener('touchend', onPinchEnd);
        content.addEventListener('touchcancel', onPinchEnd);

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
