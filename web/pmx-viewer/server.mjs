import http from 'node:http';
import { createReadStream } from 'node:fs';
import { stat } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.dirname(fileURLToPath(import.meta.url));
const modelRoot = path.resolve(root, '../../exports/pmx-test/1002_00_20260926-221744');
const mime = {
  '.html': 'text/html; charset=utf-8', '.css': 'text/css; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8', '.png': 'image/png',
  '.pmx': 'application/octet-stream', '.vmd': 'application/octet-stream',
};
const pages = new Set(['/index.html', '/viewer.js', '/motion-player.js', '/style.css']);
const server = http.createServer(async (request, response) => {
  try {
    if (request.method !== 'GET' && request.method !== 'HEAD') {
      response.writeHead(405, { Allow: 'GET, HEAD' }).end();
      return;
    }
    let urlPath = decodeURIComponent(new URL(request.url, 'http://localhost').pathname);
    if (urlPath === '/') urlPath = '/index.html';
    let base, relative;
    if (urlPath.startsWith('/model/')) {
      base = modelRoot;
      relative = urlPath.slice('/model/'.length);
    } else if (urlPath.startsWith('/vendor/three/')) {
      base = path.join(root, 'node_modules/three');
      relative = urlPath.slice('/vendor/three/'.length);
    } else if (pages.has(urlPath)) {
      base = root;
      relative = urlPath.slice(1);
    } else {
      response.writeHead(404).end('Not found');
      return;
    }
    const file = path.resolve(base, relative);
    const within = path.relative(base, file);
    if (within.startsWith('..') || path.isAbsolute(within)) {
      response.writeHead(403).end('Forbidden');
      return;
    }
    const info = await stat(file);
    if (!info.isFile()) { response.writeHead(404).end('Not found'); return; }
    response.writeHead(200, {
      'Content-Type': mime[path.extname(file)] || 'application/octet-stream',
      'Content-Length': info.size,
      'X-Content-Type-Options': 'nosniff',
      'Cache-Control': 'no-cache',
    });
    if (request.method === 'HEAD') { response.end(); return; }
    const stream = createReadStream(file);
    stream.on('error', () => response.destroy());
    response.on('close', () => stream.destroy());
    stream.pipe(response);
  } catch (error) {
    const status = error.code === 'ENOENT' || error.code === 'ENOTDIR' ? 404 : error instanceof URIError ? 400 : 500;
    response.writeHead(status).end(status === 404 ? 'Not found' : 'Request failed');
  }
});
server.on('error', error => { console.error(error.message); process.exitCode = 1; });
server.listen(8765, '127.0.0.1', () => {
  console.log('PMX viewer: http://127.0.0.1:8765');
  console.log(`Model directory: ${modelRoot}`);
});
