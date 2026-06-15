/**
 * Gallery/Images discussion type — inline image picker using the shared widget.
 * Images stay as blob: URLs until form submit, when they are uploaded sequentially
 * and replaced with CDN URLs before the form POST.
 */

(function () {
    'use strict';

    interface GalleryImage {
        url: string; // blob: for new files; CDN URL after upload
        alt: string;
    }

    const TOTAL_CAP = 12;
    let currentImages: GalleryImage[] = [];
    let pendingFiles = new Map<string, File>(); // blob URL → File
    let deferredSubmitReady = false;

    // Module-scope references so re-init (HTMX back-navigation) can tear down cleanly
    let activeWidget: any = null;
    let submitHandler: ((e: Event) => Promise<void>) | null = null;

    function setBlocker(name: string, blocked: boolean): void {
        (window as any).SnakkNewDiscussion?.setBlocker(name, blocked);
    }

    async function uploadFile(file: File): Promise<string> {
        const formData = new FormData();
        formData.append('file', file, file.name);
        const result = await (window as any).SnakkUpload.uploadWithProgress({
            url: '/bff/media/upload',
            formData,
            onProgress: () => {},
            onProcessing: () => {},
        });
        if (!result.ok || !result.data?.url) {
            throw new Error(result.error ?? `Upload failed (${result.status})`);
        }
        return result.data.url as string;
    }

    function writeHiddenInputs(): void {
        const container = document.getElementById('images-hidden-inputs');
        if (!container) return;
        container.innerHTML = '';

        currentImages.forEach(img => {
            const inp = document.createElement('input');
            inp.type = 'hidden';
            inp.name = 'ImagesImageUrls';
            inp.value = img.url;
            container.appendChild(inp);
        });
    }

    async function init(): Promise<void> {
        const form = document.getElementById('new-discussion-form') as HTMLFormElement | null;
        if (!form) return;

        const pickerContainer = document.getElementById('images-picker-container') as HTMLElement | null;
        if (!pickerContainer) return;

        // Tear down any previous session (HTMX back-navigation causes re-init on stale DOM)
        if (activeWidget) { activeWidget.destroy(); activeWidget = null; }
        if (submitHandler) { form.removeEventListener('submit', submitHandler); submitHandler = null; }
        currentImages = [];
        pendingFiles = new Map();
        deferredSubmitReady = false;
        pickerContainer.innerHTML = '';

        setBlocker('images', true);
        (window as any).SnakkGalleryNew = { getImageCount: () => currentImages.length };

        activeWidget = (window as any).SnakkEditor.createImagePickerWidget(pickerContainer, {
            pendingFiles,
            maxImages: TOTAL_CAP,
            getExternalCount: (): number => {
                const editorEl = document.getElementById('editor-container') as HTMLElement | null;
                const inst = editorEl ? (window as any).SnakkEditor.getInstance(editorEl) : null;
                return inst?.getImageCount() ?? 0;
            },
            onChange: (items: Array<{ blobUrl: string; alt: string; oversized: boolean }>, layout: string) => {
                currentImages = items
                    .filter(i => !i.oversized && i.blobUrl)
                    .map(i => ({ url: i.blobUrl, alt: i.alt }));
                const stillLoading = items.some(i => !i.oversized && !i.blobUrl);
                const layoutInput = document.getElementById('images-layout-hidden') as HTMLInputElement | null;
                if (layoutInput) layoutInput.value = layout;
                setBlocker('images', currentImages.length === 0 || stillLoading);
            },
        });

        submitHandler = async (e: Event) => {
            if (deferredSubmitReady) {
                deferredSubmitReady = false;
                writeHiddenInputs();
                return;
            }

            if (currentImages.length === 0) {
                e.preventDefault();
                return;
            }

            // Find pending uploads for images that are currently confirmed
            const toUpload = currentImages
                .filter(img => pendingFiles.has(img.url))
                .map(img => ({ url: img.url, file: pendingFiles.get(img.url)! }));

            if (toUpload.length === 0) {
                writeHiddenInputs();
                return;
            }

            // Deferred path: upload blobs before submitting
            e.preventDefault();
            setBlocker('flushing', true);

            const btnSpinner = document.getElementById('new-discussion-submit-spinner');
            const progressRow = document.getElementById('upload-progress-row');
            const chipsEl = document.getElementById('upload-image-chips');
            const statusEl = document.getElementById('upload-status-text');

            btnSpinner?.classList.remove('hidden');

            const total = toUpload.length;
            if (chipsEl) {
                chipsEl.innerHTML = '';
                for (let i = 0; i < total; i++) {
                    const chip = document.createElement('span');
                    chip.className = 'upload-chip upload-chip-waiting';
                    chip.id = `upload-chip-${i}`;
                    chipsEl.appendChild(chip);
                }
                const first = document.getElementById('upload-chip-0');
                if (first) first.className = 'upload-chip upload-chip-uploading';
            }
            if (statusEl) statusEl.textContent = `Uploading 1 of ${total}…`;
            progressRow?.classList.remove('hidden');

            let done = 0;
            try {
                for (const { url: blobUrl, file } of toUpload) {
                    const cdnUrl = await uploadFile(file);
                    URL.revokeObjectURL(blobUrl);
                    pendingFiles.delete(blobUrl);
                    currentImages = currentImages.map(i => i.url === blobUrl ? { ...i, url: cdnUrl } : i);
                    done++;
                    const doneChip = document.getElementById(`upload-chip-${done - 1}`);
                    if (doneChip) doneChip.className = 'upload-chip upload-chip-done';
                    const nextChip = document.getElementById(`upload-chip-${done}`);
                    if (nextChip) nextChip.className = 'upload-chip upload-chip-uploading';
                    if (statusEl) statusEl.textContent = done < total ? `Uploading ${done + 1} of ${total}…` : `Uploading ${total} of ${total}…`;
                }
            } catch (err) {
                console.error('[Gallery] Upload failed:', err);
                if (statusEl) statusEl.textContent = 'Upload failed. Please try again.';
                btnSpinner?.classList.add('hidden');
                progressRow?.classList.add('hidden');
                setBlocker('flushing', false);
                return;
            }

            // Success: keep button disabled and progress visible; page navigates away.
            // Do NOT call setBlocker('flushing', false) here — that would briefly re-enable
            // the button between this line and requestSubmit(), causing a visible flicker.
            btnSpinner?.classList.add('hidden');
            deferredSubmitReady = true;
            form.dataset.allowResubmit = 'true';
            form.requestSubmit();
        };

        form.addEventListener('submit', submitHandler);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
