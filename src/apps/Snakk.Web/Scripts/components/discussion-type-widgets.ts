/**
 * Discussion Type Widgets — client-side interactivity for type-specific features.
 * Data display (badges, bars, previews) is server-rendered in Razor.
 * This script handles: interactive buttons, Guide TOC, Images layout toggle.
 */

(function() {
    'use strict';

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

        const picker = document.createElement('div');
        picker.className = 'debate-position-picker';
        picker.innerHTML = '<div class="text-sm font-medium mb-2">Choose your position:</div>';

        const btnContainer = document.createElement('div');
        btnContainer.className = 'flex flex-wrap gap-2';

        positions.forEach((p: any, i: number) => {
            const btn = document.createElement('button');
            btn.type = 'button';
            btn.className = 'debate-position-picker-btn';
            btn.dataset.positionId = p.id.toString();
            const color = (colors[i] || colors[0]) as string;
            btn.style.borderColor = color;
            btn.textContent = p.label;
            btn.addEventListener('click', () => {
                btnContainer.querySelectorAll('.debate-position-picker-btn').forEach(b =>
                    b.classList.remove('debate-position-active'));
                btn.classList.add('debate-position-active');
                btn.style.backgroundColor = color;
                btn.style.color = 'white';

                let input = replyForm.querySelector('input[name="DebatePositionId"]') as HTMLInputElement;
                if (!input) {
                    input = document.createElement('input');
                    input.type = 'hidden';
                    input.name = 'DebatePositionId';
                    replyForm.appendChild(input);
                }
                input.value = p.id.toString();
            });
            btnContainer.appendChild(btn);
        });

        picker.appendChild(btnContainer);

        const editor = replyForm.querySelector('#editor-container, .composer-area');
        if (editor) {
            editor.parentNode?.insertBefore(picker, editor);
        } else {
            replyForm.prepend(picker);
        }
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

        // Mark cached images as loaded (backup for onload already fired)
        imagesDisplay.querySelectorAll('img.images-blur-up').forEach(img => {
            if ((img as HTMLImageElement).complete) {
                img.classList.add('images-loaded');
                img.closest('.images-upload-item')?.classList.add('images-item-loaded');
            }
        });

        // Lightbox — event delegation on the images container
        imagesDisplay.addEventListener('click', (e) => {
            const item = (e.target as HTMLElement).closest('.images-upload-item');
            if (!item) return;

            const allItems = imagesDisplay.querySelectorAll('.images-upload-item');
            const idx = Array.from(allItems).indexOf(item);
            if (idx < 0) return;

            const fullUrls = Array.from(allItems).map(el => {
                const img = el.querySelector('img') as HTMLImageElement | null;
                return img?.dataset.full || img?.src || '';
            });

            if (fullUrls[idx] && (window as any).SnakkLightbox) {
                (window as any).SnakkLightbox.open(fullUrls, idx);
            }
        });

        // Preload full image on hover
        imagesDisplay.addEventListener('mouseenter', (e) => {
            const item = (e.target as HTMLElement).closest('.images-upload-item');
            if (!item) return;
            const img = item.querySelector('img') as HTMLImageElement | null;
            const fullUrl = img?.dataset.full;
            if (fullUrl && (window as any).SnakkLightbox) {
                (window as any).SnakkLightbox.preloadUrl(fullUrl);
            }
        }, true);

        // Carousel interactivity (only if carousel layout)
        const track = document.getElementById('images-carousel-track') as HTMLElement | null;
        const counter = document.getElementById('images-carousel-counter');
        if (!track) return;

        const items = track.querySelectorAll('.images-upload-item');
        if (items.length <= 1) return;

        let carouselIdx = 0;

        function slide(newIdx: number): void {
            carouselIdx = newIdx;
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
        }

        // Arrow buttons (wrap around)
        document.getElementById('gup-prev')?.addEventListener('click', () => {
            slide(carouselIdx > 0 ? carouselIdx - 1 : items.length - 1);
        });
        document.getElementById('gup-next')?.addEventListener('click', () => {
            slide(carouselIdx < items.length - 1 ? carouselIdx + 1 : 0);
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

    function loadEmbed(container: HTMLElement, oembedHtml: string, embedBtn: HTMLButtonElement | null, auto: boolean): void {
        const iframeSrc = extractIframeSrc(oembedHtml);
        if (!iframeSrc) return;

        const card = container.querySelector('.link-preview-card') as HTMLElement | null;

        if (auto) {
            // Auto-embed: remove card and button entirely
            card?.remove();
            embedBtn?.remove();
        } else {
            // Manual: hide so they can be restored
            if (card) card.style.display = 'none';
            if (embedBtn) embedBtn.style.display = 'none';
        }

        // Build embed container
        const embedContainer = document.createElement('div');
        embedContainer.className = 'link-embed-container';

        const iframe = document.createElement('iframe');
        iframe.src = iframeSrc;
        iframe.setAttribute('allowfullscreen', '');
        iframe.setAttribute('allow', 'autoplay; encrypted-media');
        iframe.style.aspectRatio = '16 / 9';
        embedContainer.appendChild(iframe);

        if (!auto) {
            // Back button only for manual embeds
            const backBtn = document.createElement('button');
            backBtn.className = 'link-embed-btn';
            backBtn.textContent = 'Show link card';
            backBtn.addEventListener('click', () => {
                embedContainer.remove();
                backBtn.remove();
                if (card) card.style.display = '';
                if (embedBtn) embedBtn.style.display = '';
            });
            container.appendChild(backBtn);
        }

        container.appendChild(embedContainer);
    }

    function initLinkEmbed(): void {
        const container = document.getElementById('link-preview-container');
        if (!container) return;

        const embedBtn = container.querySelector('.link-embed-btn') as HTMLButtonElement | null;
        if (!embedBtn) return;

        const oembedHtml = embedBtn.dataset.oembedHtml || '';
        const domain = embedBtn.dataset.domain || '';

        // Auto-load if user has enabled this provider
        if (domain && isProviderAutoEmbed(domain)) {
            loadEmbed(container, oembedHtml, embedBtn, true);
            return;
        }

        // Manual click
        embedBtn.addEventListener('click', () => {
            loadEmbed(container, oembedHtml, embedBtn, false);
        });
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
