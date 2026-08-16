# Contributing to Unity Biome MCP

Thank you for contributing. This guide covers repository setup, the tests that
protect each part of the project, and the documentation contract.

## Set up the repository

Requirements:

- Git 2.14 or newer on `PATH`
- Python 3.10 or newer (CI also exercises 3.11 and 3.12)
- Unity 6000.0 or newer for Unity and live tests
- macOS, Linux, or Windows

Clone the repository and create the managed Python environment. On POSIX:

```bash
git clone https://github.com/german-krasnikov/unity-biome-mcp.git
cd unity-biome-mcp
python install.py setup
server/.venv/bin/python install.py doctor
```

On Windows PowerShell, use `py install.py setup`, then
`server\.venv\Scripts\python.exe install.py doctor`. Commands below show the
POSIX virtual-environment path; substitute the Windows path when applicable.
Before running pytest in PowerShell, set
`$env:PYTHONWARNDEFAULTENCODING = "1"` for the current shell.

Run server commands from `server/`; run installer, scripts, documentation, and
Unity runner commands from the repository root.

## Run tests

Start with the smallest suite that covers your change, then run the broader
non-live suites before opening a pull request.

### Python, installer, and scripts

```bash
# From server/
PYTHONWARNDEFAULTENCODING=1 .venv/bin/python -m pytest tests/ \
  -m "not live and not monkey" -q --tb=short

# From the repository root
PYTHONWARNDEFAULTENCODING=1 server/.venv/bin/python -m pytest install/tests/ -q --tb=short
PYTHONWARNDEFAULTENCODING=1 server/.venv/bin/python -m pytest scripts/tests/ -q --tb=short
```

The `monkey` stress suite is intentionally separate:

```bash
cd server
PYTHONWARNDEFAULTENCODING=1 .venv/bin/python -m pytest tests/ \
  -m "monkey and not live" -q --tb=short
```

### Unity EditMode and PlayMode tests

Open `unity-test-project` in Unity and wait for compilation to finish. The
durable runner is the preferred local entry point because it preserves request
identity through a domain reload:

```bash
server/.venv/bin/python run_unity_tests.py EditMode --project unity-test-project
server/.venv/bin/python run_unity_tests.py PlayMode --project unity-test-project
```

Use `--filter Namespace.Fixture.TestName` for a focused run. An unfiltered
EditMode run enforces the repository's minimum discovered-test count; do not
use `--allow-empty` or lower the minimum to hide discovery failures.

After changing C# code, call the public `sync_unity` tool and require a clean
result before trusting test output. If synchronization fails, inspect
`get_console` and `Editor.log`, then follow the recovery steps in
[`AI/reload-reference.md`](AI/reload-reference.md).

### Live Python tests

Live tests require the target Unity project to be open:

```bash
cd server
export UNITY_MCP_PROJECT_PATH="/absolute/path/to/unity-test-project"
PYTHONWARNDEFAULTENCODING=1 .venv/bin/python -m pytest tests/live/ -m live -q
```

In Windows PowerShell, set
`$env:UNITY_MCP_PROJECT_PATH = "C:\absolute\path\to\unity-test-project"`, then
run `.venv\Scripts\python.exe -m pytest tests/live/ -m live -q` from `server`.

`UNITY_MCP_PROJECT_PATH` is mandatory for the live harness. Use
`UNITY_MCP_PORT` only when you deliberately need to override project-based port
selection. The `live_cli` and `live_chat` suites also require their external
CLI/provider authentication and should be run separately.

### What to run

| Change | Minimum evidence |
|---|---|
| Python server | Focused tests, then non-live server tests |
| Installer or converter | `install/tests/` and affected ClientSkills tests |
| Repository scripts or generators | `scripts/tests/` and the relevant check mode |
| Unity C# | Focused EditMode/PlayMode tests, then the applicable full mode |
| TCP or cross-language contract | Python tests, Unity tests, and relevant live tests |
| Documentation | Documentation tests, generator checks, and strict MkDocs build |

Stop after a failing prerequisite and fix it before interpreting downstream
results.

## Code changes

Prefer the smallest change that makes the behavior explicit and testable.

- Keep one source of truth for each fact or contract.
- Add regression tests for bug fixes and meaningful behavior changes.
- Use type hints for Python APIs and `Assert.That(...)` in new NUnit tests.
- Follow the existing style near the code you change.
- Avoid speculative abstractions and unrelated formatting churn.
- Treat file size as a review signal, not an absolute pass/fail rule; split a
  file when doing so makes ownership or testing clearer.

The public tool catalog is defined in
`server/src/unity_mcp/tools/tool_specs.py`. Unity command registration and the
Python wrapper must agree on command name, arguments, mutability, mode guards,
timeout, and response semantics.

## Documentation changes

Documentation ships with the code:

- `README.md` is the concise project entry point.
- `docs/` teaches user tasks and troubleshooting.
- `AI/` records developer and agent implementation constraints.
- `CHANGELOG.md` records user-visible changes and is canonical; synchronize its
  byte-identical package mirror with `scripts/sync_changelog.py`.
- generated tool schemas, quality data, counts, and README fact blocks must be
  changed through their generators.

Update the canonical page that owns a fact and link to it elsewhere. User
guides should explain prerequisites, a copyable example, expected result, and
recovery without duplicating the generated parameter reference.

After editing the changelog, synchronize the package mirror from the repository
root:

```bash
server/.venv/bin/python scripts/sync_changelog.py
server/.venv/bin/python scripts/sync_changelog.py --check
```

Before committing generated facts, run:

```bash
server/.venv/bin/python scripts/update_readme.py --check-facts
server/.venv/bin/python scripts/update_readme.py --check
uvx --from mkdocs --with mkdocs-material --with mkdocs-minify-plugin \
  mkdocs build --strict
```

See [`docs/index.md`](docs/index.md) for the public documentation map and
[`AI/README.md`](AI/README.md) for internal documentation ownership.

## Pull requests

1. Create a focused branch from `master`.
2. Keep unrelated edits out of the diff.
3. Add tests and documentation with the implementation they describe.
4. Record the exact commands and results in the pull request.
5. Update `CHANGELOG.md` for user-visible behavior.
6. Request review only after all applicable local checks pass.

Maintainers normally squash the pull request into `master`. Never include API
keys, credentials, private project data, `.env` files, or generated local
client configuration.

For architecture and repository conventions, start with
[`AI/architecture.md`](AI/architecture.md) and [`AI/testing.md`](AI/testing.md).
