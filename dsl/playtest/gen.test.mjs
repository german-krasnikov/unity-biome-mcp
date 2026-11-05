// gen.mjs: how a form becomes C# and a row of the table, and what stops the step. usage: node --test dsl/playtest/*.test.mjs      env: BIOME_DSL_ROOT (as scripts/dsl.sh)
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { cs, element, fill, form, slotType, splice, syntax, table } from './gen.mjs';
import { load } from './surface.mjs';

const lit = word => ({ k: 'lit', word }), param = (name, type, more = {}) => ({ k: 'param', name, type, ...more }), opt = (...els) => ({ k: 'opt', els });

test('a C# string escapes the backslash, the quote and anything outside printable ASCII', () => {
  assert.equal(cs('a"b\\c\n\u{e9}'), '"a\\"b\\\\c\\u000a\\u00e9"');
});

test('a slot becomes S(name, type, argument, code, lazy) and a group G(..)', () => {
  assert.equal(element(param('timeout', 'number', { code: 'NUMBER' }), 'PT'), 'S("timeout", SlotType.Number, code: "PT_NUMBER")');
  assert.equal(element(param('op', 'match([<>]=?|==)'), 'PT'), 'S("op", SlotType.Match, "[<>]=?|==")');
  assert.equal(element(param('query', 'text', { lazy: true }), 'PT'), 'S("query", SlotType.Text, lazy: true)');
  assert.equal(element(opt(lit('AS'), param('mode', 'enum(a|b)')), 'PT'), 'G(W("AS"), S("mode", SlotType.Enum, "a|b"))');
});

test('a line is F(construct, elements), a block B(construct, head, lines)', () => {
  assert.equal(form({ name: 'PT_LOG', pattern: [lit('LOG'), opt(param('message', 'text'))] }, 'PT'), 'F("PT_LOG", W("LOG"), G(S("message", SlotType.Text)))');
  assert.equal(form({ name: 'PT_B', head: [lit('B')], items: [[lit('X')], [lit('Y')]] }, 'PT'), 'B("PT_B", new[] { W("B") }, new[] { W("X") }, new[] { W("Y") })');
});

for (const type of ['list<text>', 'ident', 'ref(KIND)', 'cmp', 'word']) {
  test(`a slot of the type ${type} stops the step, which names the type`, () => {
    assert.throws(() => slotType(type), new RegExp(`the type ${type.replace(/[()<>]/g, '\\$&')} is not carried`));
  });
}

test('a UNIQUE slot stops the step', () => {
  assert.throws(() => element(param('name', 'text', { unique: true }), 'PT'), /UNIQUE/);
});

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

test('a place of the template is filled with its value as it is, and a place without a value or a value without a place stops the step', () => {
  assert.equal(fill('a = /*ONE*/; b = /*ONE*/;', { ONE: '"$&"' }), 'a = "$&"; b = "$&";');
  assert.throws(() => fill('/*ONE*/ /*TWO*/', { ONE: '1' }), /\/\*TWO\*\/, which the step does not fill/);
  assert.throws(() => fill('/*ONE*/', { ONE: '1', TWO: '2' }), /holds no \/\*TWO\*\//);
});
