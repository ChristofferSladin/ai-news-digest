// Generates the PWA icons (a warm sun on a dark rounded square) as real PNGs using only
// Node's zlib — no native image dependencies. Run: npm run generate:icons
import { mkdirSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { deflateSync } from "node:zlib";

const publicDir = join(dirname(fileURLToPath(import.meta.url)), "..", "public");

// ---- tiny PNG encoder (8-bit RGBA) ----
const CRC_TABLE = (() => {
  const table = new Uint32Array(256);
  for (let n = 0; n < 256; n++) {
    let c = n;
    for (let k = 0; k < 8; k++) {
      c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
    }
    table[n] = c >>> 0;
  }
  return table;
})();

function crc32(buffer) {
  let c = 0xffffffff;
  for (let i = 0; i < buffer.length; i++) {
    c = CRC_TABLE[(c ^ buffer[i]) & 0xff] ^ (c >>> 8);
  }
  return (c ^ 0xffffffff) >>> 0;
}

function pngChunk(type, data) {
  const length = Buffer.alloc(4);
  length.writeUInt32BE(data.length, 0);
  const typeBuffer = Buffer.from(type, "latin1");
  const crc = Buffer.alloc(4);
  crc.writeUInt32BE(crc32(Buffer.concat([typeBuffer, data])), 0);
  return Buffer.concat([length, typeBuffer, data, crc]);
}

function encodePng(size, rgba) {
  const signature = Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]);
  const ihdr = Buffer.alloc(13);
  ihdr.writeUInt32BE(size, 0);
  ihdr.writeUInt32BE(size, 4);
  ihdr[8] = 8; // bit depth
  ihdr[9] = 6; // colour type RGBA
  const stride = size * 4;
  const raw = Buffer.alloc((stride + 1) * size);
  for (let y = 0; y < size; y++) {
    raw[y * (stride + 1)] = 0; // filter: none
    rgba.copy(raw, y * (stride + 1) + 1, y * stride, y * stride + stride);
  }
  const idat = deflateSync(raw, { level: 9 });
  return Buffer.concat([
    signature,
    pngChunk("IHDR", ihdr),
    pngChunk("IDAT", idat),
    pngChunk("IEND", Buffer.alloc(0)),
  ]);
}

// ---- drawing ----
const lerp = (a, b, t) => a + (b - a) * t;
const mix = (c1, c2, t) => [
  Math.round(lerp(c1[0], c2[0], t)),
  Math.round(lerp(c1[1], c2[1], t)),
  Math.round(lerp(c1[2], c2[2], t)),
];

const BG_TOP = [11, 18, 32];
const BG_BOTTOM = [22, 32, 58];
const SUN_INNER = [255, 209, 128];
const SUN_OUTER = [255, 138, 61];

function insideRoundedSquare(x, y, size, radius) {
  const min = radius;
  const max = size - 1 - radius;
  let cx = x;
  let cy = y;
  if (x < min && y < min) {
    cx = min;
    cy = min;
  } else if (x > max && y < min) {
    cx = max;
    cy = min;
  } else if (x < min && y > max) {
    cx = min;
    cy = max;
  } else if (x > max && y > max) {
    cx = max;
    cy = max;
  } else {
    return true;
  }
  return Math.hypot(x - cx, y - cy) <= radius;
}

function drawIcon(size, maskable) {
  const rgba = Buffer.alloc(size * size * 4);
  const center = (size - 1) / 2;
  const sunR = size * (maskable ? 0.17 : 0.205);
  const rayInner = size * (maskable ? 0.205 : 0.245);
  const rayOuter = size * (maskable ? 0.3 : 0.355);
  const corner = size * 0.2;
  const segment = Math.PI / 4;

  for (let y = 0; y < size; y++) {
    for (let x = 0; x < size; x++) {
      const i = (y * size + x) * 4;

      if (!maskable && !insideRoundedSquare(x, y, size, corner)) {
        rgba[i + 3] = 0;
        continue;
      }

      let color = mix(BG_TOP, BG_BOTTOM, y / (size - 1));
      const dx = x - center;
      const dy = y - center;
      const dist = Math.hypot(dx, dy);

      if (dist <= sunR) {
        color = mix(SUN_INNER, SUN_OUTER, dist / sunR);
      } else if (dist >= rayInner && dist <= rayOuter) {
        const angle = Math.atan2(dy, dx);
        const nearest = Math.round(angle / segment) * segment;
        if (Math.abs(angle - nearest) < 0.17) {
          color = SUN_OUTER;
        }
      }

      rgba[i] = color[0];
      rgba[i + 1] = color[1];
      rgba[i + 2] = color[2];
      rgba[i + 3] = 255;
    }
  }
  return encodePng(size, rgba);
}

mkdirSync(publicDir, { recursive: true });
const outputs = [
  ["pwa-192.png", 192, false],
  ["pwa-512.png", 512, false],
  ["pwa-512-maskable.png", 512, true],
  ["apple-touch-icon.png", 180, true],
];

for (const [name, size, maskable] of outputs) {
  writeFileSync(join(publicDir, name), drawIcon(size, maskable));
  console.log(`wrote public/${name} (${size}x${size}${maskable ? ", maskable" : ""})`);
}
