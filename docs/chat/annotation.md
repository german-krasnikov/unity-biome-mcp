# Annotation Guide

Mark up screenshots in the Chat window to highlight issues or point out features.

## Overview

Annotations are visual overlays (lines, arrows, shapes, text) drawn on screenshots. They appear in LLM responses to help clarify communication.

**Access:** Chat window → Toolbar → Annotation tools (or keyboard shortcuts).

## Tools

| Tool | Shortcut | Purpose |
|------|----------|---------|
| Pen | P | Free-hand drawing |
| Line | L | Straight line |
| Arrow | A | Directional arrow |
| Rectangle | R | Rect outline (supports fill) |
| Ellipse | E | Oval outline (supports fill) |
| Text | T | Add text label |
| Eraser | X | Remove individual strokes |

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

**Fill mode** (Rectangle and Ellipse only): None, Solid, or Semi-transparent.

## Usage

### Pen (Free-Hand)
```
1. Click Pen button or press P
2. Click + drag to draw
3. Release to finish stroke
4. Repeat for additional strokes
```

**Use case:** Circling UI elements, marking problem areas.

### Line (Straight)
```
1. Click Line button or press L
2. Click start point
3. Click end point
4. Press Enter to confirm or Escape to cancel
```

**Use case:** Connecting related elements, pointing along axis.

### Arrow (Directional)
```
1. Click Arrow button or press A
2. Click start point (tail)
3. Click end point (head)
4. Arrow automatically orients
```

**Use case:** Show direction of movement, flow, sequence.

### Rectangle (Outline)
```
1. Click Rectangle button or press R
2. Click top-left corner
3. Drag to bottom-right
4. Release to confirm
```

**Use case:** Highlight UI regions, problem zones, hitboxes.

### Ellipse (Oval)
```
1. Click Ellipse button or press E
2. Click center
3. Drag to set radius
4. Release to confirm
```

**Use case:** Highlight circular features (heads, spawners, projectiles).

### Text (Label)
```
1. Click Text button or press T
2. Click location to place text
3. Type label (single line)
4. Press Enter to confirm
```

**Use case:** Add callouts, error names, coordinates.

**Supported:** ASCII text, numbers, basic symbols.

### Eraser
```
1. Click Eraser button or press X
2. Click on a stroke to remove it
```

**Use case:** Remove individual annotations without clearing everything.

## Example Workflow

```
Screenshot taken automatically after scene change

1. User sees issue in chat response
2. In annotation toolbar: select Pen
3. Draw circle around problematic UI element
4. Select Text
5. Type "Off by 10px"
6. Select Arrow
7. Draw from buggy element to expected position
8. Press Enter to commit annotations
9. Annotations visible in chat transcript
10. LLM sees annotated image in prompt
```

## Appearing in LLM Prompt

Annotations are rasterized (baked) into the screenshot PNG before sending. The LLM receives:

- A single **binary `image_url` block** (the composited PNG with annotations burned in)
- **Text metadata** (transcription of text labels and 3D coordinate annotations)

No vector data is sent — the LLM sees the final image with all annotations visible.

## Tips

**Clear annotations:**
- Click undo (Ctrl+Z) to remove last stroke
- Clear all: Click "Clear" button in annotation toolbar
- Revert: Press Escape before confirming

**Visibility:**
- Use contrasting colors (yellow on dark, teal on light)
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

**See also:** `docs/chat/backends.md` for backend chat features, `docs/features/session-skills.md` for storing screenshots as baselines.
