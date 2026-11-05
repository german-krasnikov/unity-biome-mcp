// The playtest grammar printed as data (step 7.5 D1), from the PT_* constructs of dsl/constructs/playtest.construct, into two places:
//   Runtime/Playtest/Core/Generated/PlaytestGrammar.g.cs  Keywords (the verbs) and Forms (the constructs, in the order of the SURFACE line): what the C# host matches a line against
//   AI/playtest-dsl.md                                    the table of verbs between <!-- biome:begin verbs --> and <!-- biome:end verbs -->
// GENERATE playtest-grammar derive {dir}/playtest/gen.mjs grammar OUT <construct file> <template> <C# file> <md file>: the driver writes the two files, or in --check compares them (P16).
// The C# is PlaytestGrammar.cs.tmpl with the /*NAME*/ places filled. A type that the table cannot carry (it carries text, number, uint, match(..), enum(..)) stops the step.
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';
import { load, verbOf } from './surface.mjs';

/** A C# string: `\` and `"` escaped, anything outside printable ASCII as a \u escape. */
export const cs = s => `"${s.replace(/[\\"]|[^\x20-\x7e]/g, c => (c === '\\' || c === '"' ? `\\${c}` : `\\u${c.charCodeAt(0).toString(16).padStart(4, '0')}`))}"`;

const SIMPLE = { text: 'Text', number: 'Number', uint: 'UInt' };
/** `[SlotType member, Arg]` of a slot type of the construct file. */
export function slotType(type) {
  const m = /^(match|enum)\((.+)\)$/.exec(type);
  if (m) return [m[1] === 'match' ? 'Match' : 'Enum', m[2]];
  if (SIMPLE[type]) return [SIMPLE[type]];
  throw new Error(`playtest grammar: the type ${type} is not carried by PlaytestGrammar.g.cs (text, number, uint, match(..), enum(..))`);
}

/** The C# of one element of a form: W word, S slot, G optional group (the helpers of PlaytestGrammar.cs.tmpl). */
export function element(e, prefix) {
  if (e.k === 'lit') return `W(${cs(e.word)})`;
  if (e.k === 'opt') return `G(${e.els.map(x => element(x, prefix)).join(', ')})`;
  if (e.unique) throw new Error(`playtest grammar: ${e.name} is UNIQUE, which the table does not carry`);
  const [kind, arg] = slotType(e.type);
  return `S(${cs(e.name)}, SlotType.${kind}${arg ? `, ${cs(arg)}` : ''}${e.code ? `, code: ${cs(`${prefix}_${e.code}`)}` : ''}${e.lazy ? ', lazy: true' : ''})`;
}

/** The C# of a form: F for a line, B for a block (its head, then the line forms of its items). */
export const form = (f, prefix) => {
  const list = els => els.map(e => element(e, prefix)).join(', ');
  return f.head ? `B(${cs(f.name)}, new[] { ${list(f.head)} }, ${f.items.map(i => `new[] { ${list(i)} }`).join(', ')})` : `F(${cs(f.name)}, ${list(f.pattern)})`;
};

/** How a slot reads in the table: <name>, <name...> for the rest of the line, <name:A|B> for a word of a list. */
const slot = e => { const [kind, arg] = slotType(e.type); return `<${e.name}${kind === 'Enum' ? `:${arg}` : kind === 'Text' ? '...' : ''}>`; };
export const syntax = els => els.map(e => (e.k === 'lit' ? e.word : e.k === 'opt' ? `[${syntax(e.els)}]` : slot(e))).join(' ');

/** The rows of the table of verbs, by verb in ordinal order (a verb of two forms has two rows, in the order of the SURFACE line). */
export function table(forms) {
  const cell = f => (f.head ? `\`${syntax(f.head)}\`, then lines \`${f.items.map(syntax).join('` or `')}\`, then \`END\`` : `\`${syntax(f.pattern)}\``).replaceAll('|', '\\|');
  const rows = forms.map(f => [verbOf(f), `| ${verbOf(f)} | ${cell(f)} |`]).sort(([a], [b]) => (a < b ? -1 : a > b ? 1 : 0));
  return ['| Verb | Syntax |', '|---|---|', ...rows.map(r => r[1])].join('\n');
}

/** `text` with what lies between the markers of `region` replaced by `body`; the markers must be there once each, in order. */
export function splice(text, region, body) {
  const begin = `<!-- biome:begin ${region} -->`, end = `<!-- biome:end ${region} -->`;
  if (text.split(begin).length !== 2 || text.split(end).length !== 2 || text.indexOf(end) < text.indexOf(begin)) throw new Error(`the ${region} region must be a line ${begin} and, below it, a line ${end}`);
  return `${text.slice(0, text.indexOf(begin) + begin.length)}\n${body}\n${text.slice(text.indexOf(end))}`;
}

/** `template` with each C comment that holds only a name in capitals replaced by the value of that name; a place without a value, or a value without a place, stops the step. */
export function fill(template, values) {
  const used = new Set();
  const text = template.replace(/\/\*([A-Z]+)\*\//g, (_, name) => {
    if (!(name in values)) throw new Error(`the template holds /*${name}*/, which the step does not fill`);
    used.add(name);
    return values[name];
  });
  const unused = Object.keys(values).find(name => !used.has(name));
  if (unused) throw new Error(`the template holds no /*${unused}*/`);
  return text;
}

/** The step: `[[C# file, text], [md file, text]]`. */
export async function grammar(context) {
  const [source, template, csFile, mdFile] = context.generateArgs, pt = await load(context.toolchainRoot), { prefix, name, ordered, words, commentLine } = pt.surface;
  if (resolve(source) !== pt.path) throw new Error(`playtest grammar: the step reads ${source}, the SURFACE is defined in ${pt.path}`);
  const { outputBanner } = await import(pathToFileURL(resolve(pt.toolchain, 'packages/biome/src/support/banner.mjs')).href);
  const keywords = [...pt.verbs].sort(), rows = [];
  for (let i = 0; i < keywords.length; i += 6) rows.push(`            ${keywords.slice(i, i + 6).map(cs).join(', ')},`);
  const csText = fill(readFileSync(template, 'utf8'), {
    SURFACE: cs(name), PREFIX: cs(prefix), ORDERED: String(ordered), WORDS: String(words), COMMENT: String(commentLine),
    KEYWORDS: rows.join('\n'), FORMS: pt.forms.map(f => `            ${form(f, prefix)},`).join('\n'),
  });
  return [[csFile, outputBanner(context.project, source, readFileSync(source, 'utf8'), csFile) + csText], [mdFile, splice(readFileSync(mdFile, 'utf8'), 'verbs', table(pt.forms))]];
}
