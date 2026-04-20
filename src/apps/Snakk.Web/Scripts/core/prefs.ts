(function (): void {
    try {
        if (localStorage.getItem('snakk:disable-previews') === 'true') {
            document.documentElement.classList.add('no-discussion-previews');
        }
        if (localStorage.getItem('snakk:disable-animations') === 'true') {
            document.documentElement.classList.add('no-animations');
        }
    } catch { /* localStorage unavailable */ }
})();
