/**
 * Global Event Delegation System
 *
 * Replaces inline onclick/onchange/onsubmit handlers with data-attribute-based
 * delegation, enabling a strict CSP without 'unsafe-inline' in script-src.
 *
 * HTML patterns:
 *   <button data-action="reply-to-post" data-post-id="abc" data-author-name="Jo">
 *   <button data-modal-open="ban-modal">
 *   <button data-modal-close="ban-modal">
 *   <select data-change-action="toggle-storage">
 *   <form data-submit-action="submit-report">
 *   <textarea data-input-action="auto-grow">
 *
 * Registration (from page scripts):
 *   window.SnakkActions.on('reply-to-post', (el, e) => { ... });
 */
(function () {
    'use strict';

    // --- populate window globals from <meta> tags ---------------------------
    // Replaces the inline <script> blocks that _Layout.cshtml used to emit.
    // All existing consumers (realtime.ts, utils.ts, etc.) keep reading window.*.
    const meta = (name: string) => document.querySelector<HTMLMetaElement>(`meta[name="${name}"]`)?.content;
    (window as any).realtimeServiceUrl = meta('realtime-service-url');
    (window as any).snakkTimezone = meta('snakk-timezone');
    (window as any).currentUserId = meta('current-user-id');

    type ActionHandler = (element: HTMLElement, event: Event) => void;
    const handlers: Record<string, ActionHandler> = {};

    const api = {
        on(action: string, handler: ActionHandler): void {
            handlers[action] = handler;
        },
        onAll(map: Record<string, ActionHandler>): void {
            for (const key in map) {
                if (Object.prototype.hasOwnProperty.call(map, key)) {
                    handlers[key] = map[key]!;
                }
            }
        }
    };

    (window as any).SnakkActions = api;

    // --- click delegation ---------------------------------------------------
    document.addEventListener('click', (e: Event) => {
        const target = e.target as HTMLElement;

        // data-action
        const actionEl = target.closest<HTMLElement>('[data-action]');
        if (actionEl) {
            const name = actionEl.dataset.action!;
            // Built-in actions
            if (name === 'go-back') { history.back(); return; }

            const handler = handlers[name];
            if (handler) {
                handler(actionEl, e);
                return;
            }
        }

        // data-modal-open  (opens a <dialog> by ID)
        const openEl = target.closest<HTMLElement>('[data-modal-open]');
        if (openEl) {
            const modal = document.getElementById(openEl.dataset.modalOpen!) as HTMLDialogElement | null;
            modal?.showModal();
            return;
        }

        // data-modal-close (closes a <dialog> by ID)
        const closeEl = target.closest<HTMLElement>('[data-modal-close]');
        if (closeEl) {
            const modal = document.getElementById(closeEl.dataset.modalClose!) as HTMLDialogElement | null;
            modal?.close();
            return;
        }

        // data-stop-propagation  (replaces onclick="event.stopPropagation()")
        if (target.closest('[data-stop-propagation]')) {
            e.stopPropagation();
        }
    });

    // --- change delegation --------------------------------------------------
    document.addEventListener('change', (e: Event) => {
        const el = (e.target as HTMLElement).closest<HTMLElement>('[data-change-action]');
        if (el) {
            const handler = handlers[el.dataset.changeAction!];
            if (handler) handler(el, e);
        }
    });

    // --- input delegation ---------------------------------------------------
    document.addEventListener('input', (e: Event) => {
        const el = (e.target as HTMLElement).closest<HTMLElement>('[data-input-action]');
        if (el) {
            const handler = handlers[el.dataset.inputAction!];
            if (handler) handler(el, e);
        }
    });

    // --- submit delegation --------------------------------------------------
    document.addEventListener('submit', (e: Event) => {
        const form = (e.target as HTMLElement).closest<HTMLElement>('[data-submit-action]');
        if (!form) return;

        // Built-in: if the form has data-confirm-message, show a confirm dialog first
        const confirmMsg = form.dataset.confirmMessage;
        if (confirmMsg && !confirm(confirmMsg)) {
            e.preventDefault();
            return;
        }

        const handler = handlers[form.dataset.submitAction!];
        if (handler) handler(form, e);
    });

    // --- image blur-up load handler -----------------------------------------
    // For images with data-blur-up: add 'images-loaded' class on load/error.
    // Runs on DOMContentLoaded and observes for dynamically added images.
    function attachBlurUp(img: HTMLImageElement): void {
        const done = () => {
            img.classList.add('images-loaded');
            img.parentElement?.classList.add('images-item-loaded');
        };
        if (img.complete) done();
        else {
            img.addEventListener('load', done);
            img.addEventListener('error', done);
        }
    }

    function initBlurUp(): void {
        document.querySelectorAll<HTMLImageElement>('img[data-blur-up]').forEach(attachBlurUp);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initBlurUp);
    } else {
        initBlurUp();
    }

    // Re-run after HTMX swaps (new content may contain blur-up images)
    document.addEventListener('htmx:afterSettle', () => {
        document.querySelectorAll<HTMLImageElement>('img[data-blur-up]:not(.images-loaded)').forEach(attachBlurUp);
    });

    // ========================================================================
    // Double-submit protection — disable submit buttons on form submit
    // ========================================================================

    document.addEventListener('submit', (e) => {
        const form = e.target as HTMLFormElement;
        if (!form || form.dataset.allowResubmit) return;

        const buttons = form.querySelectorAll<HTMLButtonElement>('button[type="submit"], input[type="submit"]');
        buttons.forEach(btn => {
            if (btn.disabled) return;
            btn.disabled = true;
            btn.dataset.originalText = btn.textContent || '';
            btn.textContent = btn.dataset.submittingText || 'Submitting...';
        });

        // Re-enable after 8 seconds (safety net for failed submissions)
        setTimeout(() => {
            buttons.forEach(btn => {
                btn.disabled = false;
                if (btn.dataset.originalText) btn.textContent = btn.dataset.originalText;
            });
        }, 8000);
    });
})();
