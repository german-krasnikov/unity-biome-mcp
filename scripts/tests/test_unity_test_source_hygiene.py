"""Repository-wide source gates for the canonical Unity test architecture."""

from __future__ import annotations

import pathlib
import re

REPO_ROOT = pathlib.Path(__file__).resolve().parents[2]
CSHARP_ROOTS = (
    REPO_ROOT / "unity-plugin",
    REPO_ROOT / "unity-plugin-reload",
    REPO_ROOT / "unity-test-project" / "Assets" / "Tests",
    REPO_ROOT / "scripts" / "fixtures",
)
TEST_ROOTS = (
    REPO_ROOT / "unity-plugin" / "Editor" / "Tests",
    REPO_ROOT / "unity-plugin" / "Editor" / "Chat" / "Tests",
    REPO_ROOT / "unity-plugin" / "Editor" / "Wizard" / "Tests",
    REPO_ROOT / "unity-plugin-reload" / "Editor" / "Tests",
    REPO_ROOT / "unity-test-project" / "Assets" / "Tests",
    REPO_ROOT / "scripts" / "fixtures",
)

_NON_CODE = re.compile(
    r"//[^\r\n]*"
    r"|/\*.*?\*/"
    r'|(?:\$@|@\$|@)"(?:""|[^"])*"'
    r'|\$?"(?:\\.|[^"\\])*"'
    r"|'(?:\\.|[^'\\])*'",
    re.DOTALL,
)

def _sources(roots: tuple[pathlib.Path, ...]) -> list[pathlib.Path]:
    return sorted(
        path
        for root in roots
        if root.exists()
        for path in root.rglob("*.cs")
        if not any(part in {"Library", "Temp", "obj"} for part in path.parts)
    )


def _code(path: pathlib.Path) -> str:
    return _NON_CODE.sub("", path.read_text(encoding="utf-8"))


def _relative(path: pathlib.Path) -> str:
    return path.relative_to(REPO_ROOT).as_posix()


def test_unity_source_has_no_version_branch_compatibility_shims() -> None:
    pattern = re.compile(r"\bUNITY_[0-9_]+_OR_NEWER\b")
    offenders = [
        _relative(path)
        for path in _sources(CSHARP_ROOTS)
        if pattern.search(_code(path))
    ]
    assert offenders == []


def test_unity_tests_use_task_first_async_contract() -> None:
    forbidden = {
        "Unity lifecycle coroutine attribute": re.compile(
            r"\[\s*(?:UnityTest|UnitySetUp|UnityTearDown)\b"
        ),
        "IEnumerator test/helper method": re.compile(
            r"\bIEnumerator\s+[A-Za-z_][A-Za-z0-9_]*\s*\("
        ),
        "async void": re.compile(r"\basync\s+void\b"),
        "blocking NUnit async assertion": re.compile(
            r"\bAssert\s*\.\s*(?:ThrowsAsync|DoesNotThrowAsync)\s*\("
        ),
        "EditMode Awaitable.NextFrameAsync": re.compile(
            r"\bAwaitable\s*\.\s*NextFrameAsync\s*\("
        ),
    }
    offenders: list[str] = []
    for path in _sources(TEST_ROOTS):
        code = _code(path)
        for label, pattern in forbidden.items():
            if pattern.search(code):
                offenders.append(f"{_relative(path)}: {label}")
    assert offenders == []


def test_unity_tests_do_not_block_or_trigger_broad_refresh() -> None:
    forbidden = {
        "Thread.Sleep": re.compile(r"\bThread\s*\.\s*Sleep\s*\("),
        "blocking Wait": re.compile(r"\.\s*Wait\s*\("),
        "blocking Result": re.compile(r"\.\s*Result\b"),
        "broad AssetDatabase.Refresh": re.compile(
            r"\bAssetDatabase\s*\.\s*Refresh\s*\("
        ),
    }
    offenders: list[str] = []
    for path in _sources(TEST_ROOTS):
        code = _code(path)
        for label, pattern in forbidden.items():
            if pattern.search(code):
                offenders.append(f"{_relative(path)}: {label}")
    assert offenders == []


