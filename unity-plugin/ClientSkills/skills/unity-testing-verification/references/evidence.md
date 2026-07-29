# Evidence Policy

## Report Format

```text
CLAIM: The action changed the counter by exactly one.
EVIDENCE:
  /Subject Counter.Value: 4 -> 5
  assertion: INCREASED_BY == 1
  console delta: clean
VERDICT: CONFIRMED
```

Use `NOT CONFIRMED` when evidence is missing, ambiguous, summarized away, or
contradictory.

## Evidence Hierarchy

| Claim | Evidence |
|---|---|
| Value, count, state, reference | Exact queried data |
| Transition over time | Before/after capture plus bounded wait/assertion |
| No new errors | Console watermark delta |
| Test behavior | Exact assertion output |
| Visibility, spacing, clipping | Screenshot at named viewport |
| Motion or stability | Multiple frames plus behavioral data when semantics matter |

## Rules

- Never write “it looks like it works” for gameplay state.
- Keep exact failing step text and values.
- If visual and data evidence conflict, report the conflict; data governs
  behavioral claims.
- One changed frame is not proof of continuous animation.
- A clean screenshot is not proof of a clean console.
- Each suite case must establish its own baseline or explicitly use
  `restart_between=True`.
- Restore time scale and stop Play Mode after failure.
