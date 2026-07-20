# Unity Scene & UI Management (MCP)

Управление сценами, UI, материалами, анимациями через MCP.

## Scene Management

```
scene action="new"                                          # ОСТОРОЖНО — удаляет текущую!
scene action="open" path="Assets/Scenes/Main.unity"
scene action="save"
scene action="save" path="Assets/Scenes/Level2.unity"       # save as
scene action="discard"                                       # отменить несохранённые
scene action="open_additive" path="Assets/Scenes/Env.unity" # не закрывает текущую
scene action="list"                                          # загруженные сцены
scene action="close" path="Assets/Scenes/Env.unity"         # закрыть (multi-scene)
scene action="set_active" path="Assets/Scenes/Main.unity"   # куда новые объекты
```

**ВАЖНО:** `new` и `discard` — деструктивные. Проверь что сцена сохранена.

## Scene Environment

**Gated:** `discover_tools category="SCENE"`.

```
scene_environment action="get"
scene_environment action="set" prop="ambientMode" value="Flat"
scene_environment action="set" prop="ambientLight" value="#404040"
scene_environment action="set" prop="fog" value="true"
scene_environment action="set" prop="fogColor" value="#C8C8C8"
scene_environment action="set" prop="fogDensity" value="0.02"
scene_environment action="set" prop="reflectionIntensity" value="0.5"
```

Props: `ambientMode|ambientLight|ambientIntensity|ambientSkyColor|ambientEquatorColor|ambientGroundColor|fog|fogColor|fogMode|fogDensity|fogStartDistance|fogEndDistance|reflectionIntensity|reflectionBounces|subtractiveShadowColor|defaultReflectionResolution`.

## Menu

**Gated:** `discover_tools category="SYSTEM"`.

```
menu action="execute" path="GameObject/Create Empty"
menu action="list"                     # все корневые
menu action="list" path="GameObject"   # подпункты
```

**Note:** `Edit/` menu items не поддерживаются Unity API.

## Creating UI Elements

**Gated:** `create_ui`, `set_rect` require `discover_tools category="MEDIA"`.

```
create_ui type="Canvas" name="MainCanvas"
create_ui type="Panel" name="MenuPanel" parent="/MainCanvas" color="#00000080"
create_ui type="Button" name="PlayBtn" parent="/MainCanvas/MenuPanel" text="PLAY" font_size=24
create_ui type="Text" name="ScoreLabel" parent="/MainCanvas" text="Score: 0" font_size=32 color="#FFFFFF"
create_ui type="Image" name="Background" parent="/MainCanvas" color="#333333FF"
create_ui type="Text" name="Label" parent="/MainCanvas" text="Hello" anchor="top-left" pivot="(0,1)"
```

**Auto-Canvas:** без parent или нет Canvas — создаётся автоматически.

**Atomic rollback (v0.92):** `create_ui` uses try/catch + DestroyImmediate on failure — partially created objects are always cleaned up. No orphaned GameObjects on error.

**TMP fallback (v0.93):** `create_ui type=Text` — if TextMeshPro fails to initialize (package missing, font issue), falls back to legacy `UnityEngine.UI.Text` silently. No error returned.

Anchor presets: `center|stretch|top-left|top-right|bottom-left|bottom-right|top-center|bottom-center|middle-left|middle-right`.

## RectTransform (set_rect)

```
set_rect path="/HUD/Panel" anchor="stretch"
set_rect path="/HUD/Label" pos="(100, 200)" size="(300, 50)" pivot="(0.5, 0.5)"
set_rect path="/HUD/Panel" anchor="stretch" offset_min="10,10" offset_max="-10,-10"
```

## Materials

**Gated:** `set_material` requires `discover_tools category="SCENE"`.

```
set_material path="/MyCube" color="#FF0000"
set_material path="/MyCube" color="#00FF0080"  # с alpha
set_material path="/MyCube" color="#FFFFFF" shader="Universal Render Pipeline/Lit"
```

## Animation (MCP tool)

**Gated:** `discover_tools category="MEDIA"`.

action: `get|create|edit|add_key|remove_key|remove_curve|set_keys|set_loop|set_wrap|set_framerate|preview|get_clip_path|get_events|add_event|remove_event`.

