import fs from 'fs';
import path from 'path';

function processScssFile(filePath) {
    const original = fs.readFileSync(filePath, 'utf8');
    let content = original;

    // Remove standalone @layer declaration lines (e.g. "@layer sn-reset, sn-base, ...;")
    content = content.replace(/@layer\s+sn-[\w,\s]+;\r?\n?/g, '');

    const lines = content.split('\n');
    const result = [];
    let i = 0;

    while (i < lines.length) {
        const line = lines[i];
        const trimmed = line.trim();

        if (/^@layer\s+sn-\w+\s*\{/.test(trimmed)) {
            // Skip this @layer opening line; depth starts at 1
            let depth = 1;
            i++;

            while (i < lines.length && depth > 0) {
                const inner = lines[i];
                const opens  = (inner.match(/\{/g) || []).length;
                const closes = (inner.match(/\}/g) || []).length;
                depth += opens - closes;

                if (depth === 0) {
                    // Closing brace of the @layer block — discard it
                } else {
                    // Strip one level of indentation (4 spaces)
                    result.push(inner.replace(/^    /, ''));
                }
                i++;
            }

            // Ensure one blank line after each removed layer block
            if (result.length > 0 && result[result.length - 1] !== '') {
                result.push('');
            }
        } else {
            result.push(line);
            i++;
        }
    }

    // Collapse 3+ consecutive blank lines to 2
    let finalContent = result.join('\n').replace(/\n{3,}/g, '\n\n');

    if (finalContent !== original) {
        fs.writeFileSync(filePath, finalContent, 'utf8');
        return true;
    }
    return false;
}

function findScssFiles(dir) {
    const files = [];
    for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
        const full = path.join(dir, entry.name);
        if (entry.isDirectory()) files.push(...findScssFiles(full));
        else if (entry.name.endsWith('.scss')) files.push(full);
    }
    return files;
}

const stylesDir = 'c:/Snakk/src/apps/Snakk.Web/Styles';
const files = findScssFiles(stylesDir);
let modified = 0;

for (const f of files) {
    if (processScssFile(f)) {
        console.log('stripped:', path.relative(stylesDir, f));
        modified++;
    }
}
console.log(`\nDone — ${modified}/${files.length} files modified`);
