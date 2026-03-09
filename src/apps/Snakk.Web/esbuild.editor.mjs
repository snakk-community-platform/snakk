import { build } from 'esbuild';

await build({
    entryPoints: ['Scripts/components/markdown-editor.ts'],
    bundle: true,
    format: 'iife',
    outfile: 'wwwroot/js/dist/components/markdown-editor.js',
    minify: true,
    sourcemap: true,
    target: 'es2020',
});
