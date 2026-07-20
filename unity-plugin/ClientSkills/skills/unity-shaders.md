# Unity Shaders (MCP)

**Gated:** `discover_tools(category="ASSETS")`

Tools: `shader` (TIER2 ASSETS), `material` (TIER2 ASSETS), `material_audit` (TIER2 ASSETS, read-only), `set_material` (TIER2 **SCENE** — другая категория!), `render_analyze` (action=materials|shaders)

## Получить информацию

```
# Шейдер на объекте сцены (свойства, keywords)
shader action="get" path="/MyCube"

# Шейдер из ассета
shader action="get" path="Assets/Shaders/MyEffect.shader"

# ShaderGraph
shader action="graph_get" path="Assets/Shaders/MyShader.shadergraph"
```

`path` принимает scene paths (`/MyCube`) и asset paths (`Assets/Shaders/X.shader`). `set_material` — SCENE категория, требует `discover_tools(category="SCENE")`.

## Создание шейдера

```
# Из пресета
shader action="create" path="Assets/Shaders/MyUnlit.shader" preset="unlit"
shader action="create" path="Assets/Shaders/MyLit.shader" preset="lit"
shader action="create" path="Assets/Shaders/MyTransparent.shader" preset="transparent"

# С кастомным именем
shader action="create" path="Assets/Shaders/Custom.shader" preset="unlit" shader_name="Custom/MyEffect"

# С кастомным кодом
shader action="create" path="Assets/Shaders/Custom.shader" code="Shader \"Custom/MyShader\" { ... }"
```

### Доступные пресеты

| Preset | Description |
|--------|-------------|
| `unlit` | Простой unlit: цвет + текстура, без освещения |
| `lit` | Standard lit: metallic, smoothness, normal map |
| `transparent` | Transparent: alpha blending, прозрачность |

## Настройка свойств материала

```
# Цвет
shader action="set" path="/MyCube" prop="_Color" value="#FF0000"
shader action="set" path="/MyCube" prop="_BaseColor" value="#00FF00FF"

# Float/Range
shader action="set" path="/MyCube" prop="_Metallic" value="0.8"
shader action="set" path="/MyCube" prop="_Smoothness" value="0.5"
shader action="set" path="/MyCube" prop="_Cutoff" value="0.5"

# Vector
shader action="set" path="/MyCube" prop="_Tiling" value="(2,2,0,0)"

# Текстура (путь к asset)
shader action="set" path="/MyCube" prop="_MainTex" value="Assets/Textures/Diffuse.png"
shader action="set" path="/MyCube" prop="_BumpMap" value="Assets/Textures/Normal.png"

# Int
shader action="set" path="/MyCube" prop="_SrcBlend" value="5"
```

### Стандартные свойства

**URP/Lit:** `_BaseColor` (Color), `_BaseMap` (Tex), `_Metallic` (Float 0-1), `_Smoothness` (Float 0-1), `_BumpMap` (Tex), `_BumpScale` (Float), `_EmissionColor` (Color), `_EmissionMap` (Tex).

**Standard (Built-in):** `_Color` (Color), `_MainTex` (Tex), `_Metallic` (Float), `_Glossiness` (Float), `_BumpMap` (Tex).

## Keywords

```
shader action="set" path="/MyCube" keyword="_EMISSION" enabled="true"
shader action="set" path="/MyCube" keyword="_NORMALMAP" enabled="false"
```

Типичные: `_EMISSION`, `_NORMALMAP`, `_ALPHATEST_ON`, `_ALPHABLEND_ON`, `_METALLICGLOSSMAP`, `_SPECGLOSSMAP`.

## ShaderGraph

```
# Получить граф
shader action="graph_get" path="Assets/Shaders/MyEffect.shadergraph"

# Создать граф (preset, НЕ target!)
shader action="graph_create" path="Assets/Shaders/NewEffect.shadergraph" preset="unlit_graph"
shader action="graph_create" path="Assets/Shaders/NewLitEffect.shadergraph" preset="lit_graph"

# Добавить ноду
shader action="graph_node" path="Assets/Shaders/MyEffect.shadergraph" node_type="Color" node_action="add"

# Удалить ноду
shader action="graph_node" path="Assets/Shaders/MyEffect.shadergraph" node_id="abc123" node_action="remove"

# Соединить ноды (edge)
shader action="graph_edge" path="Assets/Shaders/MyEffect.shadergraph" output_node="node1" output_slot=0 input_node="node2" input_slot=0 edge_action="add"

# Удалить соединение
shader action="graph_edge" path="Assets/Shaders/MyEffect.shadergraph" output_node="node1" output_slot=0 input_node="node2" input_slot=0 edge_action="remove"
```

