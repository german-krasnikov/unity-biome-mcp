# Event Wiring Tools

Connect and disconnect UnityEvent persistent listeners. Wire UI buttons to methods, trigger events, and manage event callbacks without manual serialization.

## list_events

List all UnityEvent persistent listeners on a component.

**Parameters:**
- `path` (string) — Scene path to GameObject
- `component` (string) — Component type (e.g., "Button", "Toggle", "MyScript")
- `event` (string) — Event field name (e.g., "onClick", "onValueChanged")

**Output:** Array of persistent listeners with target GameObject, method name, and parameter details.

**Example:**

```python
# List Button click listeners
listeners = await list_events(path="Canvas/PlayButton", component="Button", event="onClick")

# List Toggle value change listeners
listeners = await list_events(path="Canvas/SoundToggle", component="Toggle", event="onValueChanged")

# Check custom script events
listeners = await list_events(path="GameManager", component="GameManager", event="onGameStart")
```

---

## wire_event

Connect a button, trigger, or other event to a method.

**Parameters:**
- `path` (string) — Object with the event
- `component` (string) — Component type owning the event field
- `event` (string) — Serialized field name (e.g., "onClick", "_onComplete", "onTriggerEnter")
- `target` (string) — Target scene path or asset path
- `method` (string) — Method name (e.g., "SetActive", "Play", "TakeDamage")
- `arg_type` (string, default="void") — "void" | "bool" | "int" | "float" | "string" | "object"
- `arg_value` (string, optional) — Required when arg_type != void
- `target_component_type` (string, optional) — Disambiguate when target has multiple components with the same method name
- `parameter_types` (string, optional) — Specify parameter types when method is overloaded (e.g., "string" or "int,float")

**Example:**

```python
# Connect button click
await wire_event(path="UI/StartButton", component="Button", event="onClick", 
                target="GameManager", method="StartGame")

# Connect trigger with damage argument
await wire_event(path="Spike", component="Collider", event="onTriggerEnter",
                target="Player", method="TakeDamage", arg_type="int", arg_value="10")

# Connect UI event with string argument
await wire_event(path="UI/QuitButton", component="Button", event="onClick",
                target="GameManager", method="QuitGame")

# Disambiguate when target has multiple components with same method
# (e.g., both Animator and NavMeshAgent have SetDestination)
await wire_event(path="Button", component="Button", event="onClick",
                target="Character", method="SetDestination",
                arg_type="object", target_component_type="NavMeshAgent")

# Disambiguate overloaded method by parameter type
# (e.g., Animator.SetTrigger has multiple overloads)
await wire_event(path="Button", component="Button", event="onClick",
                target="Character", method="SetTrigger",
                arg_type="string", arg_value="Attack",
                target_component_type="Animator", parameter_types="string")
```

**Common Patterns:**

| Pattern | Example |
|---------|---------|
| Button → Activate/Deactivate | `wire_event(path="Button", component="Button", event="onClick", target="Panel", method="SetActive", arg_type="bool", arg_value="true")` |
| Trigger → Damage | `wire_event(path="Spike", component="Collider", event="onTriggerEnter", target="Player", method="TakeDamage", arg_type="int", arg_value="10")` |
| Input → Method Call | `wire_event(path="Canvas/Input", component="InputField", event="onEndEdit", target="Handler", method="ProcessInput", arg_type="string", arg_value="...")` |
| UI → Animation | `wire_event(path="Button", component="Button", event="onClick", target="Character", method="PlayAnimation", arg_type="string", arg_value="Attack")` |

## unwire_event

Disconnect an event listener from a UnityEvent.

**Parameters:**
- `path` (string) — Event source GameObject
- `component` (string) — Component type owning the event field
- `event` (string) — Serialized field name (e.g., "onClick")
- `index` (int, optional) — Remove specific entry (0-based). Omit to clear all.

**Example:**

```python
# Clear all listeners on onClick
await unwire_event(path="UI/Button", component="Button", event="onClick")

# Remove specific listener at index 0
await unwire_event(path="UI/Button", component="Button", event="onClick", index=0)
```

---

## Workflow: Complete Event Setup

**Scenario:** Create a button that pauses the game on click.

1. **Create UI**
   ```python
   await create_ui(type="Button", name="PauseButton", anchor="top-right",
                  text="Pause", font_size="24")
   ```

2. **Wire to game controller**
   ```python
   await wire_event(path="Canvas/PauseButton", component="Button",
                   event="onClick", target="GameController",
                   method="TogglePause")
   ```

3. **Verify connection**
   ```python
   button = await get_component(path="Canvas/PauseButton", type="Button")
   print(button)  # Should show onClick listener
   ```

4. **Test in play mode**
   ```python
   await editor("play")
   await run_playtest(script="CLICK /Canvas/PauseButton")
   await wait_until(path="GameController", component="GameController",
                    field="IsPaused", value="true", timeout=10)
   await editor("stop")
   ```

5. **If needed, clear listeners**
   ```python
   await unwire_event(path="Canvas/PauseButton", component="Button",
                     event="onClick")
   ```

---

## Advanced Patterns

### Multi-Listener Event Chain
```python
# Wire multiple listeners to same event
await wire_event(path="Button", component="Button", event="onClick",
                target="AudioManager", method="PlaySound", 
                arg_type="string", arg_value="click")

await wire_event(path="Button", component="Button", event="onClick",
                target="UI", method="ShowMessage",
                arg_type="string", arg_value="Button pressed!")

await wire_event(path="Button", component="Button", event="onClick",
                target="Analytics", method="LogEvent",
                arg_type="string", arg_value="button_clicked")
```

### Conditional Wiring Based on State
```python
# Check if already wired
comp = await get_component(path="Button", type="Button")
if "onClick" not in comp:
    await wire_event(path="Button", component="Button", event="onClick",
                    target="Handler", method="OnClick")
```

### Toggle Active State
```python
# Button activates panel
await wire_event(path="OpenButton", component="Button", event="onClick",
                target="MenuPanel", method="SetActive",
                arg_type="bool", arg_value="true")

# Close button deactivates
await wire_event(path="CloseButton", component="Button", event="onClick",
                target="MenuPanel", method="SetActive",
                arg_type="bool", arg_value="false")
```

---

## Common Errors & Solutions

| Error | Cause | Solution |
|-------|-------|----------|
| "Method not found" | Typo or wrong component | Verify method name and component type match |
| "Target not found" | Wrong path | Use `search_scene()` to find target path |
| "Listener not added" | Event field name wrong | Check serialized field name (e.g., onClick, onValueChanged) |
| Multiple listeners | Wired same event twice | Use `unwire_event()` to clear first |

---

**See also:** [Objects Tools](objects.md) for component management, [UI Tools](ui.md) for creating UI elements, [Scene Tools](scene.md) for editor control.
