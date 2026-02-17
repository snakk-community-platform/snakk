/**
 * Toast notification system for AdminWeb
 */
(function() {
    'use strict';

    const POSITIONS = {
        'top-right': 'top-6 right-6',
        'top-left': 'top-6 left-6',
        'bottom-right': 'bottom-6 right-6',
        'bottom-left': 'bottom-6 left-6',
        'top-center': 'top-6 left-1/2 -translate-x-1/2',
        'bottom-center': 'bottom-6 left-1/2 -translate-x-1/2'
    };

    const TYPES = {
        success: {
            bg: 'bg-success',
            icon: '<svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" /></svg>'
        },
        error: {
            bg: 'bg-error',
            icon: '<svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>'
        },
        warning: {
            bg: 'bg-warning',
            icon: '<svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" /></svg>'
        },
        info: {
            bg: 'bg-info',
            icon: '<svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>'
        }
    };

    /**
     * Show a toast notification
     * @param {string} message - Message to display
     * @param {string} type - Type: 'success', 'error', 'warning', 'info'
     * @param {Object} options - Additional options
     */
    function showToast(message, type = 'info', options = {}) {
        const {
            duration = 4000,
            position = 'bottom-right',
            dismissible = true
        } = options;

        const typeConfig = TYPES[type] || TYPES.info;
        const positionClass = POSITIONS[position] || POSITIONS['bottom-right'];

        const toast = document.createElement('div');
        toast.className = `fixed ${positionClass} ${typeConfig.bg} text-white px-4 py-3 rounded-lg shadow-lg z-50 flex items-center gap-3 max-w-sm transform transition-all duration-300`;
        toast.style.opacity = '0';
        toast.style.transform = position.includes('right') ? 'translateX(400px)' : 'translateX(-400px)';

        toast.innerHTML = `
            <div class="flex-shrink-0">
                ${typeConfig.icon}
            </div>
            <p class="text-sm flex-1">${window.AdminUtils?.escapeHtml(message) || message}</p>
            ${dismissible ? `
                <button type="button" class="flex-shrink-0 opacity-70 hover:opacity-100 transition-opacity" data-dismiss-toast>
                    <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
                    </svg>
                </button>
            ` : ''}
        `;

        document.body.appendChild(toast);

        // Animate in
        requestAnimationFrame(() => {
            toast.style.opacity = '1';
            toast.style.transform = 'translateX(0)';
        });

        // Dismiss handler
        const dismiss = () => {
            toast.style.opacity = '0';
            toast.style.transform = position.includes('right') ? 'translateX(400px)' : 'translateX(-400px)';
            setTimeout(() => toast.remove(), 300);
        };

        // Dismissible button
        if (dismissible) {
            toast.querySelector('[data-dismiss-toast]').addEventListener('click', dismiss);
        }

        // Auto-dismiss
        if (duration > 0) {
            setTimeout(dismiss, duration);
        }

        return { dismiss };
    }

    /**
     * Show a confirmation modal
     * @param {string} message - Message to display
     * @param {Object} options - Additional options
     * @returns {Promise<boolean>} - Resolves to true if confirmed
     */
    function showConfirm(message, options = {}) {
        const {
            title = 'Confirm',
            confirmText = 'Confirm',
            cancelText = 'Cancel',
            type = 'warning'
        } = options;

        return new Promise((resolve) => {
            const modal = document.createElement('div');
            modal.className = 'fixed inset-0 z-50 flex items-center justify-center bg-black bg-opacity-50';

            const typeConfig = TYPES[type] || TYPES.warning;

            modal.innerHTML = `
                <div class="bg-white rounded-lg shadow-xl max-w-md w-full mx-4 transform transition-all">
                    <div class="p-6">
                        <div class="flex items-start gap-4">
                            <div class="flex-shrink-0 ${typeConfig.bg} text-white rounded-full p-3">
                                ${typeConfig.icon}
                            </div>
                            <div class="flex-1">
                                <h3 class="text-lg font-semibold text-gray-900 mb-2">${window.AdminUtils?.escapeHtml(title) || title}</h3>
                                <p class="text-sm text-gray-600">${window.AdminUtils?.escapeHtml(message) || message}</p>
                            </div>
                        </div>
                    </div>
                    <div class="bg-gray-50 px-6 py-4 flex justify-end gap-3 rounded-b-lg">
                        <button type="button" class="px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-md hover:bg-gray-50 transition-colors" data-action="cancel">
                            ${cancelText}
                        </button>
                        <button type="button" class="px-4 py-2 text-sm font-medium text-white ${typeConfig.bg} rounded-md hover:opacity-90 transition-opacity" data-action="confirm">
                            ${confirmText}
                        </button>
                    </div>
                </div>
            `;

            document.body.appendChild(modal);

            // Animate in
            requestAnimationFrame(() => {
                modal.style.opacity = '1';
            });

            const close = (confirmed) => {
                modal.style.opacity = '0';
                setTimeout(() => {
                    modal.remove();
                    resolve(confirmed);
                }, 200);
            };

            modal.querySelector('[data-action="confirm"]').addEventListener('click', () => close(true));
            modal.querySelector('[data-action="cancel"]').addEventListener('click', () => close(false));
            modal.addEventListener('click', (e) => {
                if (e.target === modal) close(false);
            });
        });
    }

    // Export to global namespace
    window.AdminNotifications = {
        showToast,
        showConfirm,
        success: (msg, opts) => showToast(msg, 'success', opts),
        error: (msg, opts) => showToast(msg, 'error', opts),
        warning: (msg, opts) => showToast(msg, 'warning', opts),
        info: (msg, opts) => showToast(msg, 'info', opts)
    };
})();
