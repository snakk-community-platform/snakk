/**
 * Snakk Chart Renderer
 * Lazy-loads Chart.js and renders chart fenced blocks embedded in posts.
 */

(function (): void {
    'use strict';

    if ((window as any).SnakkCharts) return;

    const CHARTS_VERSION = '4.4.9';
    let loadPromise: Promise<any> | null = null;

    const COLORS = [
        'rgba(99,102,241,0.8)',
        'rgba(16,185,129,0.8)',
        'rgba(245,158,11,0.8)',
        'rgba(239,68,68,0.8)',
        'rgba(59,130,246,0.8)',
        'rgba(168,85,247,0.8)',
        'rgba(20,184,166,0.8)',
        'rgba(249,115,22,0.8)',
        'rgba(236,72,153,0.8)',
        'rgba(132,204,22,0.8)',
    ];

    interface SeriesItem {
        name: string;
        data: number[] | Array<{ x: number; y: number }>;
    }

    interface ChartConfig {
        type: string;
        title?: string;
        labels?: string[];
        data?: Record<string, number>;
        series?: SeriesItem[];
    }

    function parseStringArray(str: string): string[] {
        return str.replace(/^\[|\]$/g, '').split(',').map(s => s.trim()).filter(Boolean);
    }

    function parseNumericArray(str: string): number[] | Array<{ x: number; y: number }> {
        const clean = str.replace(/^\[|\]$/g, '').trim();
        if (!clean) return [];

        if (clean.startsWith('[')) {
            // Nested arrays: [[x, y], ...] — scatter data
            const result: Array<{ x: number; y: number }> = [];
            let depth = 0, start = 0;
            for (let i = 0; i <= clean.length; i++) {
                const ch = clean[i];
                if (ch === '[') { if (depth === 0) start = i; depth++; }
                else if (ch === ']') {
                    depth--;
                    if (depth === 0) {
                        const inner = clean.slice(start + 1, i).split(',');
                        if (inner.length >= 2) {
                            result.push({ x: parseFloat(inner[0]!), y: parseFloat(inner[1]!) });
                        }
                    }
                }
            }
            return result;
        }

        return clean.split(',').map(s => parseFloat(s.trim())).filter(n => !isNaN(n));
    }

    function parseConfig(raw: string): ChartConfig | null {
        const lines = raw.split('\n');
        const config: any = {};
        let i = 0;

        while (i < lines.length) {
            const line = lines[i]!;
            const trimmed = line.trim();

            if (!trimmed || trimmed.startsWith('#')) { i++; continue; }

            const colonIdx = trimmed.indexOf(':');
            if (colonIdx === -1) { i++; continue; }

            const key = trimmed.slice(0, colonIdx).trim().toLowerCase();
            const value = trimmed.slice(colonIdx + 1).trim();

            if (key === 'data' && !value) {
                const dataMap: Record<string, number> = {};
                i++;
                while (i < lines.length) {
                    const dl = lines[i]!;
                    if (!dl.startsWith(' ') && !dl.startsWith('\t')) break;
                    const dt = dl.trim();
                    if (!dt) { i++; continue; }
                    const dc = dt.indexOf(':');
                    if (dc >= 0) {
                        const label = dt.slice(0, dc).trim();
                        const val = parseFloat(dt.slice(dc + 1).trim());
                        if (label && !isNaN(val)) dataMap[label] = val;
                    }
                    i++;
                }
                config.data = dataMap;
                continue;
            }

            if (key === 'series' && !value) {
                const series: SeriesItem[] = [];
                i++;
                let current: SeriesItem | null = null;
                while (i < lines.length) {
                    const sl = lines[i]!;
                    if (!sl.trim()) { i++; continue; }

                    const nameMatch = sl.match(/^\s*-\s+name:\s*(.+)/);
                    if (nameMatch) {
                        current = { name: nameMatch[1]!.trim(), data: [] };
                        series.push(current);
                        i++;
                        continue;
                    }

                    const dataMatch = sl.match(/^\s+data:\s*(\[.+\])/);
                    if (dataMatch && current) {
                        current.data = parseNumericArray(dataMatch[1]!) as any;
                        i++;
                        continue;
                    }

                    if (!sl.startsWith(' ') && !sl.startsWith('\t') && !sl.trim().startsWith('-')) break;
                    i++;
                }
                config.series = series;
                continue;
            }

            if (key === 'labels') {
                config.labels = parseStringArray(value);
            } else {
                config[key] = value;
            }
            i++;
        }

        return config as ChartConfig;
    }

    function buildChartJsConfig(parsed: ChartConfig): any {
        const type = parsed.type?.toLowerCase() || 'bar';
        const isDark = document.documentElement.getAttribute('data-theme') === 'dark';
        const textColor = isDark ? 'rgba(255,255,255,0.65)' : 'rgba(0,0,0,0.65)';
        const gridColor = isDark ? 'rgba(255,255,255,0.1)' : 'rgba(0,0,0,0.08)';

        const commonOptions: any = {
            responsive: true,
            aspectRatio: 3,
            animation: { duration: document.documentElement.classList.contains('sn-no-animations') ? 0 : 380 },
            plugins: {
                title: parsed.title
                    ? { display: true, text: parsed.title, color: textColor, font: { size: 14 } }
                    : { display: false },
                legend: { labels: { color: textColor } },
            },
        };

        const xyScales = {
            x: { ticks: { color: textColor }, grid: { color: gridColor } },
            y: { ticks: { color: textColor }, grid: { color: gridColor } },
        };

        if (type === 'pie' || type === 'donut' || type === 'doughnut') {
            const labels = Object.keys(parsed.data || {});
            const values = Object.values(parsed.data || {});
            return {
                type: type === 'donut' ? 'doughnut' : 'pie',
                data: {
                    labels,
                    datasets: [{
                        data: values,
                        backgroundColor: COLORS.slice(0, values.length),
                        borderWidth: 2,
                    }],
                },
                options: { ...commonOptions, aspectRatio: 2 },
            };
        }

        if (type === 'scatter') {
            return {
                type: 'scatter',
                data: {
                    datasets: (parsed.series || []).map((s, idx) => ({
                        label: s.name,
                        data: s.data,
                        backgroundColor: COLORS[idx % COLORS.length]!,
                    })),
                },
                options: { ...commonOptions, scales: xyScales },
            };
        }

        const isArea = type === 'area';
        const isStacked = type === 'stacked';
        const chartType = isArea ? 'line' : isStacked ? 'bar' : type;

        return {
            type: chartType,
            data: {
                labels: parsed.labels || [],
                datasets: (parsed.series || []).map((s, idx) => {
                    const color = COLORS[idx % COLORS.length]!;
                    const solid = color.replace('0.8)', '1)');
                    return {
                        label: s.name,
                        data: s.data,
                        backgroundColor: isArea ? color.replace('0.8)', '0.25)') : color,
                        borderColor: solid,
                        fill: isArea,
                        tension: (isArea || type === 'line') ? 0.3 : 0,
                    };
                }),
            },
            options: {
                ...commonOptions,
                scales: {
                    x: { ...xyScales.x, stacked: isStacked },
                    y: { ...xyScales.y, stacked: isStacked },
                },
            },
        };
    }

    function loadScript(src: string): Promise<void> {
        return new Promise((resolve, reject) => {
            if (document.querySelector(`script[src="${src}"]`)) { resolve(); return; }
            const script = document.createElement('script');
            script.src = src;
            script.onload = () => resolve();
            script.onerror = () => reject(new Error(`Failed to load ${src}`));
            document.head.appendChild(script);
        });
    }

    function loadChartJs(): Promise<any> {
        if (!loadPromise) {
            loadPromise = (async () => {
                try {
                    await loadScript(`/js/vendor/charts.js?v=${CHARTS_VERSION}`);
                    const Chart = (window as any).Chart;
                    if (!Chart) { console.error('Chart.js not found on window.Chart'); return null; }
                    return Chart;
                } catch (e) {
                    console.error('Failed to load Chart.js:', e);
                    loadPromise = null;
                    return null;
                }
            })();
        }
        return loadPromise;
    }

    async function renderAll(container?: HTMLElement): Promise<void> {
        const root = container || document;
        const els = root.querySelectorAll<HTMLElement>('.sn-snakk-chart[data-chart-config]:not([data-chart-rendered])');
        if (!els.length) return;

        const Chart = await loadChartJs();
        if (!Chart) return;

        els.forEach(div => {
            const canvas = div.querySelector('canvas');
            if (!canvas) return;

            const encoded = div.getAttribute('data-chart-config') || '';
            let raw = '';
            try {
                raw = new TextDecoder().decode(Uint8Array.from(atob(encoded), c => c.charCodeAt(0)));
            } catch {
                return;
            }

            const parsed = parseConfig(raw);
            if (!parsed?.type) return;

            try {
                const existing = Chart.getChart(canvas);
                if (existing) existing.destroy();
                new Chart(canvas, buildChartJsConfig(parsed));
                div.setAttribute('data-chart-rendered', 'true');
            } catch (e) {
                console.warn('Chart render failed', e);
            }
        });
    }

    async function renderIntoCanvas(canvas: HTMLCanvasElement, rawText: string): Promise<void> {
        const Chart = await loadChartJs();
        if (!Chart) return;
        const parsed = parseConfig(rawText);
        if (!parsed?.type) return;
        try {
            const existing = Chart.getChart(canvas);
            if (existing) existing.destroy();
            new Chart(canvas, buildChartJsConfig(parsed));
        } catch (e) {
            console.warn('Chart preview render failed', e);
        }
    }

    function destroyCanvas(canvas: HTMLCanvasElement): void {
        const instance = (window as any).Chart?.getChart?.(canvas);
        if (instance) instance.destroy();
    }

    (window as any).SnakkCharts = { renderAll, renderIntoCanvas, destroyCanvas };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => renderAll());
    } else {
        renderAll();
    }
    document.body.addEventListener('htmx:load', () => renderAll());
    document.body.addEventListener('htmx:historyRestore', () => renderAll());
})();
