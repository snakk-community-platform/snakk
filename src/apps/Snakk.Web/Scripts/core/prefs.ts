(function (): void {
    try {
        const listLayout = localStorage.getItem('snakk:discussion-list-layout');
        if (listLayout === 'none') {
            document.documentElement.classList.add('no-discussion-previews');
        } else if (listLayout === 'compact') {
            document.documentElement.classList.add('discussion-list-compact');
        }
        if (localStorage.getItem('snakk:disable-animations') === 'true') {
            document.documentElement.classList.add('no-animations');
        }
        if (localStorage.getItem('snakk:link-preview-compact') === 'true') {
            document.documentElement.classList.add('link-preview-compact');
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
