(function () {
    'use strict';

    var observer = null;

    function init() {
        if (observer) {
            observer.disconnect();
            observer = null;
        }
        var fab = document.getElementById('new-discussion-fab');
        var target = document.getElementById('rp-new-discussion');
        if (!fab || !target || !('IntersectionObserver' in window)) return;

        var intersecting = true;
        function update() {
            var root = document.documentElement;
            var sticky = root.classList.contains('sticky-none') || root.classList.contains('sticky-left-only');
            fab.classList.toggle('fab-collapsed', intersecting || !sticky);
        }
        observer = new IntersectionObserver(function (entries) {
            intersecting = entries[0].isIntersecting;
            update();
        });
        observer.observe(target);
        window.addEventListener('snakk:sticky-changed', update);
        update();
    }

    document.addEventListener('DOMContentLoaded', init);
    document.addEventListener('htmx:afterSettle', init);
})();
