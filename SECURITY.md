# Security Policy

Unity Biome MCP is a local developer tool with broad access to an open Unity
project. Treat every connected MCP client as trusted code running with your OS
account's permissions.

## Report a vulnerability

Please report security issues privately:

1. Use a [GitHub Security Advisory](https://github.com/german-krasnikov/unity-biome-mcp/security/advisories/new).
2. If GitHub is unavailable, email
   [german.krasnikov@gmail.com](mailto:german.krasnikov@gmail.com).

Do not include secrets or private project assets in a public issue. A useful
report includes the affected version, operating system, reproduction steps,
impact, and any proposed mitigation.

## Trust model

The Unity plugin listens only on loopback (`127.0.0.1`) and the Python server
connects to that local endpoint. Loopback prevents connections from another
machine, but it is **not authentication** and it is not a same-user boundary.
Any process on the same host that can reach the selected port may attempt to
send protocol commands.

The bridge supports multiple simultaneous local clients. Project selection and
port metadata help clients find the intended Unity instance; they do not create
an authorization boundary between projects or users.

Tool controls have distinct, limited purposes:

- The MCP Hub can enable or disable Unity command handlers in its catalog.
  Those handlers are enabled by default unless you change their setting.
- Python capability discovery controls which deferred typed MCP tools are
  visible in the current server session, including custom plugin categories.
- `UNITY_MCP_READ_ONLY=1` makes that Python server endpoint reject commands
  classified as mutating. The Unity listener has a separate project guard:
  `readOnly: true` in `ProjectSettings/UnityMCP.json`. Enable both when both
  entry points must reject mutations. These are mutation guards, not
  authentication.
- The in-Editor Chat permission deny-set is saved as UI policy. The current
  `RelayBackend` does not forward or enforce that deny-set when it starts a
  backend, so do not treat it as a server-side control.

These controls are not one universal authorization layer. Always-allowed
protocol commands and Python-only orchestration do not all pass through the
MCP Hub's Unity-handler toggle.

There is no universal confirmation dialog before every write. Review the
permissions and approval behavior of the MCP client you use as well as the
settings in Unity.

## Code execution

`execute_code` compiles and runs C# inside the Unity Editor process. Its
Security Level is configurable in the MCP Hub:

- `AllowAll` is the default and skips the pattern scan.
- `Standard` and `Strict` reject progressively broader sets of known-dangerous
  source patterns.

The scan is defense in depth, not a sandbox. Pattern matching cannot prove that
arbitrary C# is safe, and allowed Unity APIs can still modify scenes, assets,
project settings, or files. Only expose `execute_code` to clients you trust.

Other write tools can also make durable changes. Unity Undo covers many scene
mutations, but not every asset, package, generated-file, or external-process
side effect.

## Network and data flows

Core MCP traffic between the Python server and Unity stays on loopback. Some
optional features communicate beyond that boundary:

- in-Editor chat and configured agent backends start external CLI/provider
  processes, which may send prompts, selected context, and attachments to
  their provider;
- sampling-assisted features can invoke a configured external CLI;
- update checks contact the project's GitHub release endpoint, and update
  actions can invoke package or process tooling;
- installing packages or dependencies uses their normal remote registries.

The project does not promise that all features are offline. Review provider
privacy terms and the exact context attached to a request before using chat or
sampling with confidential projects. Local metrics exposed by `get_metrics`
are operational counters; do not treat that as proof that third-party clients
or providers collect no telemetry of their own.

## Safe operation

- Use the plugin only on a trusted local machine and OS account.
- Do not expose, proxy, or port-forward the Unity listener.
- Keep untrusted local processes and shared users away from sensitive projects.
- Disable tools you do not need; use `Strict` for `execute_code` when its
  restrictions are compatible with your workflow.
- Keep work under version control and review changes before committing them.
- Pin or review plugin, Python server, and third-party provider updates.
- When several Unity projects are open, set `UNITY_MCP_PROJECT_DIR` for the MCP
  server and verify the selected instance before a destructive operation.
- Stop the Unity listener or close the project when it should not accept local
  commands.

## Supported versions

| Version | Support |
|---|---|
| Latest release | Supported |
| Previous release | Best effort |
| Older releases | Unsupported |

Security fixes may require updating both the Unity package and Python server.

## Scope

Examples of in-scope reports include unauthenticated access beyond the stated
loopback boundary, command-gating bypasses, code-scan bypasses with meaningful
additional impact, unsafe path handling, unintended disclosure, and
cross-project command routing. General Unity vulnerabilities should be reported
to Unity; vulnerabilities in a third-party provider or dependency should also
be reported to that upstream project.
