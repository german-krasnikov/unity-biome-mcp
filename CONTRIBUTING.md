# Contributing to Unity Biome MCP

Thank you for your interest in contributing! This guide walks you through setting up a development environment and running the test suite.

## Quick Start for Contributors

```bash
# Clone the repo
git clone https://github.com/german-krasnikov/unity-biome-mcp.git
cd unity-biome-mcp

# Install (Python + venv + dependencies)
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
PYTHONWARNDEFAULTENCODING=1 python -m pytest tests/ -m "not live" -q

# Expected: 2728 tests passing
```

### Integration Testing (Requires Running Unity)

1. **Start Unity** on a free port (default 9500):
   ```bash
   open -a Unity  # macOS; or launch Unity manually
   ```

2. **Run Python live tests:**
   ```bash
   PYTHONWARNDEFAULTENCODING=1 UNITY_MCP_PORT=9500 python -m pytest tests/ -m "live and not live_cli" -q
   ```
   Expected: 78 live tests passing.

3. **Open Unity Test Runner** (EditMode only):
   - `Window → Testing → Test Runner`
   - Click **EditMode**
   - Click **Run All**
   - Expected: 2389+ tests passing

## Test Execution Order

Always run tests in this order to catch issues early:

| Tier | Tests | Command | Time | Cost |
|------|-------|---------|------|------|
| **1. Unit (Python)** | 2728 mocked | `pytest tests/ -m "not live"` | ~15s | $0 |
| **2. EditMode (C#)** | 2389 | Unity Test Runner → EditMode → Run All | ~30s | $0 |
| **3. Python Live** | 78 | `UNITY_MCP_PORT=9500 pytest tests/ -m "live and not live_cli"` | ~10s | $0 |
| **4. PlayMode (C#)** | 73 | Unity Test Runner → PlayMode → Run All | ~60s | $0 |
| **5. Reload Stability** | 39 | `pytest tests/test_reload_stability.py -v` | ~40s | $0 |
| **6. Real CLI (live_cli)** | 4 | `UNITY_MCP_PORT=9500 pytest tests/ -m "live_cli" -v` | ~20s | ~$0.004 |

Stop at the first failure — don't run all tiers if an earlier tier fails.

### After C# Changes

Always verify the test assembly compiles:

```bash
# Check for compilation errors (not just stale DLLs)
UNITY_MCP_PORT=9500 python3 -c "
import asyncio,struct,json,pathlib,os
def find_port():
    p=int(os.environ.get('UNITY_MCP_PORT','0'))
    if p: return p
    for f in pathlib.Path.home().glob('.unity-biome-mcp/ports/*.port'):
        try: return int(f.read_text().split('\n')[0])
        except: pass
    return 9500
async def go():
    port=find_port()
    r,w=await asyncio.open_connection('127.0.0.1',port)
    msg=json.dumps({'cmd':'get_compile_errors','args':{}}).encode()
    w.write(struct.pack('>I',len(msg))+msg);await w.drain()
    d=await r.readexactly(struct.unpack('>I',await r.readexactly(4))[0])
    resp=json.loads(d);w.close()
    print(resp.get('data','') or '[COMPILE CLEAN]')
asyncio.run(go())
"
```

## Code Style

Follow these principles for all contributions:

- **SOLID principles**: Single responsibility, Open/closed, Liskov substitution, Interface segregation, Dependency inversion
- **DRY** (Don't Repeat Yourself): Extract patterns into shared utilities
- **KISS** (Keep It Simple, Stupid): Prefer straightforward code over clever abstractions
- **TDD**: Write tests before implementation
- **File size**: Keep files under 200 lines to maintain readability and testability
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
- **Plugin system**: Register tools via `ToolRegistry` — no cross-imports
- **Serializers**: 7 types (GameObjectSerializer, ComponentSerializer, etc.) for safe data transfer
- **CommandRouter**: Async dispatch with permission gating and security scanning
- **TCP bridge**: 4-byte length-prefixed JSON, localhost-only, heartbeat recovery

## Documentation

Documentation is maintained automatically:
- Release notes go in `CHANGELOG.md`
- Tool catalog in `AI/mcp-server.md`
- Architecture in `AI/architecture.md`
- Skills and recipes in `.claude/skills/`

**Do not manually edit documentation during development** — the release workflow updates docs from code. Focus only on feature implementation and tests.

## Getting Help

- Check [`docs/README.md`](docs/README.md) for troubleshooting
- Open an issue with reproduction steps and test output
- Reference relevant test files as examples

---

**Thank you for contributing!** Every test, fix, and feature makes Unity Biome MCP more reliable for everyone.
