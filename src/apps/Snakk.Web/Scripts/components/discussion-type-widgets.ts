/**
 * Discussion Type Widgets — client-side interactivity for type-specific features.
 * Data display (badges, bars, previews) is server-rendered in Razor.
 * This script handles: interactive buttons, Guide TOC, Images layout toggle.
 */

(function() {
    'use strict';

    /**
     * Reads the inline background-image of the given element (or its nearest
     * ancestor that has one) and returns the url(...) contents, or null.
     * Used to feed the blur-data-uri from image wrapper elements into the
     * lightbox as an ambient backdrop.
     */
    // Canonical implementation lives in core/utils.ts as
    // window.SnakkUtils.extractBlurUrl. Keep a local fallback in case
    // utils.ts hasn't loaded yet.
    function extractBlurUrl(el: HTMLElement | null): string | null {
        const shared = (window as any).SnakkUtils?.extractBlurUrl;
        if (typeof shared === 'function') return shared(el);

        // Fallback — identical algorithm to SnakkUtils.extractBlurUrl.
        let cur: HTMLElement | null = el;
        for (let i = 0; i < 5 && cur; i++) {
            const bg = cur.style.backgroundImage;
            if (bg && bg !== 'none') {
                const m = bg.match(/^url\((?:["'])?(.+?)(?:["'])?\)$/);
                if (m) return m[1] || null;
            }
            const cv = cur.style.getPropertyValue('--blur-bg').trim();
            if (cv && cv !== 'none') {
                const m = cv.match(/^url\((?:["'])?(.+?)(?:["'])?\)$/);
                if (m) return m[1] || null;
            }
            cur = cur.parentElement;
        }
        return null;
    }

    function init(): void {
        initImages();

        // Detect discussion type from DOM elements instead of body dataset
        // (body dataset is set by discussion-detail.ts which may load after this script)
        const discussionId = document.body.dataset.discussionId
            || document.querySelector<HTMLElement>('[data-discussion-id]')?.dataset.discussionId
            || '';
        const isAuthenticated = document.body.dataset.isAuthenticated === 'true'
            || document.querySelector('meta[name="current-user-id"]') !== null;

        // These init from body dataset set by discussion-detail.ts — defer until it's available
        const tryInitTypeActions = (): void => {
            const type = document.body.dataset.discussionType || '';
            if (!type || type === 'Standard' || !discussionId) return;
            if (type === 'Question') initQuestionActions(discussionId, isAuthenticated);
            if (type === 'Debate') initDebateActions(discussionId, isAuthenticated);
            if (type === 'Journal') initJournalActions(discussionId, isAuthenticated);
            if (type === 'Guide') initGuideToc();
        };
        if (document.body.dataset.discussionType) {
            tryInitTypeActions();
        } else {
            // Defer — discussion-detail.ts will set body dataset shortly
            requestAnimationFrame(tryInitTypeActions);
        }
        if (document.getElementById('link-preview-container')) initLinkEmbed();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        setTimeout(init, 0);
    }

    // ─── Question: Accept answer button ─────────────────────────

    function initQuestionActions(discussionId: string, isAuthenticated: boolean): void {
        if (!isAuthenticated) return;

        // Check if already solved — if so, no buttons needed
        const solvedBadge = document.querySelector('.type-widget-solved');
        if (solvedBadge) return;

        // Add "Accept answer" buttons to non-first posts (only for OP)
        const currentUserId = document.body.dataset.currentUserId || '';
        const firstPost = document.querySelector('article[data-is-first-post="true"]');
        const opId = firstPost?.getAttribute('data-author-id');

        if (currentUserId !== opId) return; // Only OP can accept

        document.querySelectorAll('article[data-post-id]').forEach(article => {
            const el = article as HTMLElement;
            if (el.dataset.isFirstPost === 'true') return;

            const toolbar = el.querySelector('.post-toolbar-right');
            if (!toolbar) return;

            const btn = document.createElement('button');
            btn.className = 'subtle-btn';
            btn.title = 'Accept as answer';
            btn.textContent = '✅';
            btn.addEventListener('click', async () => {
                const postId = el.dataset.postId;
                if (!postId) return;
                const resp = await fetch(`/bff/discussions/${discussionId}/question/solve?postPublicId=${postId}`, { method: 'POST' });
                if (resp.ok) window.location.reload();
            });
            toolbar.prepend(btn);
        });
    }

    // ─── Debate: Position picker on reply form ──────────────────

    function initDebateActions(discussionId: string, isAuthenticated: boolean): void {
        if (!isAuthenticated) return;

        // Read positions from server-rendered debate legend
        const legendItems = document.querySelectorAll('.debate-legend-item');
        if (legendItems.length === 0) return;

        // We need position IDs — extract from the debate info data attribute or fetch
        // For now, use a lightweight fetch since we need IDs not just labels
        fetch(`/bff/discussions/${discussionId}/debate`)
            .then(r => r.ok ? r.json() : null)
            .then(data => {
                if (!data) return;
                const colors = ['var(--link-primary)', 'var(--text-tertiary)', 'oklch(0.6 0.15 50)'];
                addDebatePositionPicker(data.positions, colors);
            })
            .catch(() => {});
    }

    function addDebatePositionPicker(positions: any[], colors: string[]): void {
        const replyForm = document.getElementById('reply-form');
        if (!replyForm) return;

        // Skip if already added (e.g. HTMX re-init after swap)
        if (replyForm.querySelector('.debate-position-picker')) return;

        const picker = document.createElement('div');
        picker.className = 'debate-position-picker';
        picker.innerHTML =
            '<div class="text-sm font-medium mb-1">⚖ Choose your position</div>' +
            '<p class="text-xs text-base-content/50 mb-3">' +
                'This is a debate. Every reply must declare a position so readers ' +
                'can follow who stands where. Pick the position that best represents ' +
                'your reply — it will be shown as a label on your post. You can change ' +
                'it on a later post if your view shifts.' +
            '</p>';

        // Disable the reply submit button until a position is chosen. A debate
        // reply without a position is not allowed, so there's no point letting
        // the user try to submit and then seeing a server error.
        const submitBtn = replyForm.querySelector('#reply-submit-btn') as HTMLButtonElement | null;
        const updateSubmitState = () => {
            if (!submitBtn) return;
            const hasPosition = !!replyForm.querySelector('input[name="DebatePositionId"]');
            submitBtn.disabled = !hasPosition;
            submitBtn.title = hasPosition ? '' : 'Pick a position before replying';
        };

        const cardContainer = document.createElement('div');
        cardContainer.className = 'debate-position-cards';

        positions.forEach((p: any, i: number) => {
            const color = (colors[i] || colors[0]) as string;

            const card = document.createElement('label');
            card.className = 'debate-position-card';
            card.dataset.positionId = p.id.toString();
            card.style.setProperty('--position-color', color);

            const radio = document.createElement('input');
            radio.type = 'radio';
            radio.name = 'debate-position-visual';
            radio.value = p.id.toString();
            radio.className = 'debate-position-radio';

            const indicator = document.createElement('span');
            indicator.className = 'debate-position-card-indicator';

            const labelText = document.createElement('span');
            labelText.textContent = p.label;

            card.appendChild(radio);
            card.appendChild(indicator);
            card.appendChild(labelText);

            radio.addEventListener('change', () => {
                cardContainer.querySelectorAll<HTMLLabelElement>('.debate-position-card').forEach(c => {
                    c.classList.toggle('debate-position-card-selected', c === card);
                });

                let input = replyForm.querySelector('input[name="DebatePositionId"]') as HTMLInputElement;
                if (!input) {
                    input = document.createElement('input');
                    input.type = 'hidden';
                    input.name = 'DebatePositionId';
                    replyForm.appendChild(input);
                }
                input.value = p.id.toString();
                replyForm.dispatchEvent(new CustomEvent('snakk:debate:position-changed', { bubbles: true }));
            });

            cardContainer.appendChild(card);
        });

        picker.appendChild(cardContainer);

        const messageArea = replyForm.querySelector('.milkdown-message-area');
        if (messageArea) {
            messageArea.appendChild(picker);
        } else {
            // Fallback: above editor if no message area (non-Milkdown paths)
            const editorEl = replyForm.querySelector('#editor-container, .composer-area');
            if (editorEl) {
                editorEl.parentNode?.insertBefore(picker, editorEl);
            } else {
                replyForm.prepend(picker);
            }
        }

        // Lock submit until a position is picked.
        updateSubmitState();
    }

    // ─── Journal: Mark as update button for OP ──────────────────

    function initJournalActions(discussionId: string, isAuthenticated: boolean): void {
        if (!isAuthenticated) return;

        const currentUserId = document.body.dataset.currentUserId || '';

        document.querySelectorAll('article[data-post-id]').forEach(article => {
            const el = article as HTMLElement;
            if (el.dataset.isFirstPost === 'true') return;
            if (el.classList.contains('journal-entry')) return; // Already an entry
            if (el.dataset.authorId !== currentUserId) return; // Not OP

            const toolbar = el.querySelector('.post-toolbar-right');
            if (!toolbar) return;

            const btn = document.createElement('button');
            btn.className = 'subtle-btn';
            btn.title = 'Mark as journal update';
            btn.textContent = '📓';
            btn.addEventListener('click', async () => {
                const postId = el.dataset.postId;
                if (!postId) return;
                const resp = await fetch(`/bff/discussions/${discussionId}/journal/entry?postPublicId=${postId}`, { method: 'POST' });
                if (resp.ok) window.location.reload();
            });
            toolbar.prepend(btn);
        });
    }

    // ─── Images: Carousel interactivity (layout is server-rendered) ───

    function initImages(): void {
        const imagesDisplay = document.querySelector('.images-display');
        if (!imagesDisplay) return; // No server-rendered images

        let initCompareSliderPosition = (): void => {};

        // Spoiler reveal (must be before layout-specific code that may early-return)
        const spoilerContainer = document.querySelector('.images-spoiler');
        const spoilerOverlay = document.getElementById('images-spoiler-overlay');
        if (spoilerContainer && spoilerOverlay) {
            // Check if already revealed this session
            const discussionId = document.body.dataset.discussionId
                || document.querySelector<HTMLElement>('[data-discussion-id]')?.dataset.discussionId
                || '';
            const storageKey = discussionId ? `snakk:spoiler-revealed:${discussionId}` : '';

            function revealSpoiler(): void {
                spoilerContainer!.querySelectorAll<HTMLImageElement>('img[data-deferred-src]').forEach(img => {
                    img.src = img.dataset.deferredSrc!;
                    img.removeAttribute('data-deferred-src');
                });
                spoilerContainer!.classList.add('revealed');
                if (storageKey) sessionStorage.setItem(storageKey, '1');
                requestAnimationFrame(() => initCompareSliderPosition());
            }

            // spoiler-restore.ts may have added .revealed synchronously before
            // paint. In that case just load the deferred images; no class
            // toggle, no flash.
            if (spoilerContainer.classList.contains('revealed')) {
                revealSpoiler();
            } else {
                // Preload images on hover so reveal is instant
                spoilerOverlay.addEventListener('mouseenter', () => {
                    spoilerContainer!.querySelectorAll<HTMLImageElement>('img[data-deferred-src]').forEach(img => {
                        const p = new window.Image();
                        p.src = img.dataset.deferredSrc!;
                    });
                }, { once: true });

                spoilerOverlay.addEventListener('click', () => revealSpoiler());
            }
        }

        // Mark cached images as loaded (backup for onload already fired)
        imagesDisplay.querySelectorAll('img.images-blur-up').forEach(img => {
            if ((img as HTMLImageElement).complete) {
                img.classList.add('images-loaded');
                img.closest('.images-upload-item')?.classList.add('images-item-loaded');
            }
        });

        // Lightbox — event delegation on the images container
        let currentCarouselIdx = 0;

        function getFullUrls(): string[] {
            const allItems = imagesDisplay!.querySelectorAll('.images-upload-item');
            return Array.from(allItems).map(el => {
                const img = el.querySelector('img') as HTMLImageElement | null;
                return img?.dataset.full || img?.src || '';
            });
        }

        function getBlurs(): (string | null)[] {
            const allItems = imagesDisplay!.querySelectorAll('.images-upload-item');
            return Array.from(allItems).map(el => extractBlurUrl(el as HTMLElement));
        }

        imagesDisplay.addEventListener('click', (e) => {
            const item = (e.target as HTMLElement).closest('.images-upload-item');
            if (!item) return;

            const allItems = imagesDisplay.querySelectorAll('.images-upload-item');
            const idx = Array.from(allItems).indexOf(item);
            if (idx < 0) return;

            const fullUrls = getFullUrls();

            if (fullUrls[idx] && (window as any).SnakkLightbox) {
                (window as any).SnakkLightbox.open(fullUrls, idx, getBlurs());
            }
        });

        // Expand button (non-compare layouts) — opens lightbox at current index
        const expandBtn = document.getElementById('images-expand-btn');
        if (expandBtn) {
            expandBtn.addEventListener('click', (e) => {
                e.stopPropagation();
                const fullUrls = getFullUrls();
                if (fullUrls.length > 0 && (window as any).SnakkLightbox) {
                    (window as any).SnakkLightbox.open(fullUrls, currentCarouselIdx, getBlurs());
                }
            });
        }

        // Compare slider interactivity
        const compare = document.getElementById('images-compare') as HTMLElement | null;
        const compareSlider = document.getElementById('images-compare-slider') as HTMLElement | null;
        if (compare && compareSlider) {
            const beforeEl = compare.querySelector('.gup-compare-before') as HTMLElement | null;
            const afterEl = compare.querySelector('.gup-compare-after') as HTMLElement | null;
            if (beforeEl && afterEl) {
                function setPosition(x: number, rect: DOMRect): void {
                    const pct = Math.max(0, Math.min(1, (x - rect.left) / rect.width));
                    const rightPct = (1 - pct) * 100;
                    const leftPct = pct * 100;
                    beforeEl!.style.clipPath = `inset(0 ${rightPct}% 0 0)`;
                    afterEl!.style.clipPath = `inset(0 0 0 ${leftPct}%)`;
                    compareSlider!.style.right = `${rightPct}%`;
                }

                initCompareSliderPosition = (): void => {
                    const afterLabel = afterEl!.querySelector('.gup-compare-label-after') as HTMLElement | null;
                    const cRect = compare!.getBoundingClientRect();
                    if (afterLabel) {
                        const labelRect = afterLabel.getBoundingClientRect();
                        const labelInset = 12; // 0.75rem
                        setPosition(labelRect.left - labelInset, cRect);
                    } else {
                        setPosition(cRect.left + cRect.width * 0.5, cRect);
                    }
                };

                initCompareSliderPosition();

                let dragging = false;
                let dragRect: DOMRect | null = null;

                compareSlider.addEventListener('pointerdown', (e) => {
                    dragging = true;
                    dragRect = compare!.getBoundingClientRect();
                    compareSlider!.setPointerCapture(e.pointerId);
                    e.preventDefault();
                });

                compare.addEventListener('pointerdown', (e) => {
                    if ((e.target as HTMLElement).closest('.gup-compare-handle, .gup-compare-expand')) return;
                    dragging = true;
                    dragRect = compare!.getBoundingClientRect();
                    setPosition(e.clientX, dragRect);
                    e.preventDefault();
                });

                document.addEventListener('pointermove', (e) => {
                    if (!dragging || !dragRect) return;
                    setPosition(e.clientX, dragRect);
                });

                document.addEventListener('pointerup', () => { dragging = false; dragRect = null; });

                // Fullscreen compare
                const expandBtn = document.getElementById('images-compare-expand');
                if (expandBtn) {
                    expandBtn.addEventListener('click', (e) => {
                        e.stopPropagation();
                        openFullscreenCompare(expandBtn, beforeEl, compareSlider);
                    });
                }
            }
        }

        // Carousel interactivity (only if carousel layout)
        const track = document.getElementById('images-carousel-track') as HTMLElement | null;
        const counter = document.getElementById('images-carousel-counter');
        if (!track) return;

        const items = track.querySelectorAll('.images-upload-item');
        if (items.length <= 1) return;

        let carouselIdx = 0;

        function slide(newIdx: number): void {
            carouselIdx = newIdx;
            currentCarouselIdx = newIdx;
            track!.style.transform = `translateX(-${carouselIdx * 100}%)`;

            if (counter) counter.textContent = `${carouselIdx + 1} / ${items.length}`;

            // Lazy-load current + adjacent slides with full-res images
            [carouselIdx - 1, carouselIdx, carouselIdx + 1].forEach(idx => {
                if (idx < 0 || idx >= items.length) return;
                const img = items[idx]?.querySelector('img') as HTMLImageElement | null;
                if (img && img.dataset.full && img.src !== img.dataset.full) {
                    img.src = img.dataset.full;
                }
            });

            const prevEl = document.getElementById('gup-prev') as HTMLElement | null;
            const nextEl = document.getElementById('gup-next') as HTMLElement | null;
            if (prevEl) prevEl.style.visibility = carouselIdx === 0 ? 'hidden' : '';
            if (nextEl) nextEl.style.visibility = carouselIdx === items.length - 1 ? 'hidden' : '';
        }

        // Arrow buttons (stop at boundary)
        document.getElementById('gup-prev')?.addEventListener('click', () => {
            if (carouselIdx > 0) slide(carouselIdx - 1);
        });
        document.getElementById('gup-next')?.addEventListener('click', () => {
            if (carouselIdx < items.length - 1) slide(carouselIdx + 1);
        });

        // Arrow hover preload
        document.getElementById('gup-prev')?.addEventListener('mouseenter', () => {
            const target = carouselIdx - 1;
            const img = items[target]?.querySelector('img') as HTMLImageElement | null;
            if (img?.dataset.full) { const p = new window.Image(); p.src = img.dataset.full; }
        });
        document.getElementById('gup-next')?.addEventListener('mouseenter', () => {
            const target = carouselIdx + 1;
            const img = items[target]?.querySelector('img') as HTMLImageElement | null;
            if (img?.dataset.full) { const p = new window.Image(); p.src = img.dataset.full; }
        });

        const utils = (window as any).SnakkUtils;
        if (utils?.addCarouselSwipe && track) {
            utils.addCarouselSwipe(track, (idx: number) => {
                if (idx >= 0 && idx < items.length) slide(idx);
            }, () => carouselIdx);
        }

        slide(0);

    }



    // ─── Fullscreen compare overlay ────────────────────────────

    function openFullscreenCompare(
        expandBtn: HTMLElement,
        inlineBefore: HTMLElement,
        inlineSlider: HTMLElement
    ): void {
        const beforeUrl = expandBtn.dataset.beforeUrl || '';
        const afterUrl = expandBtn.dataset.afterUrl || '';
        const beforeBlur = expandBtn.dataset.beforeBlur || '';
        const afterBlur = expandBtn.dataset.afterBlur || '';

        // Get current slider position from inline widget
        const inlineRight = inlineSlider.style.right || '50%';

        const closeIcon = '<span class="icon icon-x" style="width:20px;height:20px" aria-hidden="true"></span>';
        const sliderChevronL = '<span class="icon icon-chevron-left" style="width:20px;height:20px" aria-hidden="true"></span>';
        const sliderChevronR = '<span class="icon icon-chevron-right" style="width:20px;height:20px" aria-hidden="true"></span>';

        const overlay = document.createElement('div');
        overlay.className = 'compare-fullscreen-overlay';

        // Parse the current right% to compute matching clip-paths
        const rightPctMatch = inlineRight.match(/([\d.]+)%/);
        const rightPct = rightPctMatch?.[1] ? parseFloat(rightPctMatch[1]) : 50;
        const leftPct = 100 - rightPct;

        const beforeBlurStyle = beforeBlur ? `background-image:url(${beforeBlur});background-size:cover;background-position:center` : '';
        const afterBlurStyle = afterBlur ? `background-image:url(${afterBlur});background-size:cover;background-position:center` : '';

        overlay.innerHTML =
            '<div class="gup-compare" id="compare-fs-widget">' +
                `<div class="gup-compare-after images-upload-item" style="${afterBlurStyle};clip-path:inset(0 0 0 ${leftPct}%)">` +
                    `<img src="${afterUrl}" alt="After" />` +
                    '<span class="gup-compare-label gup-compare-label-after">After</span>' +
                '</div>' +
                `<div class="gup-compare-before images-upload-item" style="${beforeBlurStyle};clip-path:inset(0 ${rightPct}% 0 0)">` +
                    `<img src="${beforeUrl}" alt="Before" />` +
                    '<span class="gup-compare-label gup-compare-label-before">Before</span>' +
                '</div>' +
                `<div class="gup-compare-slider" style="right:${rightPct}%">` +
                    '<div class="gup-compare-line"></div>' +
                    `<div class="gup-compare-handle">${sliderChevronL}${sliderChevronR}</div>` +
                '</div>' +
            '</div>' +
            `<button type="button" class="compare-fs-close" aria-label="Close">${closeIcon}</button>`;

        document.body.appendChild(overlay);
        document.body.style.overflow = 'hidden';

        // Trigger open animation
        requestAnimationFrame(() => overlay.classList.add('compare-fs-open'));

        const fsWidget = overlay.querySelector('#compare-fs-widget') as HTMLElement;
        const fsBefore = fsWidget.querySelector('.gup-compare-before') as HTMLElement;
        const fsAfter = fsWidget.querySelector('.gup-compare-after') as HTMLElement;
        const fsSlider = fsWidget.querySelector('.gup-compare-slider') as HTMLElement;

        function setFsPosition(x: number, rect: DOMRect): void {
            const pct = Math.max(0, Math.min(1, (x - rect.left) / rect.width));
            const rPct = (1 - pct) * 100;
            const lPct = pct * 100;
            fsBefore.style.clipPath = `inset(0 ${rPct}% 0 0)`;
            fsAfter.style.clipPath = `inset(0 0 0 ${lPct}%)`;
            fsSlider.style.right = `${rPct}%`;

            // Sync inline widget
            inlineBefore.style.clipPath = `inset(0 ${rPct}% 0 0)`;
            const inlineAfterEl = inlineBefore.parentElement?.querySelector('.gup-compare-after') as HTMLElement | null;
            if (inlineAfterEl) inlineAfterEl.style.clipPath = `inset(0 0 0 ${lPct}%)`;
            inlineSlider.style.right = `${rPct}%`;
        }

        let fsDragging = false;
        let fsDragRect: DOMRect | null = null;
        fsSlider.addEventListener('pointerdown', (e) => { fsDragging = true; fsDragRect = fsWidget.getBoundingClientRect(); fsSlider.setPointerCapture(e.pointerId); e.preventDefault(); });
        fsWidget.addEventListener('pointerdown', (e) => {
            if ((e.target as HTMLElement).closest('.gup-compare-handle')) return;
            fsDragging = true;
            fsDragRect = fsWidget.getBoundingClientRect();
            setFsPosition(e.clientX, fsDragRect);
            e.preventDefault();
        });
        document.addEventListener('pointermove', (e) => { if (fsDragging && fsDragRect) setFsPosition(e.clientX, fsDragRect); });
        document.addEventListener('pointerup', () => { fsDragging = false; fsDragRect = null; });

        function closeFs(): void {
            overlay.classList.remove('compare-fs-open');
            document.body.style.overflow = '';
            setTimeout(() => overlay.remove(), 200);
        }

        overlay.querySelector('.compare-fs-close')!.addEventListener('click', closeFs);
        overlay.addEventListener('click', (e) => {
            if (e.target === overlay) closeFs();
        });
        function onKeydown(e: KeyboardEvent): void {
            if (e.key === 'Escape' && overlay.classList.contains('compare-fs-open')) {
                closeFs();
                cleanup();
            }
        }
        function onScroll(): void {
            if (overlay.classList.contains('compare-fs-open')) {
                closeFs();
                cleanup();
            }
        }
        function cleanup(): void {
            document.removeEventListener('keydown', onKeydown);
            window.removeEventListener('scroll', onScroll);
        }
        document.addEventListener('keydown', onKeydown);
        window.addEventListener('scroll', onScroll, { passive: true });
    }

    // ─── Guide: TOC from headings ───────────────────────────────

    function initGuideToc(): void {
        const firstPost = document.querySelector('article[data-is-first-post="true"] .prose');
        if (!firstPost) return;

        const headings = firstPost.querySelectorAll('h1, h2, h3');
        if (headings.length < 2) return;

        const container = document.getElementById('guide-toc-container');
        if (!container) return;

        let tocHtml = '<div class="guide-toc"><div class="guide-toc-title">Contents</div><ul>';
        headings.forEach((heading, i) => {
            const id = `guide-heading-${i}`;
            heading.id = id;
            const level = heading.tagName.toLowerCase();
            const indent = level === 'h3' ? ' class="guide-toc-indent"' : '';
            const text = heading.textContent || '';
            tocHtml += `<li${indent}><a href="#${id}">${text.replace(/</g, '&lt;').replace(/>/g, '&gt;')}</a></li>`;
        });
        tocHtml += '</ul></div>';

        container.innerHTML = tocHtml;
    }

    // ─── Link: oEmbed toggle ──────────────────────────────────

    const OEMBED_ALLOWED_DOMAINS = [
        'youtube.com', 'www.youtube.com',
        'vimeo.com', 'player.vimeo.com',
        'open.spotify.com',
        'codepen.io',
        'twitter.com', 'platform.twitter.com', 'x.com',
        'reddit.com', 'www.reddit.com', 'embed.reddit.com',
        'imgur.com', 'i.imgur.com',
        'tiktok.com', 'www.tiktok.com',
        'bsky.app', 'embed.bsky.app',
        'canva.com', 'www.canva.com',
        'soundcloud.com', 'w.soundcloud.com',
        'twitch.tv', 'player.twitch.tv', 'clips.twitch.tv',
        'bandcamp.com'
    ];

    function extractIframeSrc(html: string): string | null {
        const parser = new DOMParser();
        const doc = parser.parseFromString(html, 'text/html');
        const iframe = doc.querySelector('iframe');
        if (!iframe) return null;

        const src = iframe.getAttribute('src');
        if (!src) return null;

        try {
            const url = new URL(src);
            const hostname = url.hostname.toLowerCase();
            if (OEMBED_ALLOWED_DOMAINS.some(d => hostname === d || hostname.endsWith('.' + d))) {
                return src;
            }
        } catch {
            // invalid URL
        }

        return null;
    }

    // Maps link domains to the provider keys stored in localStorage('snakk:embed-providers')
    const DOMAIN_TO_PROVIDER: Record<string, string> = {
        'youtube.com': 'youtube', 'www.youtube.com': 'youtube', 'youtu.be': 'youtube',
        'vimeo.com': 'vimeo', 'player.vimeo.com': 'vimeo',
        'tiktok.com': 'tiktok', 'www.tiktok.com': 'tiktok',
        'twitter.com': 'twitter', 'platform.twitter.com': 'twitter', 'x.com': 'twitter',
        'bsky.app': 'bluesky', 'embed.bsky.app': 'bluesky',
        'reddit.com': 'reddit', 'www.reddit.com': 'reddit', 'embed.reddit.com': 'reddit', 'old.reddit.com': 'reddit',
        'open.spotify.com': 'spotify',
        'soundcloud.com': 'soundcloud', 'w.soundcloud.com': 'soundcloud',
        'bandcamp.com': 'bandcamp',
        'twitch.tv': 'twitch', 'player.twitch.tv': 'twitch', 'clips.twitch.tv': 'twitch',
        'imgur.com': 'imgur', 'i.imgur.com': 'imgur',
        'codepen.io': 'codepen',
        'canva.com': 'canva', 'www.canva.com': 'canva'
    };

    function getEmbedProviderKey(domain: string): string | null {
        const d = domain.toLowerCase();
        if (DOMAIN_TO_PROVIDER[d]) return DOMAIN_TO_PROVIDER[d]!;
        // Handle bandcamp subdomains (artist.bandcamp.com)
        if (d.endsWith('.bandcamp.com')) return 'bandcamp';
        return null;
    }

    function isProviderAutoEmbed(domain: string): boolean {
        const key = getEmbedProviderKey(domain);
        if (!key) return false;
        try {
            const prefs: Record<string, boolean> = JSON.parse(localStorage.getItem('snakk:embed-providers') || '{}');
            return !!prefs[key];
        } catch {
            return false;
        }
    }

    const DENIED_KEY = 'snakk:embed-denied-providers';

    function isProviderDenied(domain: string): boolean {
        const key = getEmbedProviderKey(domain);
        if (!key) return false;
        try {
            const denied: Record<string, boolean> = JSON.parse(localStorage.getItem(DENIED_KEY) || '{}');
            return !!denied[key];
        } catch {
            return false;
        }
    }

    function setProviderDenied(domain: string, deny: boolean): void {
        const key = getEmbedProviderKey(domain);
        if (!key) return;
        try {
            const denied: Record<string, boolean> = JSON.parse(localStorage.getItem(DENIED_KEY) || '{}');
            if (deny) denied[key] = true;
            else delete denied[key];
            localStorage.setItem(DENIED_KEY, JSON.stringify(denied));
        } catch {}
    }

    function setProviderAutoEmbed(domain: string, enabled: boolean): void {
        const key = getEmbedProviderKey(domain);
        if (!key) return;
        try {
            const prefs: Record<string, boolean> = JSON.parse(localStorage.getItem('snakk:embed-providers') || '{}');
            if (enabled) prefs[key] = true;
            else delete prefs[key];
            localStorage.setItem('snakk:embed-providers', JSON.stringify(prefs));
        } catch {}
    }

    function setProviderConsent(domain: string): void {
        const key = getEmbedProviderKey(domain);
        if (!key) return;
        try {
            const consent: Record<string, string> = JSON.parse(localStorage.getItem('snakk:embed-consent') || '{}');
            consent[key] = new Date().toISOString();
            localStorage.setItem('snakk:embed-consent', JSON.stringify(consent));
        } catch {}
    }

    function loadEmbed(container: HTMLElement, oembedHtml: string): void {
        const iframeSrc = extractIframeSrc(oembedHtml);
        if (!iframeSrc) return;

        container.querySelector('.link-preview-card')?.remove();

        const embedContainer = document.createElement('div');
        embedContainer.className = 'link-embed-container';

        const iframe = document.createElement('iframe');
        iframe.src = iframeSrc;
        iframe.setAttribute('allowfullscreen', '');
        iframe.setAttribute('allow', 'autoplay; encrypted-media');
        iframe.style.aspectRatio = '16 / 9';
        embedContainer.appendChild(iframe);

        container.appendChild(embedContainer);
    }

    function showEmbedPromptInline(
        container: HTMLElement,
        oembedHtml: string,
        domain: string
    ): void {
        const bodyPattern   = container.dataset.promptBody    || '';
        const labelYes      = container.dataset.promptYes     || 'Yes';
        const labelNo       = container.dataset.promptNo      || 'No';
        const labelRemember = (container.dataset.promptRemember || 'Always allow content from {0}').replace('{0}', domain);

        const prompt = document.createElement('div');
        prompt.className = 'link-embed-prompt';

        const body = document.createElement('p');
        body.className = 'link-embed-prompt-body';
        body.textContent = bodyPattern.replace('{0}', domain);
        prompt.appendChild(body);

        const actions = document.createElement('div');
        actions.className = 'link-embed-prompt-actions';

        const noBtn = document.createElement('button');
        noBtn.type = 'button';
        noBtn.className = 'btn btn-outline btn-sm';
        noBtn.textContent = labelNo;

        const yesGroup = document.createElement('div');
        yesGroup.className = 'link-embed-prompt-yes-group';

        const rememberLabel = document.createElement('label');
        rememberLabel.className = 'link-embed-prompt-remember';
        const checkbox = document.createElement('input');
        checkbox.type = 'checkbox';
        checkbox.className = 'toggle toggle-sm toggle-primary';
        rememberLabel.appendChild(checkbox);
        rememberLabel.append(' ' + labelRemember);

        const yesBtn = document.createElement('button');
        yesBtn.type = 'button';
        yesBtn.className = 'btn btn-primary btn-sm';
        yesBtn.textContent = labelYes;

        yesGroup.appendChild(rememberLabel);
        yesGroup.appendChild(yesBtn);
        actions.appendChild(noBtn);
        actions.appendChild(yesGroup);
        prompt.appendChild(actions);
        container.appendChild(prompt);

        yesBtn.addEventListener('click', () => {
            if (checkbox.checked) setProviderAutoEmbed(domain, true);
            setProviderConsent(domain);
            prompt.remove();
            loadEmbed(container, oembedHtml);
        }, { once: true });

        noBtn.addEventListener('click', () => {
            setProviderDenied(domain, true);
            prompt.remove();

            const msgPattern    = container.dataset.deniedMessage  || '';
            const labelSettings = container.dataset.deniedSettings || 'browser settings';

            const msg = document.createElement('p');
            msg.className = 'link-embed-denied-msg';
            const parts = msgPattern.replace('{0}', domain).split('{1}');
            msg.append(parts[0] || '');
            const a = document.createElement('a');
            a.href = '/my/settings/browser';
            a.textContent = labelSettings;
            msg.append(a);
            msg.append(parts[1] || '');
            container.appendChild(msg);
        }, { once: true });
    }

    function initLinkEmbed(): void {
        const container = document.getElementById('link-preview-container');
        if (!container) return;

        const oembedHtml = container.dataset.oembedHtml || '';
        const domain = container.dataset.domain || '';

        if (!oembedHtml) return;

        // Auto-load if user has enabled this provider
        if (domain && isProviderAutoEmbed(domain)) {
            loadEmbed(container, oembedHtml);
            return;
        }

        // Show inline prompt for known providers on first visit (neither auto-embed nor denied)
        if (domain && getEmbedProviderKey(domain) && !isProviderDenied(domain)) {
            showEmbedPromptInline(container, oembedHtml, domain);
        }
    }

    // Expose for Razor onclick
    (window as any).highlightAcceptedAnswer = function(postPublicId: string): void {
        const post = document.querySelector(`article[data-post-id="${postPublicId}"]`);
        if (post) {
            post.scrollIntoView({ behavior: 'smooth', block: 'center' });
            post.classList.add('highlight-flash');
            setTimeout(() => post.classList.remove('highlight-flash'), 2000);
        }
    };
})();
