import { build, context } from 'esbuild';

const options = {
    entryPoints: [
        'Scripts/bundles/head-bundle.ts',
        'Scripts/bundles/app-bundle.ts',
        'Scripts/bundles/auth-bundle.ts',
    ],
    bundle: true,
    format: 'iife',
    outdir: 'wwwroot/js/dist/bundles',
    minify: true,
    target: 'es2020',
};

if (process.argv.includes('--watch')) {
    const ctx = await context(options);
    await ctx.watch();
    console.log('esbuild.core: watching bundle sources...');
} else {
    await build(options);
}
