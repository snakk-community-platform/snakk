/**
 * Upload helper with progress events.
 * Uses XMLHttpRequest instead of fetch() so the UI can track upload progress
 * (fetch can't report request-body progress). The fetch interceptor in
 * core/auth.ts is bypassed by XHR, so this helper must attach CSRF headers
 * manually — matches what the interceptor does for /bff/ POSTs.
 */

interface UploadOptions {
    url: string;
    formData: FormData;
    onProgress?: (percent: number, loaded: number, total: number) => void;
    onProcessing?: () => void;
    signal?: AbortSignal;
}

interface UploadResult<T = unknown> {
    ok: boolean;
    status: number;
    data?: T;
    error?: string;
}

interface SnakkUploadAPI {
    uploadWithProgress<T = unknown>(opts: UploadOptions): Promise<UploadResult<T>>;
}

(function (): void {
    'use strict';

    function getCSRFToken(): string | null {
        const snakkAuth = (window as any).SnakkAuth;
        if (snakkAuth?.getCSRFToken) return snakkAuth.getCSRFToken();
        const meta = document.querySelector<HTMLMetaElement>('meta[name="csrf-token"]');
        return meta?.getAttribute('content') ?? null;
    }

    function uploadWithProgress<T = unknown>(opts: UploadOptions): Promise<UploadResult<T>> {
        return new Promise((resolve) => {
            const xhr = new XMLHttpRequest();
            xhr.open('POST', opts.url, true);
            xhr.withCredentials = true;

            const csrf = getCSRFToken();
            if (csrf) {
                xhr.setRequestHeader('RequestVerificationToken', csrf);
                xhr.setRequestHeader('X-CSRF-TOKEN', csrf);
            }

            let processingFired = false;

            xhr.upload.onprogress = (e: ProgressEvent) => {
                if (!e.lengthComputable || !opts.onProgress) return;
                const percent = e.total > 0 ? Math.floor((e.loaded / e.total) * 100) : 0;
                opts.onProgress(percent, e.loaded, e.total);
            };

            // onloadend fires once the browser has finished shipping bytes but the
            // server is still processing (BFF → internal API → disk). Switch UI to
            // "processing…" here instead of leaving the bar pinned at 100%.
            xhr.upload.onloadend = () => {
                if (!processingFired) {
                    processingFired = true;
                    opts.onProcessing?.();
                }
            };

            xhr.onload = () => {
                const ok = xhr.status >= 200 && xhr.status < 300;
                const text = xhr.responseText;
                const ctype = xhr.getResponseHeader('content-type') || '';
                let data: T | undefined;
                let error: string | undefined;

                if (ctype.includes('application/json') && text) {
                    try {
                        data = JSON.parse(text) as T;
                    } catch {
                        /* fall through */
                    }
                }
                if (!ok) {
                    error = (data as any)?.error || xhr.statusText || `Upload failed: ${xhr.status}`;
                }
                resolve({ ok, status: xhr.status, data, error });
            };

            xhr.onerror = () => resolve({ ok: false, status: 0, error: 'Network error' });
            xhr.onabort = () => resolve({ ok: false, status: 0, error: 'Upload aborted' });

            if (opts.signal) {
                if (opts.signal.aborted) {
                    resolve({ ok: false, status: 0, error: 'Upload aborted' });
                    return;
                }
                opts.signal.addEventListener('abort', () => xhr.abort(), { once: true });
            }

            xhr.send(opts.formData);
        });
    }

    const SnakkUpload: SnakkUploadAPI = { uploadWithProgress };
    (window as any).SnakkUpload = SnakkUpload;
})();
