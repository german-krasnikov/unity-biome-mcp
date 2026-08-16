# Shaders and Materials

Use `shader` to inspect or create shader assets and edit Shader Graph files. Use
`material` to create material assets, change their values, and assign them to
renderers. This page is the canonical material/shader workflow; [Asset Tools](assets.md)
links here instead of repeating it.

## Inspect before editing

```python
shader_info = await shader(
    action="get",
    path="Assets/Shaders/Water.shader",
)

material_info = await material(
    action="get",
    path="Assets/Materials/Water.mat",
)
```

For a renderer, inspect its slots before choosing one:

```python
slots = await material(action="list_slots", object_path="/Lake")
properties = await material(
    action="list_properties",
    object_path="/Lake",
    slot=0,
)
```

Shader property names vary by render pipeline. Read `list_properties` rather than
assuming that Built-in, URP, and HDRP use the same names.

## Create and configure a material

```python
await material(
    action="create",
    path="Assets/Materials/Alert.mat",
    shader="Universal Render Pipeline/Lit",
)

await material(
    action="set_fields",
    path="Assets/Materials/Alert.mat",
    value="_BaseColor=#D92B2B\n_Smoothness=0.35",
)
```

`set_fields` accepts newline-separated `property=value` pairs and also recognizes
`shader` and `renderQueue`. Unknown properties are skipped, so read the result and
verify important values with `material(action="get", ...)`.

For one property, use `set`:

```python
await material(
    action="set",
    path="Assets/Materials/Alert.mat",
    prop="_BaseColor",
    value="#D92B2B",
)
```

Material values accept the type exposed by the shader: numbers, colors, vectors,
or a texture asset path. `prop="shader"` changes the shader;
`prop="renderQueue"` requires an integer. When `prop` is not a declared property,
`value="true"` or `"false"` enables or disables a material keyword of that name.

## Shared asset or renderer instance

When `material(action="set")` targets `object_path`, the default `target="shared"`
edits the renderer's shared material. Every renderer using that asset can change.

```python
# Deliberately edit the shared asset in slot 1.
await material(
    action="set",
    object_path="/Robot",
    slot=1,
    target="shared",
    prop="_EmissionColor",
    value="#40A0FFFF",
)

# Clone the selected slot for this renderer, then edit the clone.
await material(
    action="set",
    object_path="/Robot",
    slot=1,
    target="instance",
    prop="_EmissionColor",
    value="#40A0FFFF",
)
```

`target="asset"` currently follows the shared-material path. `target="instance"`
requires `object_path` and creates a non-asset Material for that renderer slot.
If durable reuse matters, create a `.mat` asset and assign it explicitly instead.

## Assign an existing material

`copy` assigns the same shared material to one or more renderers; it does not clone
the material asset.

```python
result = await material(
    action="copy",
    source="Assets/Materials/Alert.mat",
    targets="/Enemies/GuardA,/Enemies/GuardB",
    slot=0,
)
```

`source` can also be a scene object with a renderer. Targets that cannot be found
or have no renderer are skipped, so confirm the returned assignment count and
inspect the target slots.

For the small “create a new material and set only a color” helper, see
[`set_material`](objects.md#set_material). It creates a new Material object; use
the explicit workflow above when asset identity and reuse matter.

## Discover shaders and compilation errors

```python
available = await material(action="list_shaders", filter="Water")
errors = await material(
    action="get_errors",
    path="Assets/Shaders/Water.shader",
)
```

`list_shaders` searches shader assets in the project. `get_errors` reads Unity's
shader compiler messages for one shader asset.

## Create a `.shader` asset

`shader(action="create")` writes a shader file from raw code or one of the
`unlit`, `lit`, and `transparent` templates.

```python
created = await shader(
    action="create",
    path="Assets/Shaders/Selection.shader",
    preset="unlit",
    shader_name="Game/Selection",
)
```

The path must end in `.shader`. Unity imports the new file immediately and the
result includes the first compiler error when import fails. The built-in templates
use conventional shader source and may need adaptation for the project's render
pipeline.

`shader(action="set")` is different from the other shader actions: its `path` is a
**scene object with a Renderer**, not a shader asset. It changes a property or
keyword on that renderer's shared material. Prefer `material(action="set", ...)`
for clearer slot and shared/instance control.

## Work with Shader Graph

Start by inspecting the graph and retain the returned node IDs:

```python
graph = await shader(
    action="graph_get",
    path="Assets/Shaders/Dissolve.shadergraph",
)
```

Create a graph from a supported preset:

```python
created = await shader(
    action="graph_create",
    path="Assets/Shaders/NewEffect.shadergraph",
    preset="lit_graph",
)
```

The supported Shader Graph templates are `lit_graph` and `unlit_graph` and target
the Universal Render Pipeline.

Node and edge mutations operate on the serialized graph:

```python
graph = await shader(
    action="graph_node",
    path="Assets/Shaders/NewEffect.shadergraph",
    node_type="MultiplyNode",
    node_action="add",
)

graph = await shader(
    action="graph_edge",
    path="Assets/Shaders/NewEffect.shadergraph",
    output_node="<source-node-id>",
    output_slot=0,
    input_node="<target-node-id>",
    input_slot=0,
    edge_action="add",
)
```

For removal, pass `node_action="remove"` with `node_id`, or
`edge_action="remove"` with both node IDs and slot numbers. The current public
wrapper does not implement node configuration through `node_action`; a value other
than `remove` adds a node.

Layout is independent of topology:

```python
layout = await shader(
    action="graph_get_layout",
    path="Assets/Shaders/NewEffect.shadergraph",
)

await shader(
    action="graph_set_layout",
    path="Assets/Shaders/NewEffect.shadergraph",
    layout=layout,
)

await shader(
    action="graph_auto_layout",
    path="Assets/Shaders/NewEffect.shadergraph",
    h_gap=100,
    v_gap=60,
)
```

After any serialized graph mutation, call `graph_get` and check Unity's Console or
shader errors. Keep Shader Graph files under version control so malformed edits are
recoverable.

## Audit materials and textures

`material_audit` scans the current scene:

```python
summary = await material_audit(action="summary")
duplicates = await material_audit(action="duplicates")
compression = await material_audit(
    action="compression",
    platform="Android",
)
recommendations = await material_audit(action="recommendations")
```

Actions are `summary`, `materials`, `textures`, `duplicates`, `compression`, and
`recommendations`. The duplicate fingerprint intentionally ignores textures;
review each group before consolidating materials.

## Verification checklist

1. Read the shader's actual property names.
2. Decide whether the change should affect a shared asset or one renderer.
3. Apply one focused change.
4. Read the material or renderer slot again.
5. Check shader compiler messages and the Unity Console.
6. Capture a [screenshot](screenshots.md) when visual output is acceptance evidence.

See [Rendering analysis](assets.md#analyze-rendering) for draw calls, batching,
lights, and overdraw, and the [Generated Tool Schema](../tools-schema/index.md)
for exhaustive signatures.
