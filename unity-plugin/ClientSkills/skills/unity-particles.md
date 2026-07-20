# Unity Particle System (MCP)

**Gated:** `discover_tools(category="MEDIA")`

Tools: `particle` (TIER2 MEDIA), `vfx_intent` (TIER2 MEDIA, direct_only), `render_analyze` (action=overdraw)

## Создание

```
particle action="create" path="/VFX" name="CampFire" preset="fire"
particle action="create" path="/VFX" name="MyParticles"   # пустая
particle action="apply" path="/VFX/OldEffect" preset="explosion"  # применить пресет
```

**v0.91 name-from-path:** если `path` не найден как сцена-объект, последний сегмент становится именем, родитель — из пути. `particle action="create" path="/VFX/CampFire" preset="fire"` → создаст `CampFire` под `/VFX` без явного `name`.

Пресеты: `fire` | `smoke` | `sparks` | `rain` | `snow` | `explosion` | `magic` | `dust` | `blood` | `trail`

## Настройка

```
particle action="set" path="/VFX/Fire" module="main" prop="startSpeed" value="5"
particle action="set" path="/VFX/Fire" module="emission" prop="rateOverTime" value="100"
particle action="set" path="/VFX/Fire" module="shape" prop="shapeType" value="Cone"
particle action="get" path="/VFX/Fire"   # читать все модули
```

### main
| Property | Type |
|----------|------|
| `duration`, `startDelay`, `startSpeed`, `startSize`, `startLifetime` | float/minmax |
| `startColor` | color (`#FF6600`) |
| `gravityModifier` | float/minmax |
| `loop`, `playOnAwake` | bool |
| `maxParticles` | int |
| `simulationSpace` | `World` / `Local` |
| `startSize3D`, `startSizeX/Y/Z` | bool / float/minmax |

**MinMax:** `"min,max"` — random between values.

### emission
`enabled` (bool), `rateOverTime` (float), `rateOverDistance` (float)

### shape
`shapeType` (Cone/Sphere/Box/Circle/Edge/Hemisphere), `angle`, `radius`, `radiusThickness`, `scale`, `position`

**Not implemented:** `arc`, `rotation` → `ArgumentException`

### noise
`enabled`, `strength`, `frequency`, `scrollSpeed`, `damping`, `octaveCount`

### renderer
`renderMode` (Billboard/Stretch/HorizontalBillboard/Mesh), `velocityScale`, `lengthScale`

**Not implemented:** `sortMode`, `minParticleSize`, `maxParticleSize`

### colorOverLifetime / sizeOverLifetime / velocityOverLifetime / trails

```
particle action="set" path="/VFX/Fire" module="colorOverLifetime" prop="enabled" value="true"
particle action="set" path="/VFX/Fire" module="colorOverLifetime" prop="gradient" value="#FFAA00@0;#FF0000@0.5;#000000@1"
particle action="set" path="/VFX/Fire" module="sizeOverLifetime" prop="curve" value="0:0;0.5:1;1:0"
particle action="set" path="/VFX/Fire" module="velocityOverLifetime" prop="y" value="0:0;1:2"
particle action="set" path="/VFX/Fire" module="velocityOverLifetime" prop="space" value="World"
particle action="set" path="/VFX/Fire" module="trails" prop="enabled" value="true"
particle action="set" path="/VFX/Fire" module="trails" prop="ratio" value="1"
```

| Module | Properties |
|--------|-----------|
| `colorOverLifetime` | `enabled`, `gradient` |
| `sizeOverLifetime` | `enabled`, `curve` |
| `velocityOverLifetime` | `enabled`, `x`/`y`/`z` (curve), `space` (World/Local) |
| `trails` | `enabled`, `ratio`, `lifetime`, `minVertexDistance`, `worldSpace`, `dieWithParticles`, `widthOverTrail`, `colorOverTrail`, `colorOverLifetime` |

- **gradient:** `"#RRGGBB@t;..."` — max 8 keys
- **curve:** `"t:v;t:v;..."` — min 1 key

`rotationOverLifetime` / `collision` — только `prop="enabled"`. Любой другой prop → `ArgumentException`.

## Playback

```
particle action="play" path="/VFX/Fire"
particle action="stop" path="/VFX/Fire"
particle action="pause" path="/VFX/Fire"
```

## vfx_intent — NL создание VFX (direct_only)

```
vfx_intent instruction="create rain particle system covering the whole scene"
vfx_intent instruction="add fire effect to /Torch"
```

**direct_only — НИКОГДА в batch.**

## render_analyze — overdraw анализ

```
render_analyze action="overdraw"
```

## Batch пример

```
batch commands="
particle action=\"create\" path=\"/VFX\" name=\"CampFire\" preset=\"fire\"
particle action=\"create\" path=\"/VFX\" name=\"FireSmoke\" preset=\"smoke\"
set_property path=\"/VFX/CampFire\" component=\"Transform\" prop=\"localPosition\" value=\"(0,0,0)\"
set_property path=\"/VFX/FireSmoke\" component=\"Transform\" prop=\"localPosition\" value=\"(0,0.5,0)\"
particle action=\"set\" path=\"/VFX/CampFire\" module=\"main\" prop=\"startSpeed\" value=\"2,4\"
"
```

## Anti-patterns

| Anti-pattern | Fix |
|-------------|-----|
| `maxParticles` > 500 на мобильных | max 200-500 |
| Без preset/материала | Белые квадраты → используй preset |
| `simulationSpace=World` на движущемся объекте | Используй `Local` |
| `module="X" prop="" value="true"` | `prop="enabled" value="true"` |
| `vfx_intent` в batch | Вызывай напрямую (direct_only) |
| collision без ограничения | Минимизируй использование модуля |
