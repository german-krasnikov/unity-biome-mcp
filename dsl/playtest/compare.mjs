// The reader of the playtest SURFACE against the frozen oracle of the hand-written PlaytestParser (step 7.5).
// usage: node dsl/playtest/compare.mjs      env: BIOME_DSL_ROOT, the biome-dsl checkout of TOOLCHAIN (as scripts/dsl.sh)
// files    the `line` rows of the oracle, each file read whole: no diagnostic, and a use of the verb of the row on the line of the row, so the commands, and with them the steps, are the oracle's
// literals the `accept` / `reject` rows made of verbs that have a construct: the reader and the RULES agree with the row, or the row is HOST (INCLUDE is resolved by the host) or KNOWN (with its reason)
// examples the EXPECT lines of playtest*.examples (read as DECLARE_*, default tokenizer) say the same through the SURFACE (PT_*, TOKENS words, COMMENT line)
// probes   what the examples cannot see: the options of the SURFACE line (TOKENS words, COMMENT line, ORDERED), the slots of a line and the RULES
import { readdirSync, readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..'), dirs = [resolve(root, 'dsl/constructs')];
if (!process.env.BIOME_DSL_ROOT) throw new Error('set BIOME_DSL_ROOT to the biome-dsl checkout named by TOOLCHAIN in dsl/biome.project');
const { readSurface } = await import(pathToFileURL(resolve(process.env.BIOME_DSL_ROOT, 'implementations/typescript/packages/biome/src/support/surface-host.mjs')).href);
const read = (text, file = 'x') => readSurface('playtest', text, file, { dirs });
const unescape = s => s.replace(/\\(x[0-9a-fA-F]{2}|u[0-9a-fA-F]{4}|[\\nrt])/g, (_, e) => ({ n: '\n', r: '\r', t: '\t', '\\': '\\' })[e] ?? String.fromCharCode(parseInt(e.slice(1), 16)));
const verbs = new Set([...readFileSync(resolve(dirs[0], 'playtest.construct'), 'utf8').matchAll(/^\s+(?:PATTERN|HEAD)\s+(\S+)/gm)].map(m => m[1]));
const oracle = readFileSync(resolve(root, 'dsl/playtest/oracle.rows'), 'utf8'), rows = oracle.split('\n');
const place = d => `${d.code}@${d.line}:${d.col}`, problems = [];

// RULES: what a use must obey and the grammar cannot say; each is a way to read a line whole and wrongly. AND and OR are groups of their own, so a line can hold both
// (PT_MIXED); a chain of four terms reads its 4th into the value of the 3rd (PT_CHAIN); TOKENS words cuts at blanks, so a name with a blank and an operator word in
// [..] is cut at that word (PT_BRACKET), where the hand parser keeps [..] whole. The builder (H2) and check.mjs (D1) refuse the same three.
const rule = u => {
  const has = (...k) => k.some(x => u.head.has(x)), raw = k => u.head.get(k)?.raw ?? '';
  if (has('q2', 'q3') && has('q4', 'q5')) return 'PT_MIXED';
  const bare = v => [...v.matchAll(/"[^"]*"|\S+/g)].slice(1).some(t => t[0] === 'AND' || t[0] === 'OR');   // after the first token, a word outside quotes
  if (['v1', 'v2', 'v3', 'v4', 'v5'].some(k => bare(raw(k)))) return 'PT_CHAIN';
  const open = v => { let d = 0; for (const c of v.replace(/"[^"]*"/g, '')) d = Math.max(0, d + (c === '[' ? 1 : c === ']' ? -1 : 0)); return d > 0; };   // as SplitTokens counts
  return ['query', 'q1', 'q2', 'q3', 'q4', 'q5'].some(k => open(raw(k))) ? 'PT_BRACKET' : null;
};
// KNOWN: a row where the reader differs from the oracle, and why. Checked both ways: a row that no longer differs is stale.
const KNOWN = new Map([
  ['unity-plugin/Editor/Tests/WaitConditionTests.cs:143', 'INTENDED I3: TIMEOUT before AND/OR (AI/playtest-dsl.md: the chain, then TIMEOUT, then ABORT)'],
  ['unity-plugin/Editor/Tests/PlaytestParserCorpusTests.cs:214', 'CODEC: the JSON of an MCP argument is refused by ParseMcpStep, not by the grammar'],
]);

// files
const byFile = new Map();
for (const m of rows.map(r => /^line (\S+):(\d+) (\S+)/.exec(r)).filter(Boolean)) byFile.set(m[1], [...(byFile.get(m[1]) ?? []), `${m[2]}:${m[3]}`]);
const NOT_STEPS = new Set(['INCLUDE', 'VAL', 'VAR', 'SETUP', 'TEARDOWN']);
let commands = 0, steps = 0;
for (const [file, cmds] of byFile) {
  const out = read(readFileSync(resolve(root, file), 'utf8'), file), got = out.uses.map(u => `${u.startLine}:${u.keyword}`);
  commands += got.length; steps += out.uses.filter(u => !NOT_STEPS.has(u.keyword)).length;
  const refused = out.uses.map(rule).filter(Boolean);
  if (out.diagnostics.length || refused.length || got.join() !== cmds.join()) problems.push(`file ${file}: ${[...out.diagnostics.map(place), ...refused].join() || 'no diagnostic'}; uses [${got}], the oracle has [${cmds}]`);
}
const header = /^# (\d+) files, (\d+) commands \((\d+) steps/m.exec(oracle)?.slice(1).join();
if (header !== [byFile.size, commands, steps].join()) problems.push(`files, commands, steps: the header of oracle.rows says ${header}, the reader has ${byFile.size},${commands},${steps}`);

// literals
let same = 0, host = 0, known = 0, scope = 0, calls = 0;
const differing = new Set();
for (const m of rows.map(r => /^(accept|reject) (\S+):(\d+) (\S+) ?(.*)$/.exec(r)).filter(Boolean)) {
  calls++;
  const lines = unescape(m[5]).split('\n').map(l => l.trim()).filter(l => l && !l.startsWith('#'));
  if (!lines.length || !lines.every(l => verbs.has(l.split(/\s+/)[0]))) continue;
  scope++;
  const id = `${m[2]}:${m[3]}`, outs = lines.map(l => read(l)), verdict = outs.some(o => o.diagnostics.length || o.uses.some(rule)) ? 'reject' : 'accept';
  if (verdict === m[1]) { same++; if (KNOWN.has(id)) problems.push(`KNOWN ${id} is stale: the reader agrees with the oracle`); }
  else if (KNOWN.has(id)) { known++; differing.add(id); }
  else if (m[1] === 'reject' && lines.some(l => l.startsWith('INCLUDE '))) host++;
  else problems.push(`literal ${id} ${m[1]} ${JSON.stringify(unescape(m[5]))}: the reader says ${verdict}`);
}
for (const id of KNOWN.keys()) if (!differing.has(id)) problems.push(`KNOWN ${id} matches no differing row`);

// examples
let blocks = 0;
for (const f of readdirSync(dirs[0]).filter(f => /^playtest.*\.examples$/.test(f))) {
  for (const b of readFileSync(resolve(dirs[0], f), 'utf8').split(/\n(?=EXAMPLE )/).map(b => b.split('\n'))) {
    blocks++;
    const src = b.filter(l => l.startsWith('  SOURCE ')).map(l => l.slice(9)).join('\n'), exp = b.filter(l => l.startsWith('  EXPECT diagnostic ')).map(l => l.slice(20).replace('DECLARE_', 'PT_').replace(' at ', '@')).sort().join();
    const got = read(src).diagnostics.map(place).sort().join();
    if (got !== exp) problems.push(`example "${b[0].slice(8, 70)}" (${f}): through the SURFACE [${got}], in the file [${exp}]`);
  }
}

// probes: [line, why, expected]. Expected is the construct and the raw slots of the one use (what BuildSteps and ParseQOV make of the tokens: read, not run), a PT_ code of a rule, or the first diagnostic.
const slots = u => `${u.def.name} ${JSON.stringify(Object.fromEntries([...u.head].map(([k, v]) => [k, v.raw])))}`;
const probes = [
  ['LOG # UNPARSED: x', 'COMMENT line: a # inside a line is text', 'PT_LOG {"message":"# UNPARSED: x"}'],
  ['SET /obj Comp field a==b', 'TOKENS words: == is no token of its own', 'PT_SET {"path":"/obj","component":"Comp","field":"field","value":"a==b"}'],
  ['INVOKE /Player Health TakeDamage 42 true', 'INVOKE: Args is the rest', 'PT_INVOKE {"path":"/Player","component":"Health","method":"TakeDamage","args":"42 true"}'],
  ['INVOKE "/My Object" Comp Method', 'a quoted token is one token', 'PT_INVOKE {"path":"\\"/My Object\\"","component":"Comp","method":"Method"}'],
  ['wait 1', 'INTENDED I4: a verb is upper case', 'PT_UNKNOWN_KEYWORD@1:1'],
  ['SET /A B C 1 2 3', 'INTENDED I1: a token too many', 'PT_SYNTAX@1:14'],
  ['ASSERT /A|B|c TIMEOUT 5', 'INTENDED I1: the hand parser built op "" and a query that holds the tail; the line ends where the operator is due', 'PT_SYNTAX@1:23'],
  ['ASSERT /x|C|f ==', 'the value is due', 'PT_SYNTAX@1:15'],
  ['ASSERT /Obj|Comp|f == 1 AS my_label TIMEOUT 3', 'PlaytestParserTests.cs:487: Message my_label, Timeout 3 (the label stops at TIMEOUT)', 'PT_ASSERT {"query":"/Obj|Comp|f","op":"==","value":"1","label":"my_label","timeout2":"3"}'],
  ['ASSERT /Enemy|Health|hp == 0 TIMEOUT 3 AS enemy dead', 'PlaytestParserTests.cs:454: the label is the rest of the line', 'PT_ASSERT {"query":"/Enemy|Health|hp","op":"==","value":"0","timeout":"3","label":"enemy dead"}'],
  ['ASSERT $hud|health-label|text == "75 HP"', 'the value keeps its quotes; the builder takes them off', 'PT_ASSERT {"query":"$hud|health-label|text","op":"==","value":"\\"75 HP\\""}'],
  ['ASSERT $l == Rock AND Roll', 'INTENDED I1: AND ends no value of ASSERT (the hand parser kept Rock and dropped the rest)', 'PT_ASSERT {"query":"$l","op":"==","value":"Rock AND Roll"}'],
  ['ASSERT $x', 'one token after ASSERT is a flag', 'PT_ASSERT_FLAG {"flag":"$x"}'],
  ['ASSERT $title == Bird AS Prey', 'an unquoted AS ends the value of ASSERT and starts the label', 'PT_ASSERT {"query":"$title","op":"==","value":"Bird","label":"Prey"}'],
  ['ASSERT /[Box contains Items]/Slot|C|f == 1', 'the hand parser keeps [..] whole, TOKENS words cuts at the blank: the query would end at contains; the rule refuses (quote the name)', 'PT_BRACKET'],
  ['ASSERT /[Zone A/Zone B]/Child|Comp|f == value', 'AI/playtest-dsl.md:42: a blank in [..] is fine while the brackets close in the query', 'PT_ASSERT {"query":"/[Zone A/Zone B]/Child|Comp|f","op":"==","value":"value"}'],
  ['INVARIANT /x|C|f >= 0 TIMEOUT 5', 'INTENDED I1: INVARIANT has no tail, its value is the rest', 'PT_INVARIANT {"query":"/x|C|f","op":">=","value":"0 TIMEOUT 5"}'],
  ['WAIT_UNTIL /P|C|state == ok AND /Q|C|mode == run ABORT', 'PlaytestParserEdgeCaseTests.cs:114: ABORT ends the value and sets the flag', 'PT_WAIT_UNTIL {"q1":"/P|C|state","op1":"==","v1":"ok","q2":"/Q|C|mode","op2":"==","v2":"run","flag:ABORT":"true"}'],
  ['WAIT_UNTIL /P|C|state == ok AND /Q|C|mode == ABORT', 'PlaytestParserEdgeCaseTests.cs:106: the first token of a value is the value, even ABORT', 'PT_WAIT_UNTIL {"q1":"/P|C|state","op1":"==","v1":"ok","q2":"/Q|C|mode","op2":"==","v2":"ABORT"}'],
  ['WAIT_UNTIL q1 == v1 OR q2 == v2 TIMEOUT 5', 'WaitConditionTests.cs:111: the OR terms are q4, q5', 'PT_WAIT_UNTIL {"q1":"q1","op1":"==","v1":"v1","q4":"q2","op4":"==","v4":"v2","timeout":"5"}'],
  ['WAIT_UNTIL q1 == v1 AND q2 == v2 OR q3 == v3', 'WaitConditionTests.cs:128: AND and OR in one wait; the grammar reads it, the rule refuses', 'PT_MIXED'],
  ['WAIT_UNTIL a == 1 AND b == 2 AND c == 3 AND d == 4', 'INTENDED I3: the 4th term is read into the value of the 3rd; the rule refuses', 'PT_CHAIN'],
  ['WAIT_UNTIL q1 == v1 AND q2 == v2 AND q3 == v3 TIMEOUT 5 ABORT', 'the value of the 3rd AND term stops at TIMEOUT', 'PT_WAIT_UNTIL {"q1":"q1","op1":"==","v1":"v1","q2":"q2","op2":"==","v2":"v2","q3":"q3","op3":"==","v3":"v3","timeout":"5","flag:ABORT":"true"}'],
  ['WAIT_UNTIL q1 == v1 OR q2 == v2 OR q3 == v3 TIMEOUT 5 ABORT', 'the value of the 2nd OR term stops at TIMEOUT', 'PT_WAIT_UNTIL {"q1":"q1","op1":"==","v1":"v1","q4":"q2","op4":"==","v4":"v2","q5":"q3","op5":"==","v5":"v3","timeout":"5","flag:ABORT":"true"}'],
  ['WAIT_UNTIL $x == "a AND b" AND $y == 2', 'a stop word inside a value is quoted', 'PT_WAIT_UNTIL {"q1":"$x","op1":"==","v1":"\\"a AND b\\"","q2":"$y","op2":"==","v2":"2"}'],
  ['WAIT_UNTIL q1 == AND', 'the first token of a value is the value, so the rule leaves it', 'PT_WAIT_UNTIL {"q1":"q1","op1":"==","v1":"AND"}'],
  ['MCP get_hierarchy depth=2 INTO $tree', 'ORDERED: PT_MCP_INTO is read before PT_MCP, which would take INTO $tree into the args', 'PT_MCP_INTO {"command":"get_hierarchy","args":"depth=2","var":"$tree"}'],
  ['MCP get_hierarchy INTO $tree', 'INTO without arguments is the INTO form', 'PT_MCP_INTO {"command":"get_hierarchy","var":"$tree"}'],
  ['MCP create_object name=pt_$RUN_ID primitive=Cube', 'biome_smoke.playtest:7: no INTO, the args are the rest', 'PT_MCP {"command":"create_object","args":"name=pt_$RUN_ID primitive=Cube"}'],
  ['MCP run_sql query="insert INTO $table"', 'an INTO inside a quoted argument is no INTO: the var must be a bare $name', 'PT_MCP {"command":"run_sql","args":"query=\\"insert INTO $table\\""}'],
  ['MOVE Player TO @/Enemy.position + (5,0,0)', 'AI/playtest-dsl.md:640: the position is one token, the offset after it is text; the hand parser keeps the token and drops the offset (the builder does the same)', 'PT_MOVE {"path":"Player","to":"@/Enemy.position","offset":"+ (5,0,0)"}'],
  ['MOVE TO 1,2,3', 'the optional path steps back for TO', 'PT_MOVE {"to":"1,2,3"}'],
  ['MOVE Player TO @/Enemy.position            # use enemy\'s current position', 'AI/playtest-dsl.md:639: a # after a position is in the offset text (COMMENT line)', 'PT_MOVE {"path":"Player","to":"@/Enemy.position","offset":"# use enemy\'s current position"}'],
  ['TELEPORT Enemy @/Player.position + (2,0,0)', 'AI/playtest-dsl.md:777', 'PT_TELEPORT {"path":"Enemy","to":"@/Player.position","offset":"+ (2,0,0)"}'],
  ['MOVE Player TO 100,50', 'INTENDED I9: ParseFloats throws on two components (NumericParsing.cs:17); the position is typed', 'PT_SYNTAX@1:16'],
  ['CAPTURE gold /My Player|Wallet|Gold', 'the query is the rest of the line (PlaytestParser.Internals.cs:298 joins the tokens)', 'PT_CAPTURE {"label":"gold","query":"/My Player|Wallet|Gold"}'],
  ['CAPTURE_MIN $minFps /Stats|Fps|v OVER 3.5', 'PlaytestParserTests.cs:835', 'PT_CAPTURE_MIN {"name":"$minFps","query":"/Stats|Fps|v","over":"3.5"}'],
  ['ASSERT_CAPTURED money INCREASED_BY >= 50', 'Runtime/PlaytestParserTests.cs:353: mode, operator and value', 'PT_ASSERT_CAPTURED {"label":"money","mode":"INCREASED_BY","subop":">=","value":"50"}'],
  ['ASSERT_CAPTURED initial_pos != 100,100,0', 'INTENDED I9: AI/playtest-dsl.md:279 writes it, PlaytestState.cs:95 throws on the mode !=', 'PT_SYNTAX@1:29'],
  ['ASSERT_ONE_ACTIVE /A /B /C /D', 'two paths and the rest (PlaytestParser.Internals.cs:547 takes every token)', 'PT_ASSERT_ONE_ACTIVE {"path1":"/A","path2":"/B","more":"/C /D"}'],
  ['ASSERT_CTA', 'no state: the builder takes VISIBLE (AI/playtest-dsl.md:253)', 'PT_ASSERT_CTA {}'],
  ['DESC "check health"', 'the quotes stay in the text, the builder takes them off', 'PT_DESC {"label":"\\"check health\\""}'],
  ['MONITOR STOP', 'STOP is a name for the grammar; the builder reads it as the end', 'PT_MONITOR {"query":"STOP"}'],
];
for (const [line, why, exp] of probes) {
  const r = read(line), got = r.diagnostics[0] ? place(r.diagnostics[0]) : (rule(r.uses[0]) ?? slots(r.uses[0]));
  if (got !== exp) problems.push(`probe "${line}" (${why}): ${got}`);
}

console.log(`files ${byFile.size}: commands ${commands}, steps ${steps} | literals in scope ${scope} of ${calls}: same ${same}, host ${host}, known ${known} | examples ${blocks} | probes ${probes.length}`);
for (const p of problems) console.log('DIFF', p);
process.exit(problems.length ? 1 : 0);
