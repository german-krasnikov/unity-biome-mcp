# Using MCP Chat

MCP Chat runs a supported CLI backend inside the Unity Editor and streams text, tool calls, approvals, and results into one transcript.

## Before You Start

1. Install the Unity package through [Getting Started](../getting-started/index.md#1-install-the-unity-package).
2. Install and sign in to at least one supported CLI.
3. Open **MCP > Settings > Chat Settings** and confirm that Unity finds the CLI binary.
4. Open **MCP > Chat**.

The first relay start can take longer while `uvx` prepares the Python package. The footer shows relay startup status without blocking the Unity UI.

## Run a Task

1. Choose a backend from the first footer selector.
2. Choose a model from the second selector. Some backends also allow a custom model ID.
3. Select **Ask** or **Agent**.
4. Enter a focused request, for example:

   ```text
   Inspect the active scene, add a directional light if one is missing,
   and report any compile errors.
   ```

   To delegate work to a subagent, mention it by name: type `@` and select from
   the available subagents (loaded from `.claude/agents/` files), or type the name
   directly as `[agent:name]`.

5. Select **Send** or press Enter. Use Alt+Enter to insert a newline.
6. Review tool cards showing code changes, mutations, tasks, or agent delegation;
   the final response appears below the cards.

Changing the backend or model stops the current process, prepares the new
selection, and resets the token counters. The next **Send** starts it.

## Ask and Agent Modes

**Ask** is the default. Permission requests appear as cards with four choices:

- **Allow** approves this request.
- **Deny** rejects this request.
- **Session** auto-approves the same tool for the current Chat session.
- **Always** persists approval for that tool in Editor preferences.

After an Ask turn produces tool calls and a session ID, **Approve & Execute** requests continuation in Agent mode. This preserves Claude's stdin-driven session; per-turn backends are subject to the [current relay resume limitation](backends.md#sessions).

**Agent** auto-approves permission prompts emitted by the backend. Permission behavior differs by CLI, and some backends run with their own non-interactive flags. Agent mode does not override tools disabled on the server. Review [Settings](../settings.md#tools-and-permissions) before using it.

## Stop, Restore, and Retry

- Select **Stop**, or press Escape while a turn is running, to stop the backend and return Chat to idle.
- A stopped or failed turn is not automatically replayed. Edit or resend the request to retry it.
- After a completed or failed turn, use **Restore** to revert its Unity Undo group. Restoring an earlier turn also reverts every later tracked turn.
- If a backend process exits unexpectedly, Chat marks the turn as failed and recreates the backend for the next request.

Restore covers changes recorded through Unity Undo. It does not promise to reverse external file operations or other work that bypasses Unity Undo, and tracked groups are invalidated by an assembly reload.

## Sessions

Open the footer session menu:

- **New Session** clears the transcript, input, pending state, session-only approvals, and token counters.
- **Resume CLI Session** opens a session picker when the selected backend has a supported local session store.
- **To CLI** copies a resume command after the backend has returned a session ID.
- **Attach Image** adds an image to the next request.

Session availability and resume reliability differ by backend. Only Claude
currently has a reliable in-Chat resume path; see
[Chat Backends](backends.md#sessions) for the canonical matrix and limitation.

## Reload Recovery

Chat saves the transcript when the window closes and before an assembly reload. If a turn is active during a domain reload, it stores the pending text, context chips, mode, backend, session, and undo state. After Unity compiles cleanly, Chat attempts to restore and resend that turn through a bounded retry loop.

If recovery does not complete:

1. Wait for Unity compilation to finish.
2. Check the transcript for an error chip.
3. Use **MCP > Status > Diagnose** if the server is not healthy.
4. Resend the request, or start a **New Session** if the saved backend session is no longer valid.

## Add Visual Context and Mention Agents

Add focused context before sending:

- Type `@` to find scene objects, project assets, or subagents. Mentioning an
  agent tells the current model to delegate work to that subagent.
- Drag a Hierarchy object, Project asset, or supported external file into Chat.
- Paste an image, use **Attach Image**, or capture a Unity view from the toolbar.
- Select a scene region or annotate a screenshot; both become context chips.
- Type `/` to use the built-in `/fix-compile`, `/add-component`, `/playtest`,
  `/inspect`, and `/screenshot` prompt templates.

Context chips and agent mentions are sent with the next turn. Remove them when
they are not relevant to keep the request focused.

See [Screenshot Annotation](annotation.md) for the annotation workflow.

## Troubleshooting

| Symptom | Action |
|---|---|
| CLI binary is not found | Make the CLI available on the login-shell `PATH`, then restart Unity |
| Authentication fails | Sign in with the selected CLI outside Unity, then reopen Chat |
| First turn waits at relay startup | Keep Unity open; the initial `uvx` preparation can take longer |
| A tool is unavailable | Enable it under **MCP > Settings > Tools**, then start a **New Session** |
| A turn is silent for too long | Increase the inactivity timeout in Chat Settings; Codex enforces a higher minimum |
| Chat stopped after compilation | Wait for a clean compile, then resend or resume the CLI session |

For backend-specific process and authentication behavior, see [Chat Backends](backends.md).
