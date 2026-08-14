# Project Structure (Current)

```
unity-biome-mcp/
├── install/                    # Installation & configuration CLI (v0.38.0+, v0.45.0: connect/disconnect/pull, v0.68.0: _reconfigure_detected_clients)
│   ├── __init__.py
│   ├── bootstrap.sh            # One-liner macOS/Linux (legacy, deprecated v0.68.0)
│   ├── ui.py                   # Terminal UI (prompt, confirm, boxes, colors)
│   ├── commands.py             # Subcommand implementations (setup, update [uvx --reinstall + reconfigure], doctor, configure, uninstall, connect, disconnect, pull)
│   └── tests/                  # Bootstrap + UI + install tests
├── server/                     # Python MCP Server (see CLAUDE.md Commands section for current test count; v0.70.0: tools/ split console/screenshot/testing/editor_control, bridge split retry/result; v0.66.0: +27 diagnose/reload stability tests; v0.65.0: +8 run_tests pre-flight gate tests; v0.64.0: +75 polyline/scene tests; v0.54.1: +54 connection/focus-loss stability tests; v0.47.1: +151 config validation tests)
│   ├── src/unity_mcp/
│   │   ├── cli.py              # CLI dispatcher: configure/doctor/version/uninstall subcommands (v0.68.0)
│   │   ├── _preflight.py       # Import-time guard: one-line stderr on Python/SDK errors, not traceback (v0.68.0)
│   │   ├── server.py           # _UnstructuredMCP(FastMCP) instance, lifespan, 148 registered MCP tools, idle watchdog (useful/transport split, in-flight guard, T4, T6: dormant scheduling + TOCTOU guard + parent-alive check)
│   │   ├── timeout_categories.py # Per-command TCP timeouts; dict + get_timeout(cmd) derived from tools.tool_specs._SPECS (v0.69.0)
│   │   ├── bridge.py           # UnityBridge (TCP, heartbeat, SO_KEEPALIVE, RetryPolicy extracted v0.70.0); T5: ClientHelloPayload dataclass, _session_id/_lock_token/_started_at_utc set in __init__, session_id/lock_token read-only properties, _build_hello(msg_id), _check_version_from_hello/_fetch_and_check_version split for new→old fallback; T6: BridgeState.DORMANT/WAKING, suspend() method (CONNECTED→DORMANT, guards: queue empty + state check, resets cooldown/backoff)
│   │   ├── bridge_retry.py     # RetryPolicy class + unwrap_bridge_result() extracted (v0.70.0)
│   │   ├── bridge_result.py    # unwrap_bridge_result() helper (v0.70.0)
│   │   ├── bridge_heartbeat.py # Heartbeat management (extracted); _on_transport_activity callback wired for idle watchdog (T4)
│   │   ├── bridge_reload_state.py # Reload state tracking (extracted)
│   │   ├── bridge_socket.py    # Socket management + frame helpers (extracted; v0.80.0: frame_write(), frame_read(), frame_read_with_timeout() — shared by bridge, heartbeat, chat_relay, reload_ladder, doctor)
│   │   ├── connection_slot.py  # ConnectionSlot: single connection per project
│   │   ├── chat_relay.py       # Chat relay TCP server: 5 backends, deferred spawn for single-turn, _TRANSFORM_FNS dispatch, EOF handling (v0.67.0: +output_format/reads_stdin, +close_stdin, role-aware ping; v0.96.1: add_signal_handler wrapped in try/except NotImplementedError for Windows)
│   │   ├── cli_session.py      # CLI session state tracking + history scanning + close_stdin (v0.66.0+)
│   │   ├── backend_def.py      # 5 backend definitions: output_format enum (stream-json/codex-json/kimi-json/plain-text/opencode-json), reads_stdin flag, env_set UNITY_MCP_PORT, _run_login_shell() helper, MCP_BLANKET derived from SERVER_NAME, TTL retry cache (v0.71.0: +_run_login_shell, MCP_BLANKET, retry cache; v0.67.0: output_format replaces uses_stream_json)
│   │   ├── relay_buffer.py     # Message buffering + dequeuing for relay pipeline (v0.66.0+)
│   │   ├── stream_transform.py # 4 transform functions: _transform_line (Claude), _transform_plain_text_line (Agy), _transform_codex_line, _transform_kimi_line, _transform_opencode_line (v0.67.0: +4 specialized transformers, selected via _TRANSFORM_FNS)
│   │   ├── mcp_config_writer.py # Dynamic MCP config generation for relay (v0.66.0+; v0.96.1: uvx args include --quiet; v1.0.1: UNITY_MCP_PORT env only written when mcp_port != 0 — no baked port in permanent configs)
│   │   ├── config/             # Config module (v0.38.0+): client detection, MCP JSON merger, backup/restore; v0.47.1: GitHub-direct install, per-client root_key
│   │   │   ├── clients.py      # CLIENT_REGISTRY (Claude Code/Desktop/Cursor/Windsurf), detect_installed(), platform-aware ConfigDir (v0.47.1)
│   │   │   ├── merger.py       # merge_mcp_config(path, entry) — idempotent MCP server entry addition; SERVER_NAME = "unity-biome-mcp" (canonical source), _OLD_NAMES for migration (v0.71.0)
│   │   │   ├── backup.py       # Backup/restore config files before modifications
│   │   │   ├── resolver.py     # build_server_entry(port) — MCP server entry generator; GIT_INSTALL_URL constant (v0.47.1: shared with C#); v0.96.1: find_python() venv-first (.venv/bin/python before uvx), find_server_command() priority venv→uvx→sys, uvx includes --quiet, find_port() uses iter_port_files() for legacy ~/.unity-mcp/ports/ fallback
│   │   │   └── validator.py    # Config validation + path detection per tool; v0.47.1: skips json.loads for TOML clients, respects root_key
│   │   ├── server_filtering.py # Port discovery + TCP probe (v0.23.0), catalog push, tool filtering
│   │   ├── paths.py            # Canonical path helpers for ~/.unity-biome-mcp layout (v0.70.0: unity_mcp_dir(); v0.96.1: iter_port_files() — yields port files from primary + legacy ~/.unity-mcp/ports/, dedup by filename)
│   │   ├── server_control.py   # Graceful shutdown: list_servers, stop_server SIGTERM/taskkill (v0.55.10)
│   │   ├── lockfile.py         # Cross-platform exclusive locking + zombie detection (v0.23.0); T5: write_lock_metadata/read_lock_metadata for session identity (sessionId, lockToken) on lockfile line 2 (JSON); acquire_lock() gains metadata kwarg
│   │   ├── diagnose.py         # Shared diagnose parser + verdict logic (_parse_diagnose, _verdict, _DiagnoseFields)
│   │   ├── _update_check.py    # Version checker — GitHub releases API (v0.47.1: switched from PyPI), 24h cache, includes --reinstall flag in banner
│   │   ├── compile_state.py    # CompileStateProbe (heuristic Unity compile detection)
│   │   ├── editor_log.py       # High-level Editor.log corroboration: is_compiling_from_log(), dll_info() (148L after v0.80.0 split)
│   │   ├── editor_log_parser.py # Log path discovery + build-failure parsing (get_editor_log_path, parse_cs_errors)
│   │   ├── editor_log_freshness.py # Plugin source discovery + DLL freshness checks — pure stdlib, no intra-package imports (v0.80.0 split, 82L)
│   │   ├── editor_log_wedge.py # WedgeReport dataclass + detect_wedge() — reload-wedge heuristics from log (v0.80.0 split, 155L)
│   │   ├── middleware.py       # 23-layer middleware pipeline (env-gated UNITY_MCP_MIDDLEWARE=1); _play_state_known flag (feat/tool-disambiguation); _alias_cache dict (v0.78.x, cleared on reset_session)
│   │   ├── middleware_alias.py # Pure alias functions: parse_aliases_from_hierarchy, resolve_aliases_in_args, strip_alias_block, parse_aliases_from_get_aliases (v0.78.x); @register_post hooks for alias cache updates (v0.80.0)
│   │   ├── middleware_hooks.py # POST_HOOKS dict + @register_post decorator + run_post_hooks() (v0.80.0: alias extraction moved from inline blocks in pipeline to registered hooks)
│   │   ├── middleware_pipeline.py # wrap_send() full pipeline — pre/post hooks, blast-radius, verification, alias cache (v0.80.0: hooks extracted)
│   │   ├── middleware_async.py # Async/background operations mixin for Middleware
│   │   ├── middleware_reads.py # Read/cache methods mixin for Middleware
│   │   ├── middleware_types.py # WRITE_CMDS/READ_CMDS/_RUNTIME_ONLY_CMDS derived from tool_specs._SPECS via ToolSpec.mutability/runtime_only fields (v0.83.0 — was hardcoded); ACTION_READS: dict[str, frozenset[str]] per-command read-action subsets; is_write(cmd, args) action-aware write check; _EDITOR_READ_ACTIONS = {"state", "project_path"} (v0.78.11: editor dual-use guard)
│   │   ├── middleware_guards.py # check_play_mode_required(): fail-fast guard; _is_batch_readonly(): checks all batch lines are READ_CMDS; editor dual-use: action∈_EDITOR_READ_ACTIONS → read, absent/other → write (v0.78.11); check_blast_radius/check_verification_needed/transition accept args param — read-only batches skip guards (v0.78.10)
│   │   ├── middleware_paths.py # PathResolverMixin extracted from middleware.py
│   │   ├── plugin_api.py      # Stable public API for external plugins (RO, RW, SamplingService, strip_fences)
│   │   ├── unity_state.py      # Unity state file reader
│   │   ├── crash_log.py        # Crash log tracking
│   │   ├── degrade.py          # Graceful degradation
│   │   ├── distiller.py        # Response distillation (Haiku)
│   │   ├── compressor.py       # Response compression
│   │   ├── clarifier.py        # Ambiguity resolution
│   │   ├── errors.py           # Error types (DomainReloadError, etc.)
│   │   ├── hinter.py           # Tool suggestions
│   │   ├── inference.py        # Argument inference from context
│   │   ├── input_normalizer.py # Component/property name normalization
│   │   ├── lessons.py          # Usage pattern learning
│   │   ├── metrics.py          # Performance metrics
│   │   ├── prefetch_cache.py   # Speculative prefetch
│   │   ├── resources.py        # MCP resources
│   │   ├── llm_config.py        # LlmProfile dataclass: universal config for Claude/Codex (v0.23.0 Block 3)
│   │   ├── sampling.py         # Visual verification (DRY: uses get_profile for model selection) (v0.23.0)
│   │   ├── sampling_postproc.py # Sampling post-processing
│   │   ├── scene_brief.py      # Scene context injection
│   │   ├── schema_cache.py     # Schema caching
│   │   ├── schema_guard.py     # Pre-flight argument validation
│   │   ├── speculation.py      # Speculative prefetch layer
│   │   ├── visual_diff.py      # Visual regression diff
│   │   ├── visual_diff_pixel.py # Pixel-level visual diff helpers
│   │   ├── watchdog.py         # Proactive validation watchdog
│   │   ├── constants.py        # Shared constants (DEFAULT_PORT, etc.)
│   │   ├── console_levels.py   # Console log level definitions
│   │   ├── server_lifespan.py  # Server lifespan context manager
│   │   ├── doctor.py           # Doctor diagnostic logic
│   │   ├── doctor_report.py    # Doctor report formatting
│   │   ├── ask/                # ask() tool decomposition
│   │   │   ├── router.py       # Keyword regex router — deterministic 80% of ask() questions
│   │   │   ├── plans.py        # ToolPlan dataclass + canonical plan templates
│   │   │   ├── executor.py     # Runs ToolPlan steps via _send
│   │   │   └── summarizer.py   # Bypass for short results, Haiku for complex
│   │   ├── budget/             # Cost budgeting + adaptive routing for Haiku calls
│   │   │   ├── cost_tracker.py # Track Haiku spend per session + per day, persist to disk
│   │   │   ├── registry.py     # Static feature metadata: priority, difficulty, token estimates
│   │   │   ├── router.py       # Adaptive routing: skip/run based on budget + priority
│   │   │   └── _filelock.py    # Cross-process file lock via fcntl for budget.json
│   │   ├── do_intent/          # do() tool decomposition — NL intent to batch
│   │   │   ├── catalog.py      # Whitelist of allowed commands and signatures
│   │   │   ├── planner.py      # Haiku planner — converts intent to batch DSL
│   │   │   ├── executor.py     # Runs batch + 1 retry on partial failure
│   │   │   ├── prompt.py       # System prompt builder for planner
│   │   │   └── validator.py    # Static plan validation (max lines, forbidden commands)
│   │   ├── reflect/            # Asymmetric reflection: mutation args vs response snapshot
│   │   │   ├── rules_batch.py  # Reflection rules for batch commands
│   │   │   ├── rules_objects.py # Reflection rules for object-mutation commands
│   │   │   └── rules_runtime.py # Reflection rules for runtime/UI mutations
│   │   ├── screenshot_describe/ # Screenshot description via Haiku sampling
│   │   │   ├── describer.py    # Screenshot → text description via SamplingService
│   │   │   ├── cache.py        # Fingerprint-based description cache
│   │   │   └── prompts.py      # Prompt templates per description mode
│   │   ├── som/                # Set-of-Mark visual annotation
│   │   │   ├── overlay.py      # Pillow-based SoM overlay renderer (numbered circles, boxes)
│   │   │   ├── extract.py      # Parse and filter rects from Unity screenshot payload
│   │   │   └── diff_annotate.py # Annotate before/after images with SoM, call sampling
│   │   ├── debug/              # Debug subsystem (v0.59.0: state capture + watch system)
│   │   │   ├── __init__.py
│   │   │   └── snapshots.py    # State capture + diff (snapshot comparison for debugging)
│   │   ├── adapters/           # Multi-provider agent relay (Chat Core, T9-T24)
│   │   │   ├── __init__.py
│   │   │   ├── protocol.py     # Shared protocol defs: EventContext, AcpPayload
│   │   │   ├── acp.py          # AcpAgentAdapter: Claude/OpenCode subprocess in ACP output mode (--format acp)
│   │   │   ├── acp_parser.py   # ACP line parser: timestamp/kind/delta/data extraction
│   │   │   ├── claude_acp.py   # Claude-specific ACP adapter
│   │   │   ├── codex_acp.py    # Codex-specific ACP adapter
│   │   │   └── fixture.py      # FixtureAdapter for testing + deterministic relay validation
│   │   ├── agent_event.py      # AgentEvent canonical envelope: 16+ event kinds, provider-specific filtering, forward-compatible schema
│   │   ├── session_identity.py # SessionIdentity: session_id, lock_token, agent_id, display_name, timestamps
│   │   ├── permission_broker.py # PermissionBroker: per-session MCP tool permission prompts + consent caching
│   │   ├── global_config.py    # GlobalConfig singleton: model presets, backend selection, feature flags
│   │   ├── brief.py            # Brief dataclass: scene context envelope (compile_errors, console, hierarchy, selection, profiler)
│   │   ├── brief_builder.py    # BriefBuilder: on-demand context assembly from scene state + profiler metrics
│   │   ├── changeset.py        # Changeset: atomic multi-command transaction model
│   │   ├── changeset_coordinator.py # ChangesetCoordinator: transaction orchestration + mutation tracking
│   │   ├── changeset_file_capture.py # File snapshot capture for ChangeSet (before/after diff)
│   │   ├── changeset_journal.py # Transaction journal: mutation audit log
│   │   ├── changeset_store.py  # ChangesetStore: persistent transaction storage
│   │   ├── checkpoint.py       # Checkpoint: full scene state snapshot model
│   │   ├── checkpoint_manifest.py # CheckpointManifest: consistency verification (asset/object/component checksums)
│   │   ├── checkpoint_store.py # CheckpointStore: save/load/list checkpoint operations
│   │   ├── checkpoint_restore.py # CheckpointRestore: snapshot rollback with state verification
│   │   ├── history/            # Conversation history management (Chat Core)
│   │   │   ├── __init__.py
│   │   │   ├── models.py       # HistoryEntry dataclass: kind (User/Assistant/Tool), timestamp, metadata
│   │   │   ├── store.py        # HistoryStore: JSONL-based conversation persistence
│   │   │   ├── manager.py      # HistoryManager: store + model lifecycle + retention coordination
│   │   │   └── retention.py    # Retention policies: time-based, count-based, TTL eviction
│   │   ├── tools/              # Tool modules (50 files + __init__, Chat Core: +brief_tool.py, +changeset_tool.py, +checkpoint_tool.py; pipeline-gap sprint: +build.py, +packages.py; playtests ROI sprint: +transaction.py, +verify.py; v0.79.1: -scenarios.py -scene_session.py merged into scene.py; v0.70.0: +console.py, screenshot.py, testing.py, editor_control.py split from scene.py; v0.69.0: +tool_specs.py, _common.py, meta.py; v0.60.0: +profiling.py, rendering.py; v0.62.0: +auto_wire.py, scene_health.py)
│   │   │   ├── __init__.py     # Tool module registry
│   │   │   ├── tool_specs.py   # Single source of truth: ToolSpec dataclass with category/core/tier1/timeout_s/mutability/runtime_only fields (v0.83.0: +mutability: Literal['read','write'], +runtime_only: bool — drives middleware_types derivation); _SPECS dict: 154 entries (148 user-visible + 6 _INTERNAL)
│   │   │   ├── _common.py      # Shared registration helper: bind(module_globals, send, args) for uniform _send/_args binding (v0.69.0)
│   │   │   ├── meta.py         # Meta tools: discover_tools, doctor, resolve_tool_schema, set_llm_config, alias_status in register(mcp, send, args) pattern (v0.69.0, v0.78.9: +alias_status)
│   │   │   ├── profiling.py    # Profile MCP tool: session-based profiling, frame stats, performance analysis (v0.60.0, 412 LOC)
│   │   │   ├── rendering.py    # Render analysis + bake MCP tools: draw calls, batching, lights, LOD culling, bake operations (v0.60.0, +bake pipeline-gap sprint)
│   │   │   ├── auto_wire.py    # Auto-wiring tool: fill ObjectRef fields by semantic name/type matching (v0.62.0)
│   │   │   ├── scene_health.py # Scene health audit: hierarchy depth, naming, duplicates, origins, missing scripts (v0.62.0)
│   │   │   ├── build.py        # BuildPipeline player builder (async via MainThreadDispatcher, pipeline-gap sprint)
│   │   │   ├── packages.py     # PackageManager async operations via EditorApplication.update pump (pipeline-gap sprint)
│   │   │   ├── reload_ladder.py # Reload recovery T0-T5 ladder (MVID-delta healing proof)
│   │   │   ├── transaction.py  # scene_change_plan + apply_scene_change: pre-flight (compile/console/resolve_scene_refs/checkpoint) → plan_id (TTL 600s) → guarded apply with verify + save (playtests ROI sprint)
│   │   │   ├── verify.py       # verify_after_change: 5-gate additive pipeline (await_compile → get_compile_errors → console_since → run_tests_wait → run_playtest_suite); returns PASS or FAIL with skipped gates listed (playtests ROI sprint)
│   │   │   ├── brief_tool.py   # brief MCP tool: on-demand context brief retrieval (compile status, console errors, hierarchy, profiler metrics) (Chat Core)
│   │   │   ├── changeset_tool.py # changeset MCP tool: query atomic transaction history + mutations (Chat Core)
│   │   │   ├── checkpoint_tool.py # checkpoint MCP tool: save/load/list scene checkpoints with manifest validation (Chat Core)
│   │   │   ├── objects.py      # create/delete/find/inspect/set_parent/rename_object/clone_object/set_material; get_component+inspect accept compress=True (v0.78.9)
│   │   │   ├── scene.py        # scene, hierarchy, search + save_session/load_session/screenshot_baseline/screenshot_compare (merged from scene_session.py v0.79.1; multi-scene support)
│   │   │   ├── console.py      # get_console, get_compile_errors split from scene.py (v0.70.0); playtests ROI sprint: +console_mark (timestamp watermark, pure Python), +get_console_since (logs after watermark)
│   │   │   ├── screenshot.py   # screenshot, screenshot_compare split from scene.py (v0.70.0)
│   │   │   ├── testing.py      # durable test request/run protocol; run_tests_wait is the correlated consumer wrapper, while repository workers use run_unity_tests.py
│   │   │   ├── editor_control.py # editor control commands split from scene.py (v0.70.0); pipeline-gap sprint: +paths param for multi-select (comma-sep list)
│   │   │   ├── runtime.py      # invoke_method, wait_until (abort_on_fail), move_to, run_playtest (script= OR path= mutually exclusive, abort_on_fail, defs, snapshot_on_failure; _TCP_POLL/STEP/PLAYTEST_BUFFER constants; module-level SamplingService singleton); playtests ROI sprint: +run_playtest_suite (glob/comma/newline list → SUITE: X/Y matrix, stop_on_fail, stop_after); v0.85.1: -run_playtest_file removed (use run_playtest path=)
│   │   │   ├── batch.py        # batch, references, validate_references + _dsl_tools set; batch accepts validate_aliases=True for dry-run alias check (v0.78.9)
│   │   │   ├── codegen.py      # execute_code, get_schema, auto_fix, smart_build
│   │   │   ├── skills.py       # save/use/list_skill, apply/save/list_template + _skills_dir
│   │   │   ├── spatial.py      # validate_layout, get_spatial_context, scan_scene, check_colliders (path=optional, fixed v0.79.1), spatial_query, objects_in_polygon (v0.46.0: polygon validation + vertices param); navmesh_query +get_settings, +set_settings (pipeline-gap sprint)
│   │   │   ├── ui.py           # create_ui, set_rect, menu, shader
│   │   │   ├── animation.py    # animation, timeline, animator, particle
│   │   │   ├── asset.py        # asset, material, prefab, scriptable_object, project_settings, validate_move (v0.30.4); pipeline-gap sprint: +read_text, +write_text, +reimport, +create AnimatorController/ScriptableObject, +project_settings graphics|audio|input targets, +build_target
│   │   │   ├── connection.py   # list_connections, reconnect_unity
│   │   │   ├── autobatch.py    # setup_objects, set_properties, configure_objects (v0.55.10: _quote_if_spaces, _DOTTED_KV_RE lookahead)
│   │   │   ├── gating.py       # TIER1 + category-based capability filtering (v0.29.37; v0.83.0: _THEMED_CATEGORY_KEYS reduced 18→8 — SCENE/COMPONENTS/ASSETS/MEDIA/VERIFY/RUNTIME/TESTS/SYSTEM; _CATEGORY_ALIAS dict for backward-compat legacy name mapping; register_tools() resolves aliases before populating themed groups; FORCE_VISIBLE removed v0.70.0)
│   │   │   ├── do_tool.py      # NL intent → Haiku plan → batch execute
│   │   │   ├── ask_tool.py     # NL read-only → route → Haiku summarize
│   │   │   ├── ask_user_tool.py # ask_user MCP tool (ask_user AskUserCard routing, v0.29.11)
│   │   │   ├── permission_prompt_tool.py # permission_prompt MCP tool (Claude --permission-prompt-tool routing, v0.29.37)
│   │   │   ├── animator_intent_tool.py  # Domain NL: animator
│   │   │   ├── vfx_intent_tool.py       # Domain NL: VFX/particles
│   │   │   ├── ui_intent_tool.py        # Domain NL: UI
│   │   │   ├── intent_common.py         # Shared intent infrastructure
│   │   │   ├── budget_tool.py           # Haiku spend tracking
│   │   │   ├── metrics_tool.py          # Performance metrics tool
│   │   │   ├── code_intel.py            # compile_preflight, await_compile
│   │   │   ├── debug_tool.py            # Symptom classifier + runtime diagnostic tool (v0.59.0, 278 LOC)
│   │   │   ├── diagnostics.py           # Performance/animator/physics/memory helpers (v0.59.0, 345 LOC)
│   │   │   ├── watch.py                 # Watch system MCP tool interface (v0.59.0, 412 LOC)
│   │   │   └── _annotations.py          # Tool annotations
│   │   └── plugins/            # Plugin system — 3-source auto-discovery (auto-disabled via UNITY_MCP_SKIP_PLUGINS env)
│   │       └── __init__.py     # load_plugins(mcp, send_fn, args_fn), 3-source discovery, UNITY_MCP_SKIP_PLUGINS filtering
│   └── tests/                  # Test suite (see CLAUDE.md Commands section for current count; v0.91.0: +test_surface_parity, test_tools_verify, test_schema_parity, test_registration_parity, test_gating, test_catalog, test_deferred_schema; v0.92.0: +test_result_envelope, test_compile_workflow; playtests ROI sprint: +11 new test files for transaction/verify/watermarks/suite runner/scene refs; v0.78.11: +test_middleware_read_cmds + test_tool_schema_coverage; v0.78.x: +alias middleware tests; v0.77.0: +8 domain test files for tools gap sprint; v0.66.0: +relay/stream_transform tests; v0.59.0: +11 debug tests; v0.26.0 quality audit, v0.30.4: +2 asset validate_move baseline, v0.42.0: +25 config/TOML tests, v0.47.1: +151 config validation tests)
│       ├── helpers.py                  # DRY: make_mock_bridge() + shared test utilities (v0.26.0)
│       ├── test_server*.py             # Core + edge cases + tools
│       ├── test_bridge*.py             # TCP bridge + reconnect + resilience
│       ├── test_reload_ladder.py       # Reload recovery T0-T5 stages + verdict scenarios (20+ tests, v0.27.4)
│       ├── test_middleware*.py          # Middleware layers (god-file split in v0.26.0)
│       ├── test_middleware_alias.py     # middleware_alias pure functions: parse/resolve/strip (v0.78.x)
│       ├── test_middleware_alias_lifecycle.py # Alias cache lifecycle: seeding, reset, Hook 1+2 integration (v0.78.x)
│       ├── test_middleware_read_cmds.py    # READ_CMDS coverage: 59 tests verifying every cmd in READ_CMDS is flagged read-only + editor dual-use action parsing (v0.78.11)
│       ├── test_tool_schema_coverage.py    # FastMCP contract tests: 7 tests verify JSON Schema emitted by FastMCP matches expected params (compress, validate_aliases, alias_status registration, core tool non-empty properties, v0.78.11)
│       ├── test_result_envelope.py         # Result envelope predicates: isSuccess on run_playtest/wait_until/test_step/move_to/ask_user (v0.91.0+v0.92.0)
│       ├── test_compile_workflow.py        # STALE-DOMAIN + MANUAL-REQUIRED compile workflow gate tests (v0.92.0)
│       ├── test_batch*.py              # Batch + conflict + timeout
│       ├── test_config_gaps.py         # Config validation: resolver.py + validator.py + update_check.py + doctor; SERVER_NAME drift guard (v0.71.0) (73+78=151 tests, v0.47.1: GitHub API, git+URL, TOML clients, per-client root_key)
│       ├── test_server_name_consistency.py # Cross-language Python↔C# SERVER_NAME + MCP_BLANKET drift guard (v0.71.0)
│       ├── test_multiscene.py          # Multi-scene CRUD, transfer, diff, bugs (305 tests, v0.24.3)
│       ├── test_transfer_object.py     # transfer_object cross-scene operations (91 tests, v0.24.3)
│       ├── test_*_intent.py            # Intent tools
│       ├── test_sampling*.py           # Visual verification
│       ├── test_visual_*.py            # Visual diff + regression
│       ├── test_budget_*.py            # Budget/cost tracking
│       ├── test_scene_brief*.py        # Scene brief
│       ├── test_screenshot_*.py        # Screenshot features
│       ├── test_update_check.py        # Update checker: GitHub API, version parsing, cache TTL (v0.47.1)
│       ├── test_unstructured_mcp.py    # Guard tests: _UnstructuredMCP forces structured_output=False (4 tests, v0.50.3)
│       ├── test_debug_tool.py           # Debug tool symptom classifier tests (v0.59.0)
│       ├── test_diagnostics.py          # Diagnostics helper tests (v0.59.0)
│       ├── test_snapshots.py            # State capture + diff tests (v0.59.0)
│       ├── test_watch.py                # Watch system tests (v0.59.0)
│       ├── live/conftest.py            # Live test fixtures + _ok/_iid helpers (v0.26.0 DRY); _orphan_guard autouse fixture: root scene-object leak detection + cleanup (v0.80.1)
│       ├── live/test_multiscene_live.py        # Multi-scene live integration (158 tests, v0.24.3)
│       ├── live/test_multiscene_stress_live.py # Stress tests: large scenes, rapid operations (243 tests, v0.24.3)
│       ├── test_region.py               # Region Selection spatial queries + polygon validation (20 tests, v0.46.0)
│       ├── test_read_unity_port.py      # Port discovery waterfall + UNITY_MCP_PROJECT_DIR (7 tests, v0.52.6)
│       ├── test_bridge_port_rediscovery.py # Bridge port pinning + reconnect stability (6 tests, v0.52.6)
│       ├── test_bridge_reload_gate.py      # Reload gate (asyncio.Event): wakes on reconnect, replaces fixed sleep (5 tests, v0.78.5)
│       ├── test_bridge_role.py             # Client role/identification: UNITY_MCP_CLIENT env, set_client_label, RoleToLabel (3 tests, v0.78.5)
│       ├── test_bridge_retry.py            # RetryPolicy unit tests: decide(), allow_hint_retry, is_retry_safe gate (v0.90.0)
│       ├── test_client_hello.py            # T5: client_hello handshake (8 tests): fast-path hello response, fallback to legacy project check, version parsing from hello, backward compat new↔old protocol
│       ├── agent/                          # Chat Core adapter + event tests (T9-T24)
│       │   ├── test_acp_adapter.py         # ACP adapter: --format acp subprocess, event parsing, provider filtering
│       │   ├── test_acp_parser.py          # ACP line parser: timestamp/kind/delta/data extraction
│       │   ├── test_agent_event.py         # AgentEvent envelope: event kinds, schema versioning, forward compat
│       │   ├── test_claude_acp_adapter.py  # Claude ACP specifics
│       │   ├── test_codex_acp_adapter.py   # Codex ACP specifics
│       │   └── test_fixture_adapter.py     # FixtureAdapter: deterministic testing
│       ├── changesets/                     # Chat Core changeset + checkpoint tests
│       │   ├── test_changeset.py           # Changeset transaction model + coordination
│       │   ├── test_changeset_file_capture.py # File snapshot capture + diff
│       │   ├── test_changeset_store.py     # ChangeSet persistence + query
│       │   ├── test_checkpoint.py          # Checkpoint model + manifest
│       │   ├── test_checkpoint_manifest.py # Checksum consistency verification
│       │   ├── test_checkpoint_restore.py  # State rollback + recovery
│       │   ├── test_checkpoint_store.py    # Checkpoint save/load/list
│       │   ├── test_checkpoint_tool.py     # checkpoint MCP tool integration
│       │   ├── test_get_changeset_tool.py  # changeset MCP tool integration
│       │   └── test_session_identity.py    # SessionIdentity: session_id, lock_token, agent_id tracking
│       ├── test_brief.py                   # Brief context envelope model
│       ├── test_brief_builder.py           # BriefBuilder: on-demand context assembly
│       ├── test_brief_tool.py              # brief MCP tool integration
│       ├── test_global_config.py           # GlobalConfig singleton: model presets, backend selection
│       ├── test_history_manager.py         # HistoryManager: store + lifecycle coordination
│       ├── test_history_models.py          # HistoryEntry dataclass + serialization
│       ├── test_history_retention.py       # Retention policies: TTL eviction, count limits
│       ├── test_history_store.py           # JSONL store: persistence + recovery
│       ├── test_permission_broker.py       # PermissionBroker: per-session consent + caching
│       ├── test_chat_relay_v2.py           # Chat relay v2 protocol + schema validation
│       ├── test_connection_status.py       # Semantic connection status: connected/reconnecting/domain-reloading/disconnected (v0.78.10)
│       ├── test_lockfile.py             # PID lockfile + cleanup_stale_port_files (additions, v0.52.6); T5: +3 tests for write_lock_metadata/read_lock_metadata
│       ├── test_server_control.py       # list_servers, stop_server SIGTERM/taskkill (v0.55.10)
│       ├── test_autobatch.py            # _quote_if_spaces, _DOTTED_KV_RE, setup/set/configure_objects (v0.55.10)
│       ├── test_parse_kv.py             # parse_kv quote-strip, lookahead _KV_RE (v0.55.10)
│       ├── test_server_filtering.py     # Port discovery edge cases (v0.55.10)
│       ├── test_auto_wire.py             # auto_wire tool: 3-priority matching, dry-run (v0.62.0, 5 tests)
│       ├── test_scene_health.py          # scene_health tool: 7 checks, focus param (v0.62.0, 4 tests)
│       ├── test_playtest_path.py         # run_playtest path= param: file-based execution, mutual exclusivity, defs combo (v0.79.1)
│       ├── test_server_timeline.py       # M1–M6 timeline actions: reorder_track, duplicate_clip, add/remove_marker, set_track_offset, set_duration, add_sub_track (10 tests)
│       ├── test_server_animator.py       # M7–M10 animator actions: set_state_speed, update_transition, set_avatar, rename_state, rename_param (14 tests)
│       ├── test_server_animation.py      # M11–M14 animation actions: color curves hex, set_wrap, set_framerate, get_clip_path (13 tests)
│       ├── test_server_particle.py       # M16–M17 particle actions: trails module, play/stop/pause (18 tests)
│       ├── test_server_material.py       # Material actions: get_errors, list_shaders, set_fields
│       ├── test_server_shader.py         # ShaderGraphHelper mutations: graph_set_value, graph_connect, graph_add_node, graph_get_layout, graph_set_layout, graph_auto_layout
│       ├── test_server_objects_extra.py  # clone_object action tests
│       ├── test_objects.py               # objects tool: find_type param, IsNullOrEmpty guard (v0.90.0)
│       ├── test_console.py               # console tool: get_console watermark, get_console_since keyword/count_only (v0.90.0)
│       ├── test_vfx_intent.py            # set_vfx_quality action tests
│       ├── test_middleware_play_guard.py  # Play Mode fail-fast guard: state-unknown passthrough, edit-mode block, watch_remove exclusion (feat/tool-disambiguation)
│       ├── test_tool_descriptions.py     # Regression: TIER1 runtime tools have [Play Mode] prefix in docstring (feat/tool-disambiguation)
│       ├── test_docstring_crossrefs.py   # Regression: all 'use `tool`' cross-refs in docstrings name real tools in _SPECS (feat/tool-disambiguation)
│       ├── test_chat_relay.py            # Chat relay deferred spawn, close_stdin, role ping (v0.67.0: +close_stdin test, role in SessionMeta)
│       ├── test_relay_pipeline.py        # Relay pipeline integration + _TRANSFORM_FNS dispatch (v0.67.0)
│       ├── test_relay_monkey.py          # Relay initialization monkey tests (v0.66.0+)
│       ├── test_monkey_relay_stress.py   # Relay stress + chaos tests (v0.66.0+)
│       ├── test_monkey_chat_askmode.py   # Ask mode chat monkey tests (v0.67.0: +output_format mode switch)
│       ├── test_monkey_chat_focus.py     # Chat focus monkey tests (v0.66.0+)
│       ├── test_build_args_contract.py   # Build args protocol validation + UNITY_MCP_PORT env_set (v0.67.0)
│       ├── test_backend_def.py           # 5 backends: output_format/reads_stdin enums + detection (v0.67.0: output_format replaces uses_stream_json, +reads_stdin tests)
│       ├── test_cli_session_spawn.py     # CliSession spawn kwargs characterization (v0.71.0)
│       ├── test_mcp_config_writer.py     # Dynamic MCP config generation (v0.66.0+)
│       ├── test_stream_transform.py      # 5 transform functions: plain-text, codex, kimi, opencode, claude stream-json (v0.67.0: +_transform_plain_text_line, +_transform_codex_line, +_transform_opencode_line, +_transform_kimi_line; 327+ new lines)
│       ├── test_stream_transform_mute.py # Stream transformer muting behavior (v0.66.0+)
│       ├── relay_helpers.py              # DRY relay test utilities (v0.66.0+); live/relay_test_helpers.py (v0.67.0: session spawn helpers)
│       ├── live/test_chat_backends.py    # Live chat backends integration (v0.67.0: +137 tests for 5 backends via relay)
│       ├── live/test_chat_ui_monkey.py   # Chat UI monkey tests with live relay (v0.66.0+)
│       ├── live/chat_ui_helpers.py       # Chat UI test helpers (v0.66.0+)
│       ├── test_editor_ui_styles.py      # Editor UI style validation: USS class coverage, theme parity, Biome component styles (docs-critical-review)
│       ├── conformance/                    # Conformance suite (live TCP tests, marked @pytest.mark.conformance; verifies tool behavior parity across conditions)
│       │   ├── conftest.py                 # Shared conformance fixtures: single-worker setup (UNITY_MCP_PORT)
│       │   ├── workers.py                  # ConformanceWorker class: gate(bridge), prove_absent(bridge) lifecycle management
│       │   ├── test_read_ops.py            # Read-only command verification (get_hierarchy, get_component, etc.)
│       │   ├── test_write_ops.py           # Mutation command verification (create_object, set_property, delete_object)
│       │   ├── test_batch.py               # Batch DSL parsing + multi-command execution
│       │   ├── test_connect.py             # TCP connection establishment + identity
│       │   ├── test_alias_system.py        # VAL/VAR alias expansion + PlaytestConfig injection
│       │   ├── test_playtest_dsl.py        # Playtest runner DSL commands (MOVE, ASSERT, WAIT_UNTIL, CAPTURE_FRAMES)
│       │   └── test_error_recovery.py      # Error handling + partial batch rollback
│       ├── cross_project/                  # Dual-worker isolation tests (marked @pytest.mark.cross_project; requires SECOND_PORT + SECOND_PROJECT_PATH)
│       │   ├── conftest.py                 # Dual-worker fixture: dual_worker_session, conformance_worker; gating on env vars
│       │   ├── workers.py                  # ConformanceWorker reused from conformance/
│       │   ├── test_read_only.py           # ReadOnly MCP mode verification: Worker B blocks mutating commands, allows reads (P-NEW)
│       │   ├── test_isolation.py           # Port + scene namespace isolation between concurrent workers
│       │   ├── test_identity.py            # Worker identification + project path isolation
│       │   ├── test_fault_injection.py     # Chaos testing: network delays, timeout recovery, partial failures
│       │   └── test_workers.py             # Dual-worker coordination + teardown
│       └── ... + domain tests (190+ files total, 1018 @pytest.mark.asyncio removed v0.26.0)
├── unity-plugin-reload/        # Reload Recovery Package (independent compile-unit, v0.27.4)
│   ├── Editor/
│   │   ├── ReloadBinder.cs                   # SO_REUSEADDR bind-retry for port 9600+
│   │   ├── ReloadCommands.cs                 # Public API for recovery tools
│   │   ├── ReloadCompileNotifier.cs          # Domain load completion detector
│   │   ├── ReloadDiagnoseCommand.cs          # TCP diagnose endpoint (portable _parse_diagnose)
│   │   ├── ReloadDomainStamp.cs              # Session-scoped domain timestamp
│   │   ├── ReloadMiniServer.cs               # Mini TCP server (async accept + handler)
│   │   ├── ReloadPlugin.cs                   # Entry point (AssetImportWorker gate)
│   │   ├── ReloadPortResolver.cs             # Atomic Delete+Move port persistence
│   │   └── Tests/                            # 7 NUnit test files (asmdef: UnityMCP.Reload.Tests)
│   │       ├── ReloadCommandsTests.cs
│   │       ├── ReloadCompileNotifierTests.cs
│   │       ├── ReloadDiagnoseTests.cs
│   │       ├── ReloadDomainStampTests.cs
│   │       ├── ReloadMiniServerTests.cs
│   │       ├── ReloadPluginTests.cs
│   │       └── ReloadPortResolverTests.cs
│   ├── UnityMCP.Reload.asmdef                # Core assembly (no references)
│   ├── package.json                          # v0.1.4, "com.unity-biome-mcp.reload"
│   └── package.json.meta
├── unity-plugin/               # Unity Editor plugin; generated inventory totals live in docs/assets/_meta.json
│   ├── ClientSkills/           # Consumer skills shipped with the plugin and installed via Setup Wizard
│   │   ├── agents/             # 4 focused agents: scene editing, C# development, playtesting, diagnostics
│   │   ├── skills/             # 11 folder skills with SKILL.md and optional references/resources
│   │   └── scripts/            # claude_to_codex.py — ownership-checked Claude-to-Codex sync
│   └── Editor/
│       ├── MCPServer.cs                    # Dual TCP listeners (main + chat), port auto-assign, ClientSlot pattern; **T1: ConnectedClientCount** property (sum of active clients from both slots)
│       ├── PortResolver.cs                 # Pure testable port helpers (ResolvePort, FindFreePort, SavePorts, SaveProjectSettings) + 35 tests (v0.35.0: 4-arg chain env→ProjectSettings→Library→FindFreePort); **WI-7**: NEW atomic `BindFreePort(startFrom, skipPort, skipPort2)` for TOCTOU-safe port binding; `FindFreePortExcluding(skip1, skip2)` skips multiple ports; `ResolveReloadPort()` discovers reload port; `TrySaveAllPorts()` writes all 3 ports (main/chat/reload) atomically
│       ├── CommandRouter.cs                # RegisterAll(), guards, core dispatch (partial class)
│       ├── CommandRouter.ObjectHandlers.cs # Object mutation handlers (partial class, 274L after v0.80.0 SRP split); get_aliases command + BuildAliasSection/GetAliasesText; ApplyFieldsCompress(args, result) shared by inspect + get_component (v0.78.x)
│       ├── CommandRouter.AliasHandlers.cs  # Alias-only commands: get_aliases, alias_status, BuildAliasSection (partial class, NEW v0.80.0, 68L)
│       ├── CommandRouter.ScreenshotHandlers.cs # Screenshot dispatch via FileHandler delegate on CommandEntry (partial class, NEW v0.80.0, 72L)
│       ├── CommandRouter.ToolsCache.cs     # get_enabled_tools cache management (partial class, NEW v0.80.0, 42L)
│       ├── CommandRouter.MediaHandlers.cs  # Media/asset handlers (partial class)
│       ├── CommandRouter.Registration.cs   # 4 themed Register methods: RegisterSceneTools, RegisterRuntimeTools, RegisterMetaTools, RegisterEditorTools (partial class, v0.70.0)
│       ├── CommandOptions.cs               # Struct groups trailing Register() params: Mutating/Runtime/Required/Optional/AlwaysAllowed/AllowedDuringCompile/Description/MaxResponseChars (v0.69.0, refactored v0.70.0)
│       ├── CommandRegistry.cs              # Command registration + volatile Ready flag (reset in Clear, set in RegisterAll v0.73.1); CheckGuards returns retry-2000 when !Ready (v0.69.0: CommandOptions overloads)
│       ├── CommandValidator.cs             # Parameter validation via contract at Register() + fuzzy matching
│       ├── IMCPPlugin.cs                   # Plugin interface (Name, CommandPrefix, RegisterCommands, OnDomainReload, GetToolSubcategory, DIMs: Description, HasSettingsUI, BuildSettingsUI)
│       ├── PluginRegistry.cs               # Static plugin registry (Register, RegisterAllPlugins with CallerIsPlugin gate v0.69.0, OnDomainReload, GetCommandsForPlugin, BelongsToPlugin)
│       ├── PendingAskRegistry.cs            # Ask() method + registry for pending asks during domain reload (v0.70.0 refactored)
│       ├── PluginConfig.cs                 # Isolated EditorPrefs storage for plugins (GetString/SetString, GetBool/SetBool, GetInt/SetInt, GetFloat/SetFloat, Delete, namespace: MCPPlugin_{pluginId}_{key}, v0.65.1)
│       ├── PluginUIHelpers.cs              # Convenience UI builders (MakeCard, InlineRow, AddTextField/Toggle/Slider/IntSlider/Dropdown with auto-persist, LoadStyles, v0.65.1)
│       ├── PluginToolGrouping.cs           # Stateless grouping by subcategory (v0.55.10)
│       ├── ObjectManager.cs                # CRUD + Undo + SetActive + WireEvent + SetParent
│       ├── ObjectManager.Properties.cs     # Property setter + auto-redirect (v0.23.0: set_property("active") → SetActive)
│       ├── ObjectManager.Transfer.cs       # Move/copy objects between scenes (v0.24.3: transfer_object)
│       ├── ObjectManager.Lookup.cs         # FindType + short-name fallback for custom components (v0.23.0)
│       ├── SceneContext.cs                 # Multi-scene state centralization: IsMulti, QualifyPath, FilterByScene (v0.24.3)
│       ├── ObjectDiffHelper.cs             # Compact unified-diff format for object comparison (v0.24.3, v0.25.0: Transform properties)
│       ├── ValueParser.cs                  # Parse vectors/quaternions/colors/arrays
│       ├── InputNormalizer.cs              # Auto-fix component/property hallucinations
│       ├── HierarchySerializer.cs          # Scene → text tree + MAX_NODES + summary + incremental
│       ├── ComponentSerializer.cs          # Component → key-value + ObjectReference + UnityEvent
│       ├── ComponentSerializer.Finder.cs   # #instanceID in all path tools (v0.23.0)
│       ├── BatchHelper.cs                  # Batch text parser + per-command guards + timeout; calls AliasExpander.ExpandText per line (v0.78.8)
│       ├── AliasExpander.cs                # C#-side $sigil expansion: ExpandJson (args) + ExpandText (DSL); lazy PlaytestConfig cache, AliasConfigPostprocessor invalidation (v0.78.8); BuildPipePath(QueryAlias): preserves path|component|field for ValPath aliases (v0.78.11)
│       ├── FieldProjector.cs               # Pure static: filter inspect/get_component output to requested fields (v0.78.x, 67 LOC)
│       ├── DefaultStripper.cs              # Pure static: strip default/zero-value lines from component output (v0.78.x, 62 LOC)
│       ├── RefManager.cs                   # Ephemeral &-prefixed base62 scene refs (v1.31.0: & prefix + base62 encoding, backward-compat $ parsing)
│       ├── WirePrefix.cs                    # Wire protocol prefix constants: & (Ref) and $ (Alias) (v1.31.0)
│       ├── ErrorHelper.cs                  # Contextual errors + "did you mean?"
│       ├── RuntimeHelper.cs                # Reflection invoke + state read; method dispatch cache + field(args) syntax (v0.74.0)
│       ├── PlaytestRunner.cs               # DSL playtest executor (partial class, core); abort_on_fail, EvalCompound (v0.74.0); VAR expansion via PlaytestVarRegistry, _cachedConfig (v0.78.x); playtests ROI sprint: suite runner (list_playtest_files), snapshot_on_failure support
│       ├── PlaytestRunner.Steps.cs         # ExecuteStep dispatch (partial class, 23 cases: +Section v0.74.0, +WAIT_CAPTURED, +SWEEP_PATH playtests ROI sprint; +CAPTURE_FRAMES, +ASSERT_CHANGED v0.90.0)
│       ├── PlaytestRunner.Snapshot.cs      # BuildFailureSnapshot(step, config): extracts $sigil names from RawLine, reads current values, appends recent console errors — called when snapshot_on_failure=true (playtests ROI sprint)
│       ├── PlaytestRunner.FrameCapture.cs  # CAPTURE_FRAMES execution: screenshot sequence at fixed intervals, pixel-hash comparison for ASSERT_FRAMES_DIFFER/STATIC (v0.90.0)
│       ├── PlaytestLaunchWindow.cs         # MCP/Playtest Launcher EditorWindow: run .playtest files from Edit/Play Mode, file picker + output log (v0.90.0)
│       ├── PlaytestParser.cs               # DSL parser; MACRO/CALL, MOVE_PATH, SECTION, DESC, AND/OR WAIT_UNTIL (v0.74.0); INCLUDE (Phase -1), VAL (Phase 0.7), VAR, ParseResult, SigilRegex, _DSL_KEYWORDS (v0.78.x); playtests ROI sprint: +WAIT_CAPTURED, +SWEEP_PATH, bool ASSERT, provenance (RawLine per step); v0.90.0: +PATH_PREFIX (Phase 0.7.1), +FOR/$var IN start..end/END_FOR loop unrolling, +CAPTURE_FRAMES, +ASSERT_FRAMES_DIFFER, +ASSERT_FRAMES_STATIC, +ASSERT_CHANGED; v1.4.0: +SplitTokens (bracket/quote-aware tokenizer), +ParseQOV (operator-scan parser for ASSERT/WAIT_UNTIL)
│       ├── PlaytestLinter.cs               # Static DSL linter (no Play Mode): 3-pass (raw scan → parse → semantic), LintFile/LintScript → ERROR/WARN/INFO issues with line numbers (playtests ROI sprint)
│       ├── SceneRefResolver.cs             # Resolves reference tokens ($alias, /path, t:Type) against live scene; ResolveMany(refs, fields) → List<RefResult> (OK/MISS/AMB) (playtests ROI sprint)
│       ├── SceneRefLinter.cs               # 3-pass read-only linter: extracts path tokens from DSL → validates via SceneRefResolver → ERROR/WARN issues (playtests ROI sprint)
│       ├── PlaytestVarRegistry.cs          # Runtime VAR resolve: Register(name, @path|comp|field), ExpandVars(text), ExpandStep(step); ReadValueFn delegate injection for testability (v0.78.x)
│       ├── PlaytestPositionResolver.cs     # Position expression resolver: literal x,y,z or @/GoPath.position [± (dx,dy,dz)]; _findOverride seam for unit tests (v0.78.x)
│       ├── PlaytestState.cs + PlaytestConfig.cs
│       ├── IPlaytestSimulator.cs + IPlaytestMonitor.cs
│       ├── PlaytestMonitorRegistry.cs + SimulatorRegistry.cs  # Playtest type registries
│       ├── VisualStep.cs                   # [Serializable] step data model: 14 fields, Clone() (v0.75.0)
│       ├── ComposerStateStore.cs           # Library/PlaytestComposerState.json persistence + _testOverride seam (v0.75.0)
│       ├── PlaytestDslExporter.cs          # Pure static: List<VisualStep> → DSL string, FromParsed roundtrip (v0.75.0)
│       ├── PlaytestStepElement.cs          # UITK VisualElement per-row step editor, Bind/Unbind (v0.75.0, 282 LOC)
│       ├── PlaytestSmartDrop.cs            # ShowActionMenu: 10 actions via GenericDropdownMenu (v0.75.0)
│       ├── PlaytestDropHelper.cs           # AttachMultiDnD + ShowComponentPicker via GenericDropdownMenu (v0.75.0); GetMemberNames(Type) → public fields+props, GetZeroArgMethodNames(Type) → void methods (v0.77.12)
│       ├── PlaytestStepValidator.cs        # GetValidationError per StepType + IsScriptValid (v0.75.0)
│       ├── PlaytestFileHelper.cs           # Save/Load .playtest files via OS dialog + DSL roundtrip (v0.75.0, 42 LOC)
│       ├── PlaytestComposerWindow.cs       # UI Toolkit EditorWindow (MCP/Playtest Composer, Shift+Alt+P); rewritten from IMGUI v0.75.0
│       ├── PlaytestAliasWindow.cs          # Alias Manager EditorWindow (MCP/Alias Manager, Shift+Alt+A); drag-drop GO → row, Export .defs, Copy VAL block; delegates card rendering to PlaytestAliasCardBuilder (v0.78.x)
│       ├── PlaytestAliasCardBuilder.cs     # Static card builder: per-AliasType card layout (ValPath/ValConst/VarRuntime); cascading comp+field dropdowns, 8px status dot, DnD on path field (v0.77.12, 251 LOC)
│       ├── PlaytestAliasHelpers.cs         # Pure static: FormatLine(alias) typed dispatch, FormatVALBlock, ExportToDefs, TokenSavingsEstimate, SuggestName (v0.78.x)
│       ├── PlaytestAliasWindow.uss         # USS styles for Alias Manager (alias-drop-zone, alias-card--val-path/val-const/var CSS classes) (v0.78.x)
│       ├── MultiViewCapture.cs + MultiViewOverlay.cs + OverlayDrawer.cs  # 4-panel screenshots
│       ├── ScreenshotCapture.cs            # Camera modes: default, overview, multi_view
│       ├── CodeExecutor.cs                 # Roslyn C# execution, 3-layer security (IsAllowedAssembly: private→internal v0.26.0)
│       ├── AutoWiringHelper.cs             # Semantic ObjectReference matching: exact→contains→type (3-priority) (v0.62.0)
│       ├── SceneHealthAnalyzer.cs          # Scene audit: 7 checks (hierarchy, naming, duplicates, origins, missing, empty, disabled) (v0.62.0)
│       ├── SpatialHelper.cs                # Raycast, overlap, nearest, bounds, grid_cast
│       ├── AnimationHelper.cs + AnimationSerializer.cs
│       ├── AnimationCurveCompactor.cs     # Curve optimization: groups .x/.y/.z into vectors, dedup unchanged (v1.31.0)
│       ├── AnimatorControllerHelper.cs + AnimatorControllerSerializer.cs
│       ├── TimelineHelper.cs + TimelineSerializer.cs
│       ├── ParticleHelper.cs + ParticleSerializer.cs  # 10 presets
│       ├── ShaderHelper.cs + ShaderSerializer.cs + ShaderGraphHelper.cs + ShaderGraphHelper.Mutations.cs + ShaderGraphHelper.Layout.cs  # +110 LOC: SetNodeValue, ConnectPorts, AddNode (v0.77.0); +322 LOC: GetLayout, SetLayout, AutoLayout (v1.15.0)
│       ├── UIHelper.cs + LayoutValidator.cs
│       ├── AssetDatabaseHelper.cs + AssetHelper.cs  # pipeline-gap sprint: +read_text, +write_text, +reimport, +create AnimatorController/ScriptableObject
│       ├── BakeHelper.cs                  # Lighting + occlusion bake operations via BakeAsync + MainThreadDispatcher (pipeline-gap sprint)
│       ├── BuildHelper.cs                 # BuildPipeline player builder with target/scenes/path/dev params (pipeline-gap sprint)
│       ├── PackageManagerHelper.cs        # PackageManager async operations via EditorApplication.update pump (pipeline-gap sprint)
│       ├── ReferenceHelper.cs + ValidateReferencesHelper.cs
│       ├── SearchHelper.cs                 # Scene queries + multi-scene scanning (v0.24.3: all-scene support)
│       ├── SceneHelper.cs                  # Scene management: open additive, close, set active, list (v0.24.3)
│       ├── ProjectSettingsHelper.cs + MaterialHelper.cs  # pipeline-gap sprint: ProjectSettings +graphics, +audio, +input targets, +RemoveTag, +SetQualityLevel, +ScriptingBackend with build_target
│       ├── PrefabHelper.cs + ScriptableObjectHelper.cs
│       ├── GameStateHelper.cs + TestRunner.cs # TestRunner v0.25.0: filter param, SessionState-based pending tracking; v0.78.11: TempScenePath internal const + DeleteTempScene (delayCall cleanup after run: replaces active temp scene, deletes asset)
│       ├── ConsoleCapture.cs               # Logs → text (Issue 27: orchestrates ring buffer + problem persistence)
│       ├── ConsoleRingBuffer.cs            # Bounded in-memory log capture: init buffer (50, 5s window) + ring (450 entries)
│       ├── ConsoleProblemPersistence.cs    # SessionState-persisted problem entries (Error/Exception/Assert) across domain reload
│       ├── PrefKeys.cs                     # Central SessionState/EditorPrefs key literals (DRY)
│       ├── ClientConnectionHandler.cs       # Handles TCP client connections (v0.69.0)
│       ├── ConnectionSnapshot.cs            # **T7: NEW** — DormantInfo struct (BridgePid, Kind, Cwd) + DormantBridgeScanner static class (Scan(port, activePids) method, OverrideLockDir test seam). Detects bridge processes holding lock files but not in active TCP slots.
│       ├── ClientSlot.cs                    # Per-connection state + command dispatch (v0.69.0; v1.0.1: LingerOption(true,0) on all close paths → RST not FIN, no TIME_WAIT on Windows); **T1: CountActive()** method — thread-safe snapshot of active connection count; **T3: Per-Entry Metadata** — ClientActivityState enum, ConnectionSnapshot struct (11 fields), per-entry fields + methods (SetEntryEndpoint, SetEntryLabel, BeginCommand, EndCommand, GetEntryLabel, SetEntrySession, TakeSnapshot, DisconnectEntry, SetLastUsefulTicksForTest); **T7: GetActiveSnapshots()** alias for TakeSnapshot() used by MCPStatusWindow to feed hierarchical server list
│       ├── MainThreadDispatcher.cs          # Main-thread work queue for TCP callbacks (v0.69.0)
│       ├── EnvironmentHelper.cs             # Unity environment detection + version checks (v0.69.0)
│       ├── ErrorClassifier.cs               # Categorizes command failures for recovery (v0.69.0)
│       ├── PortFileManager.cs               # Port file lifecycle + atomic writes (v0.69.0; v1.0.1: +SaveRuntimePorts (no MCPSettings.json touch) + CleanStalePeerPortFiles (dead-PID cleanup at startup); **WI-7**: NEW ReloadPort property + EnsureAllPorts() orchestrates 3-port resolution (main/chat/reload); EnsurePorts() caches all three; ReadOnly boolean flag (reads from MCPSettings.json); SavePorts() calls TrySaveAllPorts atomically)
│       ├── ResponseGovernance.cs            # Response size limiting + overflow handling (v0.69.0)
│       ├── ConsoleStackParser.cs            # Console exception stack parsing (v0.69.0)
│       ├── ColliderFitHelper.cs             # Collider bounds fitting helpers (v0.69.0)
│       ├── CompileErrorCapture.cs + CompileNotifier.cs
│       ├── FingerprintHelper.cs + ScanHelper.cs + SceneDiffHelper.cs
│       ├── ChangeWatcher.cs + ColliderChecker.cs + SchemaHelper.cs
│       ├── MCPSettings.cs                 # Pure static data class (catalog, EnabledTools, no EditorWindow)
│       ├── PermissionConfig.cs            # SERVER_NAME + MCP_BLANKET constants (C# side, cross-platform, v0.71.0)
│       ├── CatalogParser.cs               # Plain-text catalog parser (v0.18.0+): "CORE:tool1,tool2\n..." format
│       ├── SettingsNavController.cs       # iOS-style navigational stack + slide animations (v0.23.0 Block 1)
│       ├── SettingsPageFactory.cs         # DRY builder for 5 settings pages (Tools/Permissions/Chat/Sampling/Updates) (v0.23.0 Block 1, v0.42.0: Updates page)
│       ├── MarkdownInlineFormatter.cs      # Pure Markdown→RichText formatter (bold, italic, code, links) (Editor/ base, v0.42.0)
│       ├── LlmConfig.cs                   # [Serializable] universal LLM config (Claude + Codex + Gemini) (v0.23.0 Block 3, backend field v0.30.1)
│       ├── LlmConfigStore.cs              # Load/Save LLM configs to Library/ (v0.23.0 Block 3)
│       ├── SamplingPresets.cs             # Backend + Model preset templates: Claude Fast / Gemini Flash / Codex (v0.30.1)
│       ├── MCPSettingsHub.cs              # Central hub window coordinating all settings UI (F26, v0.23.0)
│       ├── MCPHubUI.cs                    # Hub-level layout + sub-window orchestration (F26, v0.23.0)
│       ├── HubHeaderAnim.cs               # Circuit-node network animation: 5 nodes + lines + packet (F26)
│       ├── HubCardButton.cs               # Launcher card buttons for each settings window (F26)
│       ├── MCPHubDivider.cs               # Visual divider component for hub sections (F26)
│       ├── MCPHub.uss                     # Stylesheet for hub + animation classes `han-*` (F26)
│       ├── MCPToolSettingsWindow.cs       # MCP/Tool Settings window (toggles + presets + plugins)
│       ├── ToolsHeaderAnim.cs             # 5 toggle-sweep animation (400ms) — connection-aware colors (F25)
│       ├── MCPPermissionsWindow.cs        # MCP/Permissions window (deny-set config)
│       ├── PermissionsHeaderAnim.cs       # Shield + lock pulse (150ms) — connection-aware colors (F25)
│       ├── ChatSettingsHook.cs            # Event hook: OnBuildConnection fired when Connection window builds
│       ├── ArcadePalette.cs               # Centralized color constants (Up, Listen, Down, Accent) + StateClass seam (v0.52.0)
│       ├── ArcadeAnim.cs                  # Shared animation primitives: FadeIn, SlideInRight, ShakeX, PulseOnce, CountUp, etc. (v0.52.0)
│       ├── ArcadeAnim.uss                 # Shared USS keyframes + transitions (v0.52.0)
│       ├── SamplingHeaderAnim.cs          # 7-bar frequency analyzer for Sampling page (v0.52.0)
│       ├── EcosystemHeaderAnim.cs         # Header animation for the Biome/Ecosystem settings section (docs-critical-review)
│       ├── StatusAmbientAnim.cs           # Scanline + grid + sonar ring overlay for Status window (v0.52.0)
│       ├── BiomeParticleBurst.cs          # Particle burst effect for Biome UI transitions (docs-critical-review)
│       ├── BiomeToggleGroup.cs            # Toggle group control for Biome settings (docs-critical-review)
│       ├── BiomeUI.cs                     # Biome section UI orchestrator (docs-critical-review)
│       ├── UI/                             # UI design system (v0.55.10)
│       │   └── IconCanvas.cs              # Procedural icon builder (18×18, 2px stroke, theme-agnostic)
│       ├── MCPStatusWindow.cs             # Connection status monitor: server list with Kill buttons, refresh on tick, changelog via MarkdownInlineFormatter (v1.31.0: changelog DRY rendering); **T7: hierarchical server list** — per-port tree view with live TCP connections (via TakeSnapshot()) and dormant bridges (via DormantBridgeScanner.Scan), per-connection metadata rows (kind, state, idle, uptime), kill buttons with multi-bridge confirmation
│       ├── MCPStatus.uss                  # Stylesheet for Status window (T7: +25 lines for connection hierarchy: .server-entry, .server-header, .connection-row, .conn-state, .dormant-section, etc.)
│       ├── McpServerScanner.cs            # Port/lock file scanner: detects alive/phantom servers, CleanPhantomFiles orphan cleanup (v1.31.0); **T1: multi-lock support** — `ScanDetailed()` returns `UnityServerInfo[]` with per-port `McpConnectionInfo[]` list + `LiveTcpCount`; `Scan()` backward-compat wrapper; `FindConnections(port)` enumerates all server-{port}-*.lock files; test seam `OverrideLiveTcpCountGetter`
│       ├── MCPActions.cs                  # Shared actions: KillCurrent/KillAll/KillByPort + multi-bridge TerminateByPid/StopAllOnPort/CountBridgesOnPort (v1.31.0: KillByPort; T2: TerminateResult enum, multi-bridge termination with selective cleanup)
│       ├── MCPStatusModel.cs              # Pure state logic (no deps) — maps connection state → display
│       ├── MCPStatusBarWidget.cs          # Injects MCP pill into AppStatusBar via reflection
│       ├── TestSupport/                   # Test infrastructure base class + attributes (v1.12.0, separate asmdef)
│       │   ├── UnityMCP.Editor.TestSupport.asmdef
│       │   ├── UnityMcpTestBase.cs        # Base test fixture (lifecycle ownership: scene/object/asset cleanup, domain reload state, worker identity). **WI-8**: NEW RequireReadWriteBoundary() in BeginUnityMcpIsolation (after _isolationActive=true); calls EnforceReadWriteRequirement(type, methodName, isReadOnly), returns reason string or null; skips test via Assert.Ignore when attribute present and IsReadOnly=true
│       │   ├── BiomeWorkerOnlyAttribute.cs # [BiomeWorkerOnly("reason")] — NUnit-style reason-required marker for per-test disposable-worker-only execution (no one-time setup/teardown — guard runs pre-fixture in UnityMcpTestBase.SetUp)
│       │   ├── RequiresGraphicsDeviceAttribute.cs # [RequiresGraphicsDevice] — NUnit IApplyToTest skips test if GraphicsDeviceType == Null (headless CI); sets RunState.Ignored with skip reason
│       │   ├── RequiresReadWriteAttribute.cs # **WI-8**: [RequiresReadWrite("reason")] — Marks tests requiring a read-write worker. UnityMcpTestBase.EnforceReadWriteRequirement() checks at SetUp time; returns reason string or null. Tests in read-only workers are skipped via Assert.Ignore when [RequiresReadWrite] present and IsReadOnly=true. Static method enables unit testing of attribute application logic.
│       │   └── SkipOnWindowsAttribute.cs # [SkipOnWindows("reason")] — NUnit IApplyToTest skips test on Windows (RuntimePlatform.WindowsEditor), reason customizable; default is "Known Windows platform incompatibility — fix tracked separately"
│       ├── Tests/                         # Editor tests asmdef (references core, v0.26.0: +[TestFixture] to 6 classes, v0.42.0: Wizard tests moved to separate asmdef, v0.62.0: +helper tests)
│       │   ├── UnityMCP.Editor.Tests.asmdef
│       │   ├── Helpers/                  # Test infrastructure (v0.26.0)
│       │   │   ├── ChipTestBase.cs       # Base class: H() helpers centralized (12 shims extracted, v0.26.0)
│       │   │   └── TestStringHelpers.cs  # CountOccurrences utility (DRY across 4+ files, v0.26.0)
│       │   ├── AutoWiringHelperTests.cs  # ObjectReference matching: 3-priority logic (v0.62.0, 12 tests)
│       │   ├── SceneHealthAnalyzerTests.cs # Scene health audit checks + severity tags (v0.62.0, 14 tests)
│       │   ├── Roslyn/                   # Roslyn analysis tests (v0.62.0)
│       │   │   └── CompilePreflightTests.cs # Dry-run compilation check + diagnostics (v0.62.0, 6 tests)
│       │   ├── MarkdownInlineFormatterTests.cs # Rich-text formatting (bold, italic, code, links) (v0.42.0)
│       │   ├── UpdatesPageTests.cs        # Changelog rendering + update check UI (v0.42.0)
│       │   ├── LevelUpTests.cs            # LevelUp panel state machine, animation, release diff parsing (12 tests, v0.44.0)
│       │   ├── SceneTestBase.cs           # Thin compatibility specialization of UnityMcpTestBase; exposes managed-scene reset without owning lifecycle
│       │   ├── SceneCleanTestBase.cs      # Approved leak-check specialization; common UnityMcpTestBase still owns final scene/object/asset rollback
│       │   ├── MultiSceneTestBase.cs      # Multi-scene specialization; exact additive scenes are registered with the common ownership transaction
│       │   ├── MultiSceneFinderTests.cs   # Object finding across scenes + reference scanning (v0.24.3)
│       │   ├── PortResolverTests.cs       # 25+4 NUnit tests (port validation, fallback, dual-port edge cases, v0.52.6: chat collision guard)
│       │   ├── PortResolverReadOnlyTests.cs # **WI-8**: ReadOnly mode PortResolver tests — BindFreePort atomic binding, ResolveReloadPort discovery (WI-7)
│       │   ├── BatchHelperReadOnlyTests.cs # **WI-8**: Batch DSL read-only mode — verify mutations blocked, reads allowed, verification gates skipped
│       │   ├── CommandRouterReadOnlyTests.cs # **WI-8**: CommandRouter.IsReadOnly property + CheckGuards write-blocking + get_enabled_tools cache behavior in read-only
│       │   ├── RequiresReadWriteAttributeTests.cs # **WI-8**: [RequiresReadWrite] attribute + EnforceReadWriteRequirement() unit tests — reason extraction, type/method reflection, skip predicate
│       │   ├── ClientSlotMetadataTests.cs # 10 NUnit tests (per-entry metadata: ConnectedAt, Label, InFlightCount, ActivityState, LastUsefulAt, EndCommand timestamps, generation stale checks) (T3)
│       │   ├── DormantBridgeScannerTests.cs # 5 NUnit tests (Scan behavior: no files, live PIDs, excluded PIDs, dead PIDs, port filtering) (T7)
│       │   ├── MCPServerStartGuardTests.cs # 3 NUnit tests (ShouldStartServer batch mode guard, v0.52.6)
│       │   ├── MCPStatusModelTests.cs     # 14 NUnit tests (state transitions, labels, pills) [+TestFixture v0.26.0]
│       │   ├── CatalogParserTests.cs      # [+TestFixture v0.26.0]
│       │   ├── JsonHelperTests.cs         # [+TestFixture v0.26.0]
│       │   ├── MCPStatusBarPaletteTests.cs # [+TestFixture v0.26.0]
│       │   ├── ValueParserQuaternionTests.cs # [+TestFixture v0.26.0]
│       │   ├── PluginRegistryTests.cs     # [+TestFixture v0.26.0]
│       │   ├── HubHeaderAnimTests.cs      # 11 NUnit tests (circuit-node animation, packet motion, state logic) (F26)
│       │   ├── HubCardButtonTests.cs      # NUnit tests (card rendering, click behavior) (F26)
│       │   ├── MCPHubDividerTests.cs      # NUnit tests (divider styling, layout) (F26)
│       │   ├── ToolsHeaderAnimTests.cs    # 7 NUnit tests (toggle sweep, color cycling, state logic)
│       │   ├── PermissionsHeaderAnimTests.cs # 7 NUnit tests (shield pulse, state logic)
│       │   ├── ChatHeaderAnimTests.cs     # 7 NUnit tests (wifi arc, state logic)
│       │   ├── ChatSettingsHookEventTests.cs # NUnit tests (event firing, hook execution) (F26)
│       │   ├── CodeExecutorSecurityBypassTests.cs # Security hardening: comment-strip, whitespace densify, blocked patterns (v0.31.0, 15 tests)
│       │   ├── CodeExecutorSecurityTests.cs # Core security + whitespace bypass tests
│       │   ├── CodeExecutorSecurityWhitespaceBypassTests.cs # Whitespace evasion scenarios
│       │   ├── CodeExecutorTransformTests.cs # v1.4.0: brace-depth-aware ReplaceTopLevelReturns (return; in void funcs), using-hoisting regex (lowercase/underscore support)
│       │   ├── ConsoleCaptureTests.cs     # Multi-level console filter + comma-separated levels (v0.31.0)
│       │   ├── ComponentSerializerBracketFinderTests.cs # v1.4.0: bracket-aware path splitting, finding objects with [Zone A/Zone B] names
│       │   ├── ComponentSerializerSpecialCharTests.cs # v1.4.0: round-trip GetPath/FindObject with backslash escaping (/, \), bracket protection, multi-scene paths, Unicode names
│       │   ├── MultiSceneHierarchyTests.cs # Multi-scene hierarchy tests
│       │   ├── MultiSceneOperationsTests.cs # Multi-scene CRUD operations
│       │   ├── MultiSceneFinderTests.cs   # Object finding across scenes (updated v0.31.0)
│       │   ├── SceneContextMultiSceneTests.cs # Scene context multi-scene behavior
│       │   ├── ScenePathParserTests.cs    # Multi-scene path parsing: "SceneName:/" extraction (v0.31.0)
│       │   ├── SetupWizardTests.cs        # Wizard screen navigation + completion flow
│       │   ├── SetupDiagnosticsTests.cs   # Diagnostic checks (Python, imports, TCP)
│       │   ├── DiagnoseCommandTests.cs    # Doctor command + result formatting
│       │   ├── WizardAnimUtilsTests.cs    # Animation timing + interpolation
│       │   ├── InstallSourceDetectorTests.cs # 8 NUnit tests (file:/git: detection, PackageInfo parsing) (v0.45.0)
│       │   ├── LocalPluginUpdaterTests.cs # 6 NUnit tests (git pull --tags, Task.Run async, tag matching) (v0.45.0)
│       │   ├── UpmPluginUpdaterTests.cs   # 2 NUnit tests (Client.Add chain, both packages) (v0.45.0)
│       │   ├── ChatMcpConfigWriterTests.cs # 4 NUnit tests (uvx fallback for git: installs) (v0.45.0)
│       │   ├── ArcadePaletteTests.cs      # 7 NUnit tests (color constants, StateClass) (v0.52.0)
│       │   ├── ArcadeAnimTests.cs         # 6 NUnit tests (animation primitives, class toggles) (v0.52.0)
│       │   ├── SamplingHeaderAnimTests.cs # 3 NUnit tests (frequency bar animation) (v0.52.0)
│       │   ├── StatusAmbientAnimTests.cs  # 5 NUnit tests (scanline + grid + sonar effects) (v0.52.0)
│       │   ├── BiomeParticleBurstTests.cs # NUnit tests for BiomeParticleBurst effect (docs-critical-review)
│       │   ├── WizardStepAnimTests.cs     # 5 NUnit tests (slide transitions, progress bar) (v0.52.0)
│       │   ├── AnnotationIconsTests.cs    # Icon rendering tests (v0.55.10)
│       │   ├── IconCanvasTests.cs         # Canvas drawing API, rasterization tests (v0.55.10)
│       │   ├── PluginToolGroupingTests.cs # Subcategory grouping tests (v0.55.10)
│       │   ├── PluginSubcategorySettingsTests.cs # Subcategory discovery + filtering tests (v0.55.10)
│       │   ├── ValueParserTests.cs        # Quote-strip, spaces in values tests (v0.55.10)
│       │   ├── BatchHelperParserTests.cs  # Lookahead parsing tests (v0.55.10)
│       │   ├── ObjectManagerTests.cs      # SafeGetTypes, TypeCache, custom namespace tests (v0.55.10)
│       │   ├── PluginDisabledToolsTests.cs # Per-tool gating tests (v0.55.10)
│       │   ├── PluginRegistryTests.cs     # Plugin registry tests (v0.55.10)
│       │   ├── PluginConfigTests.cs       # Isolated EditorPrefs storage tests (Get/Set String/Bool/Int/Float, Delete, namespacing, v0.65.1, 9 tests)
│       │   ├── PluginUIHelpersTests.cs    # Convenience UI builder tests (MakeCard, InlineRow, Add* controls, auto-persist, LoadStyles, v0.65.1, 20 tests)
│       │   ├── PluginSettingsPageTests.cs # Plugin UI registration + settings page rendering (v0.64.0, 29 tests)
│       │   ├── CommandRouterExtractHelperTests.cs # Extract helper unit tests (v0.70.0)
│       │   ├── CommandRouterRegistrationTests.cs # Registration method tests (v0.70.0)
│       │   ├── BatchRejectionTests.cs            # Batch async/specialDispatch rejection + runtime guard + atomic rollback (feat/tool-disambiguation, 5 tests)
│       │   ├── PlaytestPathTests.cs              # run_playtest path= param: file read, traversal guard, mutual exclusivity (v0.79.1)
│       │   ├── PlaytestForLoopTests.cs           # FOR $var IN start..end / END_FOR: range expansion, nesting, max-iterations guard (v0.90.0)
│       │   ├── PlaytestFrameCaptureTests.cs      # CAPTURE_FRAMES parser + ASSERT_FRAMES_DIFFER / ASSERT_FRAMES_STATIC (v0.90.0)
│       │   ├── PlaytestPathPrefixTests.cs        # PATH_PREFIX directive: prefix applied to VAL path values, first-occurrence-wins (v0.90.0)
│       │   ├── PlaytestCaptureStringTests.cs     # CAPTURE / ASSERT_CHANGED step types (v0.90.0)
│       │   ├── Sprint3FrictionTests.cs           # Integration tests for v0.90.0 friction sprint DSL features (v0.90.0)
│       │   ├── RuntimeHelperInvokeTests.cs       # RuntimeHelper reflection invoke: private/static methods, field(args) syntax (v0.90.0)
│       │   ├── PlaytestDslExtensionTests.cs      # SECTION/DESC/MOVE_PATH/AS/abort-on-fail DSL integration (v0.74.0)
│       │   ├── PlaytestMacroTests.cs             # MACRO/CALL expansion, recursion, param substitution (v0.74.0)
│       │   ├── WaitConditionTests.cs             # AND/OR compound conditions + EvalCompound unit tests (v0.74.0)
│       │   ├── PlaytestRunnerTests.cs            # Runner integration tests (+74 lines, v0.74.0; playtests ROI sprint: suite runner + snapshot support)
│       │   ├── PlaytestSnapshotTests.cs          # BuildFailureSnapshot: sigil extraction, value capture, console append (211 tests, playtests ROI sprint)
│       │   ├── PlaytestLintTests.cs              # PlaytestLinter 3-pass: raw scan, parse warnings, semantic checks (95 tests, playtests ROI sprint)
│       │   ├── PlaytestProvenanceTests.cs        # RawLine provenance tracking in ParseResult + PlaytestStep (208 tests, playtests ROI sprint)
│       │   ├── PlaytestDslExtensionTests.cs      # WAIT_CAPTURED + SWEEP_PATH + bool ASSERT DSL extensions (251 tests, playtests ROI sprint; previously v0.74.0 SECTION/DESC)
│       │   ├── PlaytestAliasDefsTests.cs         # export/sync/validate_playtest_aliases round-trip (212 tests, playtests ROI sprint)
│       │   ├── SceneRefResolverTests.cs          # ResolveMany: OK/MISS/AMB, field validation, multi-ref (106 tests, playtests ROI sprint)
│       │   ├── SceneRefLinterTests.cs            # 3-pass DSL linting against live scene refs (93 tests, playtests ROI sprint)
│       │   ├── McpStatusCommandTests.cs          # mcp_status command registration + output format (35 tests, playtests ROI sprint)
│       │   ├── PlaytestComposerTests.cs          # Composer window state + step lifecycle (~116 tests, v0.75.0)
│       │   ├── ComposerStateStoreTests.cs        # State persistence to Library/ + path override (~98 tests, v0.75.0)
│       │   ├── PlaytestDropHelperTests.cs        # Multi-drop + component/field/method pickers (~225 tests, v0.75.0)
│       │   ├── PlaytestDslExporterTests.cs       # All 17 StepTypes + Export + FromParsed roundtrip (~412 tests, v0.75.0)
│       │   ├── PlaytestStepValidatorTests.cs     # Per-type validation error rules (~305 tests, v0.75.0)
│       │   ├── PlaytestVarTests.cs               # PlaytestVarRegistry: Register, ExpandVars, ExpandStep, unknown sigil passthrough (v0.78.x)
│       │   ├── PlaytestValComboTests.cs          # VAL+VAR+INCLUDE combos, circular ref detection (v0.78.x)
│       │   ├── PlaytestValEdgeCaseTests.cs       # VAL edge cases: whitespace, duplicates, keyword injection guard (v0.78.x)
│       │   ├── PlaytestPositionResolverTests.cs  # Literal, @-ref, offset +/-, missing object error (v0.78.x)
│       │   ├── PlaytestAliasGridTestTests.cs     # AliasWindow row CRUD, token label, DSL preview (v0.78.x)
│       │   ├── PlaytestAliasModularityTests.cs   # FormatVALLine/Block, ExportToDefs, SuggestName purity (v0.78.x)
│       │   ├── PlaytestAliasRealWorldTests.cs    # End-to-end alias → DSL → run_playtest round-trip (v0.78.x)
│       │   ├── PlaytestAliasStressTests.cs       # 100+ aliases, token savings boundary cases (v0.78.x)
│       │   ├── GetAliasesTests.cs                # get_aliases command: C# handler + name=value line format (v0.78.x)
│       │   ├── GetAliasesTypedTests.cs           # BuildAliasSection typed behavior: ValPath emits pipes, ValConst emits literal, VarRuntime skipped (v0.77.12)
│       │   ├── PlaytestAliasTestHelpers.cs       # Shared test fixtures for alias test files (v0.78.x)
│       │   ├── PlaytestAliasWindowTests.cs       # PlaytestAliasWindow UIElements: add/remove/rename, Export, Copy (v0.78.x)
│       │   ├── PlaytestAliasHelperTypedTests.cs  # FormatLine type dispatch: ValPath pipes, ValConst no-pipe literal, VarRuntime @ prefix (v0.77.12)
│       │   ├── PlaytestDropHelperMemberTests.cs  # GetMemberNames + GetZeroArgMethodNames: fields, props, methods reflection (v0.77.12)
│       │   ├── AliasExpanderTests.cs             # AliasExpander unit tests: ExpandJson escaping, ExpandText passthrough, unknown sigil intact, _tableOverride seam (v0.78.8); IsStale true/false scenarios (v0.78.9); +13 pipe tests: ValPath preserves component|field, ValConst no-pipe, unknown intact (v0.78.11)
│       │   ├── AliasStatusTests.cs               # alias_status command: IsStale tracking, loaded/empty/stale states, ExecAliasStatus registration (v0.78.9)
│       │   ├── PlaytestGlobalAliasTests.cs       # PlaytestConfig alias auto-injection: VAL block injected before user script, INCLUDE overrides, empty config no-op (v0.78.9); +6 tests: ValPath pipe format preserved in injection (v0.78.11)
│       ├── Wizard/                        # Setup Wizard + Auto-Config + Diagnostics (v0.38.0+, v0.68.0: ProjectConfigWriter auto-config, v0.42.0: 3-screen flow, 9 backends, asmdef split; v0.47.1: AiConfigScreen fallback, removed dead screens)
│       │   ├── ProjectConfigWriter.cs     # [InitializeOnLoad] auto-config orchestrator: discovers port, version, writes per-project MCP configs for all targets (v0.68.0)
│       │   ├── ProjectConfigFormats.cs    # Format registry: JSON, TOML, extensible (v0.68.0)
│       │   ├── ProjectConfigToml.cs       # TOML parsing + merging for Codex config (v0.68.0)
│       │   ├── ProjectConfigTargets.cs    # 6 AI tool targets: Claude Code, Codex, Cursor, Windsurf, VS Code, Claude Desktop (v0.68.0)
│       │   ├── GitignorePatcher.cs        # Append per-project config paths to .gitignore (idempotent, v0.68.0)
│       │   ├── SetupWizard.cs             # Auto-launch on first run, 3 screens (Welcome → PickBackend → Configure)
│       │   ├── SetupWizard.uss            # Wizard stylesheet (layout, animations)
│       │   ├── WizardScreen.cs            # Base class for wizard screens (lifecycle, navigation)
│       │   ├── WizardScreenHost.cs        # Screen container + animation orchestrator; 4 screens (updated v0.92.x: +InstallSkillsScreen)
│       │   ├── WizardAnimUtils.cs         # Reusable animation helpers (delegates to ArcadeAnim, v0.52.0)
│       │   ├── WizardStepAnim.cs          # Slide transitions + progress bar for Setup Wizard (v0.52.0)
│       │   ├── WizardAmbientAnim.cs       # Ambient background animation for Wizard screens (docs-critical-review)
│       │   ├── WizardUI.cs                # Wizard layout helpers + shared UI utilities (docs-critical-review)
│       │   ├── SetupDiagnostics.cs        # Python/TCP/config diagnostic checks + per-tool AI config validation (v0.47.1; v1.0.1: +CheckUv() uvx PATH probe with install hint, +GetPythonVersion() with 3s timeout+caching, +WhichUvx() inline to avoid Chat.CLI cyclic dep, +IsVersionAtLeast())
│       │   ├── BackendDescriptor.cs       # 9 backend definitions + IsDetected logic (BinaryName + ConfigDir); platform-aware root_key (v0.47.1)
│       │   ├── AiToolCardFactory.cs       # Reusable backend/tool card builder + platform-aware path methods (v0.47.1)
│       │   ├── SkillsInstaller.cs         # Transactional ClientSkills install, legacy migration, conflict/ownership safety
│       │   ├── Screens/                   # Screen implementations (5 total: Welcome → PickBackend → AiConfig → Configure → InstallSkills)
│       │   │   ├── WelcomeScreen.cs       # Introduction + system checks (Python found, TCP available)
│       │   │   ├── AiConfigScreen.cs      # AI tool configuration cards + fallback JSON export for UPM installs (v0.47.1, new)
│       │   │   ├── ConfigureScreen.cs     # Per-backend selection; uses GitInstallUrl constant (v0.47.1; v1.0.1: scope toggle removed — no port baked, no scope distinction)
│       │   │   ├── InstallSkillsScreen.cs # AI skills install UI; optional Codex sync; completion marker written last
│       │   │   └── PickBackendScreen.cs   # 9 backend cards (Claude Code, Desktop, Cursor, Windsurf, VS Code, Codex, Kimi, OpenCode, Antigravity)
│       │   ├── Tests/                     # Wizard assembly tests (separate asmdef)
│       │   │   ├── UnityMCP.Editor.Wizard.Tests.asmdef
│       │   │   ├── BackendDescriptorTests.cs
│       │   │   ├── ConfigureScreenTests.cs
│       │   │   ├── PickBackendScreenTests.cs
│       │   │   ├── WizardConfigWriterTests.cs # Config backup/restore, merge safety, GitInstallUrl constant (9+8=17 tests, v0.44.0-v0.47.1; v1.0.1: port-baking-removed assertions)
│       │   │   ├── AiToolCardFactoryTests.cs # Platform path methods + card rendering (20 tests, v0.47.1)
│       │   │   ├── ProjectConfigWriterTests.cs # Auto-config logic: port discovery, version, multi-target writes (v0.68.0)
│       │   │   ├── ProjectConfigFormatsTests.cs # Format registry + serialization (v0.68.0)
│       │   │   ├── ProjectConfigTomlTests.cs # TOML parsing edge cases (v0.68.0)
│       │   │   ├── ProjectConfigTargetsTests.cs # Target definitions + path rendering (v0.68.0)
│       │   │   ├── GitignorePatcherTests.cs # Append safety, idempotency, no-duplicates (v0.68.0)
│       │   │   ├── SkillsInstallerTests.cs  # 26 NUnit tests: resources, upgrades, rollback, conflicts, path safety
│       │   │   └── ... (14+ test files total)
│       │   ├── UnityMCP.Editor.Wizard.asmdef # Separate compile unit, references core Editor asmdef
│       │   └── WizardAssemblyInfo.cs      # AssemblyVersion + InternalsVisibleTo
│       ├── Profiling/                      # Profiling & Performance Analysis (v0.60.0: 6 C# files; v0.61.0: +UI folder with 10 files)
│       │   ├── FrameSample.cs              # Single frame sample data structure (fps, cpu, gpu ms, draw calls, etc.)
│       │   ├── ProfilerBridge.cs           # Lazy-init profiler access via ProfileRecorder + EditorApplication.update
│       │   ├── FrameRingBuffer.cs          # Circular 600-frame buffer (~10s at 60fps), CopyTo() method for zero-alloc export (v0.61.0)
│       │   ├── ProfileAnalyzer.cs          # Statistics computation: FPS avg/min/max/P99, compare (STABLE/IMPROVED/REGRESSED)
│       │   ├── ProfileFormatter.cs         # Human-readable profile output formatting
│       │   ├── ProfileRecorder.cs          # Record session lifecycle manager
│       │   └── UI/                         # Profiling UI Components (v0.61.0: 10 C# files)
│       │       ├── PerfWindow.cs           # Main EditorWindow: 4-tab interface (Performance, Rendering, Sessions, Memory)
│       │       ├── PerfWindow.Performance.cs # Performance tab: FPS graph, CPU/GPU bars, frame stats (partial class)
│       │       ├── PerfWindow.Rendering.cs # Rendering tab: snapshot stats, baseline compare (partial class)
│       │       ├── PerfWindow.Sessions.cs  # Sessions tab: session list, verdict badges, auto-capture (partial class)
│       │       ├── PerfWindow.Memory.cs    # Memory tab: Mono heap, GC Gen0, texture memory (partial class)
│       │       ├── PerfOverlay.cs          # SceneView UITK overlay: FPS sparkline, CPU/GPU, draw calls (5Hz refresh)
│       │       ├── PerfGraphElement.cs     # Reusable UITK VisualElement for line+fill graphs via Painter2D
│       │       ├── PerfThresholds.cs       # Color band classification: good/warn/crit thresholds + Color32.Lerp gradients
│       │       ├── AnimatedCounter.cs      # Label subclass: exponential ease lerp to target value (0.3s)
│       │       └── RecordIndicator.cs      # Pure USS pulsing red dot animation for recording state
│       ├── SerializedFieldRenameAudit.cs   # YAML scan of prefabs/scenes/SOs for stale field data after rename without [FormerlySerializedAs] (v0.92.0)
│       ├── Roslyn/                         # Roslyn-based C# analysis (v0.62.0: 4 files)
│       │   ├── RoslynLoader.cs             # Reflection-based Roslyn assembly discovery (mscorlib, UnityEngine)
│       │   ├── RoslynWorkspace.cs          # SyntaxTree → Compilation → Diagnostics pipeline
│       │   ├── RoslynFormat.cs             # OK/ERR formatter for compile_preflight results
│       │   ├── CompilePreflightCommand.cs  # Dry-run compilation check handler
│       │   └── UnityPreflightHints.cs      # Static analyzer: serialized Dictionary, non-serializable types, renamed fields without FormerlySerializedAs (v0.92.0)
│       ├── Rendering/                      # Rendering Analysis & Optimization (v0.60.0: 6 C# files + 3 partials)
│       │   ├── RenderAnalyzer.cs           # Entry point: dispatch to analysis actions (stats, overdraw, materials, etc.)
│       │   ├── RenderAnalyzer.Materials.cs # Material/texture dedup & compression audit (partial)
│       │   ├── RenderAnalyzer.Batching.cs  # SRP batcher compatibility + GPU instancing candidates (partial)
│       │   ├── RenderAnalyzer.Lights.cs    # Light categorization + shadow + probe audit (partial)
│       │   ├── RenderPipelineInspector.cs  # Runtime SRP detection + capability checks
│       │   ├── FrameDebugHelper.cs         # Frame debugger reflection-based capture
│       │   ├── LodCullingAnalyzer.cs       # LOD group analysis + occlusion culling detection
│       │   └── MaterialAuditHelper.cs      # Material/texture memory profiling
│       ├── Debug/                         # Debug UI Panel + Watch System (v0.59.0: 11 C# files, 44 tests)
│       │   ├── MCPDebugPanel.cs            # EditorWindow entry point (v0.59.0)
│       │   ├── MCPDebugUI.cs               # Core debug UI orchestrator (v0.59.0)
│       │   ├── MCPDebugUI.WatchRows.cs     # Watch list rendering (partial class, v0.59.0)
│       │   ├── MCPDebugUI.EvalBar.cs       # Expression evaluator UI (partial class, v0.59.0)
│       │   ├── MCPDebugUI.ConsolePreview.cs # Console output preview (partial class, v0.59.0)
│       │   ├── MCPDebugUI.AddWatch.cs      # Add watch dialog (partial class, v0.59.0)
│       │   ├── DebugOverlayDrawer.cs       # Scene view debug labels + sparklines (v0.59.0)
│       │   ├── WatchEntry.cs               # Single watch data structure (v0.59.0)
│       │   ├── WatchCondition.cs           # Conditional breakpoint/eval (v0.59.0)
│       │   ├── WatchEvaluator.cs           # Watch expression evaluation engine (v0.59.0)
│       │   ├── WatchRegistry.cs            # Watch storage + lifecycle (v0.59.0)
│       │   ├── WatchScheduler.cs           # Watch polling scheduler (v0.59.0)
│       │   ├── WatchCommandHandler.cs      # Handle watch commands from Python (v0.59.0)
│       │   ├── SparklineHelper.cs          # Mini performance graphs (v0.59.0)
│       │   ├── ProfilerHelper.cs           # Profiler data access helpers (v0.59.0)
│       │   ├── MemoryHelper.cs             # Memory tracking + GC integration (v0.59.0)
│       │   ├── PhysicsHelper.cs            # Physics diagnostics (raycasts, colliders) (v0.59.0)
│       │   ├── AnimatorHelper.cs           # Animator state inspection (v0.59.0)
│       │   ├── MCPDebug.uss                # Debug UI stylesheet (v0.59.0)
│       │   └── Tests/                      # 44 NUnit tests (v0.59.0)
│       │       ├── MCPDebugUITests.cs
│       │       ├── WatchEntryTests.cs
│       │       ├── WatchConditionTests.cs
│       │       ├── WatchEvaluatorTests.cs
│       │       ├── WatchRegistryTests.cs
│       │       ├── WatchSchedulerTests.cs
│       │       ├── WatchCommandHandlerTests.cs
│       │       ├── SparklineHelperTests.cs
│       │       ├── ProfilerHelperTests.cs
│       │       ├── MemoryHelperTests.cs
│       │       ├── PhysicsHelperTests.cs
│       │       └── AnimatorHelperTests.cs
│       ├── RegionTool/                     # Region Selection + Scene Annotations (v0.46.0, v0.51.0: annotations, 171 C# tests)
│       │   ├── Polygon2D.cs                 # Immutable 2D polygon, winding-number PIP, AABB bounds, CSV import/export, RDP simplify
│       │   ├── SceneRegionTool.cs           # EditorTool: multi-mode FSM (Lasso/Rect/Circle/PbP), keyboard shortcuts, state machine
│       │   ├── SceneRegionQuery.cs          # 3-stage spatial pipeline: AABB filter → component filter → PIP → cap (v0.51.0: +FindNearPolyline)
│       │   ├── SceneRegionState.cs          # LRU registry (8 slots) + EditorPrefs persistence, ToPolygon2D() factory
│       │   ├── RegionSnapshot.cs            # Data record: polygon vertices, region ID, matched GameObjects (v0.51.0: +AnnotationType, +Label, +LengthOrDistance, factory methods)
│       │   ├── SceneRegionOverlay.cs        # UIToolkit overlay for UI elements (mode display, settings)
│       │   ├── SceneAnnotationOverlay.cs    # UIToolkit overlay for annotation tools (v0.51.0)
│       │   ├── SceneAnnotationTool.cs       # Unified EditorTool entry point for all annotation modes (Shift+A, v0.51.0)
│       │   ├── SceneAnnotationShortcut.cs   # Hotkey wiring for annotation modes (Shift+A, mode switches, v0.51.0)
│       │   ├── SceneAnnotationUtils.cs      # Common validation, snapping, formatting utilities (v0.51.0)
│       │   ├── PolygonDetail.cs             # Detail level enum (High/Medium/Low) + RDP thresholds
│       │   ├── PolygonDetailSettings.cs     # EditorPrefs toggle for detail level
│       │   ├── GdSnapshotSerializer.cs      # RegionSnapshot → VAL $label lines for playtest preamble (v0.74.0; updated v0.92.x: ALIAS→VAL format)
│       │   ├── Drawing/                     # Drawing mode implementations (IDrawingMode + IAnnotationMode v0.51.0)
│       │   │   ├── IDrawingMode.cs          # Interface: Begin, Update, Finalize, IsActive, IsComplete, PreviewVertices (v0.51.0: +IAnnotationMode)
│       │   │   ├── DrawingUtils.cs          # Grid snap, point distance calculation
│       │   │   ├── LassoMode.cs             # Free-form drawing (mouse track on drag)
│       │   │   ├── RectangleMode.cs         # Orthogonal box (mouse start → end)
│       │   │   ├── CircleMode.cs            # Circle (center + radius via mouse distance)
│       │   │   ├── PointByPointMode.cs      # Manual vertex click (double-click or Enter to finish)
│       │   │   ├── PointMode.cs             # Single-point annotation with optional label (v0.51.0)
│       │   │   ├── PolylineMode.cs          # Polyline drawing with auto-length calculation (v0.51.0)
│       │   │   └── MeasurementMode.cs       # Distance measurement annotation (v0.51.0)
│       │   ├── Rendering/                   # Rendering pipeline
│       │   │   ├── RegionRenderer.cs        # GL wireframe + fill + annotation overlays, depth-tested (v0.46.0, v0.51.0: +DrawAnnotation)
│       │   │   ├── RenderStyle.cs           # Color, alpha, line width configuration
│       │   │   ├── RenderState.cs           # Active/Preview/Committed polygon states (v0.51.0: +3 annotation fields)
│       │   │   └── RegionIcons.cs           # Procedural Painter2D vector icons for tool palette + overlay (v0.46.0, 128 LOC)
│       │   └── Tests/                       # 171+ NUnit tests (v0.46.0: 104 + v0.51.0: 67 + v0.74.0: GdSnapshotSerializer)
│       │       ├── Drawing/
│       │       │   ├── LassoModeTests.cs
│       │       │   ├── RectangleModeTests.cs
│       │       │   ├── CircleModeTests.cs
│       │       │   ├── PointByPointModeTests.cs
│       │       │   ├── PolygonDetailTests.cs
│       │       │   ├── DrawingModeFactoryTests.cs
│       │       │   └── AnnotationDrawingModeTests.cs (v0.51.0: 23 tests for Point/Polyline/Measurement)
│       │       ├── Rendering/
│       │       │   ├── RegionRendererTests.cs
│       │       │   └── RenderStateAnnotationTests.cs (v0.51.0)
│       │       ├── RegionSnapshotAnnotationTests.cs (v0.51.0: 27 tests for factory methods + type-specific ShortLabel)
│       │       └── GdSnapshotSerializerTests.cs (v0.74.0: serializer label/type tests)
│       ├── Chat/CLI/RegionChipProvider.cs   # Region + annotation chip provider for chat (v0.46.0, v0.51.0: +3 format methods)
│       ├── Chat/CLI/ComponentChipProvider.cs # Component field chip provider for chat context (v0.59.0)
│       ├── Chat/CLI/ChipPropertyFormatter.cs # DRY component property formatting (v0.59.0)
│       ├── Chat/Tests/CLI/RegionChipProviderTests.cs # Chip provider tests
│       ├── Chat/Tests/CLI/RegionChipProviderAnnotationTests.cs # Annotation-specific chip provider tests (v0.51.0: 17 tests)
│       ├── Chat/Tests/CLI/PropertyContextMenuBridgeTests.cs # Property context menu tests (v0.59.0)
│       ├── Chat/Tests/CLI/SceneRegionStateTests.cs # State persistence tests
│       ├── Chat/View/PropertyContextMenuBridge.cs # Right-click property menu integration (v0.59.0)
│       ├── Chat/View/MCPChatWindow.DebugContext.cs # Debug context injection in chat (v0.59.0)
│       ├── Updates/                       # Update checking + changelog display (v0.42.0, v0.44.0: LevelUp UX, v0.45.0: install-source detection)
│       │   ├── ChangelogReader.cs         # Parse CHANGELOG.md entries (version, date, content)
│       │   ├── UpdateBanner.cs            # Update notification banner UI (DRY RepoGitUrl constant, v0.44.0)
│       │   ├── UpdateChecker.cs           # PyPI version check (RepoGitUrl constant, v0.44.0)
│       │   ├── UpdatesPage.cs             # Hub page: Check button + changelog (v0.44.0: uses LevelUpPanel)
│       │   ├── LevelUpPanel.cs            # 4-state machine: Idle→Animating→Done→Diff (v0.44.0)
│       │   ├── LevelUpAnimator.cs         # XP bar + sparkles animation (v0.44.0)
│       │   ├── ReleaseDiff.cs             # Parse CHANGELOG.md for release notes (v0.44.0)
│       │   ├── LevelUpAnim.uss            # Animation stylesheet (v0.44.0)
│       │   ├── InstallSourceDetector.cs   # Detect file: (local) vs git: (registry) via PackageInfo.source (v0.45.0)
│       │   ├── LocalPluginUpdater.cs      # git pull --tags for file: installs via Task.Run (v0.45.0)
│       │   ├── UpmPluginUpdater.cs        # Client.Add chain for both packages on git: update (v0.45.0)
│       │   ├── UpdateDispatcher.cs        # DRY routing replaces copy-paste in LevelUpPanel/UpdateBanner (v0.45.0)
│       │   └── Tests/
│       ├── MCPDiagnosePanel.cs            # Unified diagnostics panel (moved to Wizard/ with other windows)
│       ├── MCPDiagnoseWindow.cs           # Diagnostics UI window (moved to Wizard/)
│       ├── Chat/                          # In-Unity Agent Chat (v0.29.2: split into CLI + View assemblies)
│       │   ├── Mentions/                     # @Mention autocomplete system (v0.41.4)
│       │   │   ├── IMentionSource.cs          # MentionCandidate struct + IMentionSource interface
│       │   │   ├── MentionTokenParser.cs      # Pure static backward scan from cursor (allocation-free)
│       │   │   ├── MentionFuzzyScorer.cs      # Allocation-free fuzzy scoring (26-bit bitmask pre-filter)
│       │   │   ├── SceneMentionIndex.cs       # Hierarchy index with VersionTracker + 3000-entry cap
│       │   │   ├── AssetMentionIndex.cs       # Asset database index + IDisposable cleanup
│       │   │   ├── RecentMentionSource.cs     # Selection.activeGameObject + score boost
│       │   │   ├── MentionCoordinator.cs      # Merge, dedup, sort, cap at maxResults
│       │   │   └── MentionPopup.cs            # UIToolkit popup (focusable=false, max 8 rows)
│       │   ├── CLI/                        # Chat.CLI assembly: relay protocol and shared chat models
│       │   │   ├── IChatBackend.cs            # Backend interface used by the window
│       │   │   ├── RelayBackend.cs            # Only C# backend implementation
│       │   │   ├── RelayChatProcess.cs        # TCP command/event connection to Python relay
│       │   │   ├── RelaySpawner.cs            # Sidecar lifecycle and domain-reload reattachment
│       │   │   ├── BackendRegistry.cs         # Backend selection
│       │   │   ├── BackendConfig.cs           # Serializable per-backend settings
│       │   │   ├── BackendConfigStore.cs      # Project-local settings persistence
│       │   │   ├── ModelPresets.cs            # Model preset entries with contextWindow field, SetOverrides cache, ForDropdown API (v1.31.0)
│       │   │   ├── ModelContextWindows.cs     # Per-model context window configuration
│       │   │   ├── CopyAsMcpRef.cs            # Copy as MCP Ref command: Cmd+Shift+C shortcut (v1.31.0)
│       │   │   ├── ChatEvent.cs               # Normalized event struct
│       │   │   ├── UserTurnBuilder.cs         # Encode user turns
│       │   │   ├── ControlResponseBuilder.cs  # Approval and user-input responses
│       │   │   ├── ChipKindRegistry.cs        # Extensible chip providers
│       │   │   ├── ProviderRegistry.cs        # Settings/toolbar/panel provider base
│       │   │   └── ...                        # Shared models, chips, mentions, and utilities
│       │   ├── View/                       # Chat.View assembly (UI windows, rendering, cards)
│       │   │   ├── MCPChatWindow.cs           # EditorWindow UI + interaction (partial class)
│       │   │   ├── MCPChatWindow.Drain.cs     # Event draining + state updates + domain refresh trigger (F27) (partial class; v1.0.1: relay error shown inline in red when RelaySpawnState.Error != null)
│       │   │   ├── MCPChatWindow.Send.cs      # Send path: OnSend, rawText/llmText split, chip snapshot (partial class)
│       │   │   ├── MCPChatWindow.FlowBar.cs   # Activity animation track+chip (_askPending flag v0.29.37)
│       │   │   ├── MCPChatWindow.Mention.cs   # @Mention setup: debounce, popup show/hide, keyboard intercept (v0.41.4)
│       │   │   ├── MCPChatWindow.Chips.cs     # Drag-drop chip UX + removable ✕ buttons (F29: external files/folders, v0.23.0 Block 5: ProcessDraggedObject)
│       │   │   ├── MCPChatWindow.EventHandlers.cs # Last tool name tracking for timeout context hints (v0.46.0)
│       │   │   ├── MCPChatWindow.InlineChips.cs # Inline chip methods (extracted partial, F5)
│       │   │   ├── MCPChatWindow.Selector.cs  # Backend/mode selector + token reset (F1)
│       │   │   ├── MCPChatWindow.Resize.cs    # Window resize logic
│       │   │   ├── MCPChatWindow.Approve.cs   # Event handler for interactive permissions (v0.29.2+)
│       │   │   ├── MCPChatWindow.ErrorResolver.cs # Error resolver message injection (v0.62.0, partial class)
│       │   │   ├── ErrorResolverButton.cs      # Toolbar button for error-driven development (v0.62.0)
│       │   │   ├── TokenFormat.cs             # Pure Abbr(n) helper — "1.2k" / "840" token display
│       │   │   ├── EnterKeySend.cs            # Enter-to-send + Alt+Enter newline logic (pure testable)
│       │   │   ├── ChatSettingsSection.cs     # Delegate class for ChatConnectionSection (F23 refactored)
│       │   │   ├── ChatConnectionSection.cs   # [InitializeOnLoad] subscriber to ChatSettingsHook.OnBuildConnection (F23)
│       │   │   ├── ChatActivityState.cs       # Activity state tracking for grouping
│       │   │   ├── ChatLabel.cs               # Label customization + UI behavior: chat-text class for text wrapping (v1.31.0)
│       │   │   ├── ChatTranscript.cs          # Transcript layout: flex-start alignment + inner width for proper wrapping (v1.31.0)
│       │   │   ├── ChatRefAction.cs           # Click-navigate + context-menu for interactive refs
│       │   │   ├── ChatRefResolver.cs         # Scan hierarchy, resolve scene/script refs (F4 #ID)
│       │   │   ├── CopyableText.cs            # Selectable text wrapper
│       │   │   ├── CopyTextBuilder.cs         # Multi-line copy block assembly
│       │   │   ├── InputHeightCalc.cs         # Input field auto-height calculation (F30: 4-line default, tiny-window clamp fix)
│       │   │   ├── JsonArrayScan.cs           # Scan JSON arrays for streaming results
│       │   │   ├── ArgTokenizer.cs            # Shell-style quote-aware split (F9, review-hardening)
│       │   │   ├── ArgQuoting.cs              # Quote escaping helpers
│       │   │   ├── InlineChipField.cs         # Composed flex-row pill control + ReplaceMentionRangeWithChip (F5, v0.41.4 mention)
│       │   │   ├── InlineChipData.cs          # ChipData + InlineChipTracker (F5)
│       │   │   ├── InlineChipOverlay.cs       # Pill row UI (F5)
│       │   │   ├── InlineChipKeyHandler.cs    # TextField event routing (F5)
│       │   │   ├── SessionPickerPopup.cs      # UIToolkit popup for session selection (v0.41.0)
│       │   │   ├── ChipKindDetector.cs        # Pure Detect() → ChipKind (F10)
│       │   │   ├── ResponseTagInliner.cs      # [kind:ref] parser + renderer (F10)
│       │   │   ├── RestoreButton.cs           # Undo per-turn + cascade restore (F2)
│       │   │   ├── TurnUndoTracker.cs         # Group lifecycle + RestoreFromIndex (F2)
│       │   │   ├── SelectionSummary.cs        # Auto-Selection context (F4 hierarchy #ID)
│       │   │   ├── CompileAutoFix.cs          # Auto-retry on compile
│       │   │   ├── EditorStateSnapshot.cs     # Context block injection
│       │   │   ├── ToolPing.cs                # Flash object on tool-call
│       │   │   ├── HierarchyContextMenu.cs    # Right-click Hierarchy GameObject → Add to Chat Context (F16a)
│       │   │   ├── ComponentContextMenu.cs    # Right-click Component → Add to Chat Context (F16b, v0.23.0 Block 5: dual-chip @GO|@Script)
│       │   │   ├── ChipContextResolver.cs     # Resolve chips + emit typed (F10)
│       │   │   ├── AskUserCard.cs             # Interactive user input dialog (radio/checkbox/freetext, v0.29.11+, v0.29.38: codex: support)
│       │   │   ├── AskUserQuestionRow.cs      # Extracted pill-button row UI (217 LOC, v0.29.37)
│       │   │   ├── ToolApprovalCard.cs        # Risk-classified tool approval UI (Allow/Deny/Session/Always, v0.29.2)
│       │   │   ├── PlanStepCard.cs            # Agent plan step UI (Approve/Reject buttons, ACP-only)
│       │   │   ├── RiskClassifier.cs          # Tool risk categorization (v0.29.2)
│       │   │   ├── SessionAllowlist.cs        # Session-scoped tool allowlist manager (v0.29.2)
│       │   │   ├── ApproveHelper.cs           # Session management for approvals
│       │   │   ├── ApproveButtonFactory.cs    # Button builder (Allow/Deny/Session/Always)
│       │   │   ├── ChatMcpConfigWriter.cs     # Python command resolution + warning on serverDir change (v0.23.0)
│       │   │   ├── SlashTemplate.cs           # Template model
│       │   │   ├── SlashRegistry.cs           # Template registry
│       │   │   ├── SlashPopup.cs              # UIToolkit popup
│       │   │   ├── MCPChatWindow.Slash.cs     # Slash setup
│       │   │   ├── ReloadGuard.cs             # Domain-reload lock
│       │   │   ├── PendingTurnState.cs        # Persist in-flight state (v3: BackendKind) (F28: backward-compat mapping for old int=2)
│       │   │   ├── SentTextCache.cs           # Domain-reload dedup
│       │   │   ├── StderrRingBuffer.cs        # Stderr capture
│       │   │   ├── ToolCallAccumulator.cs     # Accumulate tool calls
│       │   │   ├── ToolCallRecord.cs          # Tool call record struct
│       │   │   ├── ToolChipGrouper.cs         # Group tool calls by ID
│       │   │   ├── ToolDetailBuilder.cs       # Tool card humanization
│       │   │   ├── ToolGroupState.cs          # Tool grouping state
│       │   │   ├── ToolGroupSummary.cs        # Summary of grouped tool calls
│       │   │   ├── UserToolResultParser.cs    # Parse tool results
│       │   │   ├── MCPChatWindow.uss          # UIToolkit styling (header removal + bottom footer + mention popup styles)
│       │   │   ├── AnnotateToolbarButton.cs   # Toolbar button to launch Annotation editor (v0.46.0)
│       │   │   ├── Annotation/                # Annotation editor system (v0.46.0, 11 files)
│       │   │   │   ├── AnnotationCanvas.cs    # Drawing surface (Texture2D-backed pixel rasterization)
│       │   │   │   ├── AnnotationCommand.cs   # Command pattern: Pen/Line/Arrow/Rect/Ellipse/Text/Erase + base
│       │   │   │   ├── AnnotationHistory.cs   # Undo/redo stack (commands + index)
│       │   │   │   ├── AnnotationToolState.cs # Active tool + brush state (color, size)
│       │   │   │   ├── AnnotationToolbar.cs   # Tool palette + color picker + undo/redo (UIToolkit)
│       │   │   │   ├── AnnotationEditorWindow.cs # EditorWindow host (canvas + toolbar)
│       │   │   │   ├── AnnotationRasterizer.cs # Rasterize commands to Texture2D (bresenham, scanline fills)
│       │   │   │   ├── AnnotationDrawer.cs    # Preview command strokes (GL lines, circles, text)
│       │   │   │   ├── AnnotationCompositor.cs # Flatten + PNG encode
│       │   │   │   └── AnnotationIcons.cs     # Procedural vector icons (Painter2D, 230 LOC)
│       │   │   ├── ContextProgressBar.cs      # UIToolkit progress bar for context window fill (v0.46.0)
│       │   │   ├── FieldContextMenu.cs        # Inspector context menu for field chip attachment (v0.46.0)
│       │   │   ├── Markdown/                  # Content rendering: registry seam + renderers
│       │   │   │   ├── MdBlock.cs             # Block model (enum + metadata)
│       │   │   │   ├── MarkdownParser.cs      # string → List<MdBlock> (single-pass)
│       │   │   │   ├── MarkdownParser.Blocks.cs # Block parsing helpers
│       │   │   │   ├── MarkdownInline.cs      # Inline spans → Unity rich-text (noparse <>, protect code)
│       │   │   │   ├── InlineImageThumbnail.cs # Image thumbnail rendering in paragraphs (v0.34.0, 70 LOC)
│       │   │   │   ├── ChipInlinePreviewPanel.cs # Lazy-load toggle panel for media previews (v0.35.0, 57 LOC)
│       │   │   │   ├── InlinePreviewBuilder.cs # Extensible preview factory (texture/image/model/prefab/audio, v0.35.0, 116 LOC)
│       │   │   │   ├── IChatBlockRenderer.cs  # Extension interface (can-render + render)
│       │   │   │   ├── ChatBlockRendererRegistry.cs # Ordered first-match-wins
│       │   │   │   ├── ChatBlockRendererFactory.cs # Default wiring (Mermaid first, Markdown catch-all); injects ChatRefResolver + AddRefToContext
│       │   │   │   ├── MarkdownBlockRenderer.cs # 8-kind dispatcher
│       │   │   │   ├── MarkdownBlockRenderer.Table.cs # Table grid layout (partial)
│       │   │   │   ├── MarkdownBlockRenderer.List.cs # Bullet/ordered list (partial)
│       │   │   │   ├── ImageBlockRenderer.cs  # PNG/JPG → Texture2D + click-to-open (v0.23.0: IsImageFile guard)
│       │   │   │   ├── Viewers/                # Media viewer windows (v0.23.0 Block 4, v0.34.0 expanded)
│       │   │   │   │   ├── ImageViewerWindow.cs # Modal image viewer: zoom/pan/fit controls
│       │   │   │   │   ├── MermaidViewerWindow.cs # Modal mermaid viewer: zoom/pan + exportable SVG
│       │   │   │   │   ├── ZoomPanManipulator.cs # DRY shared zoom/pan/fit logic (reusable for future viewers)
│       │   │   │   │   ├── IAssetViewer.cs      # Plugin interface for custom asset viewers (v0.34.0)
│       │   │   │   │   ├── AssetViewerFactory.cs # Registry + factory for extensible viewers (v0.34.0, 83 LOC)
│       │   │   │   │   ├── PrefabViewerWindow.cs # Prefab 3D preview window (v0.34.0, 151 LOC)
│       │   │   │   │   ├── PrefabPreviewLoader.cs # Temporary scene prefab instantiation (v0.34.0, 82 LOC)
│       │   │   │   │   ├── ModelViewerWindow.cs # 3D model viewer (.fbx/.obj/.blend/.dae, v0.34.0, 151 LOC)
│       │   │   │   │   ├── SpriteViewerWindow.cs # Sprite texture viewer with grid (v0.34.0, 78 LOC)
│       │   │   │   │   ├── AudioViewerWindow.cs # Audio clip player (v0.34.0, 142 LOC)
│       │   │   │   │   └── AudioUtilProxy.cs    # Reflection wrapper for Editor AudioUtil (v0.34.0, 66 LOC)
│       │   │   │   ├── Mermaid/               # Native Mermaid flowchart (no lib, pure parse+layout)
│       │   │   │   │   ├── MermaidGraph.cs    # POCO: nodes, edges, direction
│       │   │   │   │   ├── MermaidParser.cs   # lines → graph or null
│       │   │   │   │   ├── MermaidLayout.cs   # Kahn topo + longest-path + dynamic node sizing
│       │   │   │   │   ├── MermaidLayout.Layers.cs # Layer building + cycle guard
│       │   │   │   │   ├── MermaidBlockRenderer.cs # CanRender Mermaid, fallback to code-box (v0.23.0: opens MermaidViewerWindow)
│       │   │   │   │   ├── MermaidView.cs     # Absolute nodes + edge overlay + geom-change callback
│       │   │   │   │   └── MermaidEdgePainter.cs  # Painter2D lines + arrowheads
│       │   │   ├── Tests/                     # CLI + View assembly tests (parsing, backends, cards, interactivity)
│       │   │   │   ├── CLI/                   # CLI assembly tests (relay architecture v0.66.0+)
│       │   │   │   │   ├── ControlResponseBuilderTests.cs # Response serialization (v0.29.38+)
│       │   │   │   │   ├── RelayBackendTests.cs # Relay backend core logic (v0.66.0+)
│       │   │   │   │   ├── RelayChatProcessTests.cs # Process spawning + lifecycle (v0.66.0+)
│       │   │   │   │   ├── RelayTcpClientTests.cs # TCP bidirectional communication (v0.66.0+)
│       │   │   │   │   ├── RelayBackendConstructionMonkeyTests.cs # Initialization chaos (v0.66.0+)
│       │   │   │   │   ├── RelayBackendDrainMonkeyTests.cs # Drain path stress (v0.66.0+)
│       │   │   │   │   ├── RelayConnectionChaosTests.cs # Connection failures + recovery (v0.66.0+)
│       │   │   │   │   ├── RelayDrainStressTests.cs # High-volume message drain (v0.66.0+)
│       │   │   │   │   ├── RelayReloadSurvivalTests.cs # Domain reload recovery (v0.66.0+)
│       │   │   │   │   ├── RelayMonkeyTests.cs # Initialization monkey tests (v0.66.0+)
│       │   │   │   │   ├── RelayMonkeyChatTests.cs # Chat flow monkey tests (v0.66.0+)
│       │   │   │   │   ├── ImageAttachmentStoreTests.cs # Image attachment storage + temp files (v0.34.0, 188 tests)
│       │   │   │   │   ├── BuiltInChipProvidersTests.cs # Image/Model/Audio chip providers (v0.34.0, 214 tests)
│       │   │   │   │   ├── ProviderRegistryTests.cs # Provider registry base class (v0.34.0, 57 tests)
│       │   │   │   │   ├── MultiSceneChipTests.cs # Scene-qualified object path parsing + display (v0.30.4, 74 tests)
│       │   │   │   │   ├── TokenFormatTests.cs    # Token cost display + null-safe guards (v0.30.4, 12 tests)
│       │   │   │   │   ├── UserTurnBuilderImageTests.cs # User turn JSON with image serialization (v0.34.0, 76 tests)
│       │   │   │   │   └── ... # 40+ total CLI tests
│       │   │   │   ├── CLI/                   # CLI assembly tests
│       │   │   │   │   ├── AnnotatedScreenshotChipProviderTests.cs # Annotated image chip rendering (v0.46.0, 60 tests)
│       │   │   │   │   ├── AnnotationMetaWriterTests.cs # Annotation metadata JSON serialization (v0.46.0, 64 tests)
│       │   │   │   │   ├── AnnotationRaycasterTests.cs # Raycast hit detection + world coords (v0.46.0, 228 tests)
│       │   │   │   │   ├── FieldChipProviderTests.cs # Component field chip detection (v0.46.0, 113 tests)
│       │   │   │   │   ├── ModelContextWindowsTests.cs # LLM context window presets (v0.46.0, 27 tests)
│       │   │   │   │   ├── ScreenshotServiceTests.cs # Screenshot capture flow (v0.46.0, 69 tests)
│       │   │   │   │   ├── ScreenshotToolbarButtonTests.cs # Screenshot toolbar button (v0.46.0, 78 tests)
│       │   │   │   │   ├── AnnotateToolbarButtonTests.cs # Annotation editor launcher (v0.46.0, 42 tests)
│       │   │   │   └── View/                  # View assembly tests (UI, cards, interactivity, v0.66.0+: relay flow tests)
│       │   │   │   │   ├── AskUserCardTests.cs     # User input dialog + Codex protocol (v0.29.38 addition)
│       │   │   │   │   ├── HandleEventAcpCardsTests.cs # ACP event dispatch for PlanUpdate/FileChange/CapabilitiesChanged
│       │   │   │   │   ├── PlanStepCardTests.cs    # Plan step cards with Approve/Reject buttons
│       │   │   │   │   ├── ApproveFlowTests.cs     # Interactive approvals flow
│       │   │   │   │   ├── ChatUIMonkeyTests.cs    # Chat UI interaction monkey tests (v0.66.0+)
│       │   │   │   │   ├── ChatWindowButtonStateTests.cs # Button state transitions (v0.66.0+)
│       │   │   │   │   ├── ChatWindowDragDropMonkeyTests.cs # Drag-drop interactions (v0.66.0+)
│       │   │   │   │   ├── ChatWindowElementQueryTests.cs # DOM element queries (v0.66.0+)
│       │   │   │   │   ├── ChatWindowModeMonkeyTests.cs # Mode switch stress tests (v0.66.0+)
│       │   │   │   │   ├── ChatWindowModelChaosTests.cs # Model/backend chaos (v0.66.0+)
│       │   │   │   │   ├── ChatWindowSendMonkeyTests.cs # Send path chaos (v0.66.0+)
│       │   │   │   │   ├── ChatWindowSessionMonkeyTests.cs # Session management stress (v0.66.0+)
│       │   │   │   │   ├── ChatWindowUIInteractionTests.cs # General UI interaction (v0.66.0+)
│       │   │   │   │   ├── ChatWindowWindowLifecycleTests.cs # Window open/close lifecycle (v0.66.0+)
│       │   │   │   │   ├── ChipNavigationIntegrationTests.cs # Chip navigation integration (v0.66.0+)
│       │   │   │   │   ├── InlineChipFieldModelMonkeyTests.cs # Inline chip field chaos (v0.66.0+)
│       │   │   │   │   ├── RelayFlowWindowTests.cs # Relay chat flow window tests (v0.66.0+)
│       │   │   │   │   ├── SceneGoDragTests.cs     # Scene GameObject drag tests (v0.66.0+)
│       │   │   │   │   ├── AnnotationCanvasTests.cs # Canvas rasterization (v0.46.0, 30 tests)
│       │   │   │   │   ├── AnnotationCommandTests.cs # Command pattern + execution (v0.46.0, 86 tests)
│       │   │   │   │   ├── AnnotationCompositorTests.cs # PNG composition + flattening (v0.46.0, 82 tests)
│       │   │   │   │   ├── AnnotationEditorWindowTests.cs # Editor window lifecycle (v0.46.0, 31 tests)
│       │   │   │   │   ├── AnnotationHistoryTests.cs # Undo/redo stack (v0.46.0, 123 tests)
│       │   │   │   │   ├── AnnotationIconsTests.cs # Procedural icon rendering (v0.46.0, 77 tests)
│       │   │   │   │   ├── AnnotationRasterizerTests.cs # Line/circle rasterization (v0.46.0, 90 tests)
│       │   │   │   │   ├── AnnotationToolStateTests.cs # Tool state tracking (v0.46.0, 54 tests)
│       │   │   │   │   ├── AnnotationToolbarTests.cs # Toolbar UI + interaction (v0.46.0, 29 tests)
│       │   │   │   │   ├── ContextProgressBarTests.cs # Progress bar fill animation (v0.46.0, 57 tests)
│       │   │   │   │   ├── CopyMessageUxTests.cs   # Right-click copy + CopyFlash notification (v0.41.0)
│       │   │   │   │   ├── ChipSequenceTests.cs
│       │   │   │   │   ├── ChipSendSequenceTests.cs
│       │   │   │   │   ├── ModelSelectorTests.cs   # Per-backend model dropdown + preset selection (v0.30.4, 231 tests)
│       │   │   │   │   ├── SetModeTests.cs         # Ask↔Agent mode switch + session persistence (v0.30.4, 120 tests)
│       │   │   │   │   ├── TokenResetTests.cs      # Token counter reset + cost display (v0.30.4 upd v0.31.0, 14 tests + cost fix)
│       │   │   │   │   ├── TokenFormatTests.cs     # Token cost display formatting + null-safe guards (v0.31.0, 12 tests)
│       │   │   │   │   ├── ClipboardPasteTests.cs  # Clipboard image paste + mime detection (v0.34.0, 37 tests)
│       │   │   │   │   ├── ImageDragDropTests.cs   # Image drag-drop from Finder (v0.34.0, 154 tests)
│       │   │   │   │   ├── InlineImageThumbnailTests.cs # Image thumbnails in chat paragraphs (v0.34.0, 116 tests + 13 extended v0.35.0)
│       │   │   │   │   ├── ChipInlinePreviewPanelTests.cs # Inline preview toggle panel (v0.35.0, 8 tests)
│       │   │   │   │   ├── InlinePreviewBuilderTests.cs # Preview factory extensibility (v0.35.0, 9 tests)
│       │   │   │   │   ├── MultiImageBubbleTests.cs # Multi-image bubble rendering (v0.35.0, 3 tests)
│       │   │   │   │   ├── ImageViewerWindowTests.cs # Image viewer window (v0.35.0, 8 tests)
│       │   │   │   │   ├── PrefabViewerWindowTests.cs # Prefab preview window (v0.34.0, 198 tests)
│       │   │   │   │   ├── AssetViewerFactoryTests.cs # Media viewer factory + registry (v0.34.0, 224 tests + 11 extended v0.35.0)
│       │   │   │   │   ├── PluginSettingsInjectionTests.cs # ISettingsProvider plugin interface (v0.34.0, 72 tests)
│       │   │   │   │   ├── PluginToolbarButtonTests.cs # IToolbarButtonProvider plugin interface (v0.34.0, 105 tests)
│       │   │   │   │   ├── MentionTokenParserTests.cs  # Token parsing + cursor position (v0.41.4, 13 tests)
│       │   │   │   │   ├── MentionFuzzyScorerTests.cs  # Fuzzy scoring + word-boundary (v0.41.4, 10 tests)
│       │   │   │   │   ├── SceneMentionIndexTests.cs   # Hierarchy indexing + version tracking (v0.41.4, 7 tests)
│       │   │   │   │   ├── AssetMentionIndexTests.cs   # Asset indexing + cleanup (v0.41.4, 13 tests)
│       │   │   │   │   ├── MentionCoordinatorTests.cs  # Merging, dedup, sorting (v0.41.4, 7 tests)
│       │   │   │   │   ├── MentionPopupTests.cs        # UIToolkit popup behavior (v0.41.4, 8 tests)
│       │   │   │   │   ├── MentionIntegrationTests.cs  # End-to-end @mention flow (v0.41.4, 5 tests)
│       │   │   │   │   ├── MentionPerfTests.cs         # Index performance + scaling (v0.41.4, 5 tests)
│       │   │   │   │   ├── MentionEdgeCaseTests.cs     # Ambiguous names, rapid typing, etc (v0.41.4, 5 tests)
│       │   │   │   │   └── ... # 48+ total View tests
│       │   │   │   └── Markdown/                # Render tests
│       │   │   │       ├── MarkdownParserTests.cs
│       │   │   │       ├── MermaidParserTests.cs
│       │   │   │       └── ... # 25+ render tests
│       │   ├── UnityMCP.Editor.Chat.CLI.asmdef # CLI assembly: protocol, relay backends (independent compile, v0.29.2; v0.66.0: unified RelayBackend)
│       │   ├── UnityMCP.Editor.Chat.View.asmdef # View assembly: UI windows, rendering, cards (depends on CLI)
│       ├── ChatSettingsHook.cs            # Event hook: fires on MCPSettings rebuild
│       ├── AssemblyInfo.cs                # InternalsVisibleTo("UnityMCP.Editor.Chat.*")
│       ├── MenuHelper.cs + SceneHelper.cs + EditorStateHelper.cs  # pipeline-gap sprint: EditorStateHelper +multi-select via paths param
│       ├── NavMeshHelper.cs                 # NavMesh query + settings (pipeline-gap sprint: +get_settings, +set_settings)
│       ├── JsonHelper.cs + StringDistance.cs + UndoGroupHelper.cs + UndoGroupStack.cs (v0.64.0: T5 undo tool)
│       ├── FileOutputHelper.cs             # ScreenshotsDir = <ProjectRoot>/ScreenShots/ (v0.23.0)
│       ├── VersionTracker.cs
│       └── Roslyn/                         # Roslyn compiler for execute_code
│   └── Runtime/                           # Runtime assembly (v0.25.0: test helpers)
│       ├── UnityMCP.Runtime.TestHelpers.asmdef # Separate assembly for test utilities
│       └── TestHelpers/
│           └── TestDummyMB.cs             # Dummy MonoBehaviour for AddComponent<> in editor tests (moved from Editor/Chat/Tests v0.25.0)
├── unity-test-project/          # Unity 6000.0.65f1 / built-in UTF 1.6 canonical test project
│   ├── Assets/Tests/Editor/     # NUnit test files
│   ├── Assets/Animations/       # Animation clips + controllers
│   ├── Assets/Scenes/
│   ├── Assets/Shaders/          # TestGraph.shadergraph
│   ├── Assets/Scripts/          # Test helpers (GridPlayer, etc.)
│   └── Packages/manifest.json   # References unity-plugin via file:
├── docs/                       # User documentation
│   ├── assets/                 # SVG diagrams and badges
│   ├── install/                # Backend setup guides (v0.34.6+)
│   │   ├── kimi.md             # Kimi K2 CLI backend: Homebrew, PATH, model config
│   │   └── gemini.md           # Gemini backend: gcloud auth, model selection
│   ├── plugins/                # Plugin development guides (docs-critical-review)
│   │   └── ui-toolkit-best-practices.md # UI Toolkit patterns + UITK best practices for plugin authors
│   └── README.md               # Root documentation mirror
├── install.py                  # Setup/update/doctor/configure CLI
├── .mcp.json                   # MCP config pointing at local venv (v0.23.0: template; v0.96.1: auto-generated by `install.py setup` and `install.py update` via _write_mcp_json())
├── scripts/                    # Tooling: README stats, changelog SVG, release utilities, conformance runners
│   ├── readme_facts.py         # Source-backed inventory facts (tool count, test counts) — SSOT for README numbers
│   ├── readme_render.py        # Render README metadata, SVG statistics, and Shields endpoints
│   ├── update_readme.py        # CLI entry: collect + render README facts
│   ├── gen_changelog_svg.py    # Changelog → SVG badge
│   ├── changelog_svg_templates.py # SVG templates for changelog badge
│   ├── sync_versions.py        # Sync generated version copies from canonical pyproject.toml
│   ├── release.sh              # Non-publishing release preflight
│   ├── conformance_runner.py   # Conformance test harness: launch Workers A+B, run suite, report pass/fail matrix (WI-4)
│   ├── fault_proxy.py          # Fault injection proxy: intercepts TCP to inject delays/disconnects for chaos testing (WI-4)
│   ├── minimize_repro.py       # Minimize reproducers: binary-search failing test range (WI-4)
│   ├── export_tools.py         # Export MCP tool definitions to JSON for linting (v1.17.0+)
│   ├── quality_delta.py        # Parse linter reports, compute metrics delta, write quality data (v1.17.0+)
│   ├── check_skills_freshness.py # Static validation: skills refs, agent versions, tool parity (v1.17.0+)
│   └── tests/                  # pytest suite for scripts/ (test_facts.py, test_render.py, test_update_readme.py, test_gen_changelog_svg.py, test_conformance_runner.py, test_fault_proxy.py, test_minimize_repro.py, test_quality_delta.py)
├── AI/                         # Feature knowledge docs + changelog
├── .claude/
│   ├── skills/                 # Technical references
│   └── agents/                 # Agent specifications
└── CLAUDE.md
```
