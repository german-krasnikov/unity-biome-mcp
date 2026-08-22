# System and orchestration tools

System tools discover capabilities, maintain the Unity connection, coordinate
larger tasks, and provide recovery boundaries. Most are direct-only
orchestrators: call them by their typed MCP name instead of placing them in a
`batch` script.

For exact arguments and defaults, use the
[generated tool schema](../tools-schema/index.md). The live installed contract
is available through `resolve_tool_schema`.

## Start with status and discovery

Use `mcp_status` for a compact view of the connected scene and Editor state:

```python
status = await mcp_status()
```

The status includes:
- `scene` — current open scene name
- `playing` — true if Play Mode is active
- `mutation_mode` — true if Hot Reload or Mutation Mode toggle is enabled
- `fast_play_mode` — true if Play Mode does not reload domain
- `plugin_version`, `protocol_version`, `python_version` — cross-language version diagnostics

If a specialized tool is not visible, inspect the catalog without changing the
session, then enable only the category you need:

```python
catalog = await discover_tools(enable=False, structured=True)
await discover_tools(category="VERIFY", enable=True)
schema = await resolve_tool_schema(tools="diagnose,verify_after_change")
```

`discover_tools` recognizes ten canonical categories: `SCENE`, `COMPONENTS`,
`ASSETS`, `UGUI`, `UITOOLKIT`, `MEDIA`, `VERIFY`, `RUNTIME`, `TESTS`, and
`SYSTEM`. `get_enabled_tools` reports the current Unity-side enablement;
`get_capabilities` reports plugin capabilities; `get_schema` returns the Unity
serialization schema for a C# type.

Connection diagnostics are split by purpose:

- `list_connections` reports the active Python-to-Unity transport.
- `reconnect_unity` reconnects to an explicit port, or auto-discovers when the
  port is omitted.
- `mcp_status` reports compact Editor and scene state.
- `alias_status` checks the project alias table.

## Synchronize after code changes

Use `sync_unity` after editing C# or assembly definitions. It triggers the
required refresh, waits for a coherent domain, and reports compile failures:

```python
result = await sync_unity()
```

Do not replace that check with a fixed sleep. `recompile` requests compilation
but does not by itself prove that the new assembly loaded. For diagnosis and
the post-change verification ladder, see [Diagnostics](diagnostics.md).

## Batch multiple .cs writes into one domain reload

When writing multiple C# files, batch them into a single domain reload using write sessions:

```python
await start_write_session()
await asset(action="write_text", path="Assets/Scripts/File1.cs", content="...")
await asset(action="write_text", path="Assets/Scripts/File2.cs", content="...")
await end_write_session(sync=True)
```

This is faster than writing each file separately (which triggers a domain reload per write):

- `start_write_session()` locks assemblies and disables auto-refresh
- `end_write_session(sync=True)` releases the lock and triggers **one** domain reload
  for all buffered writes
- `sync=True` (default) waits for compilation to finish before returning
- `sync=False` returns immediately after releasing the lock
- Auto-releases after 120s watchdog if not explicitly closed

Wrap only script-affecting operations (`asset` with .cs/.asmdef/.dll paths,
`write_text` with script extensions). Non-script writes (`asset` with .prefab/.mat)
outside the session are safe.

Other maintenance tools are intentionally narrower:

- `doctor` checks installation and connection health and can remove stale local
  port and lock discovery files with `fix=True`.
- `build` runs a Unity build with explicit build settings.
- `smart_build` is a higher-level build orchestrator.
- `release_smoke` performs a compact status, alias, and compile smoke check; it
  is not a full release test suite.
- `menu` invokes a Unity menu item.
- `auto_fix` collects recent Unity errors and asks the connected MCP client's
  sampling API for a concrete fix suggestion. It does not edit files or apply
  the suggestion.

## Create a recovery boundary

Choose the smallest recovery mechanism that covers the change:

| Need | Tool | Scope |
|---|---|---|
| Group upcoming Unity mutations | `checkpoint` | Unity Undo group |
| Undo recent Unity groups | `undo_last` | Current Unity domain |
| Preserve Unity state and selected files | `checkpoint_create` | Durable checkpoint |
| Restore a durable checkpoint | `checkpoint_restore` | Undo when valid, file fallback otherwise |
| Detect scene drift | `fingerprint` | Stable scene comparison |
| Review observed Editor changes | `get_changes` | Change/event summary |

Example:

```python
saved = await checkpoint_create(paths="Assets/Scripts/Player.cs")
# Perform the bounded change and verification.
# If recovery is required, pass the returned checkpoint_id:
restored = await checkpoint_restore(checkpoint_id="<checkpoint-id>")
```

`checkpoint_restore` can overwrite captured files. The current implementation
does not yet receive post-change file hashes, so its file fallback cannot
detect edits made after the checkpoint; `force` is reserved for that future
conflict check and does not make the current fallback safer. Review or commit
important files first. No checkpoint can roll back arbitrary external-process,
package-manager, or untracked filesystem side effects.

`permission_prompt` is an integration primitive for a configured permission
broker. It is not a universal prompt automatically applied to every tool or to
raw loopback clients; see the
[security policy](https://github.com/german-krasnikov/unity-biome-mcp/blob/master/SECURITY.md).

## Coordinate higher-level work

These tools combine context or delegate a task:

| Tool | Purpose |
|---|---|
| `do` | Plan or execute a natural-language Unity change |
| `ask` | Answer a Unity/project question with bounded context |
| `ask_user` | Request user input through the supported client surface |
| `animator_intent` | Plan or apply an Animator change |
| `brief_build` | Assemble a token-budgeted project brief |
| `budget_status` | Report sampling budget state |
| `set_llm_config` | Override Claude sampling profiles for this server process |

Intent workflows and their verification pattern are covered in
[Intent Tools](../features/intent-tools.md). `execute_code` is also a SYSTEM
tool, but its risk model and examples belong in
[Code Execution](../features/code-execution.md).

## Reuse project-local automation

`save_skill`, `list_skills`, and `use_skill` store and execute small learned
operations. `save_template`, `list_templates`, and `apply_template` do the same
for C# scene templates. `save_session` and `load_session` preserve compact
session context. Storage, substitution, and limitations are documented once in
[Skills and Templates](../features/session-skills.md).

## Complete SYSTEM inventory

The SYSTEM category contains:

```text
alias_status       animator_intent      apply_template       ask
ask_user           auto_fix             brief_build          budget_status
build              checkpoint           checkpoint_create    checkpoint_restore
clear_held_types   discover_tools       do                   doctor
execute_code       fingerprint          get_capabilities     get_changes
get_enabled_tools  get_schema           list_connections     list_skills
list_templates     load_session         mcp_status           menu
permission_prompt  recompile            reconnect_unity      release_smoke
resolve_tool_schema save_session        save_skill           save_template
set_llm_config     smart_build          sync_unity           undo_last
use_skill
```

This list provides authored discoverability; the generated schema remains the
source of truth for signatures, annotations, and defaults in each release.
