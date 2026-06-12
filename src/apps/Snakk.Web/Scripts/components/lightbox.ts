/**
 * Lightbox — full-screen image viewer with prev/next navigation.
 * Opens when clicking images that have a data-full attribute.
 *
 * Progressive loading sequence per slide:
 *   1. Blur LQIP shown immediately (existing behaviour).
 *   2. Medium-tier image (_med.webp, ~900px) set as src right away.
 *   3. Full-resolution image loaded in a detached Image(); once complete
 *      the src is swapped to full — guarded by a navigation token so a
 *      stale full-res load that finishes after the user moved on is ignored.
 *   4. Adjacent preloads use the medium variant instead of full.
 *
 * URL derivation: strip ".webp" from the full URL and append "_med.webp".
 * Only applied to Snakk CDN post-image URLs (matching SNAKK_MEDIA_RE).
 * If the medium variant 404s (images ≤900px wide have no _med file) the
 * img onerror handler falls back to the full URL transparently.
 */

(function() {
    'use strict';

    // Inject lightbox CSS as soon as this script loads — the overlay can appear
    // on any page (space list, detail page, creation form) so it must be ready
    // before the first open() call without waiting for user interaction.
    (function injectCSS(): void {
        if (document.getElementById('snakk-lightbox-css')) return;
        const link = document.createElement('link');
        link.id = 'snakk-lightbox-css';
        link.rel = 'stylesheet';
        link.href = '/css/dist/lightbox.css';
        document.head.appendChild(link);
    })();

    // Matches full-resolution Snakk CDN post image URLs (no _thumb/_med suffix).
    // Same pattern used by inline-gallery.ts.
    const SNAKK_MEDIA_RE = /\/media\/posts\/\d{4}\/\d{2}\/\d{2}\/[^/._]+\.webp$/;

    function isSnakkMediaUrl(url: string): boolean {
        return SNAKK_MEDIA_RE.test(url);
    }

    /** Derive the medium-tier URL from a full-resolution URL. */
    function deriveMedUrl(fullUrl: string): string {
        return fullUrl.slice(0, -5) + '_med.webp'; // strip ".webp", append "_med.webp"
    }

    let overlay: HTMLElement | null = null;
    let contentEl: HTMLElement | null = null;
    let imgEl: HTMLImageElement | null = null;
    let lqipEl: HTMLImageElement | null = null;
    let imgWrap: HTMLElement | null = null;
    let images: string[] = [];
    let blurs: (string | null)[] = [];
    let dims: ({ w: number; h: number } | null)[] = [];
    let currentIdx = 0;
    let savedScrollY = 0;
    let isZoomed = false;

    // Navigation token: incremented on every show() call.  A full-res load
    // closure captures the token at its creation time and checks it on
    // completion; if they differ the user has already navigated and the swap
    // is skipped.
    let navToken = 0;

    let pinchScale = 1;
    let pinchTx = 0;
    let pinchTy = 0;
    let lastPinchDist = 0;
    let lastPinchMidX = 0;
    let lastPinchMidY = 0;
    let isPinching = false;
    let wasPinching = false;
    let wasDragging = false;

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

    function open(urls: string[], startIdx: number, blurUrls?: (string | null)[], dimsList?: ({ w: number; h: number } | null)[]): void {
        images = urls;
        blurs = blurUrls || [];
        dims = dimsList || [];
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

        // Bump the token so any in-flight full-res load for the previous slide
        // will see a mismatch and skip the src swap.
        const token = ++navToken;

        imgEl.classList.remove('lightbox-loaded');

        // --- Progressive loading ---
        // Step 1: show the blur LQIP immediately (existing behaviour).
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

        const fullUrl = images[idx]!;

        // Step 2: set src to the medium-tier URL right away (much faster to
        // decode than full-res; bridges the blur→sharp gap on slow connections).
        // Pre-apply the full-res dimensions so the medium renders at the same
        // visual size the full-res will occupy — no layout shift on swap.
        if (isSnakkMediaUrl(fullUrl)) {
            const medUrl = deriveMedUrl(fullUrl);
            const dim = dims[idx] ?? null;
            if (dim) {
                imgEl.style.width = dim.w + 'px';
                imgEl.style.height = dim.h + 'px';
            } else {
                imgEl.style.width = '';
                imgEl.style.height = '';
            }
            imgEl.src = medUrl;

            // If the medium file doesn't exist (images ≤900px wide have no
            // _med variant) the browser fires onerror — fall back to full URL.
            imgEl.onerror = () => {
                imgEl!.onerror = null;
                imgEl!.style.width = '';
                imgEl!.style.height = '';
                if (navToken === token) imgEl!.src = fullUrl;
            };

            // Step 3: start loading the full-res image in the background.
            // When it completes, swap only if the user hasn't navigated away.
            const fullImg = new window.Image();
            fullImg.onload = () => {
                if (navToken !== token) return; // user moved on — discard
                imgEl!.onerror = null;
                imgEl!.style.width = '';
                imgEl!.style.height = '';
                imgEl!.src = fullUrl;
            };
            fullImg.onerror = () => { /* full-res unavailable — medium is fine */ };
            fullImg.src = fullUrl;
        } else {
            // Non-CDN URL (e.g. external image): load directly as before.
            imgEl.style.width = '';
            imgEl.style.height = '';
            imgEl.onerror = null;
            imgEl.src = fullUrl;
        }

        // Update arrow visibility
        const prev = overlay?.querySelector('.lightbox-prev') as HTMLElement | null;
        const next = overlay?.querySelector('.lightbox-next') as HTMLElement | null;
        if (prev) prev.style.display = images.length > 1 ? '' : 'none';
        if (next) next.style.display = images.length > 1 ? '' : 'none';

        // Counter
        const counter = overlay?.querySelector('.lightbox-counter');
        if (counter) counter.textContent = `${idx + 1} / ${images.length}`;

        // Preload adjacent images using their medium variant (not full-res),
        // keeping bandwidth cost low while still giving an instant next-slide feel.
        preload(idx - 1);
        preload(idx + 1);
    }

    /** Preload the medium variant of an adjacent slide (falls back to full for non-CDN). */
    function preload(idx: number): void {
        if (idx < 0 || idx >= images.length) return;
        const fullUrl = images[idx]!;
        const p = new window.Image();
        p.src = isSnakkMediaUrl(fullUrl) ? deriveMedUrl(fullUrl) : fullUrl;
    }

    function zoomIn(): void {
        isZoomed = true;
        overlay?.classList.add('lightbox-zoomed');
        requestAnimationFrame(() => {
            if (!contentEl) return;
            contentEl.scrollLeft = (contentEl.scrollWidth - contentEl.clientWidth) / 2;
            contentEl.scrollTop = (contentEl.scrollHeight - contentEl.clientHeight) / 2;
        });
    }

    function zoomOut(): void {
        isZoomed = false;
        overlay?.classList.remove('lightbox-zoomed');
        resetPinchTransform();
        if (contentEl) { contentEl.scrollLeft = 0; contentEl.scrollTop = 0; }
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
            if (wasPinching || wasDragging) {
                wasDragging = false;
                return;
            }
            e.stopPropagation();
            isZoomed ? zoomOut() : zoomIn();
        });

        // Close on overlay click (not on image)
        overlay.addEventListener('click', (e) => {
            // Synthesized click after a pan — pointer capture retargets it to
            // .lightbox-content, bypassing the image's wasDragging guard.
            if (wasDragging) { wasDragging = false; return; }
            const t = e.target as HTMLElement;
            // While zoomed, capture retargets every click on the image to the
            // content element — a plain click there means zoom out, not close.
            if (isZoomed && (t === contentEl || t.classList.contains('lightbox-img-wrap') || t === imgEl)) {
                zoomOut();
                return;
            }
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
        contentEl = content;

        content.addEventListener('touchstart', (e: TouchEvent) => {
            if (e.touches.length === 2) {
                isPinching = true;
                dragActive = false; // second finger cancels any in-progress drag
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

        // Drag-to-pan when click-zoomed. Works for both mouse and touch
        // (pointer events fire even when touch-action: none is set in CSS).
        let dragActive = false;
        let dragPointerStartX = 0;
        let dragPointerStartY = 0;
        let dragScrollStartX = 0;
        let dragScrollStartY = 0;

        content.addEventListener('pointerdown', (e: PointerEvent) => {
            if (!isZoomed || !e.isPrimary) return;
            dragActive = true;
            wasDragging = false;
            dragPointerStartX = e.clientX;
            dragPointerStartY = e.clientY;
            dragScrollStartX = content.scrollLeft;
            dragScrollStartY = content.scrollTop;
            content.setPointerCapture(e.pointerId);
        }, { passive: true });

        content.addEventListener('pointermove', (e: PointerEvent) => {
            if (!dragActive || !e.isPrimary) return;
            const dx = e.clientX - dragPointerStartX;
            const dy = e.clientY - dragPointerStartY;
            if (Math.abs(dx) > 4 || Math.abs(dy) > 4) wasDragging = true;
            content.scrollLeft = dragScrollStartX - dx;
            content.scrollTop = dragScrollStartY - dy;
        });

        const onDragEnd = () => { dragActive = false; };
        content.addEventListener('pointerup', onDragEnd);
        content.addEventListener('pointercancel', onDragEnd);

        // Native image drag-and-drop kicks in after a few pixels of mouse
        // movement and fires pointercancel, killing the pan. Suppress it.
        content.addEventListener('dragstart', (e) => e.preventDefault());

        document.body.appendChild(overlay);
    }

    // Keyboard navigation
    document.addEventListener('keydown', (e) => {
        if (!overlay?.classList.contains('lightbox-open')) return;
        if (e.key === 'Escape') close();
        else if (e.key === 'ArrowLeft') prev();
        else if (e.key === 'ArrowRight') next();
    });

    // Preload a medium-tier image on hover (before the user clicks).
    // Falls back to full URL for non-CDN images.
    function preloadUrl(url: string): void {
        if (!url) return;
        const p = new window.Image();
        p.src = isSnakkMediaUrl(url) ? deriveMedUrl(url) : url;
    }

    // Expose for use by images code
    (window as any).SnakkLightbox = { open, preloadUrl };
})();
