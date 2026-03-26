/**
 * New Discussion Page (all type-specific pages share this script)
 * Handles: Milkdown editor init, sessionStorage draft restore, image upload on submit.
 */

(function() {
    'use strict';

    // Restore draft from sessionStorage if navigating from type picker
    function restoreDraft(): void {
        const params = new URLSearchParams(window.location.search);
        const draftId = params.get('draft');
        if (!draftId) return;

        const key = `snakk-draft-${draftId}`;
        const raw = sessionStorage.getItem(key);
        if (!raw) return;

        try {
            const draft = JSON.parse(raw);
            const titleInput = document.getElementById('new-discussion-title') as HTMLInputElement;
            const textarea = document.getElementById('new-discussion-content') as HTMLTextAreaElement;

            if (titleInput && draft.title) titleInput.value = draft.title;
            if (textarea && draft.content) textarea.value = draft.content;
        } catch { /* ignore parse errors */ }

        // Clean up after restoring
        sessionStorage.removeItem(key);
    }

    async function initEditor(): Promise<void> {
        // Restore draft before editor init so textarea has content
        restoreDraft();

        const container = document.getElementById('editor-container');
        const textarea = document.getElementById('new-discussion-content') as HTMLTextAreaElement;
        if (!container || !textarea) return;
        if (!(window as any).SnakkEditor) return;

        const placeholder = container.dataset.placeholder || 'Write your content...';

        const editor = await (window as any).SnakkEditor.init({
            container,
            textarea,
            placeholder,
            initialValue: textarea.value || '',
        });

        if (!editor) return;

        // Focus editor when clicking container (not toolbar/footer)
        container.addEventListener('click', (e) => {
            if (!(e.target as HTMLElement).closest('.milkdown-toolbar, .milkdown-footer')) {
                editor.focus();
            }
        });

        // Move submit button into editor footer
        const submitBtn = document.getElementById('new-discussion-submit');
        const footer = container.querySelector('.milkdown-footer');
        if (submitBtn && footer) {
            footer.appendChild(submitBtn);
            submitBtn.classList.remove('hidden');
        }

        // Intercept form submit to handle pending image uploads
        const form = textarea.closest('form') as HTMLFormElement | null;
        if (!form) return;

        form.addEventListener('submit', async (e) => {
            const pending: Map<string, File> = (window as any).SnakkEditor.getPendingUploads(container);
            const md = editor.getMarkdown();

            // Validate
            const titleInput = form.querySelector('input[name="NewTitle"]') as HTMLInputElement | null;
            if (!titleInput?.value.trim() || !md.trim()) {
                e.preventDefault();
                return;
            }

            if (pending.size > 0) {
                e.preventDefault();
                e.stopPropagation();

                const btn = form.querySelector('button[type="submit"]') as HTMLButtonElement | null;
                if (btn) {
                    btn.disabled = true;
                    btn.dataset.originalText = btn.textContent || '';
                    btn.textContent = 'Uploading images...';
                }

                try {
                    let updatedMd = md;
                    for (const [blobUrl, file] of pending) {
                        const uploadData = new FormData();
                        uploadData.append('file', file, file.name);

                        const response = await fetch('/bff/media/upload', {
                            method: 'POST',
                            body: uploadData,
                        });

                        if (!response.ok) continue;

                        const result = JSON.parse(await response.text());
                        updatedMd = updatedMd.split(blobUrl).join(result.url);
                    }

                    (window as any).SnakkEditor.clearPendingUploads(container);

                    const formData = new FormData(form);
                    formData.set('NewContent', updatedMd);

                    const response = await fetch(form.action, {
                        method: 'POST',
                        body: formData,
                    });

                    if (response.redirected) {
                        window.location.href = response.url;
                    } else {
                        window.location.reload();
                    }
                } catch (err) {
                    console.error('Error uploading images:', err);
                    if (btn) {
                        btn.disabled = false;
                        btn.textContent = btn.dataset.originalText || 'Create';
                    }
                }
                return;
            }

            // No pending uploads — sync textarea and let form submit naturally
            textarea.value = md;
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initEditor);
    } else {
        initEditor();
    }
})();
