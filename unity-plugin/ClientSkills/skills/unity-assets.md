---
name: unity-assets
description: Unity asset management via MCP — asset, prefab, scriptable_object, project_settings, shader, material, material_audit. All TIER2 ASSETS (require discover_tools category="ASSETS").
user-invocable: false
---

# Unity Assets (MCP)

Управление ассетами, материалами, префабами, ScriptableObjects через MCP.

## Gating

Все тулы TIER2 ASSETS. Перед использованием:
```
discover_tools category="ASSETS"
```
Включит: `asset`, `prefab`, `scriptable_object`, `project_settings`, `shader`, `material`, `material_audit`.

## asset

action: `find|get_info|create|move|validate_move|duplicate|delete|get_dependencies|find_dependents|import_settings|export_package|import_package`.

```
asset action="find" type="Material" folder="Assets/Materials"
asset action="find" name="PlayerConfig" type="ScriptableObject"
asset action="find" query="Player" type="Prefab"
asset action="get_info" path="Assets/Prefabs/Player.prefab"
asset action="create" type="Folder" path="Assets/NewFolder"
asset action="create" type="Material" path="Assets/Materials/NewMat.mat"
asset action="move" source="Assets/Old/Obj.prefab" dest="Assets/New/Obj.prefab"
asset action="validate_move" source="Assets/Old/Obj.prefab" dest="Assets/New/Obj.prefab"  # dry-run
asset action="duplicate" source="Assets/Configs/Base.asset" dest="Assets/Configs/Copy.asset"
asset action="delete" path="Assets/Trash/Old.mat"
asset action="get_dependencies" path="Assets/Prefabs/Player.prefab" recursive=true
asset action="find_dependents" path="Assets/Materials/Glow.mat"
asset action="export_package" path="Assets/MyStuff" output="Assets/Export/MyStuff.unitypackage"
asset action="import_package" path="/absolute/path/to/package.unitypackage"
```

## prefab

action: `create|instantiate|apply|unpack|modify|save|create_variant|revert|get_overrides|edit`.

```
prefab action="create" path="/MyObject" asset_path="Assets/Prefabs/MyObject.prefab"
prefab action="instantiate" path="Assets/Prefabs/Enemy.prefab" parent="/Enemies" position="0,0,5"
prefab action="apply" path="/MyObject"
prefab action="revert" path="/MyObject"
prefab action="revert" path="/MyObject" scope="children"          # откатить только переопределения детей
prefab action="get_overrides" path="/MyObject"
prefab action="get_overrides" path="/MyObject" format="structured" # source_prefab, changed_properties count, added/removed_components count
prefab action="save" path="/MyObject" asset_path="Assets/Prefabs/MyObject.prefab" mode="new"       # ошибка если уже существует
prefab action="save" path="/MyObject" asset_path="Assets/Prefabs/MyObject.prefab" mode="overwrite" # перезаписать (default)
prefab action="unpack" path="/MyObject"
prefab action="unpack" path="/MyObject" recursive=true

# Batch instantiate
batch(commands="""
prefab action=instantiate path=Assets/Prefabs/Coin.prefab parent=/Collectibles position=1,0,0
prefab action=instantiate path=Assets/Prefabs/Coin.prefab parent=/Collectibles position=2,0,0
prefab action=instantiate path=Assets/Prefabs/Coin.prefab parent=/Collectibles position=3,0,0
""")

# Редактировать префаб-ассет напрямую (без инстанса на сцене)
prefab action="edit" asset_path="Assets/Prefabs/Player.prefab" component="Rigidbody" prop="mass" value="5"
prefab action="edit" asset_path="Assets/Prefabs/Player.prefab" add_component="BoxCollider"
prefab action="edit" asset_path="Assets/Prefabs/Player.prefab" remove_component="CapsuleCollider"
prefab action="edit" asset_path="Assets/Prefabs/Player.prefab" child_path="Head/Eye" component="MeshRenderer" prop="enabled" value="false"  # edit child inside prefab
```

## material

action: `create|get|set|copy|list_properties|list_slots|get_errors|list_shaders|set_fields`.

```
material action="create" path="Assets/Materials/Glow.mat" shader="Universal Render Pipeline/Lit"
material action="get" path="Assets/Materials/Glow.mat"
material action="get" object_path="/MyCube"
material action="list_properties" path="Assets/Materials/Glow.mat"
material action="list_slots" object_path="/MyCube"
material action="set" path="Assets/Materials/Glow.mat" prop="_BaseColor" value="#FF0000"
material action="set" object_path="/MyCube" prop="_Metallic" value="0.8" slot=1
material action="set" object_path="/MyCube" prop="_BaseColor" value="#00FF00" target="instance"  # clone, asset_modified: false
material action="copy" source="Assets/Materials/Glow.mat" targets="/Cube1,/Cube2,/Cube3"
material action="get_errors" path="Assets/Materials/Glow.mat"
material action="list_shaders" filter="Lit"
material action="set_fields" path="Assets/Materials/Glow.mat" value="_BaseColor=#FF0000\n_Metallic=0.8"
```

**path vs object_path**: `path` = ассет (`Assets/...`), `object_path` = объект на сцене (`/Name`). `slot` = индекс материала (default 0).

**target** (только для `set`): `shared` (default) — редактирует shared material asset напрямую; `instance` — клонирует через `sharedMaterials` (требует `object_path`), возвращает `mutated: renderer_instance, asset_modified: false`; `asset` — то же что shared.

## material_audit

Сцено-широкий аудит. action: `summary|materials|textures|duplicates|compression|recommendations`.

```
material_audit action="summary"
material_audit action="duplicates"
material_audit action="compression" platform="Android"
material_audit action="recommendations" platform="iOS"
```

## scriptable_object

action: `create|get|set|list_types|find`.

```
scriptable_object action="find" type="GameConfig"
scriptable_object action="list_types" filter="Config"
scriptable_object action="create" type="LevelConfig" path="Assets/Configs/Level3.asset"
scriptable_object action="get" path="Assets/Configs/Level3.asset"
scriptable_object action="get" path="Assets/Configs/Level3.asset" fields="moveSpeed,health"  # вернуть только запрошенные поля
scriptable_object action="set" path="Assets/Configs/Level3.asset" prop="moveSpeed" value="5.5"   # → ok: moveSpeed = 3.0 → 5.5
scriptable_object action="set" path="Assets/Configs/Level3.asset" fields="speed=5.0\nhealth=100" # → ok: speed = 3.0 → 5.0\nok: health = 80 → 100
```

## project_settings

action: `get|set`. target: `tags|layers|sorting_layers|quality|physics|time|player`.

```
project_settings action="get" target="tags"
project_settings action="set" target="tags" value="Enemy"
project_settings action="set" target="layers" index=8 value="Interactable"
project_settings action="set" target="physics" prop="gravity" value="(0,-20,0)"
```

## Anti-patterns

| Anti-pattern | Problem | Fix |
|-------------|---------|-----|
| Тулы без `discover_tools category="ASSETS"` | "Unknown tool" | Всегда discover_tools сначала |
| `value` не строка | Ошибка парсинга | `value="100"` не `value=100` |
| `path` без `Assets/` | Ассет не найден | Всегда `Assets/...` |
| N вызовов вместо batch | Лишние round-trips | `batch commands="..."` для 2+ |
