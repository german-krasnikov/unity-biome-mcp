# Settings

Open **MCP > Settings** to configure the Unity-side server and the in-Unity Chat experience.

## Ports

- **Port** is the Unity MCP transport port. The default is `9500`.
- **Chat Port** is the Unity-side MCP transport reserved for in-Unity Chat. The default is `9501`; the Python relay uses a separate dynamically selected local port.
- The two ports must differ and must be between `1024` and `65535`.

Changes are saved immediately, but the MCP server must restart before new ports take effect. Open **MCP > Status** and select **Restart**, then reconnect external clients.

Port discovery normally removes the need to put a fixed port in client configuration. Keep a fixed port only when your environment requires one.

## Code Execution Security

**Security Level** controls the scan applied by the `execute_code` tool. It is not a global sandbox for every MCP tool.

| Level | Behavior |
|---|---|
| Standard | Blocks dangerous APIs and runtime reflection access while allowing type-information reflection |
| Allow All | Bypasses the code security scan |
| Strict | Applies the Standard restrictions and also blocks type-information reflection |

`Allow All` is the current default. Use it only for trusted prompts and projects.

## Tools and Permissions

These pages control different layers:

| Page | What it controls | Persistence and effect |
|---|---|---|
| **Tools** | Whether a Unity MCP command is enabled at all | On the next MCP reconnect |
| **Permissions** | The per-tool deny-set stored for the in-Unity Chat agent | Saved immediately; not currently forwarded to CLI backends |

Disabling a tool on the **Tools** page blocks both external clients and MCP Chat. The **Permissions** page stores a separate Chat-only deny-set and does not change external-client visibility.

The current relay start request does not forward that deny-set to CLI backends. Until it does, use **Tools** when a restriction must be enforced by the Unity MCP server; treat **Permissions** as saved Chat policy rather than a security boundary.

Use the **Minimal**, **Full**, and **No visuals** presets as starting points, then adjust individual categories. Use **Allow All** or **Deny All** on the Permissions page only when you intend to change the complete Chat deny-set.

## Plugins

The **Plugins** page appears when at least one registered plugin contributes a
settings UI. Open a plugin card to load its controls. Plugin tools also appear
under the **Plugins** group on the Tools page.

If a plugin has no settings UI, it does not create a Plugins card. Its tools can
still be available through the live tool catalog.

## LLM Sampling

LLM Sampling selects the backend and model used by optional summarization and
visual-analysis features. It does not change the backend selected in MCP Chat.

Each task has its own foldout with **Backend**, **Model**, **Max Turns**, and
**Timeout** controls. **Claude Fast**, **Gemini Flash**, and **Codex** apply one
preset to every task. Screenshot-dependent entries are disabled while the
`screenshot` tool is disabled.

Changes are saved immediately. The configured backend CLI must be installed and
authenticated before a sampling task can use it.

## Chat Settings

The Chat Settings page owns:

- auto-scroll and inactivity timeout
- CLI binary discovery status
- cached CLI authentication status for Claude
- per-backend model selection and stored launch options
- context-chip visibility and colors
- settings contributed by Chat extensions

Authentication happens in each backend CLI. Unity Biome MCP does not provide an
API-key field. Install and sign in to the CLI first, ensure its executable is
available on the login-shell `PATH`, then restart Unity.

Chat currently applies the selected model at launch. Stored binary overrides,
permission options, startup timeouts, and extra arguments are saved but are not
applied when the relay starts.

See [Using MCP Chat](chat/using-chat.md) for the task workflow and [Chat Backends](chat/backends.md) for process and session behavior.

## Updates and Rollback

### Update

1. Open **MCP > Settings > Updates**.
2. Select **Check for Updates**.
3. Select **Level Up!** to play the release animation.
4. Optionally select **See new stats** to review the release summary.
5. Select **Update now**.

For UPM installations, the updater installs the selected package release and refreshes the generated project server pin after Unity reloads. Local source checkouts use their local update flow.

### Roll back or repair a version mismatch (UPM installations)

1. Open **MCP > Settings > Version Picker**.
2. Select a release.
3. Choose **Roll Back** to install it.
4. If the plugin and server pin differ, choose **Align Both**.

Unity reloads assemblies during a package change. Wait for compilation to finish before reconnecting clients.

Version Picker installs a UPM package version. For a local source checkout, use
the checkout's normal source-control update or rollback flow instead.

## Recovery

| Symptom | Action |
|---|---|
| New port is ignored | Restart from **MCP > Status**, then reconnect the client |
| A tool reports that it is disabled | Enable it under **Tools**, then reconnect |
| Chat cannot find a CLI | Make the CLI available on the login-shell `PATH`, then restart Unity |
| Chat authentication fails | Sign in with the backend CLI outside Unity and reopen Chat |
| Update or rollback fails | Check the Unity Console, keep the project compile-clean, and retry |
| Plugin and server versions differ | Use **Version Picker > Align Both** |
| Connection remains unhealthy | Run **MCP > Status > Diagnose**, then follow [Getting Started diagnostics](getting-started/index.md#diagnose-a-failure) |
