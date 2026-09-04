// render.js - steps an animated (SMIL) SVG and captures frames via headless Chrome.
// Usage: node render.js <svgPath> <outDir> <frames> <size> <durationSec>
const puppeteer = require('puppeteer-core');
const fs = require('fs');
const path = require('path');

const CHROME = process.env.CHROME_PATH ||
    'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe';

const svgPath = process.argv[2];
const outDir = process.argv[3] || 'frames';
const FRAMES = parseInt(process.argv[4] || '30', 10);
const SIZE = parseInt(process.argv[5] || '440', 10);
const DUR = parseFloat(process.argv[6] || '1.0');

(async () => {
    if (!svgPath || !fs.existsSync(svgPath)) {
        console.error('SVG not found: ' + svgPath);
        process.exit(2);
    }
    if (!fs.existsSync(outDir)) fs.mkdirSync(outDir, { recursive: true });

    const svg = fs.readFileSync(svgPath, 'utf8');
    const html = '<!doctype html><html><head><meta charset="utf-8">' +
        '<style>html,body{margin:0;padding:0;background:transparent}' +
        '#wrap{width:' + SIZE + 'px;height:' + SIZE + 'px}' +
        'svg{width:100%;height:100%;display:block}</style></head>' +
        '<body><div id="wrap">' + svg + '</div></body></html>';

    const browser = await puppeteer.launch({
        executablePath: CHROME,
        headless: 'new',
        args: ['--no-sandbox', '--force-color-profile=srgb', '--hide-scrollbars']
    });
    const page = await browser.newPage();
    await page.setViewport({ width: SIZE, height: SIZE, deviceScaleFactor: 2 });
    await page.setContent(html, { waitUntil: 'domcontentloaded', timeout: 60000 });
    await new Promise(r => setTimeout(r, 300));

    await page.evaluate(() => {
        const s = document.querySelector('svg');
        // Drop the opaque page-background rects (fill #f5f5f5) so the character
        // is transparent. Do NOT touch clipPath rects (no fill) or they'd clip
        // the whole drawing away.
        document.querySelectorAll('svg rect').forEach(r => {
            if (r.closest('clipPath') || r.closest('defs')) return;
            const fill = (r.getAttribute('fill') || '').toLowerCase();
            if (fill === '#f5f5f5' || fill === '#f5f5f5ff') {
                r.style.display = 'none';
            }
        });
        if (s && s.pauseAnimations) s.pauseAnimations();
    });

    const el = await page.$('#wrap');
    for (let i = 0; i < FRAMES; i++) {
        const t = (i / FRAMES) * DUR;
        await page.evaluate((tt) => {
            const s = document.querySelector('svg');
            if (s && s.setCurrentTime) s.setCurrentTime(tt);
        }, t);
        const f = path.join(outDir, 'frame_' + String(i).padStart(3, '0') + '.png');
        await el.screenshot({ path: f, omitBackground: true });
    }

    await browser.close();
    console.log('Rendered ' + FRAMES + ' frames to ' + outDir);
})().catch(err => { console.error(err); process.exit(1); });
