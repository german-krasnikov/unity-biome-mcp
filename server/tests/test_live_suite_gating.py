import ast
from pathlib import Path

import pytest

from tests.live import conftest as live_conftest


class _Item:
    nodeid = "tests/live/test_external.py::test_backend"
    fixturenames = []

    def __init__(self, markers):
        self._markers = set(markers)
        self.added = []

    def get_closest_marker(self, name):
        return object() if name in self._markers else None

    def add_marker(self, marker):
        self.added.append(marker)


def test_paid_live_cli_lane_is_skipped_by_default(monkeypatch):
    item = _Item({"live", "live_cli"})
    monkeypatch.setattr(live_conftest, "RUN_LIVE_CLI", False)

    live_conftest.pytest_collection_modifyitems([item])

    assert len(item.added) == 1
    assert item.added[0].mark.name == "skip"
    assert "UNITY_MCP_RUN_LIVE_CLI=1" in item.added[0].mark.kwargs["reason"]


def test_paid_live_cli_lane_runs_only_when_explicitly_enabled(monkeypatch):
    item = _Item({"live", "live_cli"})
    monkeypatch.setattr(live_conftest, "RUN_LIVE_CLI", True)

    live_conftest.pytest_collection_modifyitems([item])

    assert item.added == []


def test_live_collection_rejects_mixed_edit_and_play_fixtures():
    item = _Item({"live"})
    item.fixturenames = ["ensure_edit_mode", "play_session"]

    with pytest.raises(pytest.UsageError, match="mixes ensure_edit_mode"):
        live_conftest.pytest_collection_modifyitems([item])


def _unity_project(path: Path) -> Path:
    (path / "Assets").mkdir(parents=True)
    (path / "Packages").mkdir()
    return path.resolve()


def test_live_port_discovery_uses_exact_project_and_rejects_collisions(
    tmp_path, monkeypatch
):
    worker = _unity_project(tmp_path / "worker")
    foreign = _unity_project(tmp_path / "foreign")
    ports = tmp_path / "ports"
    ports.mkdir()
    (ports / "22924.port").write_text(
        f"54170\n{worker}\nworker\n", encoding="utf-8"
    )
    (ports / "76084.port").write_text(
        f"54170\n{foreign}\nforeign\n", encoding="utf-8"
    )
    monkeypatch.setattr(live_conftest, "LIVE_PROJECT", str(worker))
    monkeypatch.setattr(live_conftest, "REAL_PORTS_DIR", ports)

    with pytest.raises(RuntimeError, match="no port file"):
        live_conftest.current_worker_port()

    (ports / "22924.port").write_text(
        f"54171\n{worker}\nworker\n", encoding="utf-8"
    )
    assert live_conftest.current_worker_port() == 54171


def test_live_bridge_factory_pins_project_and_dynamic_discovery(
    tmp_path, monkeypatch
):
    worker = _unity_project(tmp_path / "worker")
    monkeypatch.setattr(live_conftest, "LIVE_PROJECT", str(worker))
    monkeypatch.setattr(live_conftest, "current_worker_port", lambda: 54171)

    bridge = live_conftest.make_live_bridge()

    assert bridge._port == 54171
    assert bridge._expected_project_path == str(worker)
    assert bridge._port_discoverer is live_conftest.current_worker_port


