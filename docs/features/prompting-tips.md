# Prompting Tips

A useful request states the outcome, scope, constraints, and evidence of success.
You do not need to name every tool; name one only when its behavior matters.

## Describe the outcome

Start with what should be true when the task is complete:

> Add a Rigidbody to `/Player`, set its mass to 2, and confirm the serialized
> value. Do not change any other component.

Exact scene paths, asset paths, component names, and field names remove ambiguity.
If you do not know them, ask the assistant to inspect first:

> Find the active player object and show me the matching paths before changing
> anything.

## Bound the change

State constraints that affect implementation or safety:

- whether Edit Mode or Play Mode state should change;
- the scene or hierarchy root in scope;
- assets that may or may not be edited;
- whether prefab instances, source prefab assets, or shared materials may change;
- whether a destructive action should be previewed first.

For example:

> Under `/Level/Enemies` only, set `Health.maxHealth` to 100. Keep current health
> unchanged. Preview the affected paths, apply the update, then read the values
> back.

## Define acceptance evidence

Ask for evidence appropriate to the result:

- a component or asset read for serialized authoring;
- compile and Console checks for code changes;
- a Playtest assertion for runtime behavior;
- a correlated terminal NUnit result for Unity tests;
- a screenshot for visible layout or rendering.

Avoid treating “the command returned successfully” as sufficient evidence when
the actual requirement is behavioral or visual.

## Separate exploration from mutation

For a broad request, ask for a preview or plan first:

> Inspect the current HUD and propose the smallest change that adds a pause
> button. Do not modify the scene yet.

After reviewing it, authorize the exact change and its verification. This is
especially useful for prefabs, project settings, shared materials, packages, and
multi-object edits.

## Give runtime tests observable conditions

Replace vague requests such as “make sure combat works” with initial state,
action, and expected result:

> In Play Mode, capture `/Enemy|Health|currentHealth`, invoke
> `Health.TakeDamage(25)` on `/Enemy`, assert that health decreased, and assert
> that the Console is clean.

Use the [Playtest Guide](playtest.md) when you need a reusable scenario.

## Ask for concise reporting

For large tasks, specify what the final report should contain:

> Report changed files, verification commands and results, and any remaining
> uncertainty. Do not repeat unchanged schema parameters.

See the [Tool Decision Guide](tool-guide.md) for choosing between typed tools,
`batch`, `inspect`, and intent tools.
