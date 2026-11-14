# Contributing to Unity Biome MCP

Thank you for contributing. This guide covers repository setup, the tests that
protect each part of the project, and the documentation contract.

## Set up the repository

Requirements:

- Git 2.14 or newer on `PATH`
- Python 3.14 or newer
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
| A rule of `dsl/decisions/` | `scripts/dsl.sh check`, `test_decisions_examples.py` and the golden of the family, and `dotnet test unity-plugin/Tests~/Decisions/UnityMCP.Decisions.Tests.csproj -c Release` when C# is involved |
| How a chat turn ends (`dsl/chat/`, `Chat/CLI/ChatTurnDriver.cs`, `MCPChatWindow.TurnPorts.cs`) | `scripts/dsl.sh check`, `test_dsl_fresh.py` and `test_generated_cs_meta.py`, `dotnet test unity-plugin/Tests~/ChatTurn/ChatTurn.Tests.csproj -c Release`; the ports and the window wiring (`ChatTurnPortsTests`, `ChatTurnWiringTests`) need Unity EditMode |
| TCP or cross-language contract | Python tests, Unity tests, and relevant live tests |
| Documentation | Documentation tests, generator checks, and strict MkDocs build |

Stop after a failing prerequisite and fix it before interpreting downstream
results.

## Python dependencies

This project pins dependencies via `server/uv.lock` (managed by uv). After
editing `server/pyproject.toml`, regenerate the lock file:

```bash
cd server
uv lock
```

Commit both `pyproject.toml` and `uv.lock` in the same commit.

CI uses two strategies:

- **Lint job:** Installs `ruff` (the linter) directly from the lock file via
  `uv export --locked --extra dev`. This ensures the version checked by CI
  matches the pinned version in the repository. The lock consistency is
  enforced via `uv lock --check` to prevent drift.
- **Test, README, badge, and documentation jobs:** Install dependencies via
  `pip install ".[dev]"` using version ranges from `pyproject.toml`. These
  jobs resolve against live PyPI, not the lock file; some ranges are
  open-ended (`pydantic>=2.0`, `hypothesis>=6.1`, etc.), so they can pick
  newer versions than what the lock file pins.

For local work, use `uv sync` to install from the lock file (dev group
matching the dev extra) and `cd server && uv run ruff check ...` to run the
pinned linter version locally.

Dependabot automatically updates dependencies via the `uv` ecosystem (not
`pip`), which keeps both `pyproject.toml` and `uv.lock` in sync on a weekly
schedule.

## Code changes

Prefer the smallest change that makes the behavior explicit and testable.

- Keep one source of truth for each fact or contract.
- Add regression tests for bug fixes and meaningful behavior changes.
- Use type hints for Python APIs and `Assert.That(...)` in new NUnit tests.
- Follow the existing style near the code you change.
- Avoid speculative abstractions and unrelated formatting churn.
- Treat file size as a review signal, not an absolute pass/fail rule; split a
  file when doing so makes ownership or testing clearer.

The public tool catalog is `dsl/tools.biome` (see `dsl/README.md`): a tool's
category, effect, flags, timeout, arguments and schema descriptions are stated
there once, and `scripts/dsl.sh generate` prints the ToolSpec, the schema
descriptions, the wrapper of a `FORWARD` tool and the docs table from it. Unity
command registration and the Python wrapper must agree on command name,
arguments, mutability, mode guards, timeout, and response semantics.

A rule that the Python server and the Unity plugin both apply (is this call a write, did the playtest pass, which guard stops a call, why a failed
send is retried) is a decision in `dsl/decisions/`: change its `.logic` and its `.examples`, regenerate, and do not copy the rule into either plane (see
`dsl/README.md`, Decisions). A change of what the rule answers is a change of the requirement: its own commit, with the reason.

What happens when a chat turn ends (which effects, in which order, once) is one list, `dsl/chat/chatturn.feature`, not a cleanup written in each place that
detects an exit: a new way out or a new effect is a line there and an example in `chatturn.examples`; the window only detects the exit and dispatches it, and
the bodies of the effects are the ports in `MCPChatWindow.TurnPorts.cs` (see `dsl/README.md`, Chat turn).

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
