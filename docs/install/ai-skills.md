# Install AI Skills and Agents

Unity Biome MCP ships optional project-local guidance for Claude Code and
Codex. It teaches the clients how to select MCP tools, batch compatible work,
author Unity content, and verify results. Installing this guidance is separate
from configuring the MCP connection.

The package currently includes:

- 11 domain skills for MCP operations, scenes, assets and prefabs, materials
  and shaders, UI, animation, VFX, physics, C# editing, testing, and diagnostics
- 4 focused agents: `unity-scene-editor`, `unity-csharp-developer`,
  `playmode-tester`, and `unity-diagnostics`
- 1 Claude-to-Codex conversion script

## Install

1. Open the Unity project that should receive the guidance.
2. Select **MCP > Install AI Skills**.
3. Leave **Overwrite existing files** disabled for the first installation.
4. Enable **Run Codex sync after install** when this project uses Codex.
5. Select **Install**, review the log, then select **Finish**.
6. Restart Claude Code or Codex from the Unity project so it reloads the
   project-local artifacts.

The Setup Wizard can open the same installation step. The installer writes only
inside the current Unity project.

## Installed Paths

| Consumer | Project-local artifacts |
|---|---|
| Claude Code | `.claude/skills/<skill>/`, `.claude/agents/<agent>.md` |
| Codex conversion script | `.codex/scripts/claude_to_codex.py` |
| Codex generated skills | `.agents/skills/<skill>/` |
| Codex generated agents | `.codex/agents/<agent>.toml` |
| Codex ownership record | `.codex/.claude-to-codex-manifest.json` |

Claude artifacts are the canonical source for Codex generation. Supporting
references are copied with their skill, so the folder layout must remain
intact.

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
- Writes are staged and rolled back on failure. The installed-version marker is
  written only after the requested Claude installation and Codex sync succeed.

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
4. Run the installation again, then run the Codex `--check` command when Codex
   sync is enabled.

The **Finish** action remains unavailable until the requested installation
steps complete successfully.
