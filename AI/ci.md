# CI Pitfalls & Lessons Learned

## 1. File moves require a full stale-path sweep

Moving a `.cs` file (e.g. `Editor/JsonHelper.cs` → `Editor/Primitives/JsonHelper.cs`) breaks:

- **`Tests~/` csproj** — `<Compile Include="../../Editor/X.cs" />` references
- **Python tests** — `Path("unity-plugin/Editor/X.cs")` hardcoded paths in `scripts/tests/`
- **Golden files** — `server/tests/golden/registration.txt` if required/optional changed
- **AI/ docs** — stale file paths in architecture/structure docs
- **`.claude/skills/`** — stale paths in skill docs

**Checklist after any file move:**
```bash
# Find all references to the old path
grep -rn "Editor/OldName.cs" unity-plugin/Tests~/ server/tests/ scripts/tests/ AI/ .claude/ --include="*.py" --include="*.cs" --include="*.csproj" --include="*.md"
```

## 1b. Python scripts in scripts/ may be test-imported via sys.path

`scripts/decisions_golden.py`, `dsl_golden.py`, `dsl_content_lock.py`, `playtest_oracle.py` are imported by `server/tests/` via `conftest.py` sys.path injection. They look like orphans (no YAML reference) but are NOT movable.

**Before moving any script, also check:**
```bash
grep -rn "import <module_name>\|from <module_name>" server/tests/ scripts/tests/ --include="*.py"
```

## 1c. scripts/dev/ convention

`scripts/dev/` holds true orphan scripts — not referenced by CI YAML, not imported by tests, not in sys.path. Currently: `check_relay_schema.py`, `create_sonar_sln.py`, `dedup_sln.py`, `export_agent_schema.py`.

A script referenced only in a test error message string (not an import) counts as orphan.

## 2. Editing generated files requires banner + content-lock update

Generated files (from `dsl/tools.biome`) carry a `source-sha256` banner. Editing either the source or the generated file without updating both breaks 3 test gates:

| Test | What it checks |
|------|---------------|
| `test_dsl_fresh` | banner sha256 matches source file hash |
| `test_dsl_content_lock` | file hash matches `dsl/content-lock.json` |
| `test_toolmeta_vs_golden` | ToolMeta required/optional matches `registration.txt` |

**After editing `dsl/tools.biome` + generated files:**
```bash
# 1. Compute new source sha256
python3 -c "import hashlib,re; t=re.sub(r'\r\n?','\n',open('dsl/tools.biome','rb').read().decode('utf-8').removeprefix('﻿')); print(hashlib.sha256(t.encode()).hexdigest())"

# 2. Update ALL generated files that reference old sha (not just the ones you edited)
OLD_SHA="..." NEW_SHA="..." grep -rl "$OLD_SHA" server/src/unity_mcp/tools/generated/ unity-plugin/Editor/Generated/ unity-plugin/Editor/Primitives/ | xargs sed -i '' "s/$OLD_SHA/$NEW_SHA/g"

# 3. Refresh content-lock.json
python3 -c "
import hashlib,json,pathlib
lock=json.loads(pathlib.Path('dsl/content-lock.json').read_text())
for k in list(lock['files']):
    p=pathlib.Path(k)
    if p.exists():
        sha=hashlib.sha256(p.read_bytes()).hexdigest()
        if lock['files'][k]!=sha: lock['files'][k]=sha
pathlib.Path('dsl/content-lock.json').write_text(json.dumps(lock,indent=2,ensure_ascii=False)+'\n')
"

# 4. Update golden if required/optional changed
# Edit server/tests/golden/registration.txt

# 5. Remove stale KNOWN_DIVERGENT entries in test_toolmeta_vs_golden.py
```

## 3. Unity Library cache key must include C# sources

`unity-tests.yml` caches `Library/ScriptAssemblies`. If the cache key doesn't hash `.cs` files, a cached stale DLL survives even when source changed → tests run old code.

**Current key** (correct):
```yaml
key: unity-library-${{ runner.os }}-${{ hashFiles('...packages-lock.json', '...ProjectSettings/**', 'unity-plugin/Editor/**/*.cs', 'unity-test-project/Assets/**/*.cs') }}
```

## 4. ArgContract changes test behavior

When a tool enters `ArgContract._enabled`, validation moves from the handler (throws `ArgumentException` → `Debug.LogWarning`) to `CommandValidator.Validate` (returns error JSON directly, no exception, no Warning log).

Tests that `LogAssert.Expect(LogType.Warning, ...)` for validation errors will break. Fix: assert on the JSON response instead.

## 5. DSL `ARG path str` blocks create actions

`ARG path str` (no DEFAULT) generates `RequireString` → throws on empty. Tools with `ACTION create` that don't need `path` (they use `asset_path` or create new objects) must declare `ARG path ostr DEFAULT None`.

Affected tools: `animation`, `particle`, `timeline`.

## 6. BODY wrapper lazy imports break mocks on Windows

`catalog_tools.py` BODY wrappers do `from .. import X as _biome_body_mod` at call time. On Windows, this lazy import can resolve to a different module object than what `monkeypatch` patched.

**Fix:** test the inner function directly (e.g. `_fetch_test_run_json`) instead of the BODY wrapper.

## 7. SonarCloud is external and non-blocking

SonarCloud Code Analysis is an external quality gate. It fails independently of our CI. Pre-existing on master. Not a merge blocker.

## 8. jscpd baseline is pinned

`JSCPD_BASELINE_REF` in `ci-python.yml` is a pinned commit SHA. After bulk file moves or extractions that increase duplication count, bump the baseline to the commit that introduced the new clones.
