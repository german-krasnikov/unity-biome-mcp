"""Shared collapse of a raw Unity bridge dict into (text, ok)."""
from biome_mcp.models.result import CommandResult, CommandStatus


def unwrap_bridge_result(result: dict) -> tuple[str, bool]:
    """Collapse {"ok":bool,"data":...,"err":...,"file":...} into (text, ok).

    ok=True: text is data, or "Data saved to: <file>" when file is present
    (combined with data when both are present).
    ok=False: text is the err message (defaults to "Unknown error" if absent).

    The ok/error split and raw `data` extraction delegate to biome-mcp's
    CommandResult.from_raw; the "file" combination and the exact
    "Unknown error" default are Unity-specific and stay local (biome's
    generic from_raw knows neither).
    """
    cr = CommandResult.from_raw(result)
    if cr.status != CommandStatus.OK:
        return result.get("err", "Unknown error"), False
    data = cr.data
    if "file" in result:
        file_msg = f"Data saved to: {result['file']}"
        return (f"{data}\n{file_msg}" if data else file_msg), True
    return data, True