def test_unity_tests_create_preview_scenes_only_through_owned_factory() -> None:
    forbidden = re.compile(r"\bNewPreviewScene\s*\(")
    offenders = [
        _relative(path)
        for path in _sources(TEST_ROOTS)
        if forbidden.search(_code(path))
    ]
    assert offenders == []


def test_unity_tests_do_not_mutate_live_editor_transport_state() -> None:
    code_patterns = {
        "production relay stop": re.compile(r"\bRelaySpawner\s*\.\s*Stop\s*\("),
        "production dispatcher clear": re.compile(
            r"\bMainThreadDispatcher\s*\.\s*Clear\s*\(\s*\)"
        ),
        "production MCP state file": re.compile(
            r"\bMCPServer\s*\.\s*(?:WriteStateFile|DeleteStateFile)\s*\("
        ),
        "production reload update queue": re.compile(
            r"\bReloadMiniServer\s*\.\s*UpdateQueue\b"
        ),
        "live relay SessionState": re.compile(
            r"\bSessionState\s*\.\s*(?:GetInt|SetInt|EraseInt)\s*\(\s*"
            r"RelaySpawner\s*\.\s*(?:PortKey|PidKey)"
        ),
    }
    raw_patterns = {
        "live domain stamp SessionState": re.compile(
            r'\bSessionState\s*\.\s*(?:SetString|EraseString)\s*\(\s*'
            r'"MCP_DomainStamp"'
        ),
        "literal live relay SessionState": re.compile(
            r'\bSessionState\s*\.\s*(?:GetInt|SetInt|EraseInt)\s*\(\s*'
            r'"MCPChat_Relay_(?:Port|PID)"'
        ),
    }
    offenders: list[str] = []
    for path in _sources(TEST_ROOTS):
        code = _code(path)
        raw = path.read_text(encoding="utf-8")
        for label, pattern in code_patterns.items():
            if pattern.search(code):
                offenders.append(f"{_relative(path)}: {label}")
        for label, pattern in raw_patterns.items():
            if pattern.search(raw):
                offenders.append(f"{_relative(path)}: {label}")
    assert offenders == []


def test_reload_server_lifecycle_mutation_is_worker_only() -> None:
    mutation = re.compile(r"\bReloadMiniServer\s*\.\s*(?:Start|Stop)\b")
    harness = (
        REPO_ROOT
        / "unity-plugin-reload"
        / "Editor"
        / "Tests"
        / "ReloadMiniServerWorkerHarness.cs"
    )
    offenders = [
        _relative(path)
        for path in _sources(TEST_ROOTS)
        if path != harness and mutation.search(_code(path))
    ]
    assert offenders == []

    harness_code = _code(harness)
    assert "UnityMcpWorkerTestBoundary.Require" in harness_code
    assert len(mutation.findall(harness_code)) == 3

def test_reload_server_worker_harness_references_are_method_worker_only() -> None:
    reference = re.compile(r"\bReloadMiniServerWorkerHarness\b")
    harness = (
        REPO_ROOT
        / "unity-plugin-reload"
        / "Editor"
        / "Tests"
        / "ReloadMiniServerWorkerHarness.cs"
    )
    method = re.compile(
        r"(?P<attributes>(?:\s*\[[^\[\]]+\]\s*)*)"
        r"(?:(?:public|protected|internal|private|static|virtual|override|sealed|"
        r"async|new|extern|unsafe|partial)\s+)+"
        r"[^;={}]+?\s+[A-Za-z_][A-Za-z0-9_]*\s*\([^;{}]*\)\s*"
        r"(?:where\s+[^{}]+)?\{",
        re.MULTILINE,
    )
    test_attribute = re.compile(r"\[\s*(?:[A-Za-z_][A-Za-z0-9_.]*\.)?Test(?:Case)?\b")
    worker_attribute = re.compile(
        r"\[\s*(?:[A-Za-z_][A-Za-z0-9_.]*\.)?BiomeWorkerOnly\b"
    )
    offenders: list[str] = []

    for path in _sources(TEST_ROOTS):
        if path == harness:
            continue
        code = _code(path)
        references = list(reference.finditer(code))
        if not references:
            continue
        covered: set[int] = set()
        for declaration in method.finditer(code):
            body_start = declaration.end() - 1
            depth = 0
            body_end = len(code)
            for index in range(body_start, len(code)):
                if code[index] == "{":
                    depth += 1
                elif code[index] == "}":
                    depth -= 1
                    if depth == 0:
                        body_end = index
                        break

            attributes = declaration.group("attributes")
            for occurrence in references:
                if body_start < occurrence.start() < body_end:
                    covered.add(occurrence.start())
                    if not (
                        test_attribute.search(attributes)
                        and worker_attribute.search(attributes)
                    ):
                        line = code.count("\n", 0, occurrence.start()) + 1
                        offenders.append(f"{_relative(path)}:{line}")

        for occurrence in references:
            if occurrence.start() not in covered:
                line = code.count("\n", 0, occurrence.start()) + 1
                offenders.append(f"{_relative(path)}:{line}")

    assert offenders == []


