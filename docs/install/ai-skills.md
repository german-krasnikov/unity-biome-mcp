# Install AI Skills and Agents

Unity Biome MCP ships optional project-local guidance for Claude Code and
Codex. It teaches the clients how to select MCP tools, batch compatible work,
author Unity content, and verify results. Installing this guidance is separate
from configuring the MCP connection.

The package currently includes:

- 12 domain skills for MCP operations, scenes, assets and prefabs, materials
  and shaders, uGUI, UI Toolkit, animation, VFX, physics, C# editing, testing,
  and diagnostics
- 4 focused agents: `unity-scene-editor`, `unity-csharp-developer`,
  `playmode-tester`, and `unity-diagnostics`
- 1 Claude-to-Codex conversion script

## Install

1. Open the Unity project that should receive the guidance.
2. Select **MCP > Install AI Skills**.
3. Leave **Overwrite existing files** disabled for the first installation.
4. Enable **Run Codex sync after install** when this project uses Codex. This
   optional step requires Python 3.10 or newer on the `PATH` visible to Unity
   (`python3` on macOS/Linux or `python` on Windows).
5. Select **Install**, review the log, then select **Finish**.
6. Start the client from the Unity project. Codex normally detects skill
   changes automatically; restart either client if the new guidance is not
   listed.

The Setup Wizard can open the same installation step. The installer writes only
inside the current Unity project. Keep that Wizard page open while Codex sync is
running. Leaving or closing it does not kill the sync process, but the page
cannot finalize the installed-version marker; reopen it and run **Install**
again after the process finishes.

## Installed Paths

| Consumer | Project-local artifacts |
|---|---|
| Claude Code | `.claude/skills/<skill>/`, `.claude/agents/<agent>.md` |
| Codex conversion script | `.codex/scripts/claude_to_codex.py` |
| Codex generated skills | `.agents/skills/<skill>/` |
| Codex generated agents | `.codex/agents/<agent>.toml` |
| Codex ownership record | `.codex/.claude-to-codex-manifest.json` |

Claude artifacts are the converter's canonical source for Codex generation.
Supporting references are copied with their skill, so the folder layout must
remain intact.

Codex discovers repository skills from `.agents/skills/`. You can invoke one
explicitly with `/skills` or `$<skill-name>`. See the
[official Codex skills guide](https://developers.openai.com/codex/skills) for
client behavior; use this page for Unity Biome MCP's install and ownership
rules.

## Safe Updates

Re-run the installer after updating the Unity package.

- Identical files are left unchanged.
- Unmodified artifacts from supported earlier releases are migrated
  automatically.
- A different current destination is reported as a conflict unless
  **Overwrite existing files** is enabled.
- A modified legacy file always requires manual review; overwrite does not
  silently discard it.
- Codex sync refuses to replace an unowned same-name target. The Unity
  overwrite toggle does not bypass Codex ownership checks.
- Invalid ownership data, unsafe paths, and symlinked managed directories stop
  the operation before files are changed.
- The Claude install and optional Codex sync are separate transactions. A
  failure handled and reported by either stage rolls back that stage's writes.
- The installed-version marker is written only after the requested Claude
  installation and Codex sync both succeed.

If Codex sync fails after the Claude stage succeeds, the updated Claude files
remain installed, the version marker remains absent, and **Finish** stays
disabled. Resolve the reported Codex conflict or environment error, then rerun
the installer; unchanged Claude files are skipped and Codex sync is attempted
again.

When a conflict is intentional, compare the existing project file with the
packaged replacement before enabling overwrite. Keep any project-specific
instructions in a separately named skill or agent when possible.

## Run Codex Sync Manually

From the Unity project root:

```bash
python3 .codex/scripts/claude_to_codex.py --repo-root . --prune
python3 .codex/scripts/claude_to_codex.py --repo-root . --check
```

On Windows, use `python` when `python3` is not available.

`--prune` removes only unchanged artifacts previously owned by the converter or
recognized unmodified artifacts from a supported release. It preserves
unmanaged and modified files as explicit conflicts. `--check` verifies that the
generated artifacts and ownership record match the installed Claude sources.

## Recover from a Failure

1. Read the installer or converter error before changing files.
2. Resolve each reported conflict manually; do not delete the ownership record
   to bypass the check.
3. If rollback could not restore every file, use the recovery directory printed
   in the installer log.
4. Run the installation again. When Codex sync is enabled, rerun its `--check`
   command after the installer succeeds.

An abrupt Unity, Python, or operating-system termination cannot run the normal
rollback path. Before retrying, inspect any project-root
`.unity-biome-skills-*` or `.claude-to-codex-*` recovery directory and compare
its backup files with the managed destinations. Remove a recovery directory
only after the project state is verified.

The **Finish** action remains unavailable until the requested installation
steps complete successfully.
