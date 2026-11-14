// The playtest scripts of the repository against the playtest grammar (step 7.5 D1): every `*.playtest` file that git lists, read whole through the SURFACE as it is on disk
// (before INCLUDE, MACRO and VAL), and the RULES that the grammar cannot say (surface.mjs). A problem is `file:line:col: PT_CODE: message`; exit 1 on a problem, and on no file at all.
// usage: node dsl/playtest/check.mjs [file...]      env: BIOME_DSL_ROOT, the biome-dsl checkout of TOOLCHAIN (as scripts/dsl.sh)
// The host does after the file what the grammar does not read, so the check leaves it, and counts it: a `$sigil` where a typed slot is due (VAL puts its value in before the line is
// a step; the reader of the expanded line refuses what is left), and the lines of MACRO, CALL, FOR and COMMENT (text to text). The lines after INCLUDE are not read either.
import { execFileSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';
import { load, PREPROCESSOR, root, rule } from './surface.mjs';

const RULES = {
  PT_MIXED: 'AND and OR in one WAIT_UNTIL: write two waits',
  PT_CHAIN: 'a chain has at most three terms: the fourth would be read into the value of the third',
  PT_BRACKET: 'a name with a blank and an operator word inside [..] is cut at that word: write the name in quotes',
};
const SIGIL = /\$[A-Za-z_]\w*/, firstWord = line => line.trim().split(/\s+/)[0];

/** One file: `{ problems, commands, deferred, skipped }`; `read` is the reader of load(). */
export function checkText(read, text, file) {
  const lines = text.replace(/^\u{FEFF}/u, '').split('\n'), host = lines.map(l => PREPROCESSOR.has(firstWord(l)));
  const out = read(lines.map((l, i) => (host[i] ? '' : l)).join('\n'), file);
  const row = d => lines[d.line - 1] ?? '', token = d => row(d).slice(d.col - 1).match(/^(?:"[^"]*"|\S+)/)?.[0] ?? '';
  const later = d => d.col > row(d).search(/\S/) + 1;   // a $sigil is no verb: the hand parser keeps a VAL from starting with one
  const refused = out.diagnostics.filter(d => !(later(d) && SIGIL.test(token(d))));
  const rules = out.uses.flatMap(u => (rule(u) ? [{ file, line: u.span.line, col: u.span.column, code: rule(u), message: RULES[rule(u)] }] : []));
  const problems = [...refused, ...rules].sort((a, b) => a.line - b.line || a.col - b.col).map(d => `${file}:${d.line}:${d.col}: ${d.code}: ${d.message}`);
  return { problems, commands: out.uses.length, deferred: out.diagnostics.length - refused.length, skipped: host.filter(Boolean).length };
}

/** The files `[name, text]` read together: the lines to print and the exit code. */
export function checkFiles(read, files) {
  const all = files.map(([name, text]) => checkText(read, text, name)), sum = k => all.reduce((n, r) => n + r[k], 0), problems = all.flatMap(r => r.problems);
  const line = `playtest-check: ${files.length} files, ${sum('commands')} commands, ${problems.length} problems; left to the host: ${sum('deferred')} lines with a $sigil in a typed slot, ${sum('skipped')} lines of MACRO, CALL, FOR, COMMENT`;
  return files.length ? { lines: [...problems, line], code: problems.length ? 1 : 0 } : { lines: ['playtest-check: no .playtest file: this check would pass without checking'], code: 1 };
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  try {
    const named = process.argv.slice(2);
    const names = named.length ? named : execFileSync('git', ['ls-files', '-z', '--cached', '--others', '--exclude-standard', '--', '*.playtest'], { cwd: root, encoding: 'utf8' }).split('\0').filter(Boolean).sort();
    const { lines, code } = checkFiles((await load()).read, names.map(n => [n, readFileSync(resolve(named.length ? '.' : root, n), 'utf8')]));
    console.log(lines.join('\n'));
    process.exit(code);
  } catch (error) { console.error(`playtest-check: ${error.message}`); process.exit(2); }
}
