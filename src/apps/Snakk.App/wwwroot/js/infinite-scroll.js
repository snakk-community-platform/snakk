window.lazyImage = (() => {
    const refs = new WeakMap();
    const observer = new IntersectionObserver(entries => {
        entries.forEach(e => {
            if (!e.isIntersecting) return;
            refs.get(e.target)?.invokeMethodAsync('OnImageVisible');
            observer.unobserve(e.target);
            refs.delete(e.target);
        });
    }, { rootMargin: '300px' });
    return {
        observe(el, dotNetRef) { refs.set(el, dotNetRef); observer.observe(el); },
        unobserve(el) { observer.unobserve(el); refs.delete(el); }
    };
})();

window.infiniteScroll = {
    _observer: null,
    observe(sentinel, dotNetRef) {
        if (this._observer) this._observer.disconnect();
        this._observer = new IntersectionObserver(entries => {
            if (entries[0].isIntersecting)
                dotNetRef.invokeMethodAsync('LoadMoreAsync');
        }, { threshold: 0.1 });
        this._observer.observe(sentinel);
    },
    disconnect() {
        if (this._observer) { this._observer.disconnect(); this._observer = null; }
    }
};