def test_worker_only_tests_have_no_one_time_lifecycle() -> None:
    one_time = re.compile(r"\[\s*OneTime(?:SetUp|TearDown)\b")
    offenders: list[str] = []
    for path in _sources(TEST_ROOTS):
        code = _code(path)
        if "BiomeWorkerOnly" in code and one_time.search(code):
            offenders.append(_relative(path))
    assert offenders == []


def test_unity_tests_do_not_write_editorprefs_directly() -> None:
    mutation = re.compile(
        r"\bEditorPrefs\s*\.\s*"
        r"(?:SetString|SetBool|SetInt|SetFloat|DeleteKey|DeleteAll)\s*\("
    )
    offenders = [
        _relative(path)
        for path in _sources(TEST_ROOTS)
        if mutation.search(_code(path))
    ]
    assert offenders == []


def test_unity_tests_never_acquire_a_shared_editor_window() -> None:
    """A generic lookup can capture and later mutate or close an existing window."""
    forbidden = {
        "shared generic EditorWindow lookup": re.compile(
            r"(?:\bEditorWindow\s*\.\s*)?\bGetWindow(?:WithRect)?\s*<"
        ),
        "production MCPChatWindow.ShowWindow": re.compile(
            r"\bMCPChatWindow\s*\.\s*ShowWindow\s*\("
        ),
    }
    offenders: list[str] = []
    for path in _sources(TEST_ROOTS):
        code = _code(path)
        for label, pattern in forbidden.items():
            if pattern.search(code):
                offenders.append(f"{_relative(path)}: {label}")
    assert offenders == []


def test_unity_tests_do_not_skip_for_ambient_editor_state() -> None:
    """Ambient windows and selection are inputs to isolate, not skip reasons."""
    offenders = [
        _relative(path)
        for path in _sources(TEST_ROOTS)
        if "environment-sensitive" in path.read_text(encoding="utf-8").lower()
    ]
    assert offenders == []


def test_common_base_preserves_extensible_production_state() -> None:
    """Destructive registry test helpers are safe only inside the global rollback."""
    source = (
        REPO_ROOT
        / "unity-plugin"
        / "Editor"
        / "TestSupport"
        / "UnityMcpTestBase.cs"
    ).read_text(encoding="utf-8")
    required_scopes = {
        "SettingsProviderRegistry.PreserveStateForTests()",
        "ToolbarButtonRegistry.PreserveStateForTests()",
        "PanelProviderRegistry.PreserveStateForTests()",
        "ChipKindRegistry.PreserveStateForTests()",
        "PluginRegistry.PreserveStateForTests()",
        "AssetViewerFactory.PreserveStateForTests()",
        "PreviewBuilderRegistry.PreserveStateForTests()",
        "InlinePreviewBuilder.PreserveStateForTests()",
        "MixedParagraphRenderer.PreserveStateForTests()",
        "ChatSettingsHook.PreserveConnectionEventForTests()",
    }
    missing = sorted(scope for scope in required_scopes if scope not in source)
    assert missing == []


