import { build } from 'esbuild';

await build({
    entryPoints: ['charts-entry.mjs'],
    bundle: true,
    format: 'iife',
    outfile: 'wwwroot/js/vendor/charts.js',
    minify: true,
    target: 'es2020',
});
