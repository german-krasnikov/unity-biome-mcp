# Mutation Mode

Mutation Mode is an experimental MCP setting that defers Unity's domain reload
to give you faster iteration when working with the Hot Reload package.

## What it does

- Disables Unity's auto-refresh (`DisallowAutoRefresh`)
- Enables Fast Play Mode (`FastPlayMode.Apply`)
- Lets `sync_unity` and `await_compile` skip polling when no .cs files were written

## What it does NOT do

**Mutation Mode controls WHEN domain reload happens, not WHETHER it happens.**

Without the [Hot Reload](https://hotreload.net) package installed, every .cs
source edit still triggers a full domain reload. Mutation Mode only defers it
until you call `sync_unity`.

With Hot Reload installed, edits are patched in-process — no domain reload.

## Enabling

```python
editor(action="mutation_mode", enable="true")   # on
editor(action="mutation_mode", enable="false")  # off
```

Or toggle in **MCP → Settings → Mutation Mode (experimental)** in the Unity Editor.

## Play Mode mutations

Scene mutations in Play Mode are always lost on stop — this is Unity behavior,
not Mutation Mode behavior. For in-Play code changes use:

```python
execute_code(code="Debug.Log('patched');", persist_as="MyPatch")
```

## External filesystem edits

With Mutation Mode active, auto-refresh is disabled — Unity does not scan for new
files automatically. If you edit `.cs` files outside MCP (IDE, git, shell), call:

```python
sync_unity(force=True)   # bypasses the MM skip guard
```

Without `force=True`, `sync_unity` may skip because it has no record of the
external write.

## Static field warning

Without Hot Reload, static fields persist across Play sessions. Reset them with:

```csharp
[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]
static void Reset() { myStaticField = defaultValue; }
```
