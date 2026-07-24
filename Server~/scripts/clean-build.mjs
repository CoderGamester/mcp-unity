import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const scriptDirectory = path.dirname(fileURLToPath(import.meta.url));
fs.rmSync(path.join(scriptDirectory, '..', 'build'), {
  recursive: true,
  force: true,
});
