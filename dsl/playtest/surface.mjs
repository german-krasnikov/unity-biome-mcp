// The playtest SURFACE as the tools of dsl/playtest see it (compare.mjs against the oracle, check.mjs on the scripts, gen.mjs on the grammar): the definitions of dsl/constructs read by
// the toolchain of TOOLCHAIN (a biome-dsl checkout, BIOME_DSL_ROOT), how a text is read through them, and the two things the grammar cannot say, which every reader of a script obeys alike.
import { dirname, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

export const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..'), dirs = [resolve(root, 'dsl/constructs')];
export const place = d => `${d.code}@${d.line}:${d.col}`;
/** The first word of a construct (a form of the SURFACE starts with one: the verb). */
export const verbOf = f => (f.pattern ?? f.head)[0].word;

// HOST rows: the text-to-text phases of the hand parser (design class E), which the host runs before a line reaches the reader; the grammar has no construct for them
export const PREPROCESSOR = new Set(['FOR', 'END_FOR', 'COMMENT', 'END_COMMENT', 'MACRO', 'END_MACRO', 'CALL']);

// CONTINUATION: after the head of SWEEP_PATH, COMPLETE_PURCHASE or INVOKE_REPEAT, the lines whose first word is no verb of the SURFACE (waypoints, UNTIL, EXPECT, TIMEOUT, a list of
// names) belong to the host: the grammar reads the head, the reader gets those lines blank (the line numbers stay). A verb ends them, as in the native lookaheads (H3 keeps those;
// COMPLETE_PURCHASE there reads on to a TIMEOUT line, whatever comes first). Blank and comment lines neither start nor end a continuation.
const HEADS = new Set(['SWEEP_PATH', 'COMPLETE_PURCHASE', 'INVOKE_REPEAT']);
export const host = (text, verbs) => {
  let on = false;
  return text.split('\n').map(line => {
    const word = line.trim().split(/\s+/)[0];
    if (!word || word[0] === '#') return line;
    if (HEADS.has(word)) { on = true; return line; }
    if (on && !verbs.has(word.toUpperCase())) return '';
    on = false;
    return line;
  }).join('\n');
};

// RULES: what a use must obey and the grammar cannot say; each is a way to read a line whole and wrongly. AND and OR are groups of their own, so a line can hold both
// (PT_MIXED); a chain of four terms reads its 4th into the value of the 3rd (PT_CHAIN); TOKENS words cuts at blanks, so a name with a blank and an operator word in
// [..] is cut at that word (PT_BRACKET), where the hand parser keeps [..] whole; a line of an ASSERT_BATCH is a use of its own. The builder (H2) and check.mjs (D1) refuse the same three.
export const rule = u => {
  const sets = [u.head, ...u.items], has = (...k) => k.some(x => u.head.has(x)), raw = (b, k) => b.get(k)?.raw ?? '';
  if (has('q2', 'q3') && has('q4', 'q5')) return 'PT_MIXED';
  const bare = v => [...v.matchAll(/"[^"]*"|\S+/g)].slice(1).some(t => t[0] === 'AND' || t[0] === 'OR');   // after the first token, a word outside quotes
  if (['v1', 'v2', 'v3', 'v4', 'v5'].some(k => bare(raw(u.head, k)))) return 'PT_CHAIN';
  const open = v => { let d = 0; for (const c of v.replace(/"[^"]*"/g, '')) d = Math.max(0, d + (c === '[' ? 1 : c === ']' ? -1 : 0)); return d > 0; };   // as SplitTokens counts
  return sets.some(b => ['query', 'q1', 'q2', 'q3', 'q4', 'q5'].some(k => open(raw(b, k)))) ? 'PT_BRACKET' : null;
};

/**
 * The grammar and a reader over the toolchain at `toolchain` (default: BIOME_DSL_ROOT, the checkout of TOOLCHAIN, as scripts/dsl.sh needs it).
 * `forms` are the constructs in the order of the SURFACE line (ORDERED: the first that reads a line wins), `verbs` the first word of each, `read(text, file)` the uses and diagnostics.
 */
export async function load(toolchain = process.env.BIOME_DSL_ROOT && resolve(process.env.BIOME_DSL_ROOT, 'implementations/typescript')) {
  if (!toolchain) throw new Error('set BIOME_DSL_ROOT to the biome-dsl checkout named by TOOLCHAIN in dsl/biome.project');
  const { readSurface, surfaceDefs } = await import(pathToFileURL(resolve(toolchain, 'packages/biome/src/support/surface-host.mjs')).href);
  const { surface, constructs, path } = surfaceDefs('playtest', dirs);
  const forms = surface.constructs.map(name => constructs.find(c => c.name === name)), bad = forms.find(f => (f.pattern ?? f.head)[0]?.k !== 'lit');
  if (bad) throw new Error(`${path}: ${bad.name} does not start with a word`);
  const verbs = new Set(forms.map(verbOf));
  return { toolchain, surface, forms, verbs, path, read: (text, file = 'x') => readSurface('playtest', host(text, verbs), file, { dirs }) };
}
