# Python 3.14 Guidelines

Definitive reference for this project. Fence tests in `tests/test_python314_compat.py` enforce these rules.

---

## A: PEP 649 & Annotations (CRITICAL — read this first)

**PEP 649 (Python 3.14 native):** annotations are deferred _code objects_ executed lazily when
`__annotations__` is accessed — NOT strings. Incompatible with `from __future__ import annotations`.

| Rule | Why |
|------|-----|
| **NEVER** `from __future__ import annotations` | Makes annotations strings — incompatible with PEP 649 deferred code model |
| Runtime import for any type that appears in a signature | `get_type_hints()` evaluates the code object; type must be in scope |
| `TYPE_CHECKING` blocks: **ONLY** for genuine circular imports | Any annotation-referenced type not under TYPE_CHECKING must be a runtime import |
| Circular import only — use `param: "ClassName"  # noqa: UP037` | String literal defers evaluation past module load — type must still be importable when `get_type_hints()` is called |
| pydantic / MCP SDK call `get_type_hints()` at runtime | ALL annotation types must resolve — no exceptions |

**PEP 749 / `annotationlib` (3.14):** for circular-import cases, prefer
`annotationlib.get_annotations(obj, format=Format.FORWARDREF)` — resolves annotations without
importing the referenced type, eliminating the circular dependency entirely.

**ruff TC rules (TC001/TC002/TC003): NEVER auto-apply.**

```bash
ruff check --fix          # safe — does not touch TC rules
ruff check --unsafe-fixes # DANGEROUS — silently moves imports to TYPE_CHECKING
                           # breaks runtime annotation resolution; never use in CI
```

If ruff suggests moving an import to `TYPE_CHECKING`, suppress with `# noqa: TC001` (or TC002/TC003) on
the import line — never move the import.

---

## B: Async Modern Patterns (3.11 → 3.14)

| Instead of | Use | Note |
|---|---|---|
| `asyncio.gather()` | `asyncio.TaskGroup` | structured concurrency, no leaked tasks (3.11) |
| `asyncio.wait_for()` | `asyncio.timeout()` context manager | cleaner timeout scopes (3.11) |
| `asyncio.get_event_loop()` | `asyncio.get_running_loop()` | deprecated |
| `asyncio.ensure_future()` | `asyncio.create_task()` | deprecated |
| `asyncio.iscoroutinefunction()` | `inspect.iscoroutinefunction()` | **removed** in 3.14 |
| `asyncio.TimeoutError` | `TimeoutError` | merged into builtin `TimeoutError` in 3.11 — use builtin directly |
| `loop.run_until_complete()` | `asyncio.Runner` | managed lifecycle (3.11) |

```python
# TaskGroup — preferred over gather()
async with asyncio.TaskGroup() as tg:
    t1 = tg.create_task(coro1())
    t2 = tg.create_task(coro2())
# both complete here; exceptions surface as ExceptionGroup

# timeout scope — preferred over wait_for()
async with asyncio.timeout(5.0):
    result = await slow_operation()
```

**When all tasks must complete regardless of individual failure:** `gather(return_exceptions=True)` is
still correct — TaskGroup cancels siblings on first failure and is not a replacement here.

**`asyncio.eager_task_factory` (3.12):** `loop.set_task_factory(asyncio.eager_task_factory)` — reduces
per-task overhead for coroutines that don't yield immediately.

**`asyncio.QueueShutDown` (3.13):** raised when `Queue.shutdown()` is called — use for clean
producer-consumer teardown instead of sentinel values.

**Cancellation rules:**
- Never swallow `CancelledError` — always re-raise or let it propagate
- Use `asyncio.shield(coro)` when a subtask must survive parent cancellation

---

## C: Type System (3.10 → 3.14)

| Instead of | Use |
|---|---|
| `Optional[X]` | `X \| None` |
| `Union[X, Y]` | `X \| Y` |
| `TypeAlias = ...` | `type Alias = ...` (PEP 695, 3.12) |
| `def foo(x: T) -> T` with TypeVar | `def foo[T](x: T) -> T` (PEP 695, 3.12) |
| No decorator on method override | `@override` — always (3.12) |
| `-> "MyClass"` for self-return | `-> Self` (3.11) |
| `class Status(str, Enum)` | `class Status(StrEnum)` (3.11) |
| `typing.List / Dict / Callable` | `list / dict / collections.abc.Callable` |

**Bounded/constrained generics (3.12):** `def foo[T: SomeClass]()` — bound; `def foo[T: (int, str)]()` — constraint union.

**`TypeIs` (PEP 742, 3.13):** narrower than `TypeGuard` — narrows both the `True` and `False` branches.
Prefer over `TypeGuard` for type-predicate functions.

**`ReadOnly` for TypedDict (PEP 705, 3.13):** `from typing import ReadOnly` — mark individual TypedDict
fields as immutable without subclassing.

