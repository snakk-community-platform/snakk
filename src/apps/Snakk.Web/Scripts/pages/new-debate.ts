// New Debate page — add-debate-position action
(function () {
    'use strict';

    window.SnakkActions.on('add-debate-position', () => {
        const list = document.getElementById('debate-positions-list');
        if (!list || list.children.length >= 3) return;

        const input = document.createElement('input');
        input.type = 'text';
        input.name = 'DebatePositions';
        input.className = 'sn-input sn-w-full sn-input-sm';
        input.placeholder = 'Position ' + (list.children.length + 1);
        list.appendChild(input);

        if (list.children.length >= 3) {
            document.getElementById('add-debate-position-btn')?.classList.add('sn-hidden');
        }
    });
})();
