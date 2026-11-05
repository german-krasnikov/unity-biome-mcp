// The table of verbs of AI/playtest-dsl.md (step 7.5 D1), printed from the PT_* constructs of dsl/constructs/playtest.construct between <!-- biome:begin verbs --> and <!-- biome:end verbs -->.
// GENERATE playtest-verbs derive {dir}/playtest/gen.mjs verbs OUT <construct file> <md file>: the driver writes the file, or in --check compares it (P16).
// The C# reader of the same constructs is printed by the compiler (GENERATE playtest-reader reader --surface playtest, step 7.5 R7); a type of slot that it cannot carry stops that step.
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { load, verbOf } from './surface.mjs';

/** How a slot reads in the table: <name>, <name...> for the rest of the line, <name:A|B> for a word of a list. */
const slot = e => { const list = /^enum\((.+)\)$/.exec(e.type); return `<${e.name}${list ? `:${list[1]}` : e.type === 'text' ? '...' : ''}>`; };
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

/** The step: `[[md file, text]]`. */
export async function verbs(context) {
  const [source, mdFile] = context.generateArgs, pt = await load(context.toolchainRoot);
  if (resolve(source) !== pt.path) throw new Error(`playtest verbs: the step reads ${source}, the SURFACE is defined in ${pt.path}`);
  return [[mdFile, splice(readFileSync(mdFile, 'utf8'), 'verbs', table(pt.forms))]];
}
