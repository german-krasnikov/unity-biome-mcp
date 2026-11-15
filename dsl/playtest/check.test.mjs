// check.mjs: what it refuses, what it leaves to the host and what it counts. usage: node --test dsl/playtest/*.test.mjs      env: BIOME_DSL_ROOT (as scripts/dsl.sh)
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { checkFiles, checkText } from './check.mjs';
import { load } from './surface.mjs';

const { read } = await load();
const dsl = text => text.replace(/^\n/, ''), check = (text, file = 'x.playtest') => checkText(read, dsl(text), file);
const where = r => r.problems.map(p => p.split(': ').slice(0, 2).join(': '));   // `file:line:col: CODE`, without the message

test('a script that the grammar reads is clean', () => {
  const r = check(`
# a comment
SETUP
INVOKE /Player Health TakeDamage 42
ASSERT /Player|Health|hp == 58 TIMEOUT 5
`);
  assert.deepEqual(r.problems, []);
  assert.equal(r.commands, 3);
});

test('a word where a number is due is refused at its column', () => {
  assert.deepEqual(where(check(`
WAIT 1
WAIT abc
`)), ['x.playtest:2:6: PT_NUMBER']);
});

test('a mistyped verb is refused at the verb, a token too many at the token', () => {
  const r = check(`
ASERT $x == 1
SET /A B C 1 2 3
`);
  assert.deepEqual(where(r), ['x.playtest:1:1: PT_UNKNOWN_KEYWORD', 'x.playtest:2:14: PT_SYNTAX']);
});

for (const [name, source, code] of [
  ['AND with OR', 'WAIT_UNTIL q1 == v1 AND q2 == v2 OR q3 == v3', 'PT_MIXED'],
  ['a fourth term', 'WAIT_UNTIL a == 1 AND b == 2 AND c == 3 AND d == 4', 'PT_CHAIN'],
  ['a blank and an operator word inside [..]', 'ASSERT /[Box contains Items]/Slot|C|f == 1', 'PT_BRACKET'],
]) {
  test(`${name} is refused by the rules of the grammar, at the line`, () => {
    assert.deepEqual(where(check(source)), [`x.playtest:1:1: ${code}`]);
  });
}

test('a $sigil where a typed slot is due is left to the host and counted, the rest of the file is still read', () => {
  const r = check(`
VAL $d 2
WAIT $d
WAIT abc
`);
  assert.deepEqual(where(r), ['x.playtest:3:6: PT_NUMBER']);
  assert.equal(r.deferred, 1);
});

test('a $sigil is no verb', () => {
  assert.deepEqual(where(check('$cmd 1')), ['x.playtest:1:1: PT_UNKNOWN_KEYWORD']);
});

test('the lines of MACRO and CALL are the host\'s and counted, the body of a macro is read', () => {
  const r = check(`
MACRO check_health $path $expected
  ASSERT $path|HealthComponent|CurrentHealth == $expected
END_MACRO
CALL check_health /Player 100
`);
  assert.deepEqual(r.problems, []);
  assert.deepEqual([r.commands, r.skipped], [1, 3]);
});

test('the lines that go on after SWEEP_PATH are the host\'s', () => {
  assert.deepEqual(check(`
SWEEP_PATH /Player DWELL 0.4
  1,0,0 > 2,0,0
UNTIL /q|C|f >= 2 TIMEOUT 5
ASSERT /X|C|f == 1
`).problems, []);
});

test('a BOM and CRLF line ends move no position', () => {
  const text = dsl(`
WAIT 1
WAIT abc
`);
  assert.deepEqual(where(check(`\u{FEFF}${text.replace(/\n/g, '\r\n')}`)), where(check(text)));
});

test('no file is no pass; a problem in a file fails the run and the summary comes last', () => {
  assert.equal(checkFiles(read, []).code, 1);
  const r = checkFiles(read, [['a.playtest', 'WAIT 1\n'], ['b.playtest', 'WAIT abc\n']]);
  assert.equal(r.code, 1);
  assert.match(r.lines[0], /^b\.playtest:1:6: PT_NUMBER: /);
  assert.match(r.lines.at(-1), /^playtest-check: 2 files, 1 commands, 1 problems; /);
});
