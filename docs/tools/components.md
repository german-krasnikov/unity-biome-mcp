# Event Wiring Tools

Connect and disconnect UnityEvent persistent listeners. Wire UI buttons to methods, trigger events, and manage event callbacks without manual serialization.

> **Note:** `wire_event` and `unwire_event` are registered in [Object Tools](objects.md#wire_event). This page provides extended examples and workflows.

## wire_event

See [Object Tools — wire_event](objects.md#wire_event) for parameters.

**Common Patterns:**

| Pattern | Example |
|---------|---------|
| Button → Activate/Deactivate | `wire_event(path="Button", component="Button", event="onClick", target="Panel", method="SetActive", arg_type="bool", arg_value="true")` |
| Trigger → Damage | `wire_event(path="Spike", component="Collider", event="onTriggerEnter", target="Player", method="TakeDamage", arg_type="int", arg_value="10")` |
| Input → Method Call | `wire_event(path="Canvas/Input", component="InputField", event="onEndEdit", target="Handler", method="ProcessInput", arg_type="string", arg_value="...")` |
| UI → Animation | `wire_event(path="Button", component="Button", event="onClick", target="Character", method="PlayAnimation", arg_type="string", arg_value="Attack")` |

## unwire_event

See [Object Tools — unwire_event](objects.md#unwire_event) for parameters.

---

## Workflow: Complete Event Setup

**Scenario:** Create a button that pauses the game on click.

1. **Create UI**
   ```python
   await create_ui(type="Button", name="PauseButton", anchor="top-right",
                  text="Pause", fontSize="24")
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
   await wait_until(timeout=10)  # Simulate gameplay
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
