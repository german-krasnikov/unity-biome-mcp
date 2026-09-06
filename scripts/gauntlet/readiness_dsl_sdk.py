"""One bounded DSL phase: actual MCP SDK + production tools + project-pinned bridge.

Owns no Unity lifecycle, provider installation, fixtures, or scene cleanup. The
coordinator creates an owned preview object first and supplies its exact ID/name.
The limited composition omits ordinary server lifecycle/middleware: no duplicate
server eviction, global lock cleanup, update check, or replacement polling loop.
"""

import argparse
import asyncio
import contextlib
import json
import re
from datetime import datetime
from pathlib import Path

from gauntlet.readiness_canary import render_dsl


def qualify_receipt(data: dict, *, expect_failure: bool) -> None:
    """Independent semantic ledger oracle; arbitrary errors never satisfy RED."""
    steps = data.get("steps", [])
    expected = [True, not expect_failure]
    valid = (
        data.get("schema_version") == 1 and bool(data.get("run_id"))
        and len(steps) == 2
        and [s.get("index") for s in steps] == [0, 1]
        and [s.get("type") for s in steps] == ["Invoke", "Assert"]
        and [s.get("ok") for s in steps] == expected
        and [s.get("raw_passed") for s in steps] == expected
        and all(s.get("expected_fail") is False for s in steps)
        and all(s.get("console_errored") is False for s in steps)
        and data.get("passed") == sum(expected)
        and data.get("failed") == 2 - sum(expected)
        and data.get("outer", {}).get("teardown_ok") is True
        and data.get("outer", {}).get("scene_clean") is True
    )
    if not valid:
        raise ValueError("DSL ledger does not prove the requested positive/negative semantic case")


@contextlib.asynccontextmanager
async def sdk_session(send):
    """Memory transport still performs MCP initialization, schema and tool dispatch."""
    from mcp import ClientSession
    from mcp.server.fastmcp import FastMCP
    from mcp.shared.memory import create_client_server_memory_streams

    from unity_mcp.tools import editor_control, objects, runtime

    app = FastMCP("owned-readiness-dsl-qualification", log_level="ERROR")
    def args(**kwargs):
        return {key: value for key, value in kwargs.items() if value is not None}
    for module in (editor_control, objects, runtime):
        module.register(app, send, args)
    async with create_client_server_memory_streams() as (client, server):
        task = asyncio.create_task(app._mcp_server.run(
            *server, app._mcp_server.create_initialization_options()))
        try:
            async with ClientSession(*client) as session:
                await session.initialize()
                yield session
        finally:
            task.cancel()
            with contextlib.suppress(asyncio.CancelledError):
                await task


def _stamp():
    return datetime.now().astimezone().isoformat()


def decode_tool_response(name, response, *, expect_failure=False):
    text = "\n".join(block.text for block in response.content if block.type == "text")
    if response.isError:
        prefix = "Error executing tool run_playtest: "
        if name == "run_playtest" and expect_failure and text.startswith(prefix):
            raw = text[len(prefix):]
            qualify_receipt(json.loads(raw), expect_failure=True)
            return raw
        raise ValueError(f"MCP tool error: {name}")
    return text


async def run_phase(session, project: Path, instance_id: int, owned_name: str,
                    expected: int, expect_failure: bool, evidence: Path):
    script = render_dsl("/" + owned_name, expected, instance_id=instance_id)
    evidence.mkdir(parents=True, exist_ok=False)
    record = {"started_at": _stamp(), "project": str(project.resolve()),
              "instance_id": instance_id, "owned_name": owned_name,
              "expected": expected, "expect_failure": expect_failure,
              "script": script, "calls": [], "qualified": False}

    def persist():
        (evidence / "phase.json").write_text(json.dumps(record, indent=2) + "\n", encoding="utf-8")

    async def call(name, arguments):
        row = {"tool": name, "arguments": arguments, "started_at": _stamp()}
        record["calls"].append(row)
        persist()  # A timeout leaves durable evidence of the attempted effect.
        response = await asyncio.wait_for(session.call_tool(name, arguments), timeout=35)
        row.update(finished_at=_stamp(), response=response.model_dump(mode="json"))
        persist()
        return decode_tool_response(name, response, expect_failure=expect_failure)

    def require_edit_idle(state):
        for field in ("playing", "compiling"):
            match = re.search(rf"(?mi)^{field}:\s*(true|false)\s*$", state)
            if not match or match[1].lower() != "false":
                raise ValueError(f"Editor must prove {field}:False")

    try:
        actual = await call("editor", {"action": "project_path"})
        if Path(actual.strip()).resolve() != project.resolve():
            raise ValueError("Project identity mismatch")
        require_edit_idle(await call("editor", {"action": "state"}))
        owned = await call("get_object_detail", {"id": instance_id, "full": True})
        if (not owned.splitlines() or owned.splitlines()[0] != f"name: {owned_name}"
                or "[BuildReadinessCanaryProbe]" not in owned.splitlines()):
            raise ValueError("Instance ID ownership/name/component mismatch")
        raw = await call("run_playtest", {"script": script, "timeout": 10,
                                          "fresh": False, "format": "json",
                                          "snapshot_on_failure": False})
        data = json.loads(raw)
        qualify_receipt(data, expect_failure=expect_failure)
        require_edit_idle(await call("editor", {"action": "state"}))
        record.update(qualified=True, run_id=data["run_id"])
        return record
    finally:
        record["finished_at"] = _stamp()
        persist()


async def main_async(options):
    from mcp.server.fastmcp.exceptions import ToolError

    from unity_mcp.bridge import UnityBridge
    from unity_mcp.bridge_result import unwrap_bridge_result
    from unity_mcp.compile_state import CompileStateProbe

    project = options.project.resolve(strict=True)
    bridge = UnityBridge(port=options.port, probe=CompileStateProbe(project, port=options.port),
                         port_discoverer=lambda: options.port, is_retry_safe=lambda _: False,
                         expected_project_path=project)

    async def send(command, args, timeout=0):
        text, ok = unwrap_bridge_result(await bridge.send(command, args, timeout=min(timeout or 30, 30)))
        if not ok:
            raise ToolError(text)
        return text

    try:
        async with asyncio.timeout(90):
            async with sdk_session(send) as session:
                result = await run_phase(session, project, options.instance_id, options.owned_name,
                                         options.expected, options.expect_failure, options.evidence)
                print(json.dumps({"qualified": result["qualified"], "run_id": result["run_id"],
                                  "evidence": str(options.evidence.resolve())}))
    finally:
        await bridge.close()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project", type=Path, required=True)
    parser.add_argument("--port", type=int, required=True)
    parser.add_argument("--instance-id", type=int, required=True)
    parser.add_argument("--owned-name", required=True)
    parser.add_argument("--expected", type=int, choices=(101, 202), required=True)
    parser.add_argument("--expect-failure", action="store_true")
    parser.add_argument("--evidence", type=Path, required=True)
    options = parser.parse_args()
    if not 1 <= options.port <= 65535:
        parser.error("port outside valid range")
    asyncio.run(main_async(options))


if __name__ == "__main__":
    main()
