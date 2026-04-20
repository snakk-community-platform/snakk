(function (): void {
    'use strict';

    interface SqlTiming {
        commandString?: string;
        durationMilliseconds?: number;
    }

    interface Timing {
        durationMilliseconds?: number;
        children?: Timing[];
        customTimings?: { sql?: SqlTiming[] };
    }

    interface ProfileResult {
        name?: string;
        durationMilliseconds?: number;
        root?: Timing;
    }

    function collectSql(timing: Timing | undefined): SqlTiming[] {
        if (!timing) return [];
        const sql = timing.customTimings?.sql ?? [];
        const children = timing.children ?? [];
        return [...sql, ...children.flatMap(collectSql)];
    }

    async function fetchAndLog(id: string, label: string): Promise<void> {
        try {
            const resp = await fetch(`/bff/profiler/${id}`);
            if (!resp.ok) return;
            const data = await resp.json() as ProfileResult;
            const sql = collectSql(data.root);
            if (sql.length === 0) return;

            const total = data.durationMilliseconds?.toFixed(1) ?? '?';
            const count = sql.length;
            const word = count === 1 ? 'query' : 'queries';
            console.groupCollapsed(
                `%c[profiler]%c ${label} %c— ${count} ${word}, ${total}ms`,
                'color:#6c757d;font-weight:normal',
                'color:inherit',
                'color:#6c757d;font-weight:normal'
            );
            const cap = Math.min(sql.length, 5);
            for (let i = 0; i < cap; i++) {
                const q = sql[i];
                const isLast = i === cap - 1 && sql.length <= 5;
                const prefix = isLast ? '└' : '├';
                const cmd = q?.commandString?.split('\n')[0] ?? '(unknown)';
                const dur = q?.durationMilliseconds?.toFixed(1) ?? '?';
                console.debug(`  ${prefix} ${cmd} — ${dur}ms`);
            }
            if (sql.length > 5) {
                console.debug(`  └ … and ${sql.length - 5} more`);
            }
            console.groupEnd();
        } catch {
            // ignore
        }
    }

    function handleIds(ids: string, label: string): void {
        ids.split(',')
            .map(id => id.trim())
            .filter(id => id.length > 0)
            .forEach(id => { void fetchAndLog(id, label); });
    }

    // Page load: IDs injected by server into <meta name="snakk-profiler-ids">
    const metaIds = document.querySelector('meta[name="snakk-profiler-ids"]')?.getAttribute('content');
    if (metaIds) {
        handleIds(metaIds, document.title || location.pathname);
    }

    // HTMX partial requests
    document.addEventListener('htmx:afterRequest', (evt) => {
        const detail = (evt as CustomEvent).detail as {
            xhr?: XMLHttpRequest;
            pathInfo?: { requestPath?: string };
        };
        const ids = detail.xhr?.getResponseHeader('X-MiniProfiler-Ids');
        if (ids) {
            handleIds(ids, detail.pathInfo?.requestPath ?? 'htmx');
        }
    });

    // Fetch requests (JS-driven BFF calls)
    const originalFetch = window.fetch.bind(window) as typeof window.fetch;
    window.fetch = async function (input: RequestInfo | URL, init?: RequestInit): Promise<Response> {
        const response = await originalFetch(input, init);
        const ids = response.headers.get('X-MiniProfiler-Ids');
        if (ids) {
            const url = typeof input === 'string'
                ? input
                : input instanceof URL
                    ? input.href
                    : (input as Request).url;
            handleIds(ids, url);
        }
        return response;
    };
})();
