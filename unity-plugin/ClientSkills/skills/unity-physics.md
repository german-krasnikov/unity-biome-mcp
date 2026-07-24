---
name: unity-physics
description: Unity Biome MCP physics setup — Rigidbody, colliders, spatial queries, physics debug. Patterns for manage_component, configure_objects, autofit_collider, check_colliders, spatial_query, debug_physics.
user-invocable: false
---

# Unity Physics Setup (MCP)

**CORE tools** (always visible, no gating): `set_property`, `get_component`, `manage_component`.
**TIER1** (always visible): `configure_objects`.
**Tier2 SCENE** (`discover_tools category="SCENE"` first): `autofit_collider`, `check_colliders`, `spatial_query`, `region_clear`, `get_spatial_context`.
**Tier2 VERIFY** (`discover_tools category="VERIFY"` first): `scan_scene`.
**Tier2 RUNTIME** (`discover_tools category="RUNTIME"` first, Play Mode required): `debug_physics`.

## Rigidbody

```
manage_component path="/MyCube" type="Rigidbody" action="add"
set_property path="/MyCube" component="Rigidbody" prop="mass" value="2"
set_property path="/MyCube" component="Rigidbody" prop="useGravity" value="true"
set_property path="/MyCube" component="Rigidbody" prop="isKinematic" value="false"
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `mass` | float | 1 | Масса (кг) |
| `drag` | float | 0 | Линейное сопротивление |
| `angularDrag` | float | 0.05 | Угловое сопротивление |
| `useGravity` | bool | true | Гравитация |
| `isKinematic` | bool | false | Кинематический |
| `interpolation` | int | 0 | 0=None, 1=Interpolate, 2=Extrapolate |
| `collisionDetectionMode` | int | 0 | 0=Discrete, 1=Continuous, 2=ContinuousDynamic |
| `constraints` | int | 0 | Битовая маска: FreezePos X=2,Y=4,Z=8; FreezeRot X=16,Y=32,Z=64; All=126 |

## Colliders

```
# BoxCollider
manage_component path="/Obj" type="BoxCollider" action="add"
set_property path="/Obj" component="BoxCollider" prop="size" value="(2,1,2)"
set_property path="/Obj" component="BoxCollider" prop="isTrigger" value="true"

# SphereCollider
manage_component path="/Obj" type="SphereCollider" action="add"
set_property path="/Obj" component="SphereCollider" prop="radius" value="0.5"

# MeshCollider (convex=true ОБЯЗАТЕЛЬНО для Rigidbody!)
manage_component path="/Obj" type="MeshCollider" action="add"
set_property path="/Obj" component="MeshCollider" prop="convex" value="true"
```

## Multi-Property Setup (TIER1)

`configure_objects` — always visible, sets multiple properties in one call:

```
configure_objects targets="/Ball" properties={
  "Rigidbody.mass": 1.0,
  "Rigidbody.drag": 0.2,
  "SphereCollider.radius": 0.5
}
```

## Batch Physics Setup

```
batch commands="
manage_component path=\"/PhysCube\" type=\"Rigidbody\" action=\"add\"
manage_component path=\"/PhysCube\" type=\"BoxCollider\" action=\"add\"
set_property path=\"/PhysCube\" component=\"Rigidbody\" prop=\"mass\" value=\"3\"
set_property path=\"/PhysCube\" component=\"BoxCollider\" prop=\"size\" value=\"(1,1,1)\"
"
```

## Collider Validation (Tier2 SCENE)

```
discover_tools category="SCENE"
autofit_collider path="/Obj" type="box"    # box|sphere|capsule — подгонка под mesh bounds
check_colliders path="/Obj"                # проблемы коллайдеров (triggers без Rigidbody, etc)
check_colliders                            # вся сцена (top-level только — в batch требует path)
```

**Внимание:** Внутри `batch` у `check_colliders` всегда передавай `path` — `CommandValidator` проверяет контракт для sub-команд.

```
discover_tools category="VERIFY"
scan_scene    # инфраструктурный скан: rigidbody, colliders, triggers, audio, lights
```

## Spatial Analysis (Tier2 SCENE)

```
discover_tools category="SCENE"
get_spatial_context path="/Obj" radius=5.0    # коллайдеры + approach vectors + nearby

# Основные spatial_query actions:
spatial_query action="nearest" path="/Player" component="Enemy"
spatial_query action="objects_in_radius" path="/Player" radius=10.0
spatial_query action="bounds_info" path="/Building"
spatial_query action="raycast" path="/Player" target="/Enemy" layer_mask="Default,Enemy"
spatial_query action="spatial_map" cell_size=2.0

# Удаление объектов по полигону (dry_run=true по умолчанию — безопасно)
region_clear vertices="0,0;10,0;10,10;0,10"               # превью
region_clear vertices="0,0;10,0;10,10;0,10" dry_run=false  # реальное удаление
```

## Runtime Debug (Tier2 RUNTIME, Play Mode required)

```
discover_tools category="RUNTIME"
debug_physics path="/Ball" radius=5.0
```

Возвращает: Rigidbody (`vel`, `angVel`, `mass`, `kinematic`), Colliders на объекте, объекты в радиусе (Physics.OverlapSphere, max 10), layer. Только в Play Mode.

## Verification

```
get_component path="/PhysCube" type="Rigidbody"
get_component path="/PhysCube" type="BoxCollider"
check_colliders path="/PhysCube"
```

## Anti-patterns

| Anti-pattern | Fix |
|-------------|-----|
| MeshCollider без `convex=true` + Rigidbody | Всегда `convex=true` для динамических |
| `mass=0` | Минимум `mass=0.001` |
| Много MeshCollider | Примитивные коллайдеры |
| Collider на child без Rigidbody на parent | Rigidbody на root объекта |
| `region_clear dry_run=false` сразу | Сначала `dry_run=true` (default), проверь список |
| `debug_physics` вне Play Mode | Войти в Play Mode, затем вызывать |
