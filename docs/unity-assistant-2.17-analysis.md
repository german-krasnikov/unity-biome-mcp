# Unity Assistant 2.17: product, architecture and ROI analysis

**Analysis date:** August 13, 2026  
**Unity package:** `com.unity.ai.assistant 2.17.0-pre.1`  
**Biome snapshot:** branch `feature/refid-unification`, product version `1.33.0`

This report answers three questions:

1. What Unity Assistant 2.17 actually provides, including Chat, Gateway, MCP,
   context, safety, generators, Profiler, skills, and extension APIs.
2. How Unity connects Claude Code, Codex, Gemini, Cursor, and external MCP
   clients, and how that differs from Unity Biome MCP.
3. Which ideas have the highest expected return for Biome users.

## Sources and confidence

The analysis combines:

- the [Assistant 2.17 manual](https://docs.unity3d.com/Packages/com.unity.ai.assistant@2.17/manual/index.html)
  and [package changelog](https://docs.unity3d.com/Packages/com.unity.ai.assistant@2.17/changelog/CHANGELOG.html);
- Unity's [May 5, 2026 getting-started article](https://unity.com/blog/unity-ai-how-to-get-started);
- the official
  [2.17.0-pre.1 UPM source archive](https://download.packages.unity.com/com.unity.ai.assistant/-/com.unity.ai.assistant-2.17.0-pre.1.tgz),
  especially `Editor/Assistant/Acp` and `Modules/Unity.AI.MCP.Editor`;
- the supplied [MCP settings screenshot](https://cdn.sanity.io/images/fuvbjjlp/production/d7ef9615856f08a9199d7c3fb6b4001a40bf194c-2560x1440.png)
  and [agent selector screenshot](https://cdn.sanity.io/images/fuvbjjlp/production/d4dc683c0a62b23423062c275f75e767b1e43557-810x522.png);
- a source-level audit of the current Biome C#, Python, tests, and docs.

Claims are tagged conceptually as:

- **Documented:** explicitly stated in public Unity documentation.
- **Source-confirmed:** visible in the official package source, but not always in
  the manual.
- **Inference:** a product or ROI conclusion, not a Unity claim.

This is a prerelease package. Provider details and commercial entitlements can
change. Security findings below are source-level risks, not live exploit
validation.

## Executive conclusion

Unity's strongest advantage is not a larger Unity tool surface. It is a more
coherent **agent host**:

- ACP normalizes conversations, modes, models, commands, plans, tool events,
  permissions, and resume across providers.
- MCP is kept separate and exposes Unity actions to those agents.
- the Editor owns provider setup, status, history, context, and recovery UX.

Biome is already stronger where successful Unity automation is decided:

- broader and deeper Unity operations;
- deterministic PlayTest workflows and suites;
- visual baselines and diffs;
- batch, verification, diagnostics, rich tool cards, and open provider choice;
- one reusable MCP surface for external agents.

The current weakness is that Biome's in-Editor Chat promises a more uniform
experience than its backends actually provide. Claude is persistent and has a
real permission flow, while Codex, OpenCode, Kimi, and Antigravity use different
process, resume, event, and safety semantics. Some saved settings are not applied
at launch. In particular, Codex and OpenCode currently use dangerous bypass
flags, so Ask/Agent is not a reliable security contract.

**Recommended product direction:** position Biome as the
**agent-agnostic execution, test, and verification runtime for Unity**, then add
the best parts of Unity's host architecture around it. Do not compete by
building another asset-generation cloud.

The highest-return sequence is:

1. enforce permissions and mode capabilities on the server;
2. make setup health measurable end to end;
3. fix Plan/Approve and resume correctness;
4. introduce a canonical agent-event/capability contract;
5. pilot ACP behind the existing relay, without rewriting the MCP server;
6. add persistent history, visible project context, and durable recovery.

## The three Unity integration planes

Unity exposes three related but distinct systems. Treating them as one feature
obscures the reusable architecture.

### 1. Gateway: coding agents inside Unity Chat

```text
Unity Assistant UI
  -> local Unity relay
  -> ACP session
  -> Claude / Codex / Gemini / Cursor agent
  -> injected Unity MCP server
  -> local relay --mcp
  -> named pipe or Unix socket
  -> Unity Editor MCP tool registry
```

ACP is the **agent lifecycle protocol**. MCP is the **Unity tool protocol**.

The package handles ACP methods and updates for prompt, cancel, resume, mode,
model, available slash commands, streaming text and thought, plan, tool call,
tool update, file diff, and permission requests. `session/set_model` is explicitly
treated as unstable in the 2.17 source.

The provider selector shown in the supplied screenshot is therefore more than
a dropdown over CLI commands. It switches an ACP provider while retaining one
Unity conversation surface.

### 2. Unity MCP Server: Unity tools in an external client

```text
Claude Code / Cursor / Windsurf / Codex / another MCP client
  -> stdio process: Unity relay --mcp
  -> named pipe on Windows or Unix socket on macOS/Linux
  -> Unity Editor bridge
  -> built-in and custom Unity tools
```

This path supports multiple clients, tool discovery, project or PID targeting,
connection status, and direct-connection approval. The settings screenshot
shows the onboarding intent clearly: every detected client gets a configure,
status, and location workflow.

### 3. Native Assistant as an MCP client

Unity Assistant can also consume arbitrary external MCP servers over local
`stdio` or remote Streamable HTTP, including headers, manifest inspection,
timeouts, PATH overrides, status, logs, and manual refresh.

This makes the official package bidirectional: Unity is both an MCP server and
an MCP client. Biome currently concentrates on the server side, which is the
correct core for an open execution runtime.

## Provider implementation

The [Gateway manual](https://docs.unity3d.com/Packages/com.unity.ai.assistant@2.17/manual/integration/ai-gateway-intro.html)
describes local agents using the user's existing provider credentials. The
source reveals how Unity normalizes them.

| Provider | Unity 2.17 implementation | Biome implementation | Important consequence |
|---|---|---|---|
| Claude Code | Local `claude` plus an ACP adapter based on the Claude agent SDK; `CLAUDE.md`, resume, subscription login or `ANTHROPIC_API_KEY` | One persistent `claude` stream-JSON process; real `plan`/`acceptEdits`, MCP permission prompt, allowlist, resume | Biome's Claude path is already mature. ACP is useful for normalization, not an urgent replacement. |
| Codex | Bundled custom `codex-acp` adapter; existing Codex auth or `OPENAI_API_KEY`; `AGENTS.md` | New `codex exec` per turn, custom JSON transform, inline MCP config | Unity has better session/event normalization. Biome currently starts Codex with `danger-full-access` and bypasses approvals on resume. |
| Gemini | Documentation calls it bundled; inspected source looks for local `gemini`, offers npm installation, and launches `gemini --acp`; `GEMINI.md` | Deprecated external client guide; not an in-Editor backend | There is a Unity documentation/source mismatch. Native ACP support makes Gemini relatively cheap to add later. |
| Cursor | Local `agent acp`, Cursor login and MCP enablement; model switching is not uniformly supported | Configured as an external MCP client, not a Chat backend | ACP capabilities must gate UI. A shared dropdown must not imply every provider supports every control. |
| Kimi | Not documented as a Gateway provider | New process per turn; temporary MCP config; no effective Chat resume | Biome has broader provider reach, but weaker parity. Keep a legacy adapter until a conformant ACP path exists. |
| OpenCode | Not documented as a Gateway provider | New `opencode run` per turn; resume argument exists but is not reliably propagated; permission bypass | `opencode acp` is a low-cost technical pilot, but Codex likely has higher user reach. |
| Antigravity | Not documented | New process per turn; plain-text normalization | Retain as an explicitly limited backend rather than claiming full parity. |

### ACP details worth adopting

The reusable ideas are:

- a provider descriptor rather than duplicated provider enums and switch blocks;
- advertised capabilities for modes, models, commands, images, resume, and auth;
- a canonical event lifecycle for thought, text, tool, plan, diff, and completion;
- provider-specific instruction filenames such as `CLAUDE.md`, `AGENTS.md`, and
  `GEMINI.md`;
- install prerequisites, install steps, login steps, version, and troubleshooting
  metadata delivered as provider data;
- an agent session identity correlated with its MCP connection.

ACP v1 is a stable protocol, but adapters still evolve. The current Codex ACP
adapter lives under the [Agent Client Protocol organization](https://github.com/agentclientprotocol/codex-acp),
and adapter distribution, signing, updates, and cross-platform packaging remain
real ownership costs.

### What should not be copied

- Do not bundle hundreds of megabytes of provider binaries until activation data
  proves the installation win justifies the release and signing burden.
- Do not replace Biome's working external MCP architecture with Unity's relay.
- Do not assume ACP makes permissions uniform. Unity itself has a dedicated
  Codex workaround because Codex does not use the standard ACP permission flow.
- Do not auto-approve when an ACP session cannot be correlated with an MCP call.
- Do not make API-key input the primary auth path. Reusing provider subscription
  login is one of Biome's advantages.

## Session identity and permissions

Unity correlates an ACP agent session with its MCP transport using a per-session
token and Unity instance identity. Direct external connections have a separate
approval model based on process information, executable identity, hash, and
publisher/signature data, with history and revoke controls.

This is better product UX than Biome's unauthenticated loopback TCP connection.
The maximum-ROI adaptation is not a transport rewrite. It is:

1. a random per-session capability token;
2. an explicit client/session/Unity-instance handshake;
3. server-side permission evaluation immediately before tool execution;
4. approval history and revoke;
5. provider capability flags that control which modes the UI exposes.

The 2.17 Unity source also contains two risks that should not be copied:

- the Codex MCP approval workaround returns approval when it cannot find the
  associated active ACP session;
- the inspected bridge logic appears to reject calls only after an explicit
  denial, which may be inconsistent with the manual's pending-approval wording.

Both require live validation before calling them exploitable, but Biome can adopt
the architecture with stricter fail-closed semantics.

## Complete feature map

### Chat and workflow

| Capability | Unity Assistant 2.17 | Biome today | Decision |
|---|---|---|---|
| Ask | Read-only inspection and guidance | Claude maps Ask to `plan`; other backend semantics vary | Keep, but show it only when enforcement is verified. |
| Plan | Clarifying questions, structured plan, revise/approve/deny, saves `.md` in `Assets/Plans`, checklist and execution summary | Approve workflow exists, but no independent fully normalized Plan contract | High ROI after P0 correctness. Use preview/apply and explicit verification. |
| Agent | Read/write tools with permission UI | Auto-approval behavior depends on CLI; server tool disabling remains authoritative | Preserve the workflow but move enforcement below the provider. |
| Model tiers | Unity Lite/Default/Ultra, credit-aware fallback | Provider-specific model IDs, no Unity credits | Biome's BYO subscription approach is better. Add capability-based model discovery. |
| Provider selector | Unity, Claude, Codex, Gemini, Cursor in one composer | Claude, Codex, Kimi, Antigravity, OpenCode | Already competitive in breadth; parity and truthfulness matter more than another provider. |
| Commands | Provider-advertised `/review`, `/init`, and other commands | Local slash templates and workflows | ACP command discovery is worth adopting. Keep Biome workflow commands as a separate namespace. |
| History | Persistent conversations, search, favorite, rename, delete | Current transcript in `SessionState`; session picker is not a transcript store | High user impact. Store canonical Biome events project-locally outside `Assets`. |
| Tool presentation | Visible tool, arguments, output, errors, plan and diff blocks | Rich domain-specific cards and diffs | Biome is strong. Map canonical ACP events into existing cards. |
| Prompt UX | Suggested prompts, prompt history, search in conversation, copy/save code, todos and subagent UI | Attachments, chips, tool cards, slash templates, subagents | Selectively add history/search/todos; avoid decorative parity work. |

### Context and visual feedback

| Capability | Unity Assistant 2.17 | Biome today | Decision |
|---|---|---|---|
| Object context | GameObjects, components, assets, scripts, Console messages, images; picker, search and drag/drop | Rich explicit chips, selection, region, screenshots, files, watches, drag/drop | Biome is already strong. Standardize the schema for every backend. |
| Project Overview | Generated `Assets/Project_Overview.md` with purpose, loop, architecture, scenes, UI, data, dependencies, build, style, tests, caveats | No default persistent project brief; state snapshot is mostly used for reload/playtest | High ROI. Generate a visible, deterministic, incremental Project Brief with a token estimate. |
| Asset Knowledge | Local Sentis visual embeddings for textures, materials, and GameObjects | Filename/metadata/tool-based search, no local semantic visual index | Later. Valuable for large art projects but narrower than context correctness. |
| Images | Manual upload/drop, Unity-window capture, automatic capture with permission | Screenshots, attachments, visual baseline/diff, region and annotation | Biome is stronger for verification. Add an opt-in automatic capture policy only after permissions. |
| Annotation | Full-screen or Unity-only capture, brush controls, undo/redo, export | Screenshot annotation already exists | No major gap. |
| Multi-angle validation | Source/changelog describes Scene View and 2D capture flows | Multi-view screenshots and deterministic comparisons | Biome's verification loop is a differentiator; feature it more clearly. |

### Safety and recovery

| Capability | Unity Assistant 2.17 | Biome today | Decision |
|---|---|---|---|
| Per-operation policy | Allow, Ask, Deny; conversation overrides; Autorun | Tool categories are enforceable, but Chat deny settings are not forwarded | P0. Unify UI policy and server enforcement. |
| Connection trust | Direct-client approval, remembered identity, history/revoke; Gateway token correlation | Loopback transport, project/port discovery, no equivalent identity approval | Add token/identity handshake; keep TCP initially. |
| Checkpoints | Git snapshots outside project before prompts, persistent across restarts, retention and restore | Per-turn Unity Undo; invalid across domain reload and cannot cover all file side effects | Add a scoped durable change journal and preview. Do not auto-checkout a dirty user repository. |
| Restore UX | Diff preview in later changes, archived later messages, rollback attempt on failure | One-click Restore for current turn and tool-level undo where supported | Keep fast Undo; add durable recovery as a second tier. |

### Unity analysis and extension

| Capability | Unity Assistant 2.17 | Biome today | Decision |
|---|---|---|---|
| Profiler | Analyze active/saved session; attach selected frame/sample on Unity 6.4+ | Aggregate profiling window and counters; no selected-sample Chat context | Add `Analyze in Chat` for Biome captures first, then version-gated Profiler selection. |
| Skills | Filesystem `SKILL.md`, project/user/package discovery, progressive disclosure, default Deny, requirements | Domain skills, agents, templates and plugin API | Adopt default-deny discovery and requirements metadata; keep one canonical tool registry. |
| Custom tools | `[AgentTool]` and `[McpTool]`; package/runtime discovery; source includes an adapter between surfaces | Python and C# plugin/tool extension | Biome's canonical surface is preferable. Avoid separate native and MCP authoring models. |
| Assistant API | Prompt/run/headless API, typed and virtual context, custom agents; headless only for Unity provider | External MCP is programmable; in-Editor Chat has no equivalent stable public orchestration API | Lower priority than reliable protocol behavior. Revisit after canonical events. |
| External MCP servers | Native Assistant can consume stdio and Streamable HTTP tools | In-Editor Chat exposes Biome's Unity MCP; arbitrary MCP aggregation is not a core feature | Useful later as an adapter/plugin capability, not P0. |

### Generators

Unity integrates Sprite, Texture2D, PBR Material, Terrain Layer, Cubemap, simple
static 3D object, Sound, Animation, Object Picker generation, and Figma-to-UI.
Generated outputs are saved under `Project/Generations`, and prompts/settings can
be transferred to dedicated Generator interfaces.

The limitations matter:

- 3D generation is intended for simple, single-part, static props, not rigs or
  animated multipart models;
- generators require separate services, consent, credits, model evaluation, and
  asset-specific UX;
- Figma-to-UI introduces a separate token, import, screenshot, and UI-generation
  pipeline;
- local Asset Knowledge currently covers a limited set of asset classes.

**Decision:** do not build first-party generation infrastructure. Expose generator
providers through plugins or external MCP servers and invest the core team in
deterministic creation, editing, testing, and verification.

## Commercial, privacy, and operational constraints

### Access and documentation conflicts

The public sources are not fully synchronized:

- the [Gateway setup manual](https://docs.unity3d.com/Packages/com.unity.ai.assistant@2.17/manual/integration/ai-gateway-get-started.html)
  describes an eligible Unity subscription, assigned seat, and a project linked
  to the same organization;
- Unity's May 2026 article describes included access for paid plans and a trial
  path for Personal users;
- the package changelog says MCP and Gateway connections are no longer capped or
  gated by entitlement limits.

Do not base Biome positioning or a migration decision on an assumed Unity price
boundary until access is verified in the current production Editor and account
tier.

### Data and credentials

- third-party Gateway agents run locally and reuse their own credentials, but
  prompts and provider traffic still follow the selected provider's data policy;
- Unity source describes OS credential storage and log redaction for Gateway
  secrets, while user-authored external MCP headers or tokens can still be saved
  in local project/user configuration and require careful handling;
- Asset Knowledge builds embeddings locally with Sentis and keeps source assets
  local;
- Generator requests are separate cloud workflows with consent, credits, and
  provider-side processing;
- automatic visual capture is restricted to Unity windows, while explicit
  full-screen annotation can include other applications.

Biome should retain CLI-based auth as the default, use the OS keychain for any
optional secrets, redact relay logs, and show the exact context and images being
sent before a provider call.

### Product limitations

- `2.17.0-pre.1` is prerelease software with active fixes around relay reconnect,
  domain reload, resume, MCP initialization, checkpoints, and generators.
- The selected Profiler frame/sample integration requires Unity 6.4+ even where
  the base Assistant package can run on earlier Unity 6 releases.
- Project Overview is native-provider-oriented and is hidden for third-party ACP
  providers in the inspected release path.
- `RunHeadless` is available only for the native Unity provider.
- Checkpoint restore can revert manual user changes as well as Assistant changes,
  has no forward restore, and is not a substitute for long-term source control.
- The changelog documents a 4,000-character Chat input limit, a 2 MB file-read
  limit, binary-file rejection, and restrictions on namespaces available to
  generated C# execution.
- External MCP servers can return partial or provider-specific failures; Unity
  displays those outcomes but cannot make an unreliable server transactional.

## Where Biome is better today

1. **Unity execution depth.** The 148-tool surface covers scenes, objects,
   components, assets, media, runtime, tests, system actions, and verification.
2. **Deterministic validation.** PlayTest DSL/suites, wait conditions, screenshot
   baseline/diff, and `verify_after_change` address whether a change actually
   works, not only whether an agent produced code.
3. **Provider freedom.** Claude, Codex, Kimi, OpenCode, and Antigravity reuse local
   CLI authentication without a Unity cloud account or credit system.
4. **Domain presentation.** Rich cards make component reads, code diffs, images,
   media, and tool outcomes easier to inspect.
5. **Extensible canonical tool surface.** The same MCP tools can serve the Chat UI
   and external clients instead of requiring separate business implementations.

## Where Unity is better today

1. **Normalized agent lifecycle.** ACP avoids a growing collection of provider
   output parsers and one-off session semantics.
2. **Conversation product.** Persistent searchable history, provider commands,
   plan state, todos, and recovery are one coherent workspace.
3. **Onboarding.** Install, locate, configure, status, version, auth, and
   troubleshooting are first-class UI states.
4. **Durable recovery.** Checkpoints survive Editor restart and cover more than a
   Unity Undo group.
5. **Context entry points.** Project Overview and Profiler selection reduce prompt
   setup cost.
6. **Connection identity.** Direct clients have approval, remembered identity,
   status, and revoke controls.

## Current Biome correctness gaps

These should be fixed before presenting more high-level automation.

### P0: Plan approval can leave Claude in Plan mode

`ApproveAndExecute()` changes the UI state and sends another prompt, but does not
perform the same relay mode transition as the normal mode toggle. Claude may
therefore still be running with `--permission-mode plan` while the UI says Agent.

Relevant code:

- `server/src/unity_mcp/backend_def.py:213-229`
- `unity-plugin/Editor/Chat/View/MCPChatWindow.Approve.cs:12-27`
- `unity-plugin/Editor/Chat/CLI/RelayBackend.cs:120-125`
- `server/src/unity_mcp/chat_relay.py:254-273`

Use one awaited `SetMode` transition with relay acknowledgement before dispatch.
Hide or qualify Plan for backends that cannot guarantee it.

### P0: Ask/Agent is not a security boundary for every backend

Codex uses `danger-full-access` for a fresh turn and bypasses approvals/sandbox
on resume. OpenCode always uses a permission-bypass flag. The default code
execution security level is also `Allow All`. Saved Chat permission settings are
documented as not currently forwarded.

Relevant code and docs:

- `server/src/unity_mcp/backend_def.py:261-270`
- `server/src/unity_mcp/backend_def.py:357-369`
- `docs/settings.md`, Tools and Permissions

Provider modes must be capability-gated, and the Unity server must reject a
disallowed operation even if the provider never emits a permission request.

### P0: setup success is configuration-only

The wizard can write client config, but the decisive user question is whether an
authenticated agent can discover the correct Unity instance and complete a safe
read-only tool call. Add one health action that checks binary, version, auth,
config, process, bridge, discovery, and a real `get_hierarchy`/status call.

### P0: resume and launch settings are not applied consistently

Reliable in-Chat resume is limited to Claude. Per-turn backends can expose or
consume IDs without preserving the selected ID for the next turn. Binary
override, deny set, timeout, and extra args are stored but not all forwarded to
relay startup.

### P1: sampling backend selection is misleading

Settings store a backend for optional LLM sampling, while
`server/src/unity_mcp/sampling.py` still launches Claude. Either route through a
real backend adapter or temporarily remove unsupported choices.

## ROI model

Scores below are planning estimates, not measured telemetry. The formula is:

```text
ROI score = Impact (1..5) * Reach (1..5) * Confidence (0.5..1.0) / Effort (1..5)
```

| Rank | Initiative | I | R | C | E | ROI | Expected user result |
|---:|---|---:|---:|---:|---:|---:|---|
| 1 | Correct Plan -> Agent transition and capability-gate modes | 5 | 4 | 1.0 | 1 | 20.0 | UI state matches actual execution authority. |
| 2 | Server-side Permission Broker and per-session token | 5 | 5 | 1.0 | 2 | 12.5 | Ask/Deny remains effective for every provider. |
| 3 | End-to-end Connection & Backend Health | 5 | 5 | 0.9 | 2 | 11.3 | Fewer setup failures and faster diagnosis. |
| 4 | Provider contract canaries in CI/manual release gates | 4 | 5 | 0.9 | 2 | 9.0 | CLI updates stop silently breaking Chat. |
| 5 | Fix LLM sampling routing or remove false choices | 3 | 3 | 1.0 | 1 | 9.0 | Settings become truthful and verification uses the selected backend. |
| 6 | Universal context schema plus visible selection/Console toggle | 5 | 4 | 0.85 | 2 | 8.5 | Better first-turn accuracy across non-Claude agents. |
| 7 | Finish resume ID propagation for Codex/OpenCode | 4 | 4 | 0.9 | 2 | Conversations retain context without manual recovery. |
| 8 | Data-driven provider/capability registry | 4 | 5 | 0.8 | 3 | 5.3 | New providers stop requiring duplicated C# and Python edits. |
| 9 | Persistent searchable canonical Chat history | 4 | 4 | 0.9 | 3 | Users can reopen, search, fork, export, and delete work. |
| 10 | Deterministic visible Project Brief | 4 | 4 | 0.9 | 2 | Less repetitive prompting and fewer wrong-project assumptions. |
| 11 | Schema quality and task-level workflow compression | 4 | 4 | 0.9 | 2 | Lower tool-selection burden than exposing all 148 calls at once. |
| 12 | ACP pilot behind canonical `AgentEvent` | 5 | 4 | 0.65 | 4 | Lower long-term provider maintenance and richer normalized Chat. |
| 13 | Preview/apply/verify Plan workspace | 5 | 4 | 0.8 | 4 | Higher trust for multi-step changes. |
| 14 | Durable scoped change journal | 5 | 3 | 0.75 | 4 | Restore survives reload and includes touched files. |
| 15 | Profiler `Analyze in Chat` | 3 | 3 | 0.85 | 2 | Performance diagnosis starts from actual capture context. |

The score is a prioritization aid, not an absolute business case. Strategic
platform work such as ACP can rank below a small correctness fix while still
belonging in the roadmap.

## Recommended architecture

Do not rewrite the MCP server. Insert a normalized agent host behind the current
Chat boundary:

```text
Biome Chat UI and rich cards
  -> canonical AgentEvent + ProviderCapabilities contract
  -> existing reload-resistant local relay boundary
       -> ACP adapter: selected providers
       -> legacy adapter: providers without reliable ACP
  -> existing Biome MCP server
  -> server-side Permission Broker
  -> existing Unity plugin and 148-tool runtime
```

Suggested internal events:

- `session.started`, `session.resumed`, `session.failed`;
- `message.text_delta`, `message.thought_delta`;
- `plan.updated`, `command.list_updated`;
- `tool.requested`, `tool.permission_requested`, `tool.started`,
  `tool.updated`, `tool.completed`, `tool.failed`;
- `file.diff`, `usage.updated`, `turn.completed`, `turn.cancelled`.

Suggested provider capabilities:

- `persistent_session`, `resume`, `set_mode`, `set_model`;
- `permission_requests`, `server_enforced_permissions`;
- `images`, `context_mentions`, `slash_commands`;
- `plans`, `file_diffs`, `tool_progress`, `auth_status`.

The UI should render only supported controls and explain degraded behavior before
a user sends a prompt.

## ACP migration plan

### Phase 0: make current behavior safe

- implement the Permission Broker and fail-closed session correlation;
- remove dangerous bypass as the default Ask path;
- fix Plan/Approve and resume ID propagation;
- make stored launch settings either effective or unavailable;
- add backend contract tests for auth, tool discovery, permissions, images,
  cancellation, resume, and domain reload.

### Phase 1: establish a canonical contract

- normalize the existing Claude, Codex, Kimi, Antigravity, and OpenCode outputs
  into `AgentEvent`;
- replace duplicated provider lists with one descriptor registry;
- preserve existing cards and transcript serialization above the adapter layer.

### Phase 2: ACP pilot

- use native `opencode acp` as a low-cost protocol proof;
- add Codex through the maintained ACP adapter as the higher-reach product proof;
- keep the current Claude stream-JSON backend as fallback while comparing ACP
  resume, permissions, images, tool calls, and reload behavior;
- do not promote an ACP provider until the canary suite reaches parity.

### Phase 3: capability-driven UX

- populate models, modes, slash commands, auth, and resume controls from provider
  capabilities;
- map ACP plan/diff/tool updates to current Biome components;
- retire a provider-specific parser only after a measured stable period.

## 90-day roadmap

### Weeks 1-2: correctness and trust

- fix Plan/Approve transition;
- enforce tool policy server-side;
- introduce per-session token and backend capability flags;
- fix or remove misleading launch/sampling settings.

**Exit metric:** destructive canary tools are denied in Ask for every enabled
backend, and UI mode always matches relay mode.

### Weeks 3-5: activation and contracts

- ship one-click end-to-end health diagnostics;
- add provider canaries and resume tests;
- introduce the canonical event and provider descriptor contracts;
- generalize context instructions for every backend.

**Exit metric:** setup failures have a named failing stage; every release tests a
real safe Unity call for the supported Chat backends.

### Weeks 6-9: daily-use product value

- persistent searchable conversation history;
- visible Project Brief with opt-in context and token estimate;
- preview/apply/verify Plan surface;
- `Analyze in Chat` for Biome profiler captures.

**Exit metric:** users can reopen a prior conversation, understand attached
context, preview a plan, and verify its result without leaving Unity.

### Weeks 10-12: ACP and durable recovery

- run OpenCode and Codex ACP pilots behind feature flags;
- compare against legacy backends using contract canaries;
- prototype a scoped durable change journal with diff preview and safe restore.

**Exit metric:** at least one ACP provider reaches functional parity without
regressing current providers; restore never touches unrelated dirty files.

## Explicit non-goals for this cycle

- a full TCP-to-native-IPC rewrite;
- bundled provider binaries for every OS;
- first-party Sprite/3D/audio/animation generation models;
- Figma-to-UI;
- a broad rewrite of working Unity tools;
- removing legacy provider adapters before ACP parity is proven.

## Metrics to validate ROI

Instrument the funnel before and after the roadmap:

- install-to-first-successful-tool-call time and completion rate;
- backend health failures by stage: binary, auth, config, relay, MCP, Unity;
- first-turn tool success and verification pass rate;
- permission prompts, denies, bypass attempts, and uncorrelated sessions;
- resume success after new turn, backend restart, and domain reload;
- plan approval-to-success rate and restore rate;
- context attachment types and token volume;
- conversation reopen/search/fork usage;
- provider-specific parser and canary failures after CLI updates.

The key north-star is not messages sent. It is the percentage of Unity tasks
that finish with an explicit deterministic or visual verification result.

## Final decision

Unity's approach is better for **hosting heterogeneous coding agents in one
Editor Chat**. Biome's approach is better for **open, deep, verifiable Unity
automation**.

The winning combination is not to clone Unity Assistant. It is to keep Biome's
execution and verification core, adopt ACP incrementally for agent lifecycle,
and copy Unity's best trust and context patterns with stricter server-side
enforcement. That produces more user impact than generators, provider bundling,
or a transport rewrite, while preserving Biome's strongest differentiation.
