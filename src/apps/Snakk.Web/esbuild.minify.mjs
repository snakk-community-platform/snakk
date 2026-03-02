import { readdir, readFile, writeFile } from 'fs/promises';
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
