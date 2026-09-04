#!/usr/bin/env python3
"""Direct-TCP status/scenes read surface -- split out of check_unity.py to
stay under its 300-line budget (A11a, Plans/Reviews/ARCH-STF-unity-access-policy.md
§probe_script_spec). Owns the closed CLI dispatch and the probe_status/
probe_open_scenes primitives; check_unity.py re-exports both for callers.

tcp_probe/_discover_ports/_PORTS_DIR are imported back from check_unity.py
lazily (inside each function body, not at module level) -- check_unity.py
imports this module at its own top level, so a top-level reverse import here
would deadlock on the circular reference.
"""

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

# DiagnoseCommand.cs's iscompiling=<bool> field (distinct from get_status's
# own compiling= field) -- the gate probe_open_scenes() checks before ever
# sending 'scene' (allowedDuringCompile=false).
_DIAG_COMPILING_KEY = "iscompiling"


_ENVELOPE_ONLY_KEYS = frozenset({"id", "ok", "data", "err"})


def probe_status(port: int) -> dict:
    """Direct-TCP get_status read: scene/dirty/playing/compiling/port/...
    (CommandRouter.Registration.cs:82-97, alwaysAllowed+allowedDuringCompile
    -- safe any time). Folds through tcp_probe's existing key=value parser,
    stripped of the raw JSON envelope's own id/ok/data/err keys (real Unity
    responses are JSON-enveloped, so tcp_probe's dict otherwise carries both
    the folded fields and the raw envelope side by side)."""
    from check_unity import tcp_probe

    result = tcp_probe(port, cmd="get_status") or {}
    return {k: v for k, v in result.items() if k not in _ENVELOPE_ONLY_KEYS}


def probe_open_scenes(port: int, diag: dict | None = None) -> list[str] | None:
    """One line per open scene (SceneHelper.ListScenes(), SceneHelper.cs:64-78),
    or None when unreachable or mid-compile.

    Gated on a fresh diagnose probe's iscompiling field: 'scene' defaults
    allowedDuringCompile=false (CommandRouter.Registration.cs:562-563), so it
    must never be sent while Unity is compiling. Pass a pre-fetched `diag`
    to avoid a redundant probe when the caller already has one.
    """
    from check_unity import tcp_probe

    if diag is None:
        diag = tcp_probe(port)
    if diag is None or str(diag.get(_DIAG_COMPILING_KEY, "")).lower() == "true":
        return None
    result = tcp_probe(port, cmd="scene", args={"action": "list"})
    if not result:
        return None
    return [line for line in result.get("data", "").splitlines() if line]


def _parse_read_args(argv: list[str]):
    """Closed CLI surface: `status`/`scenes` only, argparse `choices=` --
    never a generic free-form command/argument passthrough (the code-level
    backstop for the direct-TCP read allowlist)."""
    import argparse

    parser = argparse.ArgumentParser(prog="check_unity.py", add_help=False)
    parser.add_argument("subcommand", nargs="?", choices=("status", "scenes"), default=None)
    return parser.parse_args(argv)


def _run_read_subcommand(subcommand: str) -> int:
    """status/scenes exit codes: 0 printed (incl. graceful COMPILING), 1
    transport error, 5 SCRIPT_ERROR."""
    from check_unity import _PORTS_DIR, _discover_ports, tcp_probe

    try:
        main_port, _reload_port = _discover_ports(_PORTS_DIR)
        if not main_port:
            print("UNREACHABLE  no live Unity port discovered")
            return 1
        if subcommand == "status":
            status = probe_status(main_port)
            if not status:
                print("UNREACHABLE  status unavailable")
                return 1
            print("  ".join(f"{k}={v}" for k, v in status.items()))
            return 0
        diag = tcp_probe(main_port)
        if diag is None:
            print("UNREACHABLE  scenes unavailable")
            return 1
        if str(diag.get(_DIAG_COMPILING_KEY, "")).lower() == "true":
            print("COMPILING  scenes unavailable, retry")
            return 0
        scenes = probe_open_scenes(main_port, diag=diag)
        if scenes is None:
            print("UNREACHABLE  scenes unavailable")
            return 1
        if not scenes:
            print("NO_SCENES  no open scenes reported")
            return 0
        for line in scenes:
            print(line)
        return 0
    except Exception as exc:
        print(f"SCRIPT_ERROR  {type(exc).__name__}: {exc}")
        return 5
