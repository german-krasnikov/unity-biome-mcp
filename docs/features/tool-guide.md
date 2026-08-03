# Tool Decision Guide

Quick reference: "Which tool should I use?" — based on Firecrawl MCP's decision matrix pattern.

## Decision Table

| I want to... | Use this tool | Why | Example |
|---|---|---|---|
| See everything in my scene | `get_hierarchy` | Shows all objects, their structure, and active/inactive status | `await get_hierarchy()` |
| Find specific objects | `search_scene` | Searches by name pattern, tag, or type | `await search_scene(query="Enemy*")` |
| Check object properties | `get_component` | Read a single component's properties | `await get_component("Player", "Transform")` |
| Check many objects at once | `inspect` | Bulk read multiple objects/components in 1 call | `await inspect(paths="Player,Enemy1", components="Health")` |
| Change a property | `set_property` | Update one value on a component | `await set_property("Player", "Transform", "position", "0,1,0")` |
| Create an object | `create_object` | Spawn a new GameObject | `await create_object(name="Cube")` |
| Delete an object | `delete_object` | Remove an object from the scene | `await delete_object("Cube")` |
| Add/remove components | `manage_component` | Add or remove a component type | `await manage_component("Player", "Rigidbody", "add")` |
| Wire up events | `wire_event` | Connect a button click or event to a method | `await wire_event("StartButton", "Button", "onClick", "GameManager", "StartGame")` |
| Run automated tests | `run_playtest` | Execute a test script in play mode | `await run_playtest(script="ASSERT Player/Health > 0")` |
| Take a screenshot | `screenshot` | Capture the scene from one or more cameras | `await screenshot(camera="multi_view")` |
| Debug compilation issues | `get_compile_errors` | Check if there are C# compilation errors | `await get_compile_errors()` |
| Read console output | `get_console` | See all Debug.Log messages and errors | `await get_console()` |
| Run diagnostics | `doctor` | Check MCP health, connections, and common issues | `await doctor()` |
| Bake lighting or occlusion | `bake` | Bake lights (async) or precalculated occlusion culling | `await bake(target="lighting", action="start")` |
| Build a player | `build` | Create a standalone player for Windows, macOS, Android, iOS, or WebGL | `await build(action="build", target="StandaloneOSX", dev=True)` |
| Manage UPM packages | `package` | List, search, add, or remove UPM packages | `await package(action="add", name="com.unity.cinemachine")` |
| Do multiple things at once | `batch` | Chain 2+ operations in a single call | `await batch("create_object name=A\ncreate_object name=B")` |
| Give a vague instruction | `do` | Natural language → automatic tool selection | `await do("create a red cube at the origin")` |

## When to Use `batch`

Use `batch` whenever you need 2+ operations:

```python
# Good: 1 batch call for 4 operations
await batch("""
create_object name=Player
create_object name=Enemy
set_property Player Transform position 0,1,0
manage_component Player Rigidbody add
""")

# Avoid: 4 separate calls
await create_object(name="Player")
await create_object(name="Enemy")
await set_property(...)
await manage_component(...)
```

## When to Use `inspect`

Use `inspect` when reading from 3+ objects or 3+ components:

```python
# Good: 1 inspect call for all reads
await inspect(paths="Player,Enemy1,Enemy2", components="Health,Damage")

# Avoid: 6 get_component calls
await get_component("Player", "Health")
await get_component("Player", "Damage")
# ... etc
```

## When to Use `do`

Use `do` for natural language tasks — it picks the best tools automatically:

```python
# Good: Let 'do' figure out the tools
await do("create a red cube 2 meters in front of the player")

# vs: manual tool selection (longer)
await create_object(name="Cube")
await set_property("Cube", "Renderer", "material.color", "1,0,0,1")
await set_property("Cube", "Transform", "position", "2,0,0")
```

## Verification Checklist

After any mutation, verify:

| Mutation | Verify with |
|---|---|
| `set_property` | `get_component` — value matches |
| `create_object` | `get_hierarchy` — object appears |
| `delete_object` | `get_hierarchy` — object gone |
| `manage_component add` | `get_component` — component exists |
| `wire_event` | `get_component` — event connected |
| `batch` (3+ ops) | verify last mutation in chain |

## Quick Flow

1. **See the scene?** → `get_hierarchy`
2. **Find something?** → `search_scene` + `get_component`
3. **Change something?** → `set_property` or `manage_component` + verify with `get_component`
4. **Create/delete?** → `create_object`/`delete_object` + verify with `get_hierarchy`
5. **Multiple ops?** → use `batch`
6. **Confused?** → use `do` (natural language)
7. **Tests pass?** → `run_playtest`
8. **Something broken?** → `get_console` + `get_compile_errors` + `doctor`
