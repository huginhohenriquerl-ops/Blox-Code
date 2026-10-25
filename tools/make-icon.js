// Renders the Clawd mascot into BloxCode/Resources/BloxCode.ico (+ a PNG preview).
// Usage: node tools/make-icon.js
const fs = require('fs'), path = require('path'), zlib = require('zlib');

// Same grid as UI/ClawdMascot.cs: 16 columns x 5 rows, each cell is 1 wide by 2 tall.
const BODY = [
  [2, 13], // row 0
  [2, 13], // row 1 (eyes punched out below)
  [0, 15], // row 2 (arms)
  [2, 13], // row 3
];
const EYES = [4, 11];
const LEGS = [3, 5, 10, 12];
const ORANGE = [0xD7, 0x77, 0x57], INK = [0x14, 0x14, 0x13], TILE = [0x26, 0x26, 0x24];

function shapeAt(x, y) { // x in [0,16), y in [0,10) mascot units
  if (x < 0 || y < 0 || x >= 16 || y >= 10) return null;
  const row = Math.floor(y / 2), col = Math.floor(x);
  if (row < 4) {
    const [a, b] = BODY[row];
    if (col < a || col > b) return null;
    if (row === 1 && EYES.includes(col)) return INK;
    return ORANGE;
  }
  if (row === 4 && LEGS.includes(col)) return ORANGE;
  return null;
}

function render(size) {
  const px = Buffer.alloc(size * size * 4);
  const ss = 4, radius = size * 0.22;
  const scale = (size * 0.8) / 16, ox = size * 0.1, oy = (size - 10 * scale) / 2;
  for (let y = 0; y < size; y++) for (let x = 0; x < size; x++) {
    let r = 0, g = 0, b = 0, a = 0;
    for (let sy = 0; sy < ss; sy++) for (let sx = 0; sx < ss; sx++) {
      const fx = x + (sx + 0.5) / ss, fy = y + (sy + 0.5) / ss;
      // rounded tile
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
