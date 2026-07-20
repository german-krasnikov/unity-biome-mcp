# Unity Animator Controller (MCP)

Управление Animator Controller через MCP: параметры, состояния, переходы, слои, blend trees.

**Gated:** `discover_tools category="MEDIA"`.

## Получить информацию

```
animator action="get" path="/Character"                # вся структура
animator action="get" path="/Character" state="Idle"   # конкретное состояние
```

## Параметры

```
animator action="add_param" path="/Character" params="Speed:float:0; Jump:trigger; IsGrounded:bool:true; Health:int:100"
animator action="rename_param" path="/Character" param="Speed" name="MoveSpeed"
```
Синтаксис: `Name:Type:Default`. Types: `float`, `int`, `bool`, `trigger`. `rename_param`: `param`=старое имя, `name`=новое.

**Compact alias format (v0.93):** `params=Speed:float:0; IsGrounded:bool:false` — несколько параметров через `;` в одном вызове. Альтернатива: передать `name`/`type`/`value` по отдельности для одного параметра (`params` имеет приоритет). Null-safe: если `params` пуст — fallback на `name`+`type`+`value`.

## Состояния

```
animator action="add_state" path="/Character" states="Idle:Idle.anim; Walk:Walk.anim; Run:Run.anim"
animator action="add_state" path="/Character" states="Punch:Punch.anim" layer=1     # добавить в слой по индексу
animator action="set_default" path="/Character" state="Idle"
animator action="rename_state" path="/Character" state="Idle" name="Idle_Base"
animator action="set_state_speed" path="/Character" state="Run" value="1.5"
```
Синтаксис: `StateName:ClipPath` (clip опционально). `rename_state`: `state`=старое имя, `name`=новое (ищет по всем слоям). `set_state_speed`: `value`=множитель скорости.

**Compact alias format (v0.93):** `states=Idle; Walk; Run` — несколько состояний через `;`. Альтернатива: `state="Idle"` (одно состояние, без клипа). Null-safe: если `states` пуст — fallback на `state`.

## Переходы

```
animator action="add_transition" path="/Character" source="Idle" target="Walk" conditions="Speed>0.1"
animator action="add_transition" path="/Character" source="*" target="Death" conditions="Health=0"
animator action="add_transition" path="/Character" source="Attack" target="Idle" has_exit_time=true exit_time=0.9
animator action="add_transition" path="/Character" source="Walk" target="Run" conditions="Speed>3" duration=0.25
animator action="update_transition" path="/Character" source="Idle" target="Walk" duration=0.5 conditions="Speed>0.2"
```
`update_transition` меняет существующий переход (duration/exit_time/has_exit_time/conditions) вместо создания нового.

### Синтаксис conditions
```
Speed>0.1    # float/int Greater       IsGrounded    # bool If (true)
Speed<0.1    # float/int Less          !IsGrounded   # bool IfNot (false)
Type=2       # int Equals              Jump          # trigger If
State!=0     # int NotEqual
```

| Param | Description |
|-------|-------------|
| `source` | Исходное состояние (`*` = AnyState) |
| `target` | Целевое состояние |
| `conditions` | Условия через `;` |
| `duration` | Длительность перехода (сек) |
| `exit_time` | Время выхода (0-1, доля анимации) |
| `has_exit_time` | Ждать exit_time (auto-false если есть conditions) |
| `layer` | Индекс слоя (int, default 0). Для `add_state`/`add_transition`/`set_default`/`update_transition` принимает ТОЛЬКО число — имя слоя тут не резолвится |

## Слои (Layers)

```
animator action="add_layer" path="/Character" name="UpperBody" weight=1.0 blending="Additive"
animator action="set_layer_weight" path="/Character" layer="UpperBody" weight=0.5
animator action="set_layer_blending" path="/Character" layer="UpperBody" blending="Override"
animator action="rename_layer" path="/Character" layer="UpperBody" name="Torso"
animator action="remove_layer" path="/Character" layer="Torso"
```

| Param | Description |
|-------|-------------|
| `layer` | Только в CRUD-действиях слоя (`remove_layer`/`rename_layer`/`set_layer_weight`/`set_layer_blending`) принимает индекс (int) ИЛИ имя (string); слой 0 (Base) нельзя удалить |
| `weight` | defaultWeight слоя, 0.0–1.0 |
| `blending` | `Override` \| `Additive` |

## Удаление

```
animator action="remove" path="/Character" type="param" name="Speed"
animator action="remove" path="/Character" type="state" name="Idle"
animator action="remove" path="/Character" type="transition" source="Idle" target="Walk"
```

## Blend Trees

### Создание
```
animator action="add_blend_tree" path="/Char" state="Locomotion" blend_type="1d" param="Speed"
```
`blend_type`: `1d` | `2d_simple` | `2d_freeform` | `2d_cartesian` | `direct`.
`param` (и `param_y` для 2D) создаётся автоматически как float.

### Children

```
# 1D — "clipName:threshold"
animator action="edit_blend_tree" path="/Char" state="Locomotion" edit_action="add_child" children="Idle:0; Walk:0.5; Run:1"

# 2D — "clipName:thresholdX,thresholdY"
animator action="edit_blend_tree" path="/Char" state="Move" edit_action="add_child" children="Idle:0,0; WalkFwd:0,1; WalkRight:1,0"
```

### Редактирование
```
animator action="edit_blend_tree" path="/Char" state="Locomotion" edit_action="set_thresholds" children="Idle:0; Walk:0.3; Run:1"
animator action="edit_blend_tree" path="/Char" state="Locomotion" edit_action="remove_child" children="Walk"
animator action="edit_blend_tree" path="/Char" state="Move" edit_action="set_param" param="MoveX" param_y="MoveY"
animator action="edit_blend_tree" path="/Char" state="Move" edit_action="set_type" blend_type="2d_freeform"
```

### Просмотр
```
animator action="get_blend_tree" path="/Char" state="Locomotion"
```

### edit_action reference

| edit_action | Описание | Параметры |
|-------------|----------|-----------|
| `add_child` | Добавить clip(s) | `children` |
| `remove_child` | Убрать clip(s) | `children` (имена) |
| `set_thresholds` | Обновить пороги | `children` |
| `set_param` | Сменить параметр(ы) | `param`, `param_y` |
| `set_type` | Сменить blend type | `blend_type` |

## Batch

```
# Multi-state setup in one call
batch(commands="""
animator action=add_param path=/Character params=Speed:float:0; IsGrounded:bool:true
animator action=add_state path=/Character states=Idle:Idle.anim; Walk:Walk.anim; Run:Run.anim
animator action=add_transition path=/Character source=Idle target=Walk conditions=Speed>0.1
animator action=add_transition path=/Character source=Walk target=Run conditions=Speed>3
""")
```
## NL-ярлык

`animator_intent` — TIER2 SYSTEM, **direct_only** (never in `batch`). `debug_animator` — RUNTIME, Play Mode only.
## Anti-patterns

| Anti-pattern | Fix |
|-------------|-----|
| Переход без conditions и без exit_time | Добавь conditions или has_exit_time=true |
| Дублирующиеся параметры | Проверяй `get` перед `add_param` |
| Несуществующий state в transition | Сначала `add_state`, потом `add_transition` |
| `layer="Name"` в add_state/add_transition/set_default | Там нужен числовой индекс; имя слоя работает только в Layers CRUD |
