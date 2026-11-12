// C# constants for the ProjectConfigTarget data contract (T1/Q10, dsl/data/projectconfig.data-contract):
// `biome data generate --target cs` does not exist in this toolchain yet -- TOOLCHAIN 20fd6e4d (dsl/biome.project)
// landed only Core's naming/layout hooks for a future C# data target ("feat(core): name and layout hooks for a
// C# data target"); no language package exports a csDataTarget yet (confirmed: `biome data generate --target cs`
// exits 2 with LANG_UNSUPPORTED). This is the sanctioned stand-in for that gap -- a GRAMMAR_V1 `derive` step,
// the same escape hatch dsl/playtest/gen.mjs already uses for a surface the toolchain does not fully print. It
// reads the same contract and rows the --target py step validates and prints the DataPlan by hand in C# 9.
// Delete this file and the GENERATE cs-projectconfig line once biome ships --target cs, and switch the step to
// `data ... --target cs --out ...` directly (GENERATE py-projectconfig below is the pattern to copy).
import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { dirname, relative, resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

/** sha256 of a source's text (no BOM, CRLF normalized to LF) -- same formula as kit's banner (support/banner.mjs
 *  sourceSha256), so scripts/dsl_content_lock.py's BANNER regex locks this file exactly as it locks the Python one. */
const sourceSha256 = text => createHash('sha256').update(text.replace(/^\uFEFF/, '').replace(/\r\n?/g, '\n')).digest('hex');

const KW = new Set(['abstract', 'as', 'base', 'bool', 'break', 'byte', 'case', 'catch', 'char', 'checked', 'class', 'const', 'continue', 'decimal', 'default', 'delegate', 'do', 'double', 'else', 'enum', 'event', 'explicit', 'extern', 'false', 'finally', 'fixed', 'float', 'for', 'foreach', 'goto', 'if', 'implicit', 'in', 'int', 'interface', 'internal', 'is', 'lock', 'long', 'namespace', 'new', 'null', 'object', 'operator', 'out', 'override', 'params', 'private', 'protected', 'public', 'readonly', 'ref', 'return', 'sbyte', 'sealed', 'short', 'sizeof', 'static', 'string', 'struct', 'switch', 'this', 'throw', 'true', 'try', 'typeof', 'uint', 'ulong', 'unchecked', 'unsafe', 'ushort', 'using', 'virtual', 'void', 'volatile', 'while']);
const id = n => (KW.has(n) ? '@' + n : n);
// C# convention is PascalCase public fields; the contract's own field names (key, relativePath, ...) stay the
// constructor PARAMETER names (and the named-argument keys at each row's call site), so this only affects the
// struct's field declarations -- the one place backward compat with the hand-written source (Key, RootKey, ...) matters.
const pascal = n => n[0].toUpperCase() + n.slice(1);
const SC = { string: 'string', bool: 'bool', int: 'int', number: 'double', any: 'object' };
const item = t => (t.k === 'scalar' ? SC[t.name] : t.k === 'ref' || t.k === 'enum' ? 'string' : t.name);
const type = t => (t.k === 'list' ? `${item(t.item)}[]` : item(t));
const str = s => `"${s.replace(/\\/g, '\\\\').replace(/"/g, '\\"').replace(/\n/g, '\\n').replace(/\r/g, '\\r').replace(/\t/g, '\\t')}"`;
const val = n => {
  if (n.k === 'text') return str(n.v);
  if (n.k === 'bool' || n.k === 'int' || n.k === 'number') return String(n.v);
  if (n.k === 'null') return 'null';
  throw new Error(`gen-cs-data: unsupported value kind ${n.k} (extend val() in dsl/data/gen-cs-data.mjs)`);
};

/** `record` (one RECORD of the contract) and its row set `list` printed as a readonly struct + a static holder --
 *  the exact shape unity-plugin/Editor/Wizard/ProjectConfigTargets.cs hand-wrote before T1 (same field names and
 *  types), so the consumer needed no change beyond reading `${record.name}Rows.All` instead of its own array. */
function printCs(record, list, ns) {
  const ctorArgs = record.fields.map(f => `${type(f.type)} ${id(f.name)}${f.presence === 'optional' ? ' = null' : ''}`).join(', ');
  const struct = `    internal readonly struct ${record.name}\n    {\n${record.fields.map(f => `        internal readonly ${type(f.type)} ${pascal(f.name)};`).join('\n')}\n        internal ${record.name}(${ctorArgs})\n        {\n${record.fields.map(f => `            ${pascal(f.name)} = ${id(f.name)};`).join('\n')}\n        }\n    }`;
  const rows = `    internal static class ${record.name}Rows\n    {\n        internal static readonly ${record.name}[] All =\n        {\n${list.rows.map(r => `            new ${record.name}(${r.fields.map(m => `${id(m.name)}: ${val(m.value)}`).join(', ')}),`).join('\n')}\n        };\n    }`;
  return `namespace ${ns}\n{\n${struct}\n\n${rows}\n}\n`;
}

/** The step: `[[outFile, text]]`. `context.generateArgs` is `[contractFile, rowsFile, outFile]`, the three paths
 *  after the `OUT` placeholder on the GENERATE line (no watched-file guard: this module exports no `OUT`). */
export async function projectConfigTargets(context) {
  const [contractFile, rowsFile, outFile] = context.generateArgs;
  const toolchain = process.env.BIOME_DSL_ROOT && resolve(process.env.BIOME_DSL_ROOT, 'implementations/typescript');
  if (!toolchain) throw new Error('set BIOME_DSL_ROOT to the biome-dsl checkout named by TOOLCHAIN in dsl/biome.project');
  const { readDataContract, validateData, planData } = await import(pathToFileURL(resolve(toolchain, 'packages/biome/src/support/surface-host.mjs')).href);
  const { readDataRows } = await import(pathToFileURL(resolve(toolchain, 'packages/biome/src/support/data-reader.mjs')).href);

  const contractText = readFileSync(contractFile, 'utf8');
  const { contract, diagnostics: contractProblems } = readDataContract(contractText, contractFile);
  if (!contract) throw new Error(`gen-cs-data: ${contractFile}: ${contractProblems.map(d => d.message).join('; ')}`);

  const read = readDataRows(readFileSync(rowsFile, 'utf8'), rowsFile);
  if (read.diagnostics.length) throw new Error(`gen-cs-data: ${rowsFile}: ${read.diagnostics.map(d => d.message).join('; ')}`);

  const record = contract.records[0];
  const sources = [{ sourceId: rowsFile, record: record.name, rows: read.rows }];
  const checked = validateData(contract, sources);
  if (checked.diagnostics.length) throw new Error(`gen-cs-data: ${checked.diagnostics.map(d => d.message).join('; ')}`);

  const plan = planData(contract, checked.rows, sources.map(s => ({ record: s.record, sourceId: s.sourceId })));
  const planRecord = plan.records.find(r => r.name === record.name), list = plan.lists.find(l => l.record === record.name);

  const root = resolve(dirname(contractFile), '..', '..'), bannerPath = relative(root, contractFile).split('\\').join('/');
  const banner = `// Generated from ${bannerPath}\n// source-sha256=${sourceSha256(contractText)}\n// Edit the source, then run biome generate.\n\n`;
  return [[outFile, banner + printCs(planRecord, list, 'UnityMCP.Editor.Wizard')]];
}