def test_common_base_uses_exact_runtime_transactions_and_unwinds_them_last() -> None:
    source = (
        REPO_ROOT
        / "unity-plugin"
        / "Editor"
        / "TestSupport"
        / "UnityMcpTestBase.cs"
    ).read_text(encoding="utf-8")

    required = {
        "SyncHelper.BeginTestIsolation()",
        "CompileNotifier.BeginTestIsolation()",
        "ReloadGuard.BeginTestIsolation(",
        "MCPChatWindow.BeginTestIsolation(",
    }
    assert sorted(item for item in required if item not in source) == []

    order = [
        source.index('NormalizeForOwnershipCleanup("final managed test scene rollback"'),
        source.index("_chatWindowIsolation?.Dispose()"),
        source.index("_reloadGuardIsolation?.Dispose()"),
        source.index("_relayIsolation?.Dispose()"),
        source.index("_relaySpawnIsolation?.Dispose()"),
        source.index("_updateCheckerIsolation?.Dispose()"),
        source.index("_chatSettingsHookIsolation?.Dispose()"),
        source.index("_mixedParagraphRendererIsolation?.Dispose()"),
        source.index("_inlinePreviewBuilderIsolation?.Dispose()"),
        source.index("_previewBuilderRegistryIsolation?.Dispose()"),
        source.index("_assetViewerFactoryIsolation?.Dispose()"),
        source.index("_pluginRegistryIsolation?.Dispose()"),
        source.index("_chipKindIsolation?.Dispose()"),
        source.index("_panelProviderIsolation?.Dispose()"),
        source.index("_toolbarButtonIsolation?.Dispose()"),
        source.index("_settingsProviderIsolation?.Dispose()"),
        source.index("CommandRegistry.RestoreForTest(_commandRegistryBaseline)"),
        source.index("_compileNotifierIsolation?.Dispose()"),
        source.index("_syncIsolation?.Dispose()"),
        source.index("LogAssert.ignoreFailingMessages = _logAssertIgnoreBaseline"),
    ]
    assert order == sorted(order)


def test_viewer_and_preview_scopes_restore_exact_state() -> None:
    sources = {
        "AssetViewerFactory.cs": (
            REPO_ROOT
            / "unity-plugin/Editor/Chat/View/Viewers/AssetViewerFactory.cs"
        ),
        "PreviewBuilderRegistry.cs": (
            REPO_ROOT
            / "unity-plugin/Editor/Chat/View/Preview/PreviewBuilderRegistry.cs"
        ),
        "InlinePreviewBuilder.cs": (
            REPO_ROOT
            / "unity-plugin/Editor/Chat/View/Markdown/InlinePreviewBuilder.cs"
        ),
        "MixedParagraphRenderer.cs": (
            REPO_ROOT
            / "unity-plugin/Editor/Chat/View/Markdown/MixedParagraphRenderer.cs"
        ),
    }
    required = {
        "AssetViewerFactory.cs": {
            "new List<KeyValuePair<string, IAssetViewer>>(_registry)",
            "AssetChipProviderBase.ViewerLauncher = _viewerLauncher",
            "ImageChipProvider.ImageFallbackViewer = _imageFallbackViewer",
        },
        "PreviewBuilderRegistry.cs": {
            "new List<Entry>(_entries)",
            "_entries.AddRange(_entriesSnapshot)",
            "_version = _versionSnapshot",
        },
        "InlinePreviewBuilder.cs": {
            "TextureLoader = _textureLoader",
            "AssetPreviewLoader = _assetPreviewLoader",
            "AudioClipLoader = _audioClipLoader",
        },
        "MixedParagraphRenderer.cs": {"ContextOverride = _context"},
    }
    missing: list[str] = []
    for name, path in sources.items():
        source = path.read_text(encoding="utf-8")
        if "PreserveStateForTests()" not in source:
            missing.append(f"{name}: PreserveStateForTests")
        missing.extend(
            f"{name}: {token}" for token in required[name] if token not in source
        )
    assert sorted(missing) == []


def test_preview_fixtures_leave_restoration_to_common_base() -> None:
    fixtures = (
        "AssetViewerFactoryTests.cs",
        "InlinePreviewBuilderTests.cs",
        "MixedParagraphRendererTests.cs",
        "ResponseTagPillTests.cs",
    )
    root = REPO_ROOT / "unity-plugin/Editor/Chat/Tests/View"
    offenders = [
        name
        for name in fixtures
        if "[TearDown]" in _code(root / name)
    ]
    assert offenders == []


