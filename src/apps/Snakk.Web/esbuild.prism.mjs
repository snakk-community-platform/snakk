import { build } from 'esbuild';

await build({
    entryPoints: ['prism-entry.mjs'],
    bundle: true,
    format: 'iife',
    outfile: 'wwwroot/js/vendor/prism.js',
    minify: true,
    target: 'es2020',
});
