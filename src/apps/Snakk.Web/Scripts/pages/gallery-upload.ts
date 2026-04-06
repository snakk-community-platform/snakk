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
        blurDataUri: string | null;
        fileName: string;
        fileKey: string | null;
    }

    let images: UploadImage[] = [];
    type ImagesLayout = 'grid' | 'masonry' | 'justified' | 'carousel' | 'hero';
    type ImagesMode = 'empty' | 'single' | 'multi';
    let currentLayout: ImagesLayout = 'masonry';
    let lastUserLayout: ImagesLayout | null = null;
    let currentMode: ImagesMode = 'empty';
    let hasShownHint = false;
    let carouselIndex = 0;

    // ─── State machine ──────────────────────────────────────────

    function updateMode(): void {
        const uploadedCount = images.filter(i => i.url !== '' && i.url !== '__failed__').length;
        const totalCount = images.length;
        const newMode: ImagesMode = totalCount === 0 ? 'empty' : totalCount === 1 ? 'single' : 'multi';

        if (newMode === currentMode) return;

        currentMode = newMode;

        if (newMode === 'multi') {
            // Restore last user layout or default to grid
            if (lastUserLayout) {
                currentLayout = lastUserLayout;
                syncLayoutPicker(currentLayout);
            }
            showLayoutSection();

            // Show hint on first transition to multi
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
    dropZone.addEventListener('click', () => fileInput.click());

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

        // Add placeholders for all files at once
        const placeholders: number[] = [];
        fileArray.forEach(file => {
            images.push({ url: '', thumbnailUrl: null, blurDataUri: null, fileName: file.name, fileKey: fileKeyOf(file) });
            placeholders.push(images.length - 1);
        });
        renderPreview();

        // Upload all in parallel
        await Promise.all(fileArray.map(async (file, i) => {
            const placeholderIdx = placeholders[i]!;
            const key = fileKeyOf(file);

            try {
                const formData = new FormData();
                formData.append('file', file, file.name);

                const response = await fetch('/bff/media/upload', {
                    method: 'POST',
                    body: formData,
                });

                if (!response.ok) {
                    images[placeholderIdx] = { url: '__failed__', thumbnailUrl: null, blurDataUri: null, fileName: file.name, fileKey: key };
                    return;
                }

                const result = JSON.parse(await response.text());
                images[placeholderIdx] = { url: result.url, thumbnailUrl: result.thumbnailUrl || null, blurDataUri: result.blurDataUri || null, fileName: file.name, fileKey: key };
            } catch (err) {
                console.error('Images upload error:', err);
                images[placeholderIdx] = { url: '__failed__', thumbnailUrl: null, blurDataUri: null, fileName: file.name, fileKey: key };
            }
        }));

        // Remove failed uploads
        images = images.filter(img => img.url !== '__failed__');
        renderPreview();
        updateHiddenInputs();

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
        } else {
            // Use thumbnail for grid-like layouts, full image for carousel/hero-main
            const useThumbnail = !forceFullImage && thumbnailLayouts.has(currentLayout) && img.thumbnailUrl;
            const src = useThumbnail ? img.thumbnailUrl : img.url;
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
    }

    function renderPreview(): void {
        if (!preview) return;

        updateMode();

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
        }

        // Add image button below the preview
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
