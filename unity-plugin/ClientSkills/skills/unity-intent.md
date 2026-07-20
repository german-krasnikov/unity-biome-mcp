---
name: unity-intent
description: Natural language intent tools for Unity MCP — do, ask, animator_intent, ui_intent, vfx_intent. Use when you want to express a Unity scene change in plain language instead of writing batch DSL manually.
user-invocable: false
---

# Unity Intent Tools (NL Meta-Tools)

Natural language convenience wrappers. Each delegates to Haiku internally. **ALL are direct_only — never put in batch.**

## Tool Reference

| Tool | Category | Gating | Мутация |
|------|----------|--------|---------|
| `ask` | SYSTEM | TIER1 — всегда видим | RO |
| `do` | SYSTEM | tier2 — `discover_tools category="SYSTEM"` | RW |
| `animator_intent` | SYSTEM | tier2 — `discover_tools category="SYSTEM"` | RW |
| `ui_intent` | MEDIA | tier2 — `discover_tools category="MEDIA"` | RW |
| `vfx_intent` | MEDIA | tier2 — `discover_tools category="MEDIA"` | RW |

**direct_only** = каждый вызов отдельно. В batch НЕ работают.

## do — универсальный NL → scene mutation

SYSTEM, direct_only, tier2. Принимает произвольный NL-интент, Haiku генерирует batch DSL план.

```
# Preview без исполнения — всегда сначала
do(instruction="create a health bar above the player", dry_run=true)

# Execute
do(instruction="create a health bar above the player")

# Создать объект с компонентами
do(instruction="create capsule Player, add Rigidbody with mass=2, freeze rotation X and Z")
```

## ask — read-only scene query

SYSTEM, direct_only, TIER1 (всегда виден без discover_tools). Только чтение — попытка мутации вернёт ошибку.

```
ask(question="what components does /Player have?")
ask(question="what's the current scene hierarchy under /UI?")
ask(question="are there any disabled objects?")
ask(question="what lights are in the scene and their settings?")
```

## animator_intent — NL → Animator

SYSTEM, direct_only, tier2. Генерирует DSL: PARAM/STATE/DEFAULT/TRANS → batch команды `animator`.

```
# Нужен discover_tools сначала
discover_tools(category="SYSTEM")

animator_intent(
  target="/Character",
  intent="idle walk run states, Speed float param, transitions at 0.1 and 3.0 thresholds"
)

animator_intent(target="/CTA_Button", intent="pulse animation: Idle and Pulse states, trigger DoPulse", dry_run=true)
```

## ui_intent — NL → UI hierarchy

MEDIA, direct_only, tier2. Генерирует UI DSL → `create_ui` + `set_rect` batch.

```
discover_tools(category="MEDIA")

ui_intent(instruction="create a settings menu with volume slider and back button")

# Готовый template (без Haiku, мгновенно)
ui_intent(intent="", template="hud")
ui_intent(intent="", template="menu")   # templates: hud|menu|dialog|grid
```

## vfx_intent — NL → VFX/Particles

MEDIA, direct_only, tier2. Генерирует VFX DSL → `particle` batch. Есть встроенные пресеты.

```
discover_tools(category="MEDIA")

# Пресет (без Haiku, мгновенно)
vfx_intent(target="/VFX/Explosion", intent="fire_explosion")  # пресеты: fire_explosion|magic_burst|dissolve|glow_outline|smoke_trail

# NL intent
vfx_intent(
  target="/Torch/ParticleEffect",
  instruction="add fire particle effect",
  kind="particle"
)
```

## Intent vs точные тулы

**Intent** — быстро, NL convenience, не для точности:
- "создай 3 куба в ряд с красным материалом"
- "что за компоненты на Player?"

**Точные тулы** — когда нужен полный контроль:
- `set_property` с конкретным значением
- `animator action="add_transition"` с точным exit_time/duration
- `batch` с 10+ командами

## Anti-patterns

```
# WRONG: intent tools в batch
batch(commands="""
do instruction="create cube"
do instruction="create sphere"
""")

# RIGHT: отдельные вызовы (direct_only)
do(instruction="create cube")
do(instruction="create sphere")

# WRONG: do для точных значений
do(instruction="set health to 100")

# RIGHT: set_property для точности
set_property(path="/Player", component="Health", property="currentHp", value=100)

# WRONG: skip dry_run для сложных задач
do(instruction="set up 3 lights, animator, UI panel and VFX")

# RIGHT: сначала preview
do(instruction="set up 3 lights, animator, UI panel and VFX", dry_run=true)
```

## Verification (обязательно после мутации)

```
do(instruction="create enemy spawner at center")
get_hierarchy(depth=2)          # объекты созданы?
get_component(path="/EnemySpawner", type="EnemySpawner")  # настроен?
screenshot()                    # визуально OK?
```
