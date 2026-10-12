import { build } from 'esbuild';
import { cp, mkdir, rm } from 'node:fs/promises';

await rm('dist', { recursive: true, force: true });
await mkdir('dist', { recursive: true });
await build({ entryPoints: ['src/popup.ts', 'src/background.ts', 'src/content.ts', 'src/page.ts'], bundle: true, minify: false, outdir: 'dist', target: 'chrome120' });
await cp('manifest/manifest.json', 'dist/manifest.json');
await cp('src/popup.html', 'dist/popup.html');
await cp('src/popup.css', 'dist/popup.css');
await cp('src/blocked.html', 'dist/blocked.html');
await cp('src/focus.html', 'dist/focus.html');
await cp('src/fonts', 'dist/fonts', { recursive: true });
