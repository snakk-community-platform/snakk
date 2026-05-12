(function(): void {
    'use strict';

    const toggle = document.getElementById('iama-scheduled-toggle') as HTMLInputElement | null;
    const fields = document.getElementById('iama-schedule-fields');
    const startInput = document.getElementById('iama-start') as HTMLInputElement | null;

    if (toggle && fields && startInput) {
        toggle.addEventListener('change', () => {
            fields.classList.toggle('hidden', !toggle.checked);
            startInput.required = toggle.checked;
        });
    }

    // Verification note markdown editor
    (async () => {
        const container = document.getElementById('iama-verif-editor-container') as HTMLElement | null;
        const textarea = document.getElementById('iama-verif-content') as HTMLTextAreaElement | null;
        if (!container || !textarea) return;

        for (let i = 0; i < 50 && !(window as any).SnakkEditor; i++) {
            await new Promise(r => setTimeout(r, 100));
        }
        if (!(window as any).SnakkEditor) return;

        await (window as any).SnakkEditor.init({
            container,
            textarea,
            placeholder: container.dataset.placeholder || '',
            initialValue: textarea.value || '',
            allowedFeatures: ['emoji', 'bold', 'italic', 'underline', 'highlight', 'spoiler', 'link'],
        });
    })();
})();
