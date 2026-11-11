// The reader of the playtest SURFACE against the frozen oracle of the hand-written PlaytestParser (step 7.5).
// usage: node dsl/playtest/compare.mjs      env: BIOME_DSL_ROOT, the biome-dsl checkout of TOOLCHAIN (as scripts/dsl.sh)
// files    the `line` rows of the oracle, each file read whole: no diagnostic, and a use of the verb of the row on the line of the row, so the commands, and with them the steps, are the oracle's
// literals every `accept` / `reject` row of the oracle, none left out: the reader and the RULES agree with the row, or the row is HOST (the host resolves INCLUDE and expands FOR and COMMENT) or KNOWN (with its reason)
// examples the EXPECT lines of playtest*.examples (read as DECLARE_*, default tokenizer) say the same through the SURFACE (PT_*, TOKENS words, COMMENT line), and every construct is read from at least one SOURCE line
// probes   what the examples cannot see: the options of the SURFACE line (TOKENS words, COMMENT line, ORDERED), the slots of a line and the RULES
import { readdirSync, readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { dirs, load, place, PREPROCESSOR, root, rule } from './surface.mjs';

const { forms, read } = await load();
const unescape = s => s.replace(/\\(x[0-9a-fA-F]{2}|u[0-9a-fA-F]{4}|[\\nrt])/g, (_, e) => ({ n: '\n', r: '\r', t: '\t', '\\': '\\' })[e] ?? String.fromCharCode(parseInt(e.slice(1), 16)));
const oracle = readFileSync(resolve(root, 'dsl/playtest/oracle.rows'), 'utf8'), rows = oracle.split('\n');
const problems = [];

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
const texts = [];
for (const [file, cmds] of byFile) {
  const source = readFileSync(resolve(root, file), 'utf8'), out = read(source, file), got = out.uses.map(u => `${u.startLine}:${u.keyword}`);
  texts.push(source); commands += got.length; steps += out.uses.filter(u => !NOT_STEPS.has(u.keyword)).length;
  const refused = out.uses.map(rule).filter(Boolean);
  if (out.diagnostics.length || refused.length || got.join() !== cmds.join()) problems.push(`file ${file}: ${[...out.diagnostics.map(place), ...refused].join() || 'no diagnostic'}; uses [${got}], the oracle has [${cmds}]`);
}
const header = /^# (\d+) files, (\d+) commands \((\d+) steps/m.exec(oracle)?.slice(1).join();
if (header !== [byFile.size, commands, steps].join()) problems.push(`files, commands, steps: the header of oracle.rows says ${header}, the reader has ${byFile.size},${commands},${steps}`);

// literals
let same = 0, include = 0, preprocessor = 0, known = 0, calls = 0;
const differing = new Set();
for (const m of rows.map(r => /^(accept|reject) (\S+):(\d+) (\S+) ?(.*)$/.exec(r)).filter(Boolean)) {
  calls++; texts.push(unescape(m[5]));
  const id = `${m[2]}:${m[3]}`, text = unescape(m[5]), words = text.split('\n').map(l => l.trim().split(/\s+/)[0]);
  if (words.some(w => PREPROCESSOR.has(w))) { preprocessor++; continue; }
  const out = read(text), verdict = out.diagnostics.length || out.uses.some(rule) ? 'reject' : 'accept';
  if (verdict === m[1]) { same++; if (KNOWN.has(id)) problems.push(`KNOWN ${id} is stale: the reader agrees with the oracle`); }
  else if (KNOWN.has(id)) { known++; differing.add(id); }
  else if (m[1] === 'reject' && words.includes('INCLUDE')) include++;
  else problems.push(`literal ${id} ${m[1]} ${JSON.stringify(text)}: the reader says ${verdict}`);
}
for (const id of KNOWN.keys()) if (!differing.has(id)) problems.push(`KNOWN ${id} matches no differing row`);
const calledFor = /^# (\d+) calls/m.exec(oracle)?.[1];
if (calledFor !== String(calls)) problems.push(`literals: the header of oracle.rows says ${calledFor} calls, the rows are ${calls}`);

// mutations: a script reads the same whatever its line ends and blanks are. A BOM is a character of the first line, so the columns of line 1 move (the host reads files as File.ReadAllText does and strips it): only codes and lines are compared
const sign = (out, columns) => [...out.uses.map(u => `${u.startLine}:${u.keyword}`), ...out.diagnostics.map(d => (columns ? place(d) : `${d.code}@${d.line}`))].join();
const MUTATIONS = [['CRLF', t => t.replace(/\n/g, '\r\n'), true], ['BOM', t => `\uFEFF${t}`, false], ['blanks at the end of a line', t => t.replace(/\n/g, ' \t\n'), true]];
let mutants = 0;
for (const text of texts) for (const [name, mutate, columns] of MUTATIONS) {
  mutants++;
  if (sign(read(mutate(text)), columns) !== sign(read(text), columns)) problems.push(`mutation ${name}: ${JSON.stringify(text.slice(0, 60))} is read differently`);
}

// examples
let blocks = 0;
const covered = new Set();   // the constructs that a SOURCE line of an example is read as: a line that reads is a positive example of its construct, in any block
for (const f of readdirSync(dirs[0]).filter(f => /^playtest.*\.examples$/.test(f))) {
  for (const b of readFileSync(resolve(dirs[0], f), 'utf8').split(/\n(?=EXAMPLE )/).map(b => b.split('\n'))) {
    blocks++;
    const src = b.filter(l => l.startsWith('  SOURCE ')).map(l => l.slice(9)).join('\n'), exp = b.filter(l => l.startsWith('  EXPECT diagnostic ')).map(l => l.slice(20).replace('DECLARE_', 'PT_').replace(' at ', '@')).sort().join();
    const out = read(src), got = out.diagnostics.map(place).sort().join();
    for (const u of out.uses) covered.add(u.def.name);
    if (got !== exp) problems.push(`example "${b[0].slice(8, 70)}" (${f}): through the SURFACE [${got}], in the file [${exp}]`);
  }
}
for (const c of forms) if (!covered.has(c.name)) problems.push(`construct ${c.name} is read from no SOURCE line of playtest*.examples: write a positive example`);

// probes: [text, why, expected]. Expected is the construct and the raw slots of each use (what BuildSteps and ParseQOV make of the tokens: read, not run) or a PT_ code of a rule, joined by " | ", or the first diagnostic.
const raws = b => Object.fromEntries([...b].map(([k, v]) => [k, v.raw])), slots = u => `${u.def.name} ${JSON.stringify(raws(u.head))}${u.items.length ? ` ${JSON.stringify(u.items.map(raws))}` : ''}`;
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
  ['WAIT_CAPTURED gold UNCHANGED OVER 2 TIMEOUT 8', 'AI/playtest-dsl.md:955: OVER before TIMEOUT; the builder takes timeout ?? timeout2', 'PT_WAIT_CAPTURED {"label":"gold","mode":"UNCHANGED","over":"2","timeout2":"8"}'],
  ['WAIT_CAPTURED gold INCREASED_BY TIMEOUT 5', 'AI/playtest-dsl.md:969: a BY mode without a sub-value; TIMEOUT is no operator', 'PT_WAIT_CAPTURED {"label":"gold","mode":"INCREASED_BY","timeout":"5"}'],
  ['CAPTURE_FRAMES 3 INTERVAL 0.5 CAMERA Main MODE list LABEL run1', 'PlaytestFrameCaptureTests.cs:33', 'PT_CAPTURE_FRAMES {"count":"3","interval":"0.5","camera":"Main","mode":"list","label":"run1"}'],
  ['WAIT_STABLE /Obj|C|f DELTA 1 OVER 3 TIMEOUT 10', 'PlaytestParserTests.cs:911', 'PT_WAIT_STABLE {"query":"/Obj|C|f","delta":"1","over":"3","timeout":"10"}'],
  ['ASSERT_CONSERVED SUM /A|C|f + /B|C|f == 100 OVER 5', 'PlaytestParserTests.cs:950: the names are one text, the builder splits it at +', 'PT_ASSERT_CONSERVED {"flag:SUM":"true","terms":"/A|C|f + /B|C|f","expected":"100","over":"5"}'],
  ['ASSERT_CONSERVED a + b OVER 5', 'SUM is optional, as in the hand parser (PlaytestParser.Internals.cs:381)', 'PT_ASSERT_CONSERVED {"terms":"a + b","over":"5"}'],
  ['SIMULATE random_walk DURATION 60 TIMESCALE 2 TARGET "/Player" FREQUENCY 5', 'Runtime/PlaytestParserTests.cs:401', 'PT_SIMULATE {"name":"random_walk","duration":"60","timescale":"2","target":"\\"/Player\\"","frequency":"5"}'],
  ['TRACE_FLOW FROM /A TO /B FIELD Count TIMEOUT 10', 'Runtime/PlaytestParserTests.cs:436', 'PT_TRACE_FLOW {"from":"/A","to":"/B","field":"Count","timeout":"10"}'],
  ['MOVE_PATH 1,0,0 > 5,0,0 TIMEOUT 8', 'PlaytestDslExtensionTests.cs:70: the waypoints end before TIMEOUT', 'PT_MOVE_PATH {"path":"1,0,0 > 5,0,0","timeout":"8"}'],
  ['ASSERT_BATCH\nASSERT Player/Health == 100\nASSERT Enemy/Health > 0 TIMEOUT 5\nEND', 'INTENDED I1: the value of a line of a block is the rest of it (the hand parser dropped TIMEOUT 5)', 'PT_ASSERT_BATCH {} [{"query":"Player/Health","op":"==","value":"100"},{"query":"Enemy/Health","op":">","value":"0 TIMEOUT 5"}]'],
  ['ASSERT_BATCH\nASSERT /[Box contains Items]/Slot|C|f == 1\nEND', 'the RULES see the lines of a block: the query is cut at contains', 'PT_BRACKET'],
  ['SWEEP_PATH /Player DWELL 0.4\n  1,0,0 > 2,0,0\nUNTIL /q|C|f >= 2 TIMEOUT 5\nASSERT /X|C|f == 1', 'CONTINUATION: the waypoints and UNTIL are the host\'s, the verb after them is read (PlaytestDslExtensionTests.cs:166)', 'PT_SWEEP_PATH {"path":"/Player","dwell":"0.4"} | PT_ASSERT {"query":"/X|C|f","op":"==","value":"1"}'],
  ['SWEEP_PATH /Player 0.4\n  1,0,0 > 2,0,0', 'CONTINUATION does not hide the error of the head (PlaytestDslExtensionTests.cs:159)', 'PT_SYNTAX@1:20'],
  ['INVOKE_REPEAT 2 /P C M\nwait 1', 'CONTINUATION: a verb in lower case ends it, so I4 is reported on that line', 'PT_UNKNOWN_KEYWORD@2:1'],
  ['COMPLETE_PURCHASE /Store/Item EXPECT\n  /Store|PlacementPurchase|IsPurchased\n  /Player|Currency|Coins > 0\nTIMEOUT 5\nLOG done', 'AI/playtest-dsl.md:387: names and TIMEOUT go on, LOG is read', 'PT_COMPLETE_PURCHASE {"path":"/Store/Item","flag:EXPECT":"true"} | PT_LOG {"message":"done"}'],
  ['INVOKE_REPEAT 3 /Player PlayerController TakeDamage 10\nEXPECT /Player|PlayerController|Health < 70 TIMEOUT 5', 'AI/playtest-dsl.md:516', 'PT_INVOKE_REPEAT {"count":"3","path":"/Player","component":"PlayerController","method":"TakeDamage","args":"10"}'],
];
for (const [text, why, exp] of probes) {
  const r = read(text), got = r.diagnostics[0] ? place(r.diagnostics[0]) : r.uses.map(u => rule(u) ?? slots(u)).join(' | ');
  if (got !== exp) problems.push(`probe ${JSON.stringify(text)} (${why}): ${got}`);
}

console.log(`files ${byFile.size}: commands ${commands}, steps ${steps} | literals ${calls}: same ${same}, host ${include + preprocessor} (INCLUDE ${include}, FOR/COMMENT ${preprocessor}), known ${known} | mutants ${mutants} | examples ${blocks}, constructs read ${covered.size}/${forms.length} | probes ${probes.length}`);
for (const p of problems) console.log('DIFF', p);
process.exit(problems.length ? 1 : 0);
