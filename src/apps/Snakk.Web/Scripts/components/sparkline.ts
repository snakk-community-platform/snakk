(function (): void {
    'use strict';

    if ((window as any).SnakkSparkline) return;

    if ((window as any).SnakkSparkline) return;

    interface SparklineDay {
        date: string;
        postCount: number;
        discussionCount: number;
    }

    interface SparklineOptions {
        metric?: 'postCount' | 'discussionCount';
        strokeColor?: string;
        strokeWidth?: number;
        fill?: boolean;
    }

    function renderSvg(container: HTMLElement, data: SparklineDay[], opts: SparklineOptions = {}): void {
        const W = 120;
        const H = 28;
        const metric = opts.metric ?? 'postCount';
        const strokeColor = opts.strokeColor ?? 'var(--color-primary, #6366f1)';
        const strokeWidth = opts.strokeWidth ?? 1.5;
        const useFill = opts.fill ?? true;

        const values = data.map(d => d[metric]);
        const maxVal = Math.max(...values, 1);
        const padX = 2;
        const padY = 3;
        const innerW = W - padX * 2;
        const innerH = H - padY * 2;
        const step = values.length > 1 ? innerW / (values.length - 1) : innerW;

        const pts = values.map((v, i) => {
            const x = padX + i * step;
            const y = padY + innerH - (v / maxVal) * innerH;
            return `${x.toFixed(1)},${y.toFixed(1)}`;
        });

        const polylinePoints = pts.join(' ');
        let inner = `<polyline points="${polylinePoints}" fill="none" stroke="${strokeColor}" stroke-width="${strokeWidth}" stroke-linecap="round" stroke-linejoin="round"/>`;

        if (useFill && pts.length > 1) {
            const baseline = (padY + innerH).toFixed(1);
            const left = `${padX.toFixed(1)},${baseline}`;
            const right = `${(padX + (values.length - 1) * step).toFixed(1)},${baseline}`;
            inner += `<polygon points="${left} ${polylinePoints} ${right}" fill="${strokeColor}" fill-opacity="0.12" stroke="none"/>`;
        }

        container.innerHTML = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${W} ${H}" width="${W}" height="${H}" preserveAspectRatio="none" class="sn-sparkline-svg">${inner}</svg>`;
    }

    async function fetchAndRender(container: HTMLElement): Promise<void> {
        container.setAttribute('data-sparkline-loaded', '');
        const metric = (container.dataset['sparklineMetric'] ?? 'postCount') as 'postCount' | 'discussionCount';

        // DOM-delivered path — data embedded in the attribute, no fetch needed
        const inlineData = container.dataset['sparklineData'];
        if (inlineData) {
            try {
                const data = JSON.parse(inlineData) as SparklineDay[];
                if (data.length > 0) {
                    renderSvg(container, data, { metric });
                    container.classList.add('sn-sparkline--loaded');
                }
            } catch { }
            return;
        }

        // Async fetch path
        const entityType = container.dataset['sparklineType'];
        const entityId = container.dataset['sparklineId'] ?? '';
        const days = parseInt(container.dataset['sparklineDays'] ?? '7', 10);

        if (!entityType) return;
        if (entityType !== 'platform' && !entityId) return;

        try {
            const url = `/bff/activity/sparkline?entityType=${encodeURIComponent(entityType)}&entityId=${encodeURIComponent(entityId)}&days=${days}`;
            const resp = await fetch(url);
            if (!resp.ok) return;
            const data: SparklineDay[] = await resp.json() as SparklineDay[];
            if (data.length > 0) {
                renderSvg(container, data, { metric });
                container.classList.add('sn-sparkline--loaded');
            }
        } catch {
            // sparklines are non-critical; ignore fetch/parse errors
        }
    }

    function initSparklines(): void {
        document.querySelectorAll<HTMLElement>(
            '[data-sparkline-type]:not([data-sparkline-loaded]),' +
            '[data-sparkline-data]:not([data-sparkline-loaded])'
        ).forEach(c => {
            void fetchAndRender(c);
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initSparklines);
    } else {
        initSparklines();
    }
    document.addEventListener('htmx:afterSwap', initSparklines);

    (window as any).SnakkSparkline = { render: renderSvg, init: initSparklines };
})();
