// The reader of the playtest SURFACE against the frozen oracle of the hand-written PlaytestParser (step 7.5).
// usage: node dsl/playtest/compare.mjs [--expect-pending ASSERT=49,MCP=4]      env: BIOME_DSL_ROOT, the biome-dsl checkout of TOOLCHAIN (as scripts/dsl.sh)
// files   the files of the `line` rows, read whole: a diagnostic is PT_UNKNOWN_KEYWORD on a line of a verb that has no construct yet, and only there
// literals the `accept` / `reject` rows made of verbs that have a construct: the reader agrees with the row, or the row is HOST (INCLUDE is resolved by the host)
// examples the EXPECT lines of playtest.examples (read as DECLARE_*, default tokenizer) say the same through the SURFACE (PT_*, TOKENS words, COMMENT line)
// probes  what the examples of the constructs cannot see: the options of the SURFACE line (TOKENS words, COMMENT line) and the slots of a line
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..'), dirs = [resolve(root, 'dsl/constructs')];
if (!process.env.BIOME_DSL_ROOT) throw new Error('set BIOME_DSL_ROOT to the biome-dsl checkout named by TOOLCHAIN in dsl/biome.project');
const { readSurface } = await import(pathToFileURL(resolve(process.env.BIOME_DSL_ROOT, 'implementations/typescript/packages/biome/src/support/surface-host.mjs')).href);
const read = (text, file = 'x') => readSurface('playtest', text, file, { dirs });
const unescape = s => s.replace(/\\(x[0-9a-fA-F]{2}|u[0-9a-fA-F]{4}|[\\nrt])/g, (_, e) => ({ n: '\n', r: '\r', t: '\t', '\\': '\\' })[e] ?? String.fromCharCode(parseInt(e.slice(1), 16)));
const verbs = new Set([...readFileSync(resolve(dirs[0], 'playtest.construct'), 'utf8').matchAll(/^\s+(?:PATTERN|HEAD)\s+(\S+)/gm)].map(m => m[1]));
const rows = readFileSync(resolve(root, 'dsl/playtest/oracle.rows'), 'utf8').split('\n');
const problems = [];

// files
const byFile = new Map();
for (const m of rows.map(r => /^line (\S+):(\d+) (\S+)/.exec(r)).filter(Boolean)) byFile.set(m[1], [...(byFile.get(m[1]) ?? []), [Number(m[2]), m[3]]]);
let readOk = 0;
const pending = new Map();
for (const [file, cmds] of byFile) {
  const out = read(readFileSync(resolve(root, file), 'utf8'), file), want = cmds.filter(([, v]) => !verbs.has(v));
  want.forEach(([, v]) => pending.set(v, (pending.get(v) ?? 0) + 1)); readOk += cmds.length - want.length;
  const got = out.diagnostics.map(d => `${d.code}@${d.line}`).join(), exp = want.map(([n]) => `PT_UNKNOWN_KEYWORD@${n}`).join();
  if (got !== exp || out.uses.length !== cmds.length - want.length) problems.push(`file ${file}: diagnostics [${got}] expected [${exp}], ${out.uses.length} uses`);
}
const table = [...pending].sort().map(([v, n]) => `${v}=${n}`).join(','), expect = process.argv[2] === '--expect-pending' ? process.argv[3] : null;
if (expect !== null && expect !== table) problems.push(`pending verbs ${table}, expected ${expect}`);

// literals
let same = 0, host = 0, scope = 0;
for (const m of rows.map(r => /^(accept|reject) (\S+):(\d+) (\S+) ?(.*)$/.exec(r)).filter(Boolean)) {
  const lines = unescape(m[5]).split('\n').map(l => l.trim()).filter(l => l && !l.startsWith('#'));
  if (!lines.length || !lines.every(l => verbs.has(l.split(/\s+/)[0]))) continue;
  scope++;
  const verdict = lines.some(l => read(l).diagnostics.length) ? 'reject' : 'accept';
  if (verdict === m[1]) same++; else if (m[1] === 'reject' && lines.some(l => l.startsWith('INCLUDE '))) host++; else problems.push(`literal ${m[2]}:${m[3]} ${m[1]} ${JSON.stringify(unescape(m[5]))}: the reader says ${verdict}`);
}

// examples
const blocks = readFileSync(resolve(dirs[0], 'playtest.examples'), 'utf8').split(/\n(?=EXAMPLE )/).map(b => b.split('\n'));
for (const b of blocks) {
  const src = b.filter(l => l.startsWith('  SOURCE ')).map(l => l.slice(9)).join('\n'), exp = b.filter(l => l.startsWith('  EXPECT diagnostic ')).map(l => l.slice(20).replace('DECLARE_', 'PT_').replace(' at ', '@')).sort().join();
  const got = read(src).diagnostics.map(d => `${d.code}@${d.line}:${d.col}`).sort().join();
  if (got !== exp) problems.push(`example "${b[0].slice(8, 70)}": through the SURFACE [${got}], in the file [${exp}]`);
}

// probes: [line, what the hand parser makes of it (BuildSteps, tokens of SplitTokens) or what the design says, expected]
const slots = u => JSON.stringify(Object.fromEntries([...u.head].map(([k, v]) => [k, v.raw])));
const probes = [
  ['LOG # UNPARSED: x', 'COMMENT line: a # inside a line is text', '{"message":"# UNPARSED: x"}'],
  ['SET /obj Comp field a==b', 'TOKENS words: == is no token of its own', '{"path":"/obj","component":"Comp","field":"field","value":"a==b"}'],
  ['INVOKE /Player Health TakeDamage 42 true', 'INVOKE: Args is the rest', '{"path":"/Player","component":"Health","method":"TakeDamage","args":"42 true"}'],
  ['INVOKE "/My Object" Comp Method', 'a quoted token is one token', '{"path":"\\"/My Object\\"","component":"Comp","method":"Method"}'],
];
for (const [line, why, exp] of probes) { const r = read(line); const got = r.diagnostics.length ? r.diagnostics[0].code : slots(r.uses[0]); if (got !== exp) problems.push(`probe "${line}" (${why}): ${got}`); }
for (const [line, code, at] of [['wait 1', 'PT_UNKNOWN_KEYWORD', '1:1'], ['SET /A B C 1 2 3', 'PT_SYNTAX', '1:14']]) { const d = read(line).diagnostics[0]; if (`${d?.code}@${d?.line}:${d?.col}` !== `${code}@${at}`) problems.push(`probe "${line}": ${d?.code}@${d?.line}:${d?.col}`); }

console.log(`files ${byFile.size}: lines read ${readOk}, pending ${table || 'none'} | literals in scope ${scope}: same ${same}, host ${host} | examples ${blocks.length} | probes ${probes.length + 2}`);
for (const p of problems) console.log('DIFF', p);
process.exit(problems.length ? 1 : 0);
