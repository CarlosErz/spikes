// Spike, lado del navegador: leer el CSV con un lector RFC 4180 propio, validar cada fila y
// armar el body JSON. Corre en Node 24 (mismos motores V8 que Chrome). Uso: node cliente.mjs [filas]
import { performance } from 'node:perf_hooks';

/** Lector RFC 4180 minimo: comillas dobles, "" escapado, CRLF o LF, sin adivinar separador. */
export function parseCsv(text) {
  const rows = [];
  let row = [], field = '', i = 0, quoted = false;
  while (i < text.length) {
    const c = text[i];
    if (quoted) {
      if (c === '"') {
        if (text[i + 1] === '"') { field += '"'; i += 2; continue; }
        quoted = false; i++; continue;
      }
      field += c; i++; continue;
    }
    if (c === '"' && field === '') { quoted = true; i++; continue; }
    if (c === ',') { row.push(field); field = ''; i++; continue; }
    if (c === '\r' && text[i + 1] === '\n') i++;
    if (c === '\n' || c === '\r') { row.push(field); rows.push(row); row = []; field = ''; i++; continue; }
    field += c; i++;
  }
  if (quoted) throw new Error('Comillas sin cerrar');
  if (field !== '' || row.length) { row.push(field); rows.push(row); }
  return rows;
}

const HEADER = ['code', 'description', 'measurementUnit', 'unitPrice', 'priceIncludesTax', 'taxCategory', 'workType', 'availability', 'estimatedMinutes', 'equipmentTypeCodes'];
const ENUMS = { measurementUnit: ['Service', 'Hour', 'Meter'], taxCategory: ['General', 'ZeroRate', 'Exempt'], workType: ['Labor', 'Material', 'Installation'], availability: ['QuoteOnly', 'OrderOnly', 'Both'] };
const BAD = /[\u0000-\u0008\u000B\u000C\u000E-\u001F\u007F‪-‮⁦-⁩]/;

function toBody(rows) {
  const [head, ...data] = rows;
  if (head.join(',') !== HEADER.join(',')) throw new Error('Encabezados');
  const errors = [], out = [];
  data.forEach((r, idx) => {
    const line = idx + 2;
    if (r.length !== HEADER.length) { errors.push({ line, column: '*', code: 'WIDTH' }); return; }
    if (r.some((v) => BAD.test(v) || v.length > 4096)) { errors.push({ line, column: '*', code: 'CHARS' }); return; }
    for (const [k, allowed] of Object.entries(ENUMS)) if (!allowed.includes(r[HEADER.indexOf(k)])) errors.push({ line, column: k, code: 'ENUM' });
    if (!/^\d+(\.\d{1,2})?$/.test(r[3])) errors.push({ line, column: 'unitPrice', code: 'DECIMAL' });
    out.push({
      code: r[0].trim(), description: r[1], measurementUnit: r[2], unitPrice: r[3], priceIncludesTax: r[4] === 'true',
      taxCategory: r[5], workType: r[6], availability: r[7], estimatedMinutes: r[8] === '' ? null : Number(r[8]),
      equipmentTypeCodes: r[9] === '' ? [] : r[9].split('|'),
    });
  });
  return { body: JSON.stringify({ rows: out }), errors };
}

function generate(n) {
  const V = ['Mantenimiento preventivo', 'Limpieza profunda', 'Instalación', 'Reparación', 'Diagnóstico', 'Recarga de gas'];
  const T = ['minisplit', 'equipo de ventana', 'condensadora', 'evaporadora', 'chiller', 'fan & coil'];
  const X = ['', '', ' hasta 2 toneladas', ' incluye limpieza de serpentín', ' con "refrigerante" R-410A'];
  let s = HEADER.join(',') + '\r\n', seed = 42;
  const r = (m) => { seed = (seed * 1103515245 + 12345) % 2147483648; return seed % m; };
  for (let i = 0; i < n; i++) {
    const d = `${V[r(V.length)]} de ${T[r(T.length)]}${X[r(X.length)]}`.replaceAll('"', '""');
    s += `SRV-${String(i + 1).padStart(5, '0')},"${d}",${ENUMS.measurementUnit[r(3)]},${(r(900000) / 100 + 150).toFixed(2)},${r(2) ? 'true' : 'false'},${ENUMS.taxCategory[r(3)]},${ENUMS.workType[r(3)]},${ENUMS.availability[r(3)]},${r(5) ? 15 + r(465) : ''},${['', 'MINI', 'MINI|VENT'][r(3)]}\r\n`;
  }
  return s;
}

async function gzipSize(str) {
  const stream = new Blob([str]).stream().pipeThrough(new CompressionStream('gzip'));
  return (await new Response(stream).arrayBuffer()).byteLength;
}

const n = Number(process.argv[2] ?? 5000);
const csv = generate(n);
const median = (a) => a.sort((x, y) => x - y)[Math.floor(a.length / 2)];
for (let i = 0; i < 20; i++) toBody(parseCsv(csv));
const tParse = [], tAll = [];
for (let i = 0; i < 100; i++) {
  const a = performance.now(); const rows = parseCsv(csv); const b = performance.now(); toBody(rows); const c = performance.now();
  tParse.push(b - a); tAll.push(c - a);
}
const { body, errors } = toBody(parseCsv(csv));
const tz = performance.now(); const gz = await gzipSize(body); const tzEnd = performance.now() - tz;
console.log(`Filas ${n} | Node ${process.version}`);
console.log(`CSV ${(Buffer.byteLength(csv) / 1024).toFixed(0)} KB -> JSON ${(Buffer.byteLength(body) / 1024).toFixed(0)} KB (gzip ${(gz / 1024).toFixed(0)} KB en ${tzEnd.toFixed(1)} ms) | errores ${errors.length}`);
console.log(`Leer CSV p50 ${median(tParse).toFixed(2)} ms | leer + validar + JSON p50 ${median(tAll).toFixed(2)} ms`);
