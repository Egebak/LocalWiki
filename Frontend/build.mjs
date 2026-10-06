import { build } from 'esbuild';
import { mkdir, copyFile } from 'node:fs/promises';
import { resolve } from 'node:path';
await mkdir('src/LocalWiki.App/wwwroot/js', { recursive: true });
await build({ absWorkingDir: process.cwd(), entryPoints: [resolve('Frontend/wikiInterop.js')], outfile: resolve('src/LocalWiki.App/wwwroot/js/wikiInterop.js'), bundle: true, format: 'esm', platform: 'browser', minify: true, loader: { '.svg': 'dataurl', '.woff2': 'dataurl', '.woff': 'dataurl', '.ttf': 'dataurl' } });
await copyFile('src/LocalWiki.App/wwwroot/js/wikiInterop.css', 'src/LocalWiki.App/wwwroot/editor.css');
