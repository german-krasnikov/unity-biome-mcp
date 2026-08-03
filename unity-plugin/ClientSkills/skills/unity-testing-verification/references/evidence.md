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

## Durable Test Evidence

A Unity NUnit verdict names the exact `request_id`, `run_id`, and `utf_guid` and
comes from a reconciled terminal snapshot. Exact expected leaves must equal
unique terminal leaves, including after domain reload. A structured initializing
response, TCP disconnect, port change, partial UTF root aggregate, or caller
timeout is progress/transport evidence, not a new run and not a verdict.
A correlated `state=prepared` intent may be continued once with its identical
request payload and already assigned `run_id`; after dispatch, another
`run_tests` call is a protocol violation.

For reload acceptance, retain the expected control phase, port and observer
generation history, one `Passed` attempt per exact leaf, and proof that the
control record was archived. For Python live acceptance, report deterministic
results separately from `live_cli`; paid external tests are opt-in with
`UNITY_MCP_RUN_LIVE_CLI=1` and their quota/authentication state is a separate
dependency.

Release evidence preserves the strict sequential order: repository Python unit,
server unit excluding `live`, two back-to-back complete durable C# EditMode
runs plus fault/reload acceptance, final-port rediscovery, then deterministic
Python live. Record each command, count, duration, identity, port transition,
and paid-lane skip; a focused pass cannot substitute for a release gate.
