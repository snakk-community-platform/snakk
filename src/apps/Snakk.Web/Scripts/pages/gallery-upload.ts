/**
 * Images Upload — handles drag/drop, immediate upload, live preview in grid/carousel.
 */

(function() {
    'use strict';

    const dropZone = document.getElementById('images-drop-zone');
    const fileInput = document.getElementById('images-file-input') as HTMLInputElement | null;
    const preview = document.getElementById('images-preview');
    const hiddenInputs = document.getElementById('images-hidden-inputs');
    const layoutPicker = document.getElementById('images-layout-picker');
    const layoutSection = document.getElementById('images-layout-section');
    const layoutHint = document.getElementById('images-layout-hint');

    if (!dropZone || !fileInput || !preview || !hiddenInputs) return;

    interface UploadImage {
        url: string;
        thumbnailUrl: string | null;
        mediumThumbnailUrl: string | null;
        blurDataUri: string | null;
        fileName: string;
        fileKey: string | null;
    }

    const MAX_IMAGES = 20;
    let images: UploadImage[] = [];
    // Persisted slider position for the compare layout — survives re-render
    // (e.g. after the user clicks the swap-before/after button) so the
    // slider doesn't snap back to its default position on every tweak.
    let comparePct: number | null = null;
    type ImagesLayout = 'grid' | 'masonry' | 'justified' | 'carousel' | 'hero' | 'compare';
    type ImagesMode = 'empty' | 'single' | 'multi';
    let currentLayout: ImagesLayout = 'masonry';
    let lastUserLayout: ImagesLayout | null = null;
    let currentMode: ImagesMode = 'empty';
    let hasShownHint = false;
    let carouselIndex = 0;

    // ─── Submit-button blockers ─────────────────────────────────
    // Coordinates with new-discussion.ts on the shared #new-discussion-submit:
    //   images    — set until at least one upload has completed
    //   uploading — set while any upload is in flight
    function recomputeImageBlockers(): void {
        const api = window.SnakkNewDiscussion;
        if (!api) return;
        const uploading = images.some(i => i.url === '');
        const hasUploaded = images.some(i => i.url !== '' && i.url !== '__failed__');
        api.setBlocker('images', !hasUploaded);
        api.setBlocker('uploading', uploading);
    }

    // Seed the images blocker at page load (no uploads yet).
    recomputeImageBlockers();

    // Belt-and-braces: even if a blocker slips, intercept submit.
    const form = dropZone.closest('form') as HTMLFormElement | null;
    form?.addEventListener('submit', (e) => {
        const hasUploaded = images.some(i => i.url !== '' && i.url !== '__failed__');
        const uploading = images.some(i => i.url === '');
        if (!hasUploaded || uploading) {
            e.preventDefault();
        }
    });

    // ─── State machine ──────────────────────────────────────────

    function updateMode(): void {
        const uploadedCount = images.filter(i => i.url !== '' && i.url !== '__failed__').length;
        const totalCount = images.length;
        const newMode: ImagesMode = totalCount === 0 ? 'empty' : totalCount === 1 ? 'single' : 'multi';

        // Show/hide compare option (exactly 2 images only) — always check, even if mode didn't change
        const compareOption = document.getElementById('images-layout-compare');
        if (compareOption) {
            compareOption.style.display = totalCount === 2 ? '' : 'none';
            if (totalCount !== 2 && currentLayout === 'compare') {
                currentLayout = 'masonry';
                lastUserLayout = 'masonry';
                syncLayoutPicker(currentLayout);
            }
        }

        if (newMode === currentMode) return;

        currentMode = newMode;

        if (newMode === 'multi') {
            if (lastUserLayout) {
                currentLayout = lastUserLayout;
                syncLayoutPicker(currentLayout);
            }
            showLayoutSection();

            if (!hasShownHint && uploadedCount >= 2) {
                hasShownHint = true;
                showLayoutHint();
            }
        } else {
            hideLayoutSection();
        }
    }

    function showLayoutSection(): void {
        if (!layoutSection) return;
        layoutSection.classList.remove('images-layout-section-hidden');
    }

    function hideLayoutSection(): void {
        if (!layoutSection) return;
        layoutSection.classList.add('images-layout-section-hidden');
    }

    function showLayoutHint(): void {
        if (!layoutHint) return;
        layoutHint.classList.remove('hidden');
        layoutHint.classList.add('images-layout-hint-visible');
        setTimeout(() => {
            layoutHint.classList.remove('images-layout-hint-visible');
            layoutHint.classList.add('images-layout-hint-fading');
            setTimeout(() => {
                layoutHint.classList.add('hidden');
                layoutHint.classList.remove('images-layout-hint-fading');
            }, 300);
        }, 3000);
    }

    function syncLayoutPicker(layout: ImagesLayout): void {
        if (!layoutPicker) return;
        layoutPicker.querySelectorAll('.images-layout-option').forEach(o =>
            o.classList.toggle('images-layout-active', (o as HTMLElement).dataset.layout === layout));
        const radio = layoutPicker.querySelector(`input[value="${layout}"]`) as HTMLInputElement | null;
        if (radio) radio.checked = true;
    }

    // ─── Layout picker ──────────────────────────────────────────

    layoutPicker?.querySelectorAll('.images-layout-option').forEach(option => {
        option.addEventListener('click', () => {
            const layout = (option as HTMLElement).dataset.layout as ImagesLayout;
            if (!layout) return;

            currentLayout = layout;
            lastUserLayout = layout;
            layoutPicker!.querySelectorAll('.images-layout-option').forEach(o =>
                o.classList.toggle('images-layout-active', (o as HTMLElement).dataset.layout === layout));

            renderPreview();
        });
    });

    // ─── Drop zone ──────────────────────────────────────────────

    // Click to open file picker
    dropZone.addEventListener('click', () => {
        if (images.length >= MAX_IMAGES) return;
        fileInput.click();
    });

    // File selected
    fileInput.addEventListener('change', () => {
        if (fileInput.files) handleFiles(fileInput.files);
        fileInput.value = ''; // Reset so same file can be re-selected
    });

    // Drag events
    dropZone.addEventListener('dragover', (e) => {
        e.preventDefault();
        dropZone.classList.add('gallery-drop-zone-active');
    });

    dropZone.addEventListener('dragleave', () => {
        dropZone.classList.remove('gallery-drop-zone-active');
    });

    dropZone.addEventListener('drop', (e) => {
        e.preventDefault();
        dropZone.classList.remove('gallery-drop-zone-active');
        if (e.dataTransfer?.files) handleFiles(e.dataTransfer.files);
    });

    // ─── Upload handling ────────────────────────────────────────

    function fileKeyOf(file: File): string {
        return `${file.name}|${file.size}`;
    }

    let duplicateNoticeTimer: number | null = null;
    function showDuplicateNotice(count: number): void {
        const dropZoneEl = dropZone as HTMLElement;
        let notice = document.getElementById('images-duplicate-notice');
        if (!notice) {
            notice = document.createElement('div');
            notice.id = 'images-duplicate-notice';
            notice.className = 'images-duplicate-notice';
            dropZoneEl.parentElement?.insertBefore(notice, dropZoneEl.nextSibling);
        }
        notice.textContent = count === 1
            ? 'Skipped 1 duplicate image (already added)'
            : `Skipped ${count} duplicate images (already added)`;
        notice.classList.remove('hidden');

        if (duplicateNoticeTimer !== null) window.clearTimeout(duplicateNoticeTimer);
        duplicateNoticeTimer = window.setTimeout(() => {
            notice?.classList.add('hidden');
            duplicateNoticeTimer = null;
        }, 4000);
    }

    let limitNoticeTimer: number | null = null;
    function showLimitNotice(skipped: number, total: number): void {
        const dropZoneEl = dropZone as HTMLElement;
        let notice = document.getElementById('images-limit-notice');
        if (!notice) {
            notice = document.createElement('div');
            notice.id = 'images-limit-notice';
            notice.className = 'images-duplicate-notice';
            dropZoneEl.parentElement?.insertBefore(notice, dropZoneEl.nextSibling);
        }
        notice.textContent = skipped === total
            ? `Gallery limit reached — maximum ${MAX_IMAGES} images allowed.`
            : `Added ${total - skipped} of ${total} — ${skipped} skipped (limit: ${MAX_IMAGES} images).`;
        notice.classList.remove('hidden');
        if (limitNoticeTimer !== null) window.clearTimeout(limitNoticeTimer);
        limitNoticeTimer = window.setTimeout(() => {
            notice?.classList.add('hidden');
            limitNoticeTimer = null;
        }, 5000);
    }

    function updateImageCount(): void {
        const countEl = document.getElementById('images-count');
        if (!countEl) return;
        countEl.textContent = images.length === 0 ? '' : `${images.length} / ${MAX_IMAGES}`;
    }

    async function handleFiles(files: FileList): Promise<void> {
        // Convert to array immediately — FileList can be invalidated
        const allFiles = Array.from(files).filter(f => f.type.startsWith('image/'));
        if (allFiles.length === 0) return;

        // Deduplicate: skip files that match an existing image's name+size
        const existingKeys = new Set(images.map(i => i.fileKey).filter(k => k !== null));
        const seenInBatch = new Set<string>();
        const fileArray: File[] = [];
        let skippedCount = 0;

        for (const file of allFiles) {
            const key = fileKeyOf(file);
            if (existingKeys.has(key) || seenInBatch.has(key)) {
                skippedCount++;
                continue;
            }
            seenInBatch.add(key);
            fileArray.push(file);
        }

        if (skippedCount > 0) {
            showDuplicateNotice(skippedCount);
        }
        if (fileArray.length === 0) return;

        // Enforce max image limit
        const remaining = MAX_IMAGES - images.length;
        if (remaining <= 0) {
            showLimitNotice(fileArray.length, fileArray.length);
            return;
        }
        if (fileArray.length > remaining) {
            showLimitNotice(fileArray.length - remaining, fileArray.length);
            fileArray.splice(remaining);
        }

        // Add placeholders for all files at once
        const placeholders: number[] = [];
        fileArray.forEach(file => {
            images.push({ url: '', thumbnailUrl: null, mediumThumbnailUrl: null, blurDataUri: null, fileName: file.name, fileKey: fileKeyOf(file) });
            placeholders.push(images.length - 1);
        });
        renderPreview();
        recomputeImageBlockers();

        // Per-slot progress: direct DOM mutation keyed by data-index.
        // Safe because renderPreview() isn't called between start and completion
        // (await Promise.all awaits all slots; the next render is after they finish).
        const updateSlotProgress = (idx: number, percent: number | 'processing'): void => {
            if (!preview) return;
            const slot = preview.querySelector(`[data-index="${idx}"]`);
            if (!slot) return;
            const bar = slot.querySelector<HTMLProgressElement>('progress.images-upload-item-progress');
            const label = slot.querySelector<HTMLDivElement>('.images-upload-item-label');
            if (percent === 'processing') {
                if (label) label.textContent = 'Processing…';
                if (bar) bar.value = 100;
            } else {
                if (bar) bar.value = percent;
                if (label) label.textContent = `${percent}%`;
            }
        };

        // Upload all in parallel
        await Promise.all(fileArray.map(async (file, i) => {
            const placeholderIdx = placeholders[i]!;
            const key = fileKeyOf(file);

            try {
                const formData = new FormData();
                formData.append('file', file, file.name);

                const result = await window.SnakkUpload.uploadWithProgress<{
                    url: string;
                    thumbnailUrl?: string;
                    mediumThumbnailUrl?: string;
                    blurDataUri?: string;
                }>({
                    url: '/bff/media/upload',
                    formData,
                    onProgress: (percent: number) => updateSlotProgress(placeholderIdx, percent),
                    onProcessing: () => updateSlotProgress(placeholderIdx, 'processing'),
                });

                if (!result.ok || !result.data) {
                    images[placeholderIdx] = { url: '__failed__', thumbnailUrl: null, mediumThumbnailUrl: null, blurDataUri: null, fileName: file.name, fileKey: key };
                    return;
                }

                images[placeholderIdx] = {
                    url: result.data.url,
                    thumbnailUrl: result.data.thumbnailUrl || null,
                    mediumThumbnailUrl: result.data.mediumThumbnailUrl || null,
                    blurDataUri: result.data.blurDataUri || null,
                    fileName: file.name,
                    fileKey: key,
                };
            } catch (err) {
                console.error('Images upload error:', err);
                images[placeholderIdx] = { url: '__failed__', thumbnailUrl: null, mediumThumbnailUrl: null, blurDataUri: null, fileName: file.name, fileKey: key };
            }
        }));

        // Remove failed uploads
        images = images.filter(img => img.url !== '__failed__');
        renderPreview();
        updateHiddenInputs();
        recomputeImageBlockers();

        // Auto-advance carousel
        if (currentLayout === 'carousel') {
            carouselIndex = images.filter(i => i.url !== '').length - 1;
        }
    }

    // ─── Preview rendering ──────────────────────────────────────

    const reorderableLayouts = new Set(['grid', 'hero', 'masonry', 'justified']);

    // Layouts that should use thumbnail instead of full image
    const thumbnailLayouts = new Set(['grid', 'masonry', 'justified', 'hero']);

    function renderItem(img: UploadImage, i: number, extraClass?: string, forceFullImage?: boolean): string {
        const isUploading = img.url === '';
        const canDrag = !isUploading && reorderableLayouts.has(currentLayout);
        let cls = 'images-upload-item';
        if (isUploading) cls += ' images-upload-item-loading';
        if (extraClass) cls += ' ' + extraClass;

        // Blur-up background
        const blurStyle = img.blurDataUri
            ? ` style="background-image:url(${img.blurDataUri});background-size:cover;background-position:center"`
            : '';

        let html = `<div class="${cls}" data-index="${i}"${canDrag ? ' draggable="true"' : ''}${blurStyle}>`;
        if (isUploading) {
            html += '<div class="images-upload-item-skeleton skeleton"></div>';
            html += '<progress class="progress progress-primary images-upload-item-progress" max="100" value="0"></progress>';
            html += '<div class="images-upload-item-label">0%</div>';
        } else {
            // Single-image preview: prefer the 600px medium thumbnail over the full image.
            // Otherwise use 300px thumbnail for grid-like layouts, full image for carousel/hero-main.
            const useThumbnail = !forceFullImage && thumbnailLayouts.has(currentLayout) && img.thumbnailUrl;
            let src: string | null;
            if (!forceFullImage && images.length === 1 && img.mediumThumbnailUrl) {
                src = img.mediumThumbnailUrl;
            } else if (useThumbnail) {
                src = img.thumbnailUrl;
            } else {
                src = img.url;
            }
            html += `<img src="${src}" data-full="${img.url}" alt="${img.fileName}" loading="lazy" class="images-blur-up" data-blur-up />`;
            html += `<button type="button" class="images-upload-item-delete" data-index="${i}" title="Remove image">&times;</button>`;
        }
        return html + '</div>';
    }

    function bindPreviewEvents(): void {
        if (!preview) return;

        // Attach blur-up load handlers
        preview.querySelectorAll<HTMLImageElement>('img[data-blur-up]').forEach(img => {
            const done = () => {
                img.classList.add('images-loaded');
                img.parentElement?.classList.add('images-item-loaded');
            };
            if (img.complete) {
                done();
            } else {
                img.addEventListener('load', done);
                img.addEventListener('error', done);
            }
        });

        // Bind delete buttons
        preview.querySelectorAll('.images-upload-item-delete').forEach(btn => {
            btn.addEventListener('click', (e) => {
                e.stopPropagation();
                const idx = parseInt((btn as HTMLElement).dataset.index || '-1');
                if (idx >= 0) removeImage(idx);
            });
        });

        // Bind add more button
        document.getElementById('images-add-more')?.addEventListener('click', () => fileInput?.click());

        // Bind compare swap button — swaps the first two images (before/after)
        // in the data array and re-renders.
        document.getElementById('images-compare-swap')?.addEventListener('click', (e) => {
            e.stopPropagation();
            if (images.length < 2) return;
            const a = images[0]!;
            images[0] = images[1]!;
            images[1] = a;
            renderPreview();
            updateHiddenInputs();
        });

        // Compare slider interaction (preview)
        const cmpContainer = document.getElementById('images-compare-preview');
        const cmpSlider = document.getElementById('images-compare-slider-preview');
        const cmpBefore = cmpContainer?.querySelector('.gup-compare-before') as HTMLElement | null;
        const cmpAfter = cmpContainer?.querySelector('.gup-compare-after') as HTMLElement | null;
        if (cmpContainer && cmpSlider && cmpBefore && cmpAfter) {
            let dragging = false;
            function applyPct(pct: number): void {
                const clamped = Math.max(0, Math.min(1, pct));
                const rightPct = (1 - clamped) * 100;
                const leftPct = clamped * 100;
                cmpBefore!.style.clipPath = `inset(0 ${rightPct}% 0 0)`;
                cmpAfter!.style.clipPath = `inset(0 0 0 ${leftPct}%)`;
                cmpSlider!.style.right = `${rightPct}%`;
                comparePct = clamped;
            }
            function setPos(x: number): void {
                const rect = cmpContainer!.getBoundingClientRect();
                applyPct((x - rect.left) / rect.width);
            }

            // Restore the previous slider position if there is one (e.g. after
            // a swap-before/after re-render). Otherwise default to the
            // "After" label position.
            if (comparePct !== null) {
                requestAnimationFrame(() => applyPct(comparePct!));
            } else {
                const afterLabel = cmpAfter.querySelector('.gup-compare-label-after') as HTMLElement | null;
                if (afterLabel) {
                    requestAnimationFrame(() => {
                        const labelRect = afterLabel.getBoundingClientRect();
                        setPos(labelRect.left - 12);
                    });
                }
            }

            cmpSlider.addEventListener('pointerdown', (e) => { dragging = true; cmpSlider!.setPointerCapture(e.pointerId); e.preventDefault(); });
            cmpContainer.addEventListener('pointerdown', (e) => {
                // Ignore clicks on the slider handle (it has its own pointer
                // capture) and on the swap button — otherwise clicking swap
                // would treat the click as a slider drag and overwrite the
                // saved position.
                const target = e.target as HTMLElement;
                if (target.closest('.gup-compare-handle, .gup-compare-swap-btn')) return;
                dragging = true;
                setPos(e.clientX);
                e.preventDefault();
            });
            document.addEventListener('pointermove', (e) => { if (dragging) setPos(e.clientX); });
            document.addEventListener('pointerup', () => { dragging = false; });
        }
    }

    function renderPreview(): void {
        if (!preview) return;

        updateMode();
        updateImageCount();

        if (images.length === 0) {
            preview.classList.add('hidden');
            preview.innerHTML = '';
            return;
        }

        preview.classList.remove('hidden');
        let html = '';

        // Single image: large preview, no layout
        if (currentMode === 'single') {
            html += '<div class="gup-single">';
            html += renderItem(images[0]!, 0);
            html += '</div>';
            if (images.length < MAX_IMAGES)
                html += '<button type="button" class="images-add-more-btn" id="images-add-more">+ Add more images</button>';
            preview.innerHTML = html;
            bindPreviewEvents();
            return;
        }

        switch (currentLayout) {
            case 'grid':
                html += '<div class="gup-grid">';
                images.forEach((img, i) => { html += renderItem(img, i); });
                html += '</div>';
                break;

            case 'masonry':
                html += '<div class="gup-masonry">';
                images.forEach((img, i) => { html += renderItem(img, i); });
                html += '</div>';
                break;

            case 'justified':
                html += '<div class="gup-justified">';
                images.forEach((img, i) => { html += renderItem(img, i); });
                html += '</div>';
                break;

            case 'carousel': {
                const uploaded = images.filter(i => i.url !== '');
                carouselIndex = Math.min(carouselIndex, Math.max(0, uploaded.length - 1));
                html += '<div class="gup-carousel">';
                html += '<div class="gup-carousel-track" style="transform:translateX(-' + (carouselIndex * 100) + '%)">';
                images.forEach((img, i) => {
                    // Current slide: full image. Adjacent: preload. Others: blur/thumbnail
                    const isActive = i === carouselIndex;
                    const isAdjacent = Math.abs(i - carouselIndex) === 1;
                    const src = (isActive || isAdjacent) ? img.url : (img.thumbnailUrl || img.url);
                    const blurStyle = img.blurDataUri
                        ? ` style="background-image:url(${img.blurDataUri});background-size:cover;background-position:center"`
                        : '';
                    const isUploading = img.url === '';
                    html += `<div class="images-upload-item" data-index="${i}"${blurStyle}>`;
                    if (isUploading) {
                        html += '<div class="images-upload-item-skeleton skeleton"></div>';
                    } else {
                        html += `<img src="${src}" data-full="${img.url}" alt="${img.fileName}" class="images-blur-up" data-blur-up />`;
                        html += `<button type="button" class="images-upload-item-delete" data-index="${i}" title="Remove image">&times;</button>`;
                    }
                    html += '</div>';
                });
                html += '</div>';
                if (uploaded.length > 1) {
                    html += '<button type="button" class="gup-carousel-arrow gup-carousel-prev" id="gup-prev"><svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="15 18 9 12 15 6"/></svg></button>';
                    html += '<button type="button" class="gup-carousel-arrow gup-carousel-next" id="gup-next"><svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="9 6 15 12 9 18"/></svg></button>';
                }
                html += '</div>';
                if (uploaded.length > 1) {
                    html += '<div class="gup-carousel-dots">';
                    uploaded.forEach((_, i) => {
                        html += `<span class="gup-carousel-dot${i === carouselIndex ? ' gup-carousel-dot-active' : ''}" data-idx="${i}"></span>`;
                    });
                    html += '</div>';
                }
                break;
            }

            case 'hero':
                html += '<div class="gup-hero">';
                if (images.length > 0) html += renderItem(images[0]!, 0, undefined, true);
                if (images.length > 1) {
                    html += '<div class="gup-hero-grid">';
                    for (let i = 1; i < images.length; i++) html += renderItem(images[i]!, i);
                    html += '</div>';
                }
                html += '</div>';
                break;

            case 'compare':
                if (images.length >= 2) {
                    const beforeImg = images[0]!;
                    const afterImg = images[1]!;
                    const beforeBg = beforeImg.blurDataUri ? `background-image:url(${beforeImg.blurDataUri});background-size:cover;background-position:center` : '';
                    const afterBg = afterImg.blurDataUri ? `background-image:url(${afterImg.blurDataUri});background-size:cover;background-position:center` : '';
                    html += '<div class="gup-compare" id="images-compare-preview">';
                    html += `<div class="gup-compare-after images-upload-item" style="${afterBg}"><img src="${afterImg.url}" alt="After" /><span class="gup-compare-label gup-compare-label-after">After</span></div>`;
                    html += `<div class="gup-compare-before images-upload-item" style="${beforeBg}"><img src="${beforeImg.url}" alt="Before" /><span class="gup-compare-label gup-compare-label-before">Before</span></div>`;
                    html += '<div class="gup-compare-slider" id="images-compare-slider-preview"><div class="gup-compare-line"></div><div class="gup-compare-handle"><svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><polyline points="15 18 9 12 15 6"></polyline></svg><svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><polyline points="9 6 15 12 9 18"></polyline></svg></div></div>';
                    html += '<button type="button" class="gup-compare-swap-btn" id="images-compare-swap" title="Swap before / after" aria-label="Swap before and after">';
                    html += '<svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="17 1 21 5 17 9"></polyline><path d="M3 11V9a4 4 0 0 1 4-4h14"></path><polyline points="7 23 3 19 7 15"></polyline><path d="M21 13v2a4 4 0 0 1-4 4H3"></path></svg>';
                    html += '</button>';
                    html += '</div>';
                }
                break;
        }

        // Add image button below the preview (hidden at limit)
        if (images.length < MAX_IMAGES)
            html += '<button type="button" class="images-add-more-btn" id="images-add-more">+ Add more images</button>';

        preview.innerHTML = html;
        bindPreviewEvents();

        // Drag-n-drop reorder
        if (reorderableLayouts.has(currentLayout)) {
            let dragIdx: number | null = null;

            preview.querySelectorAll('.images-upload-item[draggable="true"]').forEach(item => {
                const el = item as HTMLElement;

                el.addEventListener('dragstart', (e) => {
                    dragIdx = parseInt(el.dataset.index || '-1');
                    el.classList.add('images-drag-active');
                    if (e.dataTransfer) e.dataTransfer.effectAllowed = 'move';
                });

                el.addEventListener('dragend', () => {
                    el.classList.remove('images-drag-active');
                    dragIdx = null;
                    preview.querySelectorAll('.images-drag-over').forEach(d => d.classList.remove('images-drag-over'));
                });

                el.addEventListener('dragover', (e) => {
                    e.preventDefault();
                    if (e.dataTransfer) e.dataTransfer.dropEffect = 'move';
                    el.classList.add('images-drag-over');
                });

                el.addEventListener('dragleave', () => {
                    el.classList.remove('images-drag-over');
                });

                el.addEventListener('drop', (e) => {
                    e.preventDefault();
                    el.classList.remove('images-drag-over');
                    const dropIdx = parseInt(el.dataset.index || '-1');
                    if (dragIdx !== null && dragIdx !== dropIdx && dragIdx >= 0 && dropIdx >= 0) {
                        // Swap in data array
                        const temp = images[dragIdx]!;
                        images[dragIdx] = images[dropIdx]!;
                        images[dropIdx] = temp;

                        // Hero: re-render when index 0 changes, so the hero image gets full-res src
                        if (currentLayout === 'hero' && (dragIdx === 0 || dropIdx === 0)) {
                            renderPreview();
                            updateHiddenInputs();
                            return;
                        }

                        // Swap DOM elements directly (no re-render)
                        const dragEl = preview?.querySelector(`[data-index="${dragIdx}"]`) as HTMLElement | null;
                        const dropEl = el;
                        if (dragEl && dropEl && dragEl.parentNode) {
                            const placeholder = document.createElement('div');
                            dragEl.parentNode.insertBefore(placeholder, dragEl);
                            dropEl.parentNode!.insertBefore(dragEl, dropEl);
                            placeholder.parentNode!.insertBefore(dropEl, placeholder);
                            placeholder.remove();

                            // Update data-index attributes
                            dragEl.dataset.index = String(dropIdx);
                            dropEl.dataset.index = String(dragIdx);

                            // Update delete button indices
                            const dragDel = dragEl.querySelector('.images-upload-item-delete') as HTMLElement | null;
                            const dropDel = dropEl.querySelector('.images-upload-item-delete') as HTMLElement | null;
                            if (dragDel) dragDel.dataset.index = String(dropIdx);
                            if (dropDel) dropDel.dataset.index = String(dragIdx);
                        }

                        updateHiddenInputs();
                    }
                });
            });
        }

        // Carousel nav — slide + lazy load adjacent images
        function slideCarousel(newIndex: number): void {
            carouselIndex = newIndex;
            const track = preview?.querySelector('.gup-carousel-track') as HTMLElement | null;
            if (track) track.style.transform = `translateX(-${carouselIndex * 100}%)`;
            preview?.querySelectorAll('.gup-carousel-dot').forEach((dot, i) => {
                dot.classList.toggle('gup-carousel-dot-active', i === carouselIndex);
            });

            // Preload current + adjacent slides with full images
            [carouselIndex - 1, carouselIndex, carouselIndex + 1].forEach(idx => {
                if (idx < 0 || idx >= images.length) return;
                const img = images[idx];
                if (!img || !img.url) return;
                const item = track?.children[idx]?.querySelector('img') as HTMLImageElement | null;
                if (item && item.src !== img.url && item.dataset.full) {
                    item.src = item.dataset.full;
                }
            });
        }

        // Preload on arrow hover
        document.getElementById('gup-prev')?.addEventListener('mouseenter', () => {
            const target = carouselIndex - 1;
            if (target >= 0 && images[target]?.url) {
                const preloadImg = new window.Image();
                preloadImg.src = images[target]!.url;
            }
        });
        document.getElementById('gup-next')?.addEventListener('mouseenter', () => {
            const target = carouselIndex + 1;
            if (target < images.length && images[target]?.url) {
                const preloadImg = new window.Image();
                preloadImg.src = images[target]!.url;
            }
        });

        document.getElementById('gup-prev')?.addEventListener('click', () => {
            if (carouselIndex > 0) slideCarousel(carouselIndex - 1);
        });
        document.getElementById('gup-next')?.addEventListener('click', () => {
            if (carouselIndex < images.length - 1) slideCarousel(carouselIndex + 1);
        });
        preview.querySelectorAll('.gup-carousel-dot').forEach(dot => {
            dot.addEventListener('click', () => {
                slideCarousel(parseInt((dot as HTMLElement).dataset.idx || '0'));
            });
        });
    }

    function removeImage(index: number): void {
        const img = images[index];
        images.splice(index, 1);
        renderPreview();
        updateHiddenInputs();
        recomputeImageBlockers();

        // Delete draft from server
        if (img && img.url) {
            fetch(`/bff/media/draft?url=${encodeURIComponent(img.url)}`, { method: 'DELETE' }).catch(() => {});
        }
    }

    function updateHiddenInputs(): void {
        if (!hiddenInputs) return;
        hiddenInputs.innerHTML = '';
        images.forEach(img => {
            if (img.url) {
                const input = document.createElement('input');
                input.type = 'hidden';
                input.name = 'ImagesImageUrls';
                input.value = img.url;
                hiddenInputs.appendChild(input);
            }
        });
    }
})();
