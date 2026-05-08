(function(): void {
    'use strict';

    const toggle = document.getElementById('iama-scheduled-toggle') as HTMLInputElement | null;
    const fields = document.getElementById('iama-schedule-fields');
    const startInput = document.getElementById('iama-start') as HTMLInputElement | null;

    if (!toggle || !fields || !startInput) return;

    toggle.addEventListener('change', () => {
        fields.classList.toggle('hidden', !toggle.checked);
        startInput.required = toggle.checked;
    });
})();
