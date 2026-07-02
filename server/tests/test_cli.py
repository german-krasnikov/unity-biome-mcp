"""Tests for the unity-mcp CLI dispatcher (configure/doctor/version/uninstall)."""
import pytest


# ─── dispatch: routing ───────────────────────────────────────────────────────

def test_dispatch_empty_argv_returns_none():
    from unity_mcp.cli import dispatch
    assert dispatch([]) is None


def test_dispatch_unknown_subcommand_returns_1_and_prints_stderr(capsys):
    from unity_mcp.cli import dispatch
    code = dispatch(["docter"])
    assert code == 1
    err = capsys.readouterr().err
    assert "unknown subcommand" in err
    assert "docter" in err


def test_dispatch_help_flag_returns_0(capsys):
    from unity_mcp.cli import dispatch
    code = dispatch(["-h"])
    assert code == 0
    out = capsys.readouterr().out
    assert "unity-mcp" in out


# ─── dispatch: configure ─────────────────────────────────────────────────────

def test_dispatch_configure_calls_merge_for_detected_tools(monkeypatch, tmp_path):
    from unity_mcp.cli import dispatch
    from unity_mcp.config import clients as c
    from unity_mcp.config import merger as m

    cfg = tmp_path / "mcp.json"
    monkeypatch.setattr(c, "detect_installed", lambda: ["claude-code"])
    monkeypatch.setattr(c.CLIENT_REGISTRY["claude-code"], "config_path", cfg)

    calls = []

    def fake_merge(path, entry, root_key="mcpServers", entry_transformer=None):
        calls.append((path, root_key))

    monkeypatch.setattr(m, "merge_mcp_config", fake_merge)

    code = dispatch(["configure"])

    assert code == 0
    assert calls == [(cfg, "mcpServers")]


def test_dispatch_configure_with_tool_flag_configures_only_that_tool(monkeypatch, tmp_path):
    from unity_mcp.cli import dispatch
    from unity_mcp.config import clients as c
    from unity_mcp.config import merger as m

    cfg_target = tmp_path / "claude.json"
    cfg_other = tmp_path / "cursor.json"
    monkeypatch.setattr(c.CLIENT_REGISTRY["claude-code"], "config_path", cfg_target)
    monkeypatch.setattr(c.CLIENT_REGISTRY["cursor"], "config_path", cfg_other)
    monkeypatch.setattr(c, "detect_installed", lambda: ["claude-code", "cursor"])

    calls = []

    def fake_merge(path, entry, root_key="mcpServers", entry_transformer=None):
        calls.append(path)

    monkeypatch.setattr(m, "merge_mcp_config", fake_merge)

    code = dispatch(["configure", "--tool", "claude-code"])

    assert code == 0
    assert calls == [cfg_target]


def test_dispatch_configure_no_tools_detected_returns_1(monkeypatch, capsys):
    from unity_mcp.cli import dispatch
    from unity_mcp.config import clients as c

    monkeypatch.setattr(c, "detect_installed", lambda: [])

    code = dispatch(["configure"])

    assert code == 1
    err = capsys.readouterr().err
    assert "--tool" in err


# ─── dispatch: doctor ────────────────────────────────────────────────────────

def test_dispatch_doctor_calls_run_doctor_and_prints_report(monkeypatch, capsys):
    from unity_mcp.cli import dispatch
    from unity_mcp import doctor as d
    from unity_mcp.doctor_report import CheckResult

    results = [CheckResult(name="python", ok=True, detail="3.12 OK")]

    async def fake_run_doctor(fix=False):
        return results

    monkeypatch.setattr(d, "run_doctor", fake_run_doctor)

    code = dispatch(["doctor"])

    assert code == 0
    out = capsys.readouterr().out
    assert "python" in out
    assert "1/1 checks passed" in out


def test_dispatch_doctor_fix_flag_passed_through(monkeypatch, capsys):
    from unity_mcp.cli import dispatch
    from unity_mcp import doctor as d

    captured = {}

    async def fake_run_doctor(fix=False):
        captured["fix"] = fix
        return []

    monkeypatch.setattr(d, "run_doctor", fake_run_doctor)

    dispatch(["doctor", "--fix"])

    assert captured["fix"] is True


# ─── dispatch: version ───────────────────────────────────────────────────────

def test_dispatch_version_prints_canonical_version(monkeypatch, capsys):
    import unity_mcp
    from unity_mcp.cli import dispatch

    monkeypatch.setattr(unity_mcp, "__version__", "9.9.9")

    code = dispatch(["version"])

    assert code == 0
    out = capsys.readouterr().out
    assert "9.9.9" in out


# ─── dispatch: uninstall ─────────────────────────────────────────────────────

def test_dispatch_uninstall_calls_remove_mcp_entry_for_detected_tools(monkeypatch, tmp_path, capsys):
    from unity_mcp.cli import dispatch
    from unity_mcp.config import clients as c
    from unity_mcp.config import merger as m

    cfg = tmp_path / "mcp.json"
    cfg.write_text("{}", encoding="utf-8")
    monkeypatch.setattr(c, "detect_installed", lambda: ["claude-code"])
    monkeypatch.setattr(c.CLIENT_REGISTRY["claude-code"], "config_path", cfg)

    calls = []

    def fake_remove(path, root_key="mcpServers"):
        calls.append((path, root_key))
        return True

    monkeypatch.setattr(m, "remove_mcp_entry", fake_remove)

    code = dispatch(["uninstall"])

    assert code == 0
    assert calls == [(cfg, "mcpServers")]
    out = capsys.readouterr().out
    assert "Removed" in out


def test_dispatch_uninstall_reports_skip_when_entry_absent(monkeypatch, tmp_path, capsys):
    from unity_mcp.cli import dispatch
    from unity_mcp.config import clients as c
    from unity_mcp.config import merger as m

    cfg = tmp_path / "mcp.json"
    cfg.write_text("{}", encoding="utf-8")
    monkeypatch.setattr(c, "detect_installed", lambda: ["claude-code"])
    monkeypatch.setattr(c.CLIENT_REGISTRY["claude-code"], "config_path", cfg)
    monkeypatch.setattr(m, "remove_mcp_entry", lambda path, root_key="mcpServers": False)

    code = dispatch(["uninstall"])

    assert code == 0
    out = capsys.readouterr().out
    assert "skipped" in out


# ─── main: fallthrough to MCP server ─────────────────────────────────────────

def test_main_zero_argv_falls_through_to_server_main(monkeypatch):
    import unity_mcp.server as srv
    from unity_mcp import cli

    calls = []
    monkeypatch.setattr(srv, "main", lambda: calls.append(True))
    monkeypatch.setattr("sys.argv", ["unity-mcp"])

    cli.main()

    assert calls == [True]
