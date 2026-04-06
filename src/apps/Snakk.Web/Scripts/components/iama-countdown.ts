/**
 * IAMA Countdown — ticks down to the target UTC time specified in data-countdown-to
 * and writes the remaining days/hours/minutes/seconds to child <span> elements
 * using daisyUI's countdown component (--value CSS variable).
 */
(function () {
    'use strict';

    interface CountdownUnits {
        root: HTMLElement;
        targetMs: number;
        days: HTMLElement | null;
        hours: HTMLElement | null;
        minutes: HTMLElement | null;
        seconds: HTMLElement | null;
    }

    const countdowns: CountdownUnits[] = [];
    let tickHandle: number | null = null;

    function parseTarget(el: HTMLElement): number | null {
        const raw = el.dataset.countdownTo;
        if (!raw) return null;
        const ms = Date.parse(raw);
        return Number.isFinite(ms) ? ms : null;
    }

    function setValue(el: HTMLElement | null, value: number): void {
        if (!el) return;
        el.style.setProperty('--value', String(value));
        el.textContent = String(value);
    }

    function renderCountdown(c: CountdownUnits, nowMs: number): boolean {
        const diffMs = c.targetMs - nowMs;
        if (diffMs <= 0) {
            setValue(c.days, 0);
            setValue(c.hours, 0);
            setValue(c.minutes, 0);
            setValue(c.seconds, 0);
            c.root.classList.add('fp-iama-countdown--expired');
            return false; // expired, stop ticking this one
        }

        const totalSec = Math.floor(diffMs / 1000);
        const days = Math.floor(totalSec / 86400);
        const hours = Math.floor((totalSec % 86400) / 3600);
        const minutes = Math.floor((totalSec % 3600) / 60);
        const seconds = totalSec % 60;

        setValue(c.days, days);
        setValue(c.hours, hours);
        setValue(c.minutes, minutes);
        setValue(c.seconds, seconds);
        return true;
    }

    function tick(): void {
        const nowMs = Date.now();
        for (let i = countdowns.length - 1; i >= 0; i--) {
            const entry = countdowns[i];
            if (!entry) continue;
            const stillTicking = renderCountdown(entry, nowMs);
            if (!stillTicking) countdowns.splice(i, 1);
        }
        if (countdowns.length === 0 && tickHandle !== null) {
            window.clearInterval(tickHandle);
            tickHandle = null;
        }
    }

    function register(root: HTMLElement): void {
        if (root.dataset.countdownInitialized === '1') return;
        const targetMs = parseTarget(root);
        if (targetMs === null) return;

        const entry: CountdownUnits = {
            root,
            targetMs,
            days: root.querySelector<HTMLElement>('[data-unit="days"]'),
            hours: root.querySelector<HTMLElement>('[data-unit="hours"]'),
            minutes: root.querySelector<HTMLElement>('[data-unit="minutes"]'),
            seconds: root.querySelector<HTMLElement>('[data-unit="seconds"]'),
        };

        root.dataset.countdownInitialized = '1';
        // Render once immediately, then add to the tick loop if still running.
        const nowMs = Date.now();
        if (renderCountdown(entry, nowMs)) {
            countdowns.push(entry);
            if (tickHandle === null) {
                tickHandle = window.setInterval(tick, 1000);
            }
        }
    }

    function scan(scope: ParentNode | Document = document): void {
        scope.querySelectorAll<HTMLElement>('[data-countdown-to]').forEach(register);
    }

    // Initial scan on load
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => scan());
    } else {
        scan();
    }

    // Rescan after htmx swaps so dynamically loaded cards get countdowns too.
    document.body.addEventListener('htmx:afterSettle', (e) => {
        const target = (e as CustomEvent).detail?.target as ParentNode | undefined;
        scan(target ?? document);
    });
})();
