# Annotation Guide

Mark up screenshots in the Chat window to highlight issues or point out features.

## Overview

Annotations are visual overlays (lines, arrows, shapes, text) drawn on screenshots and attached to the next Chat request.

**Access:** Capture or attach an image in MCP Chat, then open the annotation editor.

## Tools

| Tool | Shortcut | Purpose |
|------|----------|---------|
| Pen | P | Free-hand drawing |
| Line | L | Straight line |
| Arrow | A | Directional arrow |
| Rectangle | R | Rect outline (supports fill) |
| Ellipse | E | Oval outline |
| Text | T | Add text label |
| Eraser | X | Paint a transparent erase stroke |

## Color Palette

A single **active color** applies to all tools. Pick from the 8-color palette in the second toolbar row:

| Swatch | Color | RGB |
|--------|-------|-----|
| 🔴 | Red (default) | `(255, 50, 50)` |
| 🔵 | Blue | `(50, 150, 255)` |
| 🟢 | Green | `(50, 200, 50)` |
| 🟡 | Yellow | `(255, 200, 0)` |
| 🟠 | Orange | `(255, 130, 0)` |
| 🟣 | Purple | `(200, 50, 255)` |
| ⚪ | White | `(255, 255, 255)` |
| ⚫ | Black | `(0, 0, 0)` |

**Stroke width** presets: Thin (2px), Medium (3px, default), Thick (5px).

**Fill mode** (Rectangle only): None, Solid, or Semi-transparent.

## Usage

### Pen (Free-Hand)

1. Select **Pen** or press **P**.
2. Drag to draw.
3. Release to finish the stroke.
4. Repeat for additional strokes.

**Use case:** Circling UI elements, marking problem areas.

### Line (Straight)

1. Select **Line** or press **L**.
2. Drag from the start point to the end point.
3. Release to confirm.

**Use case:** Connecting related elements, pointing along axis.

### Arrow (Directional)

1. Select **Arrow** or press **A**.
2. Drag from the tail to the head.
3. Release to confirm.

**Use case:** Show direction of movement, flow, sequence.

### Rectangle (Outline)

1. Select **Rectangle** or press **R**.
2. Start at one corner of the target area.
3. Drag to the opposite corner.
4. Release to confirm.

**Use case:** Highlight UI regions, problem zones, hitboxes.

### Ellipse (Oval)

1. Select **Ellipse** or press **E**.
2. Start at the center.
3. Drag to set the radius.
4. Release to confirm.

**Use case:** Highlight circular features (heads, spawners, projectiles).

### Text (Label)

1. Select **Text** or press **T**.
2. Select the label position.
3. Type a single-line label.
4. Press **Enter** to confirm.

**Use case:** Add callouts, error names, coordinates.

Text labels are single-line and are also included in the annotation metadata.

### Eraser

1. Select **Eraser** or press **X**.
2. Drag across marks to erase them.

**Use case:** Remove part of a mark without clearing the canvas.

## Example Workflow

1. Capture or attach a screenshot in MCP Chat.
2. Open the annotation editor and select **Pen**.
3. Circle the problematic UI element.
4. Select **Text** and enter a short label such as `Off by 10px`.
5. Select **Arrow** and point from the current position to the expected position.
6. Select **Send to Chat** or press **Ctrl+Enter** / **Cmd+Enter**.
7. Confirm that the annotated image appears as a context chip.
8. Send the Chat request.

## Appearing in LLM Prompt

Annotations are rasterized into a composited PNG before sending. The request
receives:

- the composited image
- text metadata with camera context and visible objects
- 3D raycast results and text-label metadata when **Show 3D coordinates** is enabled

No vector command data is sent.

## Tips

**Clear annotations:**
- Click undo (Ctrl+Z) to remove the last annotation command
- Clear all: Click "Clear" button in annotation toolbar
- While entering text, press Escape to cancel that label

**Visibility:**
- Use contrasting colors (yellow on dark, blue on light)
- Thin lines for precision; thicker for UI elements
- Multiple arrows for flow sequences

**Efficiency:**
- Combine shapes (rectangle outline + text label inside)
- Use arrows to connect related annotations
- Keep text labels short and specific

## Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| P | Activate Pen |
| L | Activate Line |
| A | Activate Arrow |
| R | Activate Rectangle |
| E | Activate Ellipse |
| T | Activate Text |
| X | Activate Eraser |
| Ctrl+Z / Cmd+Z | Undo |
| Ctrl+Shift+Z / Ctrl+Y / Cmd+Y | Redo |
| Ctrl+Enter / Cmd+Enter | Send to Chat |

## Limitations

- Annotations are **rasterized** into the screenshot (not stored as separate vector data)
- Text is **single-line** (no multi-line labels)
- Coordinates are **normalized 0..1** (resolution-independent, relative to viewport)

---

**See also:** [Using MCP Chat](index.md) for the full Chat workflow and [Skills and Templates](../features/session-skills.md) for storing screenshots as baselines.
