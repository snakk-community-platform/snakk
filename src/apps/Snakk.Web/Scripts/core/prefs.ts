(function (): void {
    try {
        if (localStorage.getItem('snakk:disable-previews') === 'true') {
            document.documentElement.classList.add('no-discussion-previews');
        }
        if (localStorage.getItem('snakk:disable-animations') === 'true') {
            document.documentElement.classList.add('no-animations');
        }
        const sticky = localStorage.getItem('snakk:sidebar-sticky');
        if (sticky === 'none') {
            document.documentElement.classList.add('sticky-none');
        } else if (sticky === 'both') {
            // no class — both sidebars stick
        } else {
            document.documentElement.classList.add('sticky-left-only');
        }
    } catch { /* localStorage unavailable */ }
})();
