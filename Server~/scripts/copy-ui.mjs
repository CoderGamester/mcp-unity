import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const serverRoot = path.resolve(here, '..');

const srcHtml = path.join(serverRoot, 'src', 'ui', 'unity-dashboard.html');
const outDir = path.join(serverRoot, 'build', 'ui');
const outHtml = path.join(outDir, 'unity-dashboard.html');

if (!fs.existsSync(srcHtml)) {
  console.error(`UI source file not found: ${srcHtml}`);
  process.exit(1);
}

fs.mkdirSync(outDir, { recursive: true });
fs.copyFileSync(srcHtml, outHtml);

console.log(`Copied UI: ${srcHtml} -> ${outHtml}`);
