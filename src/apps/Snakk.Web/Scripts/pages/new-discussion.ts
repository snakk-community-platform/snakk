/**
 * New Discussion Page (all type-specific pages share this script)
 * Handles: Milkdown editor init, sessionStorage draft restore, form validation.
 * Images are uploaded immediately by the editor — no submit-time upload needed.
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

        sessionStorage.removeItem(key);
    }

    async function initEditor(): Promise<void> {
        restoreDraft();

        const container = document.getElementById('editor-container');
        const textarea = document.getElementById('new-discussion-content') as HTMLTextAreaElement;
        if (!container || !textarea) return;
        if (!(window as any).SnakkEditor) return;

        const placeholder = container.dataset.placeholder || 'Write your content...';
        const hideImageButton = container.dataset.hideImage === 'true';

        const editor = await (window as any).SnakkEditor.init({
            container,
            textarea,
            placeholder,
            initialValue: textarea.value || '',
            hideImageButton,
        });

        if (!editor) return;

        container.addEventListener('click', (e) => {
            if (!(e.target as HTMLElement).closest('.milkdown-toolbar, .milkdown-footer')) {
                editor.focus();
            }
        });

        const submitBtn = document.getElementById('new-discussion-submit');
        const footer = container.querySelector('.milkdown-footer');
        if (submitBtn && footer) {
            footer.appendChild(submitBtn);
            submitBtn.classList.remove('hidden');
        }

        const form = textarea.closest('form') as HTMLFormElement | null;
        if (!form) return;

        form.addEventListener('submit', (e) => {
            const md = editor.getMarkdown();
            const titleInput = form.querySelector('input[name="NewTitle"]') as HTMLInputElement | null;

            if (!titleInput?.value.trim() || !md.trim()) {
                e.preventDefault();
                return;
            }

            // Sync editor content to hidden textarea for form submission
            textarea.value = md;
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initEditor);
    } else {
        initEditor();
    }
})();
