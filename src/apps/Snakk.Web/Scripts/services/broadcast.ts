(function (): void {
    'use strict';

    if (window.SnakkBroadcast) return;

    let _ch: BroadcastChannel | null = null;
    const _handlers = new Map<string, Set<(msg: SnakkBroadcastMessage) => void>>();

    function init(): BroadcastChannel | null {
        if (typeof BroadcastChannel === 'undefined') return null;
        if (_ch) return _ch;
        _ch = new BroadcastChannel('snakk');
        _ch.addEventListener('message', (e: MessageEvent<SnakkBroadcastMessage>) => {
            const msg = e.data;
            if (typeof msg?.type !== 'string') return;
            _handlers.get(msg.type)?.forEach(fn => fn(msg));
        });
        return _ch;
    }

    window.SnakkBroadcast = {
        send(msg: SnakkBroadcastMessage): void {
            init()?.postMessage(msg);
        },
        on<T extends SnakkBroadcastMessage['type']>(
            type: T,
            handler: (msg: Extract<SnakkBroadcastMessage, { type: T }>) => void
        ): () => void {
            init();
            if (!_handlers.has(type)) _handlers.set(type, new Set());
            const h = handler as unknown as (msg: SnakkBroadcastMessage) => void;
            _handlers.get(type)!.add(h);
            return () => { _handlers.get(type)?.delete(h); };
        }
    };
})();
