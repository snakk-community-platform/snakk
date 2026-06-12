import { readdir, readFile, writeFile, rm } from 'fs/promises';
import { join } from 'path';
import { transform } from 'esbuild';

const distDir = './wwwroot/js/dist';

async function minifyDir(dir) {
    const entries = await readdir(dir, { withFileTypes: true });
    for (const entry of entries) {
        const fullPath = join(dir, entry.name);
        if (entry.isDirectory()) {
            await minifyDir(fullPath);
        } else if (entry.name.endsWith('.js') && !entry.name.endsWith('.min.js')) {
            const code = await readFile(fullPath, 'utf-8');
            const result = await transform(code, { minify: true });
            await writeFile(fullPath, result.code);
        }
    }
}

await minifyDir(distDir);

// Remove the dead t.g.js artifact emitted by tsc — no classic (non-bundled) script
// loads it at runtime; esbuild bundles that need it inline the content directly.
for (const f of ['t.g.js', 't.g.js.map']) {
    await rm(join(distDir, 'locales', f), { force: true });
}
