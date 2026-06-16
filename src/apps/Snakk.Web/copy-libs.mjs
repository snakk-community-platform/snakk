import { copyFile, mkdir } from 'node:fs/promises';
import { dirname } from 'node:path';

const copies = [
    ['node_modules/htmx.org/dist/htmx.min.js', 'wwwroot/js/vendor/htmx.min.js'],
    // htmx-head-support is patched locally — do NOT copy from node_modules
    ['node_modules/prismjs/themes/prism-tomorrow.min.css', 'wwwroot/css/vendor/prism.css'],
    ['node_modules/dompurify/dist/purify.min.js', 'wwwroot/js/vendor/dompurify.min.js'],
];

await Promise.all(copies.map(async ([src, dest]) => {
    await mkdir(dirname(dest), { recursive: true });
    await copyFile(src, dest);
}));
