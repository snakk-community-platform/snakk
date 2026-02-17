/**
 * Event delegation for AdminWeb actions
 * Handles all [data-action] elements
 */
(function() {
    'use strict';

    const actions = new Map();

    /**
     * Register an action handler
     * @param {string} name - Action name
     * @param {Function} handler - Handler function
     */
    function registerAction(name, handler) {
        actions.set(name, handler);
    }

    /**
     * Unregister an action handler
     * @param {string} name - Action name
     */
    function unregisterAction(name) {
        actions.delete(name);
    }

    /**
     * Handle action click
     * @param {Element} element - The element with data-action
     */
    async function handleAction(element) {
        const actionName = element.dataset.action;
        const handler = actions.get(actionName);

        if (!handler) {
            console.warn(`No handler registered for action: ${actionName}`);
            return;
        }

        // Disable button during execution
        const originalText = element.innerHTML;
        const isButton = element.tagName === 'BUTTON';

        if (isButton) {
            element.disabled = true;
            if (element.dataset.loadingText) {
                element.innerHTML = element.dataset.loadingText;
            }
        }

        try {
            await handler(element);
        } catch (error) {
            console.error(`Error handling action ${actionName}:`, error);
            if (window.AdminNotifications) {
                window.AdminNotifications.error('An error occurred. Please try again.');
            }
        } finally {
            if (isButton) {
                element.disabled = false;
                element.innerHTML = originalText;
            }
        }
    }

    /**
     * Handle form submit with data-action
     * @param {HTMLFormElement} form - The form element
     * @param {Event} event - Submit event
     */
    async function handleFormAction(form, event) {
        event.preventDefault();

        const actionName = form.dataset.action;
        const handler = actions.get(actionName);

        if (!handler) {
            console.warn(`No handler registered for form action: ${actionName}`);
            return;
        }

        const submitButton = form.querySelector('[type="submit"]');
        const originalText = submitButton?.innerHTML;

        if (submitButton) {
            submitButton.disabled = true;
            if (submitButton.dataset.loadingText) {
                submitButton.innerHTML = submitButton.dataset.loadingText;
            }
        }

        try {
            const formData = new FormData(form);
            await handler(form, formData);
        } catch (error) {
            console.error(`Error handling form action ${actionName}:`, error);
            if (window.AdminNotifications) {
                window.AdminNotifications.error('An error occurred. Please try again.');
            }
        } finally {
            if (submitButton) {
                submitButton.disabled = false;
                submitButton.innerHTML = originalText;
            }
        }
    }

    // Event delegation for clicks
    document.addEventListener('click', (event) => {
        const actionElement = event.target.closest('[data-action]');

        if (actionElement && actionElement.tagName !== 'FORM') {
            event.preventDefault();
            handleAction(actionElement);
        }
    });

    // Event delegation for form submits
    document.addEventListener('submit', (event) => {
        const form = event.target;

        if (form.dataset.action) {
            handleFormAction(form, event);
        }
    });

    // Export to global namespace
    window.AdminActions = {
        register: registerAction,
        unregister: unregisterAction
    };

    // Dispatch event when actions system is ready
    document.dispatchEvent(new CustomEvent('admin:actions:ready'));
})();