def test_sync_and_compile_notifier_scopes_capture_their_complete_state() -> None:
    sync = (REPO_ROOT / "unity-plugin" / "Editor" / "SyncHelper.cs").read_text(
        encoding="utf-8"
    )
    compile_notifier = (
        REPO_ROOT / "unity-plugin" / "Editor" / "CompileNotifier.cs"
    ).read_text(encoding="utf-8")

    sync_required = {
        "_ops = Ops",
        "_clock = NowSeconds",
        "_syncComplete = OnSyncComplete",
        "_syncFailed = OnSyncFailed",
        "IntSessionValue.Capture(EpochKey)",
        "BoolSessionValue.Capture(CleanKey)",
        "StringSessionValue.Capture(StateKey)",
        "StringSessionValue.Capture(ErrKey)",
        "FloatSessionValue.Capture(TriggerTimeKey)",
        "BoolSessionValue.Capture(CompileStartedKey)",
        "StringSessionValue.Capture(StampKey)",
        "StringSessionValue.Capture(StampAtTriggerKey)",
        "StringSessionValue.Capture(AllAsmErrKey)",
    }
    compile_required = {
        "_clock = NowSecondsFloat",
        "FloatSessionValue.Capture(StartKey)",
        "FloatSessionValue.Capture(DurationKey)",
        "BoolSessionValue.Capture(FailedKey)",
    }
    assert sorted(item for item in sync_required if item not in sync) == []
    assert sorted(
        item for item in compile_required if item not in compile_notifier
    ) == []


def test_reload_guard_nesting_requires_explicit_test_ownership() -> None:
    reload_guard = (
        REPO_ROOT
        / "unity-plugin"
        / "Editor"
        / "Chat"
        / "CLI"
        / "ReloadGuard.cs"
    ).read_text(encoding="utf-8")
    common_base = (
        REPO_ROOT
        / "unity-plugin"
        / "Editor"
        / "TestSupport"
        / "UnityMcpTestBase.cs"
    ).read_text(encoding="utf-8")

    assert "IsTestIsolationOwnedBy(string ownerId)" in reload_guard
    assert "RepairOrphanedTestIsolation(string currentOwnerId)" in reload_guard
    assert "RepairOrphanedTestIsolation(isolationOwnerId)" in common_base
    assert "IsTestIsolationOwnedBy(isolationOwnerId)" in common_base
    assert "var nestedTestScope = reloadOps is IReloadGuardTestOps" not in common_base


def test_update_checker_always_restores_context_from_scope_dispose() -> None:
    source = (
        REPO_ROOT / "unity-plugin" / "Editor" / "Updates" / "UpdateChecker.cs"
    ).read_text(encoding="utf-8")
    scope = source[
        source.index("private sealed class TestIsolationScope") :
        source.index("internal static IDisposable BeginTestIsolation()")
    ]
    assert re.search(
        r"finally\s*\{[^}]*_currentContext\s*=\s*_previous;[^}]*"
        r"_disposed\s*=\s*true;",
        scope,
        re.DOTALL,
    )


def test_scene_region_tests_use_transactional_state_isolation() -> None:
    forbidden = {
        "direct persistence-path mutation": re.compile(
            r"\bSceneRegionState\s*\.\s*PersistPath\s*="
        ),
        "direct retention-limit mutation": re.compile(
            r"\bSceneRegionState\s*\.\s*MaxRegions\s*="
        ),
        "destructive region cleanup registration": re.compile(
            r"\bRegisterCleanup\s*\(\s*SceneRegionState\s*\.\s*Clear\s*\)"
        ),
    }
    offenders: list[str] = []
    for path in _sources(TEST_ROOTS):
        code = _code(path)
        for label, pattern in forbidden.items():
            if pattern.search(code):
                offenders.append(f"{_relative(path)}: {label}")
    assert offenders == []


def test_client_slot_liveness_never_probes_handler_owned_socket_readiness() -> None:
    """Poll/Available races the handler read and can reset a live connection."""
    source = (
        REPO_ROOT / "unity-plugin" / "Editor" / "ClientSlot.cs"
    ).read_text(encoding="utf-8")

    assert ".Poll(" not in source
    assert ".Available" not in source
    assert "IsEntryActive" in source