**`warnings.deprecated` (PEP 702, 3.13):** `@warnings.deprecated("Use bar() instead")` — emits
`DeprecationWarning` on use; works on functions, methods, and classes.

**Protocol vs ABC:** use `Protocol` for external boundaries and mocking (duck-typing, no inheritance
required); use `ABC` when you need instantiation enforcement or mixin behavior.

**`collections.abc` preferred imports:** `Callable`, `Sequence`, `Mapping`, `Iterator`, `Generator`,
`Awaitable`, `Coroutine` — all from `collections.abc`, not `typing`.

Still in `typing`: `Any`, `Literal`, `Protocol`, `TypeVar`, `ClassVar`, `Final`,
`TYPE_CHECKING`, `overload`, `runtime_checkable`, `Self`, `override`, `TypeIs`, `ReadOnly`.

---

## D: Pattern Matching (3.10)

Prefer `match/case` over `elif` chains for structural dispatch (protocol commands, error routing):

```python
match command:
    case {"cmd": cmd_name, "args": args} if cmd_name in REGISTRY:
        return await REGISTRY[cmd_name](args)
    case {"cmd": unknown}:
        raise ValueError(f"Unknown command: {unknown}")
    case _:
        raise TypeError("Malformed message")
```

---

## E: Error Handling (3.11+)

```python
# ExceptionGroup from TaskGroup — use except* not except
try:
    async with asyncio.TaskGroup() as tg:
        tg.create_task(risky1())
        tg.create_task(risky2())
except* ValueError as eg:
    for exc in eg.exceptions:
        logger.error(exc)
    raise  # never swallow — re-raise the ExceptionGroup

# Context enrichment — add_note() before re-raise
try:
    process(data)
except ProcessingError as e:
    e.add_note(f"Input was: {data!r}")
    raise
```

Never swallow exceptions inside TaskGroup — they form ExceptionGroups that must propagate.

---

## F: Stdlib Modernization

| Instead of | Use |
|---|---|
| `try: import tomllib\nexcept: import tomli` | `import tomllib` (stdlib since 3.11) |
| `os.walk()` | `pathlib.Path.walk()` (3.12) |
| `os.path.join / exists / dirname` | `pathlib.Path` everywhere |
| `class Status(str, Enum)` | `class Status(StrEnum)` (3.11) |

**`tomllib` caveats:** read-only (no write support); requires `open(..., "rb")` binary mode. For writing
TOML use `tomli-w` or a compatible library.

---

## G: Stability & Reliability

- `asyncio.TaskGroup` — structured concurrency; cancellation never leaks tasks
- **TaskGroup cascade:** one task failure cancels ALL sibling tasks. Don't group independent work that
  should complete regardless of peer failures — use `gather(return_exceptions=True)` instead.
- `asyncio.timeout()` scopes — not `wait_for` — for timeout boundaries
- `contextlib.aclosing()` — wraps async generators that must be explicitly closed
- `filterwarnings = ["error::DeprecationWarning"]` in pytest — catch deprecations as failures
- **Third-party exemption:** add `"ignore::DeprecationWarning:third_party_lib"` to suppress warnings
  from external packages you don't control.

---

## H: Testability

- `Protocol` over ABC — structural typing; mock by duck-typing, no inheritance required
- Fence tests in `test_python314_compat.py` scan the codebase for anti-patterns
- `test_all_annotations_resolve` calls `typing.get_type_hints(obj, include_extras=True)` on every
  public callable — catches TYPE_CHECKING regressions before pydantic/MCP SDK does at runtime
- `AsyncMock` for async callables; compatible with TaskGroup patterns

---

## I: Modularity & Maintainability

- `Protocol` over ABC — no inheritance coupling; implementors satisfy by structure
- `@override` on every override — static refactoring safety; catches missed renames
- `-> Self` return type — chainable builder APIs without forward references
- `__all__` — explicit public API surface; controls what `import *` and docs expose
- `@dataclass(slots=True, frozen=True, kw_only=True)` — prefer for immutable value types;
  `kw_only` prevents positional-argument order bugs on refactoring

---

## J: Tooling

```toml
# pyproject.toml
[tool.ruff]
target-version = "py314"

[tool.ruff.lint]
# TC001/TC002/TC003 — never auto-apply; breaks PEP 649 annotation resolution
# Suppress false positives with # noqa: TC001 on the import line
ignore = ["TC001", "TC002", "TC003"]

[tool.pytest.ini_options]
asyncio_mode = "auto"      # pytest-asyncio >= 1.3.0 required
filterwarnings = ["error::DeprecationWarning"]
```

ruff summary: `--fix` is safe; `--unsafe-fixes` is dangerous — most critically for this project
due to TC001–TC003 silently moving runtime imports to TYPE_CHECKING.
