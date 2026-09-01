# Mutation Mode (Source Patch)

Mutation Mode enables faster iteration on method bodies by patching them in-memory, without triggering a full recompilation or domain reload. This is an optional, experimental feature powered by a Fast Script Reload adapter.

## What it does

When Mutation Mode is ON and the FSR adapter is installed, changes to a single method body are applied immediately:

- **No compilation:** Your code change applies in ~0.5 s
- **No domain reload:** Game state is preserved  
- **No file overhead:** Changes take effect in the running Editor

**Without the adapter:** Mutation Mode becomes equivalent to the standard compile path (8–90 s domain reload).

## When to use it

✓ **Good fits:**
- Iterating on a single method's logic repeatedly
- Quick bug fixes that don't change signatures or class layout
- Testing behavior changes without restarting the Editor

✗ **Not suitable for:**
- Adding new methods, fields, types, or constructors
- Async/iterator methods or lambda functions
- Changes to method signatures or class hierarchy  
- MonoBehaviour subclasses being mutated (held only, not edited)
- Play Mode (edits are lost on stop)

## Enable or disable

### Via MCP

```python
editor(action="mutation_mode", enable="true")   # Enable
editor(action="mutation_mode", enable="false")  # Disable (reverts to normal compile)
```

### Via Editor UI

Open **MCP > Settings**, scroll to **General** section, and toggle **Mutation Mode (experimental)**.

The checkbox reflects your preference and provider readiness. States:

| Toggle state | Meaning |
|---|---|
| **Checked, enabled** | Mode is ON; provider is installed and ready |
| **Unchecked, enabled** | Mode is OFF; you can enable it |
| **Disabled (gray)** | Provider not installed, or mode is Busy/Disabling/in Recovery, or Editor is in Play Mode |
| **Disabled + warning** | Mode is in Recovery state; requires a domain reload before it can be re-enabled |

Disabling always triggers exactly one script reload.

## Installing the provider

The Mutation Mode checkbox is **disabled** if the optional provider package is not installed. This is fail-closed by design: the base MCP plugin does not depend on or bundle the provider, so Mutation Mode is entirely optional.

**When provider is absent:**
- Checkbox shows: **disabled (gray)** with tooltip "Mutation Mode provider package is not installed"
- MCP calls: `editor(action="mutation_mode", enable=true)` returns `source_patch_provider absent`
- Behavior: All `.cs` writes use the standard Unity compile path

**To enable Mutation Mode:**

Add the Fast Script Reload (FSR) provider package to your project's `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.unity.modules.core": "...",
    "com.handzlikchris.fastscriptreload": "https://github.com/german-krasnikov/FastScriptReload.git?path=/Assets#b90a5c3fd7cfa452f23e8a807cc7bd61dc934bbf"
  }
}
```

After adding the dependency, Unity resolves the package. The Mutation Mode checkbox becomes **enabled** (though unchecked, reflecting the Off state) and you can toggle it.

> [!NOTE]
> The FastScriptReload provider is licensed under MIT by Chris Handzlik. See [Third-Party Notices](../../THIRD-PARTY-NOTICES.md) for full copyright and license details.

**To disable Mutation Mode permanently:**

Remove the provider dependency from `manifest.json`. The project reverts to the `Unavailable` state with the checkbox disabled. Standard `.cs` compile behavior is restored automatically.

## Check status

```python
mcp_status()
```

Look for these fields:

- `source_patch_intent` — your preference (true/false)
- `source_patch_provider` — package installed/unavailable
- `source_patch_state` — current state (Off, OnReady, Busy, Recovery)

If `state` is not `OnReady`, mutations will fall back to the standard compile path.

## Example workflow

```python
# 1. Enable Mutation Mode
editor(action="mutation_mode", enable="true")

# 2. Check readiness
status = mcp_status()
if status.source_patch_state != "OnReady":
    print("Provider not installed or not ready; using standard compile")

# 3. Edit a method body (a plain utility class, not a MonoBehaviour)
asset(action="write_text", 
      path="Assets/Game/DamageCalculator.cs",
      content="""
public sealed class DamageCalculator {
    public float Compute(float baseDamage, float armor) {
        Debug.Log("New armor mitigation formula");
        return baseDamage * (1f - armor / (armor + 100f));
    }
}
""")

# 4. Disable when done
editor(action="mutation_mode", enable="false")
```

## Recovery

If a mutation fails, Mutation Mode transitions to a **Recovery** state. This is fail-closed: no partial or uncertain changes are applied.

To clear Recovery:

1. Check what went wrong with `mcp_status()`
2. Verify your edit matches the limitations below
3. Disable and re-enable: `editor(action="mutation_mode", enable="false")`
   — this is a legal transition from Recovery (Recovery → Disabling); it
   triggers the same single causal Domain Reload a normal disable uses and
   releases any AutoRefresh lease still held from the failed mutation. Then
   call `enable="true"` again as a separate step once state reads Off.

If the problem persists, fall back to the standard compile path (disable Mutation Mode).

## Limitations and Validation

**Path validation:** All `.cs` writes are validated before any effects (Read/Write/Lease). Invalid paths are rejected with a clean warning: empty paths, absolute paths, non-.cs files, paths outside `Assets/` (e.g. `Packages/`, `../`), and traversal sequences (`..` segments). Path validation occurs even if Mutation Mode is OFF, and rejects before Recovery state can be reached.

Mutations are only admitted if they meet all these constraints:

| Constraint | Rationale |
|---|---|
| Existing, non-generic sync methods only | Body-only replacements can't add complexity (new shapes require re-compilation) |
| Non-MonoBehaviour utility classes | MonoBehaviour dynamic types lack a file path in Unity's script registry |
| Sync methods (no async/iterator) | Async state machines have complex IL patterns that require full recompilation |
| No lambdas, local functions, closures | These generate hidden types that can't be patched in-place |
| Single file at a time | Structural consistency requires atomic single-source writes |
| Assets/ only (not Packages/) | Project code only; package code must rebuild |
| No Play Mode mutations | Runtime edits don't survive a Play Mode cycle |
| Mono backend only | Il2Cpp (ahead-of-time compiled) requires full rebuild |

## Supported Platforms and Unity Versions

Qualified for:
- **Unity 6000.0.65f1 (Mono backend)**
- **macOS ARM64:** CI-qualified
- **Linux x64:** CI-qualified

Engineering-supported (CI qualification pending):
- **Windows x64:** Note: headed-GUI unavailable on GH-hosted runners; CI qualification requires external infrastructure

## Token budget

Checking `mcp_status()` costs ~30 tokens. Check once at the start of your session; re-check only after toggling Mutation Mode.

## FAQ

**Q: Does Mutation Mode change my saved files?**  
A: No. Mutations exist only in the Editor's memory. When you stop the Editor or reload, your source files remain unchanged until you explicitly save them.

**Q: What if I edit the same method multiple times with Mutation ON?**  
A: Each edit is patched immediately, one at a time. Sequential edits are safe.

**Q: Can I use Mutation Mode with compile errors?**  
A: No. The method's syntax must be valid C#. Syntax errors are rejected during preflight check.

**Q: What happens if domain reload occurs?**  
A: Mutation Mode automatically transitions to OFF. Re-enable it explicitly in your next session to resume mutations.

**Q: Is my data/scene preserved?**  
A: Yes. Unlike a full domain reload, mutations preserve all runtime state (fields, GameObject state, etc.).
