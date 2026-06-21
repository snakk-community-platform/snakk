(function(): void {
    'use strict';

    const teardowns: Array<() => void> = [];

    function runTeardowns(): void {
        for (const fn of teardowns) { try { fn(); } catch {} }
        teardowns.length = 0;
    }

    async function init(): Promise<void> {
        runTeardowns();

        const toggle = document.getElementById('iama-scheduled-toggle') as HTMLInputElement | null;
        const fields = document.getElementById('iama-schedule-fields');
        const startInput = document.getElementById('iama-start') as HTMLInputElement | null;

        if (toggle && fields && startInput) {
            const toggleHandler = () => {
                fields.classList.toggle('sn-hidden', !toggle.checked);
                startInput.required = toggle.checked;
            };
            toggle.addEventListener('change', toggleHandler);
            teardowns.push(() => toggle.removeEventListener('change', toggleHandler));
        }

        const container = document.getElementById('iama-verif-editor-container') as HTMLElement | null;
        const textarea = document.getElementById('iama-verif-content') as HTMLTextAreaElement | null;
        if (!container || !textarea) return;

        for (let i = 0; i < 50 && !(window as any).SnakkEditor; i++) {
            await new Promise(r => setTimeout(r, 100));
        }
        if (!(window as any).SnakkEditor) return;

        (window as any).SnakkEditor.destroy(container);

        await (window as any).SnakkEditor.init({
            container,
            textarea,
            placeholder: container.dataset.placeholder || '',
            initialValue: textarea.value || '',
            allowedFeatures: ['emoji', 'bold', 'italic', 'underline', 'highlight', 'spoiler', 'link'],
        });

        teardowns.push(() => (window as any).SnakkEditor?.destroy(container));
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
