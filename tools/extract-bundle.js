// Extracts a .NET single-file bundle (apphost + embedded assemblies). Read-only on the source.
const fs = require('fs'), path = require('path'), zlib = require('zlib');
const [src, outDir] = process.argv.slice(2);
const buf = fs.readFileSync(src);
// Bundle signature: SHA-256 of ".net core bundle"
const sig = Buffer.from('8b1202b96a61209838720c3e74a8b3e0e8f47681a78e3a5dcf9a16b7b1b4f0c5'.replace(/../g, h => h + ' ').trim().split(' ').map(x => parseInt(x, 16)));
const altSig = Buffer.from([0x8b,0x12,0x02,0xb9,0x6a,0x61,0x20,0x38,0x72,0x7b,0x93,0x02,0x14,0xd7,0xa0,0x32,0x13,0xf5,0xb9,0xe6,0xef,0xae,0x33,0x18,0xee,0x3b,0x2d,0xce,0x24,0xb3,0x6a,0xae]);
let at = buf.indexOf(altSig);
if (at < 0) at = buf.indexOf(sig);
if (at < 0) throw new Error('bundle signature not found');
const headerOffset = Number(buf.readBigInt64LE(at - 8));
if (headerOffset <= 0) throw new Error('not a bundle (header offset 0)');
let p = headerOffset;
const u32 = () => { const v = buf.readUInt32LE(p); p += 4; return v; };
const i64 = () => { const v = Number(buf.readBigInt64LE(p)); p += 8; return v; };
const str = () => { let len = 0, shift = 0, b; do { b = buf[p++]; len |= (b & 0x7f) << shift; shift += 7; } while (b & 0x80); const s = buf.toString('utf8', p, p + len); p += len; return s; };
const major = u32(), minor = u32(), count = u32(), id = str();
console.log(`bundle v${major}.${minor} files=${count} id=${id}`);
if (major >= 2) { i64(); i64(); i64(); i64(); i64(); }
for (let i = 0; i < count; i++) {
  const off = i64(), size = i64(), csize = major >= 6 ? i64() : 0, type = buf[p++], rel = str();
  let data = buf.subarray(off, off + (csize || size));
  if (csize) data = zlib.inflateRawSync(data);
  const dest = path.join(outDir, rel);
  if (!path.resolve(dest).startsWith(path.resolve(outDir))) throw new Error('path escape: ' + rel);
  fs.mkdirSync(path.dirname(dest), { recursive: true });
  fs.writeFileSync(dest, data);
  console.log(type, size, rel);
}
