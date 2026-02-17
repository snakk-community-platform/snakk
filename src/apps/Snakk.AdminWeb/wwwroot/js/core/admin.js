/**
 * Admin Panel Core Functionality
 * Handles alerts, form validation, and common UI interactions
 */
(function() {
    'use strict';

    /**
     * Auto-dismiss alerts after 5 seconds
     */
    function initAlerts() {
        const alerts = document.querySelectorAll('.alert');
        alerts.forEach(alert => {
            setTimeout(() => {
                alert.style.transition = 'opacity 0.3s';
                alert.style.opacity = '0';
                setTimeout(() => alert.remove(), 300);
            }, 5000);
        });
    }

    /**
     * Validate a form
     * @param {HTMLFormElement} form - The form element to validate
     * @returns {boolean} - Whether the form is valid
     */
    function validateForm(form) {
        if (!form) return false;

        const requiredFields = form.querySelectorAll('[required]');
        let isValid = true;

        requiredFields.forEach(field => {
            if (!field.value.trim()) {
                field.classList.add('border-red-500');
                isValid = false;
            } else {
                field.classList.remove('border-red-500');
            }
        });

        return isValid;
    }

    /**
     * Handle form validation on submit
     * @param {HTMLFormElement} form
     * @param {Event} event
     */
    async function handleFormValidation(form, event) {
        const isValid = validateForm(form);
        if (!isValid) {
            event.preventDefault();
            if (window.AdminNotifications) {
                AdminNotifications.error('Please fill in all required fields');
            }
        }
    }

    /**
     * Event delegation for admin actions
     */
    document.addEventListener('click', async (e) => {
        const action = e.target.closest('[data-action]');
        if (!action) return;

        const actionName = action.dataset.action;

        switch (actionName) {
            case 'confirm-delete': {
                e.preventDefault();
                const message = action.dataset.confirmMessage || 'Are you sure you want to delete this item? This action cannot be undone.';

                if (window.AdminNotifications) {
                    const confirmed = await AdminNotifications.showConfirm(message, { type: 'error' });
                    if (confirmed) {
                        // If action has a form, submit it
                        const form = action.closest('form');
                        if (form) {
                            form.submit();
                        } else {
                            // If action has href, follow it
                            const href = action.getAttribute('href');
                            if (href) {
                                window.location.href = href;
                            }
                        }
                    }
                } else {
                    // Fallback to native confirm
                    const confirmed = confirm(message);
                    if (confirmed) {
                        const form = action.closest('form');
                        if (form) {
                            form.submit();
                        } else {
                            const href = action.getAttribute('href');
                            if (href) {
                                window.location.href = href;
                            }
                        }
                    }
                }
                break;
            }

            case 'validate-form': {
                e.preventDefault();
                const form = action.closest('form');
                if (form) {
                    const isValid = validateForm(form);
                    if (isValid) {
                        form.submit();
                    } else if (window.AdminNotifications) {
                        AdminNotifications.error('Please fill in all required fields');
                    }
                }
                break;
            }
        }
    });

    /**
     * Handle form submission with automatic validation
     */
    document.addEventListener('submit', (e) => {
        const form = e.target;

        // Only validate forms with data-validate attribute
        if (form.hasAttribute('data-validate')) {
            handleFormValidation(form, e);
        }
    });

    /**
     * Initialize on DOM ready
     */
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initAlerts);
    } else {
        initAlerts();
    }

    // Re-init alerts after HTMX swaps
    if (typeof htmx !== 'undefined') {
        document.body.addEventListener('htmx:afterSwap', initAlerts);
    }

    // Export utilities for use in Razor pages if needed
    window.AdminCore = {
        validateForm
    };
})();
