# Reusable Skills, Templates, and Session Context

Unity Biome MCP can store small project-local automations under `.claude/`:

- a **learned skill** is saved C# or batch text plus a description;
- a **template** is a saved C# snippet for recreating scene content;
- **session context** is a compact hierarchy summary for comparison after a
  reconnect or restart.

These helpers are different from the agent instruction skills installed by the
Unity setup wizard. Treat learned skills and templates as executable project code:
review them before reuse and keep only trusted content.

## Save and run a learned skill

### Batch skill

Batch text is a good fit for a short sequence of existing MCP commands:

```python
await save_skill(
    name="create_marker",
    description="Create a positioned cube under /Markers",
    code=(
        "create_object name=${name} primitive=Cube parent=/Markers\n"
        "set_property path=/Markers/${name} component=Transform "
        "prop=m_LocalPosition value=${position}"
    ),
)

result = await use_skill(
    "create_marker",
    params="name=SpawnA,position=(1,0,2)",
)
```

### C# skill

Code containing common C# tokens is stored as a C# skill and later runs through
`execute_code`:

```python
await save_skill(
    name="reset_player_position",
    description="Move /Player to the origin",
    code='''
var player = GameObject.Find("Player");
if (player == null) throw new System.Exception("Player not found");
UnityEditor.Undo.RecordObject(player.transform, "Reset Player Position");
player.transform.position = Vector3.zero;
return player.transform.position.ToString();
''',
)

result = await use_skill("reset_player_position")
```

Placeholders use `${key}`. Pass replacements as comma-separated `key=value`
pairs. Commas inside parentheses are kept together, so Vector values such as
`(1,0,2)` work. Substitution is textual and does not validate C# or escape values;
do not pass untrusted text into executable templates.

Use `list_skills()` to see each saved skill's kind, description, and use count.
Names cannot contain `/`, `\\`, or `..`.

## Save and apply a scene template

A template is always C# and is stored as a `.cs` snippet:

```python
await save_template(
    name="marker_row",
    code='''
var root = new GameObject("${name}");
for (int i = 0; i < ${count}; i++)
{
    var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
    marker.name = $"Marker_{i}";
    marker.transform.SetParent(root.transform);
    marker.transform.localPosition = new Vector3(i * ${spacing}, 0, 0);
}
return root.name;
''',
)

result = await apply_template(
    "marker_row",
    params="name=SpawnMarkers,count=4,spacing=2.5",
)
```

`list_templates()` returns the available template names. `apply_template` executes
the substituted snippet with an Undo label, but only operations that explicitly
use Unity Undo APIs are guaranteed to participate in Undo. Verify the hierarchy
after applying a template.

## Save session context

```python
saved = await save_session()
# ...reconnect or restart the MCP client...
comparison = await load_session()
```

`save_session` stores a timestamp and compact hierarchy summary.
`load_session` prints that previous summary beside a fresh current summary. It
does **not** reopen scenes, restore object values, or roll back changes. Use it as
cold-start orientation, then inspect the relevant subtree before editing.

For restorable file/scene checkpoints and recovery constraints, use
[System Tools](../tools/system.md#create-a-recovery-boundary).

## Track and compare changes

Cross-cutting state helpers have canonical documentation elsewhere:

- [`get_changes`](../tools/system.md#create-a-recovery-boundary) returns editor
  events since the previous read; `clear=False` keeps them for the next call.
- [`fingerprint`](../tools/system.md#create-a-recovery-boundary) provides a cheap
  hash for “did this subtree change?” checks.
- [`scene_diff`](../tools/scene.md#scene_diff) compares serialized hierarchy lines
  with its previous snapshot.
- [`screenshot_baseline` and `screenshot_compare`](../tools/screenshots.md) provide
  visual regression evidence.

These mechanisms answer different questions; none is a replacement for a saved
Unity scene or source control.

## Storage

| Item | Project-relative location | Contents |
|---|---|---|
| Learned skills | `.claude/skills/learned/*.json` | Description, executable text, kind, usage metadata |
| Templates | `.claude/templates/*.cs` | Executable C# snippets |
| Session context | `.claude/session-context.json` | Timestamp and compact hierarchy summary |
| Screenshot baselines | `.claude/baselines/*.png` | Images used by visual comparison |

Decide explicitly whether these local automation files belong in version control.
They can contain project paths and executable code, and the `.claude` directory is
commonly ignored.