```
animation action="get" path="/Character"
animation action="create" path="/Door" clip_name="DoorOpen" property="localEulerAngles.y" keys="t:0 v:0; t:1 v:90"
animation action="edit" path="/Door" clip="DoorOpen" property="localEulerAngles.y" keys="t:0 v:0; t:0.5 v:45; t:1 v:90"
animation action="set_keys" path="/Door" clip="DoorOpen" property="localPosition" keys="t:0 v:(0,0,0); t:1 v:(0,2,0)"
animation action="set_loop" path="/Door" clip="DoorOpen" keys="true"
animation action="preview" path="/Door" clip="DoorOpen" time=0.5
animation action="add_event" path="/Char" clip="Attack" time=0.3 function_name="OnHit" int_param="10"
```

`edit` и `add_key` — синонимы (добавляют/апдейтят keyframes). `set_keys` — заменяет всю кривую целиком.

Full params: `.claude/skills/unity-animation.md`.

## Timeline (MCP tool)

**Gated:** `discover_tools category="MEDIA"`.

action: `get|create|add_track|remove_track|add_clip|remove_clip|set_binding|set_timing|mute|unmute|lock|unlock|rename_track|reorder_track|duplicate_clip|add_marker|remove_marker|set_track_offset|set_duration|add_sub_track|set_clip_in|get_bindings|preview`.

```
timeline action="get" path="/Director"
timeline action="add_track" path="/Director" track="MoveTrack" track_type="Animation" binding="/Character"
timeline action="remove_track" path="/Director" track="MoveTrack"
timeline action="add_clip" path="/Director" track="MoveTrack" clip="RunClip" start=0.0 duration=2.5
timeline action="remove_clip" path="/Director" track="MoveTrack" clip="RunClip"
timeline action="set_binding" path="/Director" track="MoveTrack" binding="/Character"
timeline action="set_timing" path="/Director" track="MoveTrack" clip="RunClip" blend_in=0.2 blend_out=0.3
timeline action="mute" path="/Director" track="MoveTrack"
timeline action="unmute" path="/Director" track="MoveTrack"
timeline action="lock" path="/Director" track="MoveTrack"
timeline action="unlock" path="/Director" track="MoveTrack"
timeline action="rename_track" path="/Director" track="MoveTrack" name="RunTrack"
timeline action="reorder_track" path="/Director" track="MoveTrack" index=0
timeline action="duplicate_clip" path="/Director" track="MoveTrack" clip="RunClip" offset=2.5
timeline action="add_marker" path="/Director" track="MoveTrack" start=1.0 name="Hit"
timeline action="remove_marker" path="/Director" track="MoveTrack" name="Hit"
timeline action="set_track_offset" path="/Director" track="MoveTrack" value="scene"
timeline action="set_duration" path="/Director" duration=10.0
timeline action="add_sub_track" path="/Director" track="Group1" track_type="Animation" name="ChildTrack"
timeline action="set_clip_in" path="/Director" track="MoveTrack" clip="RunClip" clip_in=0.5
timeline action="get_bindings" path="/Director"
timeline action="preview" path="/Director" time=2.5
timeline action="create" path="/Director" asset_path="Assets/Timelines/Cutscene.playable"
```

track_type: `Animation|Audio|Activation|Control|Signal|Group`.

Full params: `.claude/skills/unity-timeline.md`.

## UI Intent (direct_only — NEVER in batch)

**Gated:** `discover_tools category="MEDIA"`.

```
ui_intent instruction="create a pause menu with Resume and Quit buttons"
ui_intent instruction="add health bar at top-left corner"
```

## Validate Layout

**Gated:** `discover_tools category="MEDIA"`. Read-only.

```
validate_layout path="/Canvas/MenuPanel"    # check RectTransform/anchors/overlaps
```

## Batch UI Example

```
batch commands="
create_ui type=\"Canvas\" name=\"GameUI\"
create_ui type=\"Panel\" name=\"TopBar\" parent=\"/GameUI\" anchor=\"top-center\" size=\"(0,60)\"
set_rect path=\"/GameUI/TopBar\" anchor=\"stretch\" offset_min=\"0,-60\" offset_max=\"0,0\"
create_ui type=\"Text\" name=\"Title\" parent=\"/GameUI/TopBar\" text=\"My Game\" font_size=24 anchor=\"center\" color=\"#FFFFFF\"
"
```
