# Contributing to Unity Biome MCP

Thank you for your interest in contributing! This guide walks you through setting up a development environment and running the test suite.

## Quick Start for Contributors

```bash
# Clone the repo
git clone https://github.com/german-krasnikov/unity-biome-mcp.git
cd unity-biome-mcp

# Install (Python + venv + dependencies + auto-generates .mcp.json)
python install.py setup

# Verify installation
python install.py doctor

# Configure your AI tool (Claude Code, Cursor, etc.)
python install.py configure --tool claude-code
```

## Development Setup

### Requirements
- **Python 3.10+** (tested on 3.12)
- **Unity 6000.0+** (for integration tests only)
- **TCP port 9500** available (default MCP port)
- **macOS, Linux, or Windows** (all platforms supported)

### Working Directory
```bash
cd server  # All Python work happens here
```

### Local Testing (No Unity Required)
```bash
# Unit tests only — fast, $0, no Unity needed
PYTHONWARNDEFAULTENCODING=1 python -m pytest tests/ \
  -m "not live and not live_cli and not live_chat and not monkey" \
  --ignore=tests/live --strict-markers -q

# Expected: command exits successfully
```

Run the `monkey` stress suite separately; it is intentionally excluded from the
fast unit command.

### Integration Testing (Requires Running Unity)

1. **Start Unity** — the plugin auto-assigns a port and writes it to `~/.unity-biome-mcp/ports/`:
   ```bash
   open -a Unity  # macOS; or launch Unity manually
   ```

2. **Select the project and run Python live tests:**
   ```bash
   export UNITY_MCP_PROJECT_DIR="/absolute/path/to/your/UnityProject"
   PYTHONWARNDEFAULTENCODING=1 python -m pytest tests/ -m "live and not live_cli" -q
   ```
   The server selects the live port whose recorded project path best matches
   `UNITY_MCP_PROJECT_DIR`. Set `UNITY_MCP_PORT` only when you need to override
   that selection. Expected: command exits successfully.

3. **Open Unity Test Runner** (EditMode only):
   - `Window → Testing → Test Runner`
   - Click **EditMode**
   - Click **Run All**
   - Expected: the EditMode run completes without failures

## Test Execution Order

Always run tests in this order to catch issues early:

| Tier | Tests | Command | Requirement |
|------|-------|---------|-------------|
| **1. Reload Stability** | Focused bridge stability | `pytest tests/test_reload_stability.py -v` | Python environment |
| **2. Unit (Python)** | Non-live, non-stress Python suite | `pytest tests/ -m "not live and not live_cli and not live_chat and not monkey" --ignore=tests/live --strict-markers` | Python environment |
| **3. Stress (Python)** | Monkey/property stress suite | `pytest tests/ -m "monkey and not live and not live_cli and not live_chat" --ignore=tests/live --strict-markers` | Python environment |
| **4. EditMode (C#)** | Unity EditMode suite | Unity Test Runner → EditMode → Run All | Running Unity project |
| **5. PlayMode (C#)** | Unity PlayMode suite | Unity Test Runner → PlayMode → Run All | Running Unity project |
| **6. Python Live** | Unity-connected Python suite | `pytest tests/ -m "live and not live_cli"` | Running target Unity project |
| **7. Live Chat** | Standalone `live_chat` suite | `pytest tests/ -m "live_chat" -v` | Authenticated CLI and target Unity project |
| **8. Real CLI** | `live_cli` suite | `pytest tests/ -m "live_cli" -v` | Authenticated CLI and target Unity project |

Stop at the first failure — don't run all tiers if an earlier tier fails.

### After C# Changes

Always reload before running Unity tests:

1. Call `force_refresh`.
2. Wait at least 15 seconds.
3. Call `diagnose` and require a clean verdict.
4. If the verdict and Editor behavior disagree, inspect `get_console`, then
   `Editor.log`.
5. Run the focused EditMode or PlayMode tests.

Do not use `get_compile_errors` alone as proof that the new assembly loaded. See
[reload recovery](.claude/skills/reload-recovery.md) for the escalation ladder.

## Code Style

Follow these principles for all contributions:

- **SOLID principles**: Single responsibility, Open/closed, Liskov substitution, Interface segregation, Dependency inversion
- **DRY** (Don't Repeat Yourself): Extract patterns into shared utilities
- **KISS** (Keep It Simple, Stupid): Prefer straightforward code over clever abstractions
- **TDD**: Write tests before implementation
- **File size**: Keep files under 300 lines to maintain readability and testability
- **No "future-proofing"**: Only add abstractions when refactoring existing code, never preemptively

### Python style
- Use type hints for function signatures
- Async functions preferred for I/O-bound operations
- 100-character line limit (flexible for URLs/long strings)
- Use f-strings for formatting

### C# style
- Follow [C# Coding Conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- `ConfigureAwait(false)` on all async calls in non-UI code
- Use `readonly` and immutable types where possible
- NUnit assertions with `Assert.That()` (fluent style)

## Pull Request Process

1. **Branch from `master`**:
   ```bash
   git checkout -b feature/my-feature
   ```

2. **Make changes**. Test locally at each tier (unit → EditMode → live).

3. **Push and open PR**:
   ```bash
   git push origin feature/my-feature
   ```

4. **Open a PR** — maintainer will review and run the test suite.
   Run all applicable test tiers locally before requesting review.

5. **Merge strategy**: Squash commits onto `master` for a clean history.

## Architecture

For architectural decisions and design patterns, see [`AI/architecture.md`](AI/architecture.md).

Key concepts:
- **Tool catalog**: `tools/tool_specs.py` owns public tool metadata; tool modules register their wrappers during server composition
- **Serialization**: purpose-specific Unity helpers such as `HierarchySerializer` and `ComponentSerializer` own wire-safe output
- **CommandRouter**: Async dispatch with permission gating and security scanning
- **TCP bridge**: 4-byte length-prefixed JSON, localhost-only, heartbeat recovery

## Documentation

Documentation is maintained with code:
- Release notes go in `CHANGELOG.md`
- User workflows go in `docs/`
- Agent implementation constraints go in `AI/`
- Generated README facts are updated with `python scripts/update_readme.py --all`
- Before committing generated facts, run `python scripts/update_readme.py --check-facts`
  and `python scripts/update_readme.py --check`

Update the smallest canonical page that owns the behavior. Avoid copying the
same workflow into several files.

## Getting Help

- Check [`docs/README.md`](docs/README.md) for troubleshooting
- Open an issue with reproduction steps and test output
- Reference relevant test files as examples

---

**Thank you for contributing!** Every test, fix, and feature makes Unity Biome MCP more reliable for everyone.
