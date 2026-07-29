# Prompting Tips for Unity Biome MCP

Get the best results when using Unity Biome MCP with AI assistants. These tips follow competitive best practices from CoderGamester and Memory MCP.

## General Tips

**Use batch for multiple operations**
When you need to perform 2+ actions, use `batch` instead of separate calls. Saves tokens and time.

```
await batch("""
create_object name=Player parent=Level1
create_object name=Enemy parent=Level1
set_property Player Transform position 0,1,0
""")
```

**Use inspect instead of multiple get_component calls**
To read properties from N objects in one call, use `inspect` instead of N separate `get_component` calls.

```
# AVOID: 3 separate calls
await get_component("Player", "Health")
await get_component("Enemy1", "Health")
await get_component("Enemy2", "Health")

# GOOD: 1 call
await inspect(paths="Player,Enemy1,Enemy2", components="Health")
```

**Always verify after mutations**
Every `set_property`, `create_object`, or component change should be verified with a read tool to confirm the change succeeded.

```
await set_property("Player", "Transform", "position", "0,1,0")
result = await get_component("Player", "Transform")  # Verify position is (0,1,0)
```

**Use devil's advocate for multi-step plans**
Before running 3+ mutations, name the failure modes and which tool detects each one.

```
Plan: create object → add component → wire event
Fail 1: component type typo → get_compile_errors
Fail 2: event target path wrong → validate_references
```

## Example Prompts by Category

### Scene Setup
"Create a room with 4 walls, floor, ceiling, and a point light. Walls should have a brown material, floor should be gray, light should be at (5, 3, 0) with intensity 1.5"

### Object Editing
"Find all objects tagged 'Enemy' and set their health to 100. Then verify by reading the Health component from each one"

### Animation
"Add an idle-to-walk animator controller to the Player. The idle state should loop, walk state should be 1.5x speed"

### Testing
"Run a playtest: teleport player to checkpoint, assert score > 0, check for console errors"

### Diagnostics
"Check if there are any broken references in the scene. Use doctor first, then get_console to see what's broken"

## Token-Saving Patterns

**Batch multiple operations**
Use `batch` for two or more compatible operations to avoid repeated tool-call
envelopes.

```
await batch("op1\nop2\nop3")
```

**Use inspect for bulk reads**
Reading 3+ objects → `inspect` saves tokens vs loop of `get_component`.

```
await inspect(paths="Player,Enemy1,Enemy2", components="Health,Damage")  # 1 call
```

**Search before creating**
Use `search_scene` before `create_object` to avoid duplicates.

```
existing = await search_scene(query="Cube*")
if not existing:
    await create_object(name="Cube")
```

**Use natural language when unsure**
The `do` tool understands English and translates to optimal tools automatically, often more concise than manual tool picking.

```
await do("create 5 red cubes in a line starting at origin")
```

## Common Patterns

**Creation with verification**
```
await create_object(name="Player")
result = await get_hierarchy()  # Verify it's in the hierarchy
```

**Bulk property editing**
```
await batch("""
set_property Enemy1 Health health 50
set_property Enemy2 Health health 50
set_property Enemy3 Health health 50
""")
await inspect(paths="Enemy1,Enemy2,Enemy3", components="Health")  # Verify all changed
```

**Component addition with error checking**
```
await manage_component("Player", "Rigidbody", "add")
errors = await get_compile_errors()  # Ensure no broken refs
```

**Search and modify**
```
enemies = await search_scene(query="Enemy*")
# Then batch modify all found objects
```

**Multi-view screenshot for verification**
```
await screenshot(camera="multi_view")  # See scene from multiple angles at once
```

## Key Takeaways

- Use `batch` for 2+ operations
- Use `inspect` instead of loops
- Verify every mutation immediately
- Use `do` when uncertain
- Always check `get_console` after changes
- Use `doctor` for diagnostics before digging deeper
