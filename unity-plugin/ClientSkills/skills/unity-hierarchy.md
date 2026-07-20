# Unity Hierarchy Management (MCP)

## Reading Hierarchy

```
get_hierarchy                                   # depth=2 default
get_hierarchy depth=2 root="/Environment"       # subtree
get_hierarchy summary=true                      # compact ~20 tokens
get_hierarchy compress=true                     # groups repeated children
get_hierarchy incremental=true                  # NO_CHANGE if scene unchanged
get_hierarchy components=true                   # include component types
get_hierarchy scene="Level2"                    # multi-scene filter
```

> Start with `summary=true`, then drill into subtrees with `root=`.

## Creating Objects

```
create_object name="MyObject"
create_object name="Child" parent="/Environment"
create_object name="Floor" primitive="Plane"                           # Cube|Sphere|Cylinder|Capsule|Plane|Quad
create_object name="Cube" primitive="Cube" components="Rigidbody,BoxCollider"
create_object name="Tree" prefab_path="Assets/Prefabs/Tree.prefab"
```

Batch create (2+ objects — 80-95% token savings):
```
batch commands="
create_object name=\"Wall_L\" parent=\"/Level\" primitive=\"Cube\"
create_object name=\"Wall_R\" parent=\"/Level\" primitive=\"Cube\"
create_object name=\"Floor\" parent=\"/Level\" primitive=\"Plane\"
"
```

Create + configure in one call (TIER1, direct_only — not inside batch):
```
setup_objects spec='[{"name":"Turret","parent":"/Defenses","components":["Turret"],"config":{"Turret":{"range":10,"damage":5}}}]'
```

## Configure Existing Objects (TIER1, direct_only — not inside batch)

Multi-object property writes — one call vs N set_property:
```
configure_objects objects_and_config='[
  {"path":"/Player","components":{"Health":{"max":200,"current":200}}},
  {"path":"/Enemy","components":{"Health":{"max":50,"current":50}}}
]'
```

## Transactional Scene Edits (CORE — always available)

Pre-flight gate + atomic execute with rollback on failure:
```
scene_change_plan goal="add Health component" targets="/Player"
# → plan_id=abc123

apply_scene_change plan_id=abc123 commands="manage_component path=/Player action=add type=Health" verify=true save=true
```

## Searching

```
search_scene query="Player"                         # TIER1 — by name/tag/component
search_scene query="t:Rigidbody tag=Enemy"
search_scene query="Wall" root="/Level" limit=10    # scope to subtree
find_objects name="Player"                          # Tier2 SCENE — needs discover_tools("SCENE")
```

| Task | Tool |
|------|------|
| By name | `search_scene query="X"` |
| By component + tag | `search_scene query="t:X tag=Y"` |
| Subtree structure | `get_hierarchy root="/X"` |
| Quick overview | `get_hierarchy summary=true` |

## Spatial Query (Tier2 SCENE — `discover_tools("SCENE")` first)

```
spatial_query action="objects_in_radius" path="/Player" radius=5
spatial_query action="in_front_of" path="/Camera" distance=3
```

> **v0.93.1:** `objects_in_radius` results are sorted nearest-first; `cap` truncates after sort.

## Read Unity Events (Tier2 SCENE — `discover_tools("SCENE")` first)

Lists Unity events (e.g. `onClick`, `onValueChanged`) on all components of an object:
```
get_unity_events path="/Player"
```

## Delete / Show-Hide

```
delete_object path="/Level/OldWall"                 # TIER1 SCENE
delete_object path="/Level/Group" force=true        # non-empty container
set_active path="/UI/Tutorial" active=false
set_active path="/UI/Tutorial" active=true
```

## Reparent / Rename / Order

```
set_parent path="/OldParent/Child" parent="/NewParent"
set_parent path="/Group/Obj" parent=null                            # move to scene root
rename_object path="/Obj" name="NewName"        # Tier2 SCENE — returns new path, use it in subsequent calls
set_sibling_index path="/Obj" index=0           # 0 = first child (Tier2 SCENE)
```

## Scene Management

```
scene action="list"
scene action="open" path="Assets/Scenes/X.unity"
scene action="save"
scene action="open_additive" path="Assets/Scenes/Y.unity"
scene action="close" path="Assets/Scenes/Y.unity"
```

Environment lighting (Tier2 SCENE — `discover_tools("SCENE")` first):
```
scene_environment action="get"
scene_environment action="set" prop="fogColor" value="#8080FF"
```

## Move Between Scenes (Tier2 SCENE)

```
transfer_object path="/Props/Tree" action=move target_scene="Level2"
transfer_object path="/Props/Tree" action=copy parent="/Props/Copies"   # duplicate in same scene
```

> **v0.93 copy fix:** `action=copy` now sets active scene before Instantiate, so clone lands in the correct target scene (not source).

## Region Clear — mass delete by XZ polygon (Tier2 SCENE)

```
region_clear vertices="0,0;10,0;10,10;0,10"                        # dry_run=true by default
region_clear vertices="0,0;10,0;10,10;0,10" filter="Grass" dry_run=false
```

## Verify After Mutations

```
validate_references path="/Level"               # TIER1 — broken refs report
lint_scene_refs                                 # TIER1 — non-mutating lint
verify_after_change                             # CORE — 5-gate: compile→errors→console→tests
resolve_scene_refs path="/Level"                # CORE — resolves broken refs by GUID lookup
scan_scene                                      # Tier2 VERIFY — needs discover_tools("VERIFY")
scene_health focus="hierarchy"                  # Tier2 VERIFY — severity-tagged audit
```

## State Hash & Diff

```
fingerprint                                     # scene hash (~5 tokens), skip re-reading if unchanged
fingerprint path="/Level" depth=2               # subtree hash
scene_diff                                      # compare with last snapshot (Tier2 SCENE)
```

## Best Practices

```
/Level
  /Environment      — static objects
  /Characters       — characters
  /Interactables    — interactive
  /VFX              — effects
  /UI               — Canvas + UI
```

- `get_hierarchy summary=true` first, drill with `root=` per subtree
- Search before creating: `search_scene query="Player"`
- 2+ mutations → `scene_change_plan` + `apply_scene_change` for rollback safety
- 2+ property writes → `configure_objects` instead of N `set_property` calls
- No spaces in names: `Wall_Left` not `Wall Left`
- `rename_object` returns new path — use it in all subsequent calls
