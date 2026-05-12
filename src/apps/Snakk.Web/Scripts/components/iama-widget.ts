(function () {
    'use strict';

    function init(): void {
        const btn = document.querySelector<HTMLButtonElement>('.sn-iama-end-btn');
        if (!btn) return;

        const discussionId = btn.dataset.discussionId;
        const confirmLabel = btn.dataset.confirmLabel || 'Yes, end it';
        if (!discussionId) return;

        btn.addEventListener('click', () => {
            if (btn.dataset.confirming === 'true') return;
            btn.dataset.confirming = 'true';

            btn.disabled = true;

            const wrapper = document.createElement('span');
            wrapper.className = 'sn-iama-end-confirm';

            const confirmBtn = document.createElement('button');
            confirmBtn.type = 'button';
            confirmBtn.className = 'btn btn-sm btn-error';
            confirmBtn.textContent = confirmLabel;

            const cancelBtn = document.createElement('button');
            cancelBtn.type = 'button';
            cancelBtn.className = 'btn btn-sm btn-ghost';
            cancelBtn.textContent = '✕';

            wrapper.appendChild(confirmBtn);
            wrapper.appendChild(cancelBtn);
            btn.replaceWith(wrapper);

            cancelBtn.addEventListener('click', () => {
                wrapper.replaceWith(btn);
                btn.disabled = false;
                delete btn.dataset.confirming;
            });

            confirmBtn.addEventListener('click', async () => {
                confirmBtn.disabled = true;
                cancelBtn.disabled = true;

                try {
                    const res = await fetch(`/bff/discussions/${discussionId}/iama/end`, { method: 'POST' });
                    if (res.ok) {
                        window.location.reload();
                    } else {
                        wrapper.replaceWith(btn);
                        btn.disabled = false;
                        delete btn.dataset.confirming;
                    }
                } catch {
                    wrapper.replaceWith(btn);
                    btn.disabled = false;
                    delete btn.dataset.confirming;
                }
            });
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
