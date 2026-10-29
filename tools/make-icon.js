// Renders Bit, the Blox Code mascot, into BloxCode/Resources/BloxCode.ico (+ docs/icon.png).
// Usage: node tools/make-icon.js
const fs = require('fs'), path = require('path'), zlib = require('zlib');

// Same idle frame as UI/BitMascot.cs, on its 16 x 17 grid of square cells.
const BODY = [0xD9, 0x77, 0x57], TOP = [0xEC, 0xA5, 0x87], SHADE = [0xB0, 0x55, 0x38];
const SCREEN = [0x1A, 0x19, 0x18], GLOW = [0xFF, 0xF1, 0xE6], SPARK = [0xFF, 0xC8, 0x5A];
const TILE = [0x26, 0x26, 0x24];

const cells = [];
const R = (x, y, w, h, c) => cells.push([x, y, w, h, c]);
R(7, 0, 2, 2, SPARK); R(7, 2, 2, 1, SHADE);                          // antenna
R(2, 3, 12, 1, TOP); R(1, 4, 14, 1, TOP);                             // lit top
R(1, 5, 13, 9, BODY); R(14, 5, 1, 9, SHADE); R(2, 14, 12, 1, SHADE);  // block
R(0, 9, 1, 2, SHADE); R(15, 9, 1, 2, SHADE);                          // arms
R(4, 6, 8, 1, SCREEN); R(3, 7, 10, 4, SCREEN); R(4, 11, 8, 1, SCREEN); // screen
R(5, 7, 2, 2, GLOW); R(9, 7, 2, 2, GLOW); R(7, 10, 2, 1, GLOW);       // face
R(3, 15, 2, 2, SHADE); R(11, 15, 2, 2, SHADE);                        // legs

function shapeAt(x, y) {
  for (let i = cells.length - 1; i >= 0; i--) {
    const [cx, cy, w, h, c] = cells[i];
    if (x >= cx && x < cx + w && y >= cy && y < cy + h) return c;
  }
  return null;
}

function render(size) {
  const px = Buffer.alloc(size * size * 4);
  const ss = 4, radius = size * 0.22;
  const scale = (size * 0.78) / 17, ox = (size - 16 * scale) / 2, oy = (size - 17 * scale) / 2;
  for (let y = 0; y < size; y++) for (let x = 0; x < size; x++) {
    let r = 0, g = 0, b = 0, a = 0;
    for (let sy = 0; sy < ss; sy++) for (let sx = 0; sx < ss; sx++) {
      const fx = x + (sx + 0.5) / ss, fy = y + (sy + 0.5) / ss;
      const dx = Math.max(radius - fx, 0, fx - (size - radius)), dy = Math.max(radius - fy, 0, fy - (size - radius));
      if (dx * dx + dy * dy > radius * radius) continue;
      const c = shapeAt((fx - ox) / scale, (fy - oy) / scale) || TILE;
      r += c[0]; g += c[1]; b += c[2]; a += 255;
    }
    const n = ss * ss, i = (y * size + x) * 4, cov = a / 255;
    px[i] = cov ? r / cov : 0; px[i + 1] = cov ? g / cov : 0; px[i + 2] = cov ? b / cov : 0; px[i + 3] = a / n;
  }
  return px;
}

function crc32(buf) {
  let c, crc = 0xffffffff;
  for (let n = 0; n < buf.length; n++) {
    c = (crc ^ buf[n]) & 0xff;
    for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
    crc = (crc >>> 8) ^ c;
  }
  return (crc ^ 0xffffffff) >>> 0;
}

function png(size, rgba) {
  const chunk = (type, data) => {
    const len = Buffer.alloc(4); len.writeUInt32BE(data.length);
    const td = Buffer.concat([Buffer.from(type), data]);
    const crc = Buffer.alloc(4); crc.writeUInt32BE(crc32(td));
    return Buffer.concat([len, td, crc]);
  };
  const ihdr = Buffer.alloc(13);
  ihdr.writeUInt32BE(size, 0); ihdr.writeUInt32BE(size, 4); ihdr[8] = 8; ihdr[9] = 6;
  const raw = Buffer.alloc(size * (size * 4 + 1));
  for (let y = 0; y < size; y++) rgba.copy(raw, y * (size * 4 + 1) + 1, y * size * 4, (y + 1) * size * 4);
  return Buffer.concat([Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
    chunk('IHDR', ihdr), chunk('IDAT', zlib.deflateSync(raw)), chunk('IEND', Buffer.alloc(0))]);
}

const sizes = [16, 24, 32, 48, 64, 128, 256];
const images = sizes.map(s => png(s, render(s)));
const header = Buffer.alloc(6); header.writeUInt16LE(0, 0); header.writeUInt16LE(1, 2); header.writeUInt16LE(sizes.length, 4);
let offset = 6 + 16 * sizes.length;
const dir = sizes.map((s, i) => {
  const e = Buffer.alloc(16);
  e[0] = s >= 256 ? 0 : s; e[1] = s >= 256 ? 0 : s; e.writeUInt16LE(1, 4); e.writeUInt16LE(32, 6);
  e.writeUInt32LE(images[i].length, 8); e.writeUInt32LE(offset, 12); offset += images[i].length;
  return e;
});
const out = path.join(__dirname, '..', 'BloxCode', 'Resources');
fs.mkdirSync(out, { recursive: true });
fs.writeFileSync(path.join(out, 'BloxCode.ico'), Buffer.concat([header, ...dir, ...images]));
fs.writeFileSync(path.join(__dirname, '..', 'docs', 'icon.png'), images[images.length - 1]);
console.log('wrote', path.join(out, 'BloxCode.ico'));