def test_live_test_modules_do_not_read_static_main_port():
    """Main-port ownership and reload rediscovery belong to live conftest."""
    live_tests = Path(__file__).parent / "live"
    offenders = []
    for path in sorted(live_tests.glob("test_*.py")):
        tree = ast.parse(path.read_text(encoding="utf-8"), filename=str(path))
        for node in ast.walk(tree):
            if isinstance(node, ast.ImportFrom):
                if any(alias.name == "LIVE_PORT" for alias in node.names):
                    offenders.append(f"{path.name}:{node.lineno}")
                continue
            if isinstance(node, ast.Subscript):
                owner = node.value
                key = node.slice
                if (
                    isinstance(owner, ast.Attribute)
                    and owner.attr == "environ"
                    and isinstance(key, ast.Constant)
                    and key.value == "UNITY_MCP_PORT"
                ):
                    offenders.append(f"{path.name}:{node.lineno}")
                continue
            if not isinstance(node, ast.Call) or not node.args:
                continue
            function = node.func
            reads_environment = (
                isinstance(function, ast.Attribute)
                and (
                    (
                        function.attr == "get"
                        and isinstance(function.value, ast.Attribute)
                        and function.value.attr == "environ"
                    )
                    or function.attr == "getenv"
                )
            )
            if not reads_environment:
                continue
            key = node.args[0]
            if isinstance(key, ast.Constant) and key.value == "UNITY_MCP_PORT":
                offenders.append(f"{path.name}:{node.lineno}")

    assert offenders == [], (
        "live tests must use the project-aware bridge fixture instead of a "
        f"static UNITY_MCP_PORT: {offenders}"
    )


def test_non_paid_live_tests_cannot_skip_missing_runtime_contracts():
    """Only explicitly marked paid CLI coverage may be unavailable by design."""
    live_tests = Path(__file__).parent / "live"
    offenders = []
    for path in sorted(live_tests.glob("test_*.py")):
        source = path.read_text(encoding="utf-8")
        if "pytest.mark.live_cli" in source:
            continue
        tree = ast.parse(source, filename=str(path))
        for node in ast.walk(tree):
            if not isinstance(node, ast.Call):
                continue
            function = node.func
            if (
                isinstance(function, ast.Attribute)
                and isinstance(function.value, ast.Name)
                and function.value.id == "pytest"
                and function.attr == "skip"
            ):
                offenders.append(f"{path.name}:{node.lineno}")

    assert offenders == [], (
        "required live coverage must fail when its runtime contract is missing; "
        f"unexpected pytest.skip calls: {offenders}"
    )


def test_reconnect_recovery_is_project_pinned_and_fail_closed():
    path = Path(__file__).parent / "live" / "test_reconnect.py"
    source = path.read_text(encoding="utf-8")
    tree = ast.parse(source, filename=str(path))
    functions = {
        node.name: ast.get_source_segment(source, node) or ""
        for node in tree.body
        if isinstance(node, ast.AsyncFunctionDef)
    }

    ping_once = functions["_worker_ping_once"]
    assert "make_live_bridge()" in ping_once
    assert 'bridge.send("ping", {})' in ping_once
    assert "_bridge_up" not in source

    recovery = functions["_wait_unity_recovery"]
    assert "await _wait_fresh_connect()" in recovery
    assert "pytest.fail" in recovery


def test_chat_live_module_has_verified_window_owner_scope():
    path = Path(__file__).parent / "live" / "test_chat_ui_monkey.py"
    source = path.read_text(encoding="utf-8")
    tree = ast.parse(source, filename=str(path))
    owner = next(
        (
            node
            for node in tree.body
            if isinstance(node, ast.AsyncFunctionDef)
            and node.name == "_chat_window_owner"
        ),
        None,
    )

    assert owner is not None
    fixture = next(
        (
            decorator
            for decorator in owner.decorator_list
            if isinstance(decorator, ast.Call)
            and isinstance(decorator.func, ast.Attribute)
            and decorator.func.attr == "fixture"
        ),
        None,
    )
    assert fixture is not None
    keywords = {keyword.arg: keyword.value for keyword in fixture.keywords}
    assert isinstance(keywords.get("autouse"), ast.Constant)
    assert keywords["autouse"].value is True
    assert isinstance(keywords.get("scope"), ast.Constant)
    assert keywords["scope"].value == "module"

    owner_source = ast.get_source_segment(source, owner) or ""
    for required in (
        "make_live_bridge",
        "STOP_BACKEND_IF_PRESENT",
        "CLOSE_WINDOW",
        "FIND_WINDOW",
        "raise AssertionError",
    ):
        assert required in owner_source
