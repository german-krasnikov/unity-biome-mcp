// gen.mjs: how a form becomes a row of the table of verbs. usage: node --test dsl/playtest/*.test.mjs      env: BIOME_DSL_ROOT (as scripts/dsl.sh)
// The C# of the forms is printed by the compiler (GENERATE playtest-reader reader); its tests are reader-cs.sources of biome-dsl and Tests~/PlaytestGrammar.
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { splice, syntax, table } from './gen.mjs';
import { load } from './surface.mjs';

const lit = word => ({ k: 'lit', word }), param = (name, type, more = {}) => ({ k: 'param', name, type, ...more }), opt = (...els) => ({ k: 'opt', els });

test('the table reads <name>, <name...> for the rest of the line, <name:A|B> for a word of a list, [..] for a group, and keeps | out of the cell syntax', () => {
  const els = [lit('SET'), param('path', 'match(\\S+)'), param('rest', 'text'), opt(lit('AS'), param('mode', 'enum(a|b)'))];
  assert.equal(syntax(els), 'SET <path> <rest...> [AS <mode:a|b>]');
  assert.match(table([{ name: 'PT_SET', pattern: els }]), /\| SET \| `SET <path> <rest\.\.\.> \[AS <mode:a\\\|b>\]` \|$/);
});

test('the table has a row per form, ordered by verb in code units, and two rows for a verb of two forms', async () => {
  const { forms } = await load(), rows = table(forms).split('\n').slice(2), verbs = rows.map(r => r.split(' | ')[0].slice(2));
  assert.equal(rows.length, forms.length);
  assert.deepEqual(verbs, [...verbs].sort());
  assert.equal(verbs.filter(v => v === 'ASSERT').length, 2);
});

test('a region is replaced between its markers, once; a missing, repeated or reversed marker stops the step', () => {
  const page = 'intro\n<!-- biome:begin verbs -->\nold\n<!-- biome:end verbs -->\noutro\n';
  assert.equal(splice(page, 'verbs', 'new'), 'intro\n<!-- biome:begin verbs -->\nnew\n<!-- biome:end verbs -->\noutro\n');
  for (const bad of ['no region\n', page + page, '<!-- biome:end verbs -->\n<!-- biome:begin verbs -->\n']) assert.throws(() => splice(bad, 'verbs', 'new'), /must be a line/);
});
