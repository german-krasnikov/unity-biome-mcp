# Transactions And Verification

Verification is selected by risk. A screenshot proves appearance, not serialized
or runtime values.

## Mutation Workflow

1. `console_mark()` and retain the exact returned token.
2. Resolve all target paths.
3. For a risky scene edit, use `scene_change_plan(goal=..., targets=...)` to
   perform compile/target preflight and create a checkpoint.
4. Execute compatible mutations with `batch(..., on_error="stop", atomic=True)`
   or use a specialized aggregate tool.
5. Read back changed properties.
6. Run `validate_references(path="<changed root>")` when references changed.
7. Run `get_console_since(mark_id="<exact token>")`.
8. Save only after the evidence is clean.

`scene_change_plan` performs preflight and creates a checkpoint; it does not
apply a mutation. `apply_scene_change` runs its synchronous batch with
`atomic=True` and `on_error="stop"`. A batch failure stops verification and
saving. With `verify=True`, broken references, console errors, or an
unavailable verification result also prevent saving. With `verify=False`, an
explicitly requested save may follow a successful batch.

Atomic batch rollback covers compatible Unity scene commands. It cannot undo
external filesystem or process side effects caused by commands such as
`execute_code`; keep those operations outside the transaction.

## `verify_after_change`

The gates are additive:

1. wait for compilation, always;
2. read compile errors, always;
3. check console errors, only with `mark_id`;
4. run NUnit, only with `run_tests_mode`;
5. run a playtest suite, only with `playtests`.

It does not validate object references, scan the scene, or take a screenshot.
Add those checks explicitly when the acceptance criteria require them.

```text
verify_after_change(
  mark_id="<console token>",
  run_tests_mode="EditMode",
  test_filter="FeatureTests",
  timeout=180
)
validate_references(path="/FeatureRoot")
screenshot()
```

## Code Changes

```text
compile_preflight(
  file_path="Assets/Scripts/FeatureController.cs",
  new_content="<complete proposed file>"
)
```

Write only after preflight succeeds. Then call `sync_unity(timeout=60)` once;
it triggers the Unity refresh and waits for compilation, domain reload, and
reconnection. Use `await_compile` only when another action already started
compilation and you only need to wait for its result.

## Evidence Levels

| Claim | Required evidence |
|---|---|
| Serialized value changed | Read the exact component field |
| References remain valid | `validate_references` on the changed root |
| No new runtime errors | Console watermark delta |
| Code compiles and is live | Clean terminal `sync_unity` result from the new domain |
| Behavior works | Data assertion or NUnit/playtest result |
| Layout looks correct | Screenshot or visual comparison |
