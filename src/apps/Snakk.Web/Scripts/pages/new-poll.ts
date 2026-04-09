// New Poll page — add-poll-option action
(function () {
    'use strict';

    window.SnakkActions.on('add-poll-option', () => {
        const list = document.getElementById('poll-options-list');
        if (!list || list.children.length >= 20) return;

        const input = document.createElement('input');
        input.type = 'text';
        input.name = 'PollOptions';
        input.className = 'input w-full input-sm';
        input.placeholder = 'Option ' + (list.children.length + 1);
        list.appendChild(input);
    });

    const segmentCheck = document.getElementById('poll-segmented-check') as HTMLInputElement | null;
    const segmentFields = document.getElementById('segment-fields');
    if (segmentCheck && segmentFields) {
        segmentCheck.addEventListener('change', () => {
            segmentFields.classList.toggle('hidden', !segmentCheck.checked);
        });
    }

    const closeDateCheck = document.getElementById('poll-close-date-check') as HTMLInputElement | null;
    const closeDateField = document.getElementById('close-date-field');
    if (closeDateCheck && closeDateField) {
        closeDateCheck.addEventListener('change', () => {
            closeDateField.classList.toggle('hidden', !closeDateCheck.checked);
        });
    }
})();
