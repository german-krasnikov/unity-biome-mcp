# Unity Component Management (MCP)

## Adding / Removing / Toggling Components

CORE — always available (no discover_tools needed).

```
manage_component path="/Obj" type="Rigidbody" action="add"
manage_component path="/Obj" type="AudioSource" action="remove"
manage_component path="/Obj" type="Collider" action="enable"
manage_component path="/Obj" type="Collider" action="disable"
```

## Reading Components

```
get_component path="/Player" type="Transform"                         # single component
get_component path="/Player" type="Rigidbody" fields="mass,drag"      # field projection (saves tokens)
inspect paths="/A,/B" components="Transform"                          # multi-object (1 call vs N)
get_components_list id=12345                                          # list all (by instance ID)
get_object_detail id=12345                                            # full dump (heavy, debug)
```

## Setting Properties

### value is ALWAYS a string
```
set_property path="/Obj" component="Light" prop="intensity" value="2.5"       # number
set_property path="/Obj" component="MeshRenderer" prop="enabled" value="false" # bool
set_property path="/Obj" component="Light" prop="type" value="1"              # enum (0=Spot,1=Dir,2=Point)
set_property path="/Obj" component="Transform" prop="localPosition" value="(1,2,3)"    # Vector3
set_property path="/Obj" component="Light" prop="color" value="(1,0.5,0,1)"  # Color as Vector4
```

### ObjectReference formats
```
value="/Player"                       # scene path
value="Assets/Materials/Red.mat"      # asset path
value="Assets/Anim.fbx::Walk"        # sub-asset (clip)
value="#12345"                        # instance ID
value="null"                         # clear reference
```

### dry_run — preview without applying
```
set_property ... prop="intensity" value="5" dry_run=true
```

## set_property_delta — atomic increment

```
set_property_delta path="/Obj" component="Rigidbody" prop="mass" delta="+0.5"
set_property_delta path="/Obj" component="Transform" prop="localPosition" delta="(+1,0,0)"
# Returns: old -> new
```

```
# BAD: read -> modify -> set_property (3 calls, race condition)
# GOOD: set_property_delta (1 atomic call)
```

## configure_objects — multi-prop on multiple objects (TIER1, direct_only — not inside batch)

```
configure_objects targets="/Player" properties={"Rigidbody.mass": 2.0, "Rigidbody.drag": 0.5, "Rigidbody.useGravity": true}
configure_objects targets="/A,/B" properties={"Light.intensity": 1.5, "Light.color": "(1,0.8,0,1)"}
```

Single call replaces multiple set_property calls.

## object_diff — compare two GameObjects

```
object_diff path_a="/ObjA" path_b="/ObjB"
object_diff path_a="SceneA:/Alice" path_b="SceneB:/Bob"   # cross-scene
```

## set_active — show/hide

```
set_active path="/UI/Panel" active=false
set_active path="/UI/Panel" active=true
```

## Read Unity Events (Tier2 SCENE)

**Gated:** `discover_tools category="SCENE"`.

```
get_unity_events path="/Player"           # lists all Unity events on every component
get_unity_events path="/UI/Button"        # e.g. returns onClick, onPointerDown
```

Read-only. Use to discover what events exist before wiring.

## Wire / Unwire UnityEvents

**Gated:** `discover_tools category="COMPONENTS"`.

```
# Wire persistent listener
wire_event path="/Btn" component="Button" event="onClick" target="/Handler" method="OnClick"
wire_event path="/Btn" component="Button" event="onClick" target="/Door" method="SetActive" \
  arg_type="bool" arg_value="true"

# arg_type: void (default) | bool | int | float | string | object
# arg_value: required when arg_type != void. For object: scene/asset path.

# Unwire by index (0-based) or all
unwire_event path="/Btn" component="Button" event="onClick" index=0
unwire_event path="/Btn" component="Button" event="onClick"          # clear all
```

## auto_wire — auto-connect references

**Gated:** `discover_tools category="COMPONENTS"`.

```
auto_wire path="/Player" dry_run=true    # preview what would be connected
auto_wire path="/Player"                 # apply connections
```

## Validate References

```
validate_references path="/Root"                       # depth=3 default, [ERROR]/[MISSING] only
validate_references path="/Root" depth=1               # quick top-level
validate_references path="/Root" verbose=true           # include [OK] lines
validate_references path="/Root" ignore_optional=true   # skip [Optional] fields
```

## References (linking objects)

**Gated:** `discover_tools category="COMPONENTS"`.

```
references action="get" path="/Obj"                                  # outgoing refs
references action="get" path="/Obj" children=true depth=2            # recursive
references action="find_to" path="/Target"                           # who references this?
references action="remap" path="/Obj" source="/Old" target="/New"    # remap single
references action="remap" path="/Obj" mappings="/OldA->/NewA;/OldB->/NewB"  # batch remap
```

## Batch Operations

```
batch commands="
manage_component path=\"/E1\" type=\"Rigidbody\" action=\"add\"
manage_component path=\"/E2\" type=\"Rigidbody\" action=\"add\"
set_property path=\"/E1\" component=\"Rigidbody\" prop=\"mass\" value=\"2\"
set_property path=\"/E2\" component=\"Rigidbody\" prop=\"mass\" value=\"2\"
"
```

**Rule: 2+ operations -> batch.** 10-100x faster.

## Verification

```
manage_component path="/Obj" type="AudioSource" action="add"
set_property path="/Obj" component="AudioSource" prop="volume" value="0.8"
get_component path="/Obj" type="AudioSource"       # verify
```