graph_create: `preset` — параметр называется **`preset`, не `target`**. Значения: `"unlit_graph"` | `"lit_graph"` (не `"UniversalRP"`) — любое другое значение кидает `ArgumentException`.
graph_node: `node_type` (Color, Texture2D, Multiply, Add, UV, Time...), `node_id`, `node_action` (add/remove).
graph_edge: `output_node`, `output_slot` (0-based), `input_node`, `input_slot` (0-based), `edge_action` (add/remove).

### ShaderGraph Properties (graph_add_property / graph_remove_property / graph_rename_property)

```
# Добавить property (name required, остальное optional)
shader action="graph_add_property" path="Assets/Shaders/MyEffect.shadergraph" name="_Glow" type="Float" default_value="1.0"

# С кастомным reference name (иначе авто: "_" + name)
shader action="graph_add_property" path="Assets/Shaders/MyEffect.shadergraph" name="TintColor" type="Color" reference_name="_TintColor"

# Boolean property
shader action="graph_add_property" path="Assets/Shaders/MyEffect.shadergraph" name="UseGlow" type="Boolean" default_value="true"

# Удалить property
shader action="graph_remove_property" path="Assets/Shaders/MyEffect.shadergraph" name="_Glow"

# Переименовать property
shader action="graph_rename_property" path="Assets/Shaders/MyEffect.shadergraph" name="_Glow" new_name="_GlowIntensity"
```

Параметры: `name` (имя property; для remove/rename — текущее имя), `type` (`Float`|`Color`|`Vector`|`Texture2D`|`Boolean`, default `"Float"`), `default_value` (применяется только для `Float`/`Boolean` — для `Color`/`Vector`/`Texture2D` игнорируется), `reference_name` (shader-код reference, default `"_" + name`), `new_name` (только для `graph_rename_property`, обязателен).

**Внимание:** `graph_add_property` кидает `ArgumentException`, если property с таким `name` уже существует. `graph_remove_property`/`graph_rename_property` кидают `InvalidOperationException`, если `name` не найден.

## Batch пример

```
batch commands="
shader action=\"create\" path=\"Assets/Shaders/GlowUnlit.shader\" preset=\"unlit\" shader_name=\"Custom/GlowUnlit\"
shader action=\"set\" path=\"/GlowCube\" prop=\"_BaseColor\" value=\"#00FFFF\"
shader action=\"set\" path=\"/GlowCube\" keyword=\"_EMISSION\" enabled=\"true\"
shader action=\"set\" path=\"/GlowCube\" prop=\"_EmissionColor\" value=\"#00FFFF\"
"
```

## render_analyze — анализ материалов/шейдеров

```
render_analyze action="materials"   # отчёт по использованию материалов
render_analyze action="shaders"     # отчёт по шейдерам
material_audit                      # неиспользуемые материалы
```

## Verification

```
shader action="get" path="/MyCube"
screenshot
```

## Anti-patterns

| Anti-pattern | Problem | Fix |
|-------------|---------|-----|
| Путь без `.shader` расширения | Ошибка создания | Всегда `path="Assets/Shaders/X.shader"` |
| Неверное имя property | "Property not found" | Сначала `shader action="get"` — посмотри доступные |
| URP property на Standard шейдере | Свойство не существует | `_BaseColor` (URP) vs `_Color` (Standard) |
| Кастомный код без валидации | Шейдер с ошибками | MCP вернёт warning с ошибками компиляции |
| graph_edge к несуществующей ноде | Ошибка | Сначала `graph_get` для получения node IDs |
| `graph_create target="UniversalRP"` | Ошибка — нет параметра `target` | Используй `preset="unlit_graph"` / `preset="lit_graph"` |
| `graph_add_property` с уже существующим `name` | `ArgumentException: Property already exists` | Сначала `graph_get` — проверь список properties |
