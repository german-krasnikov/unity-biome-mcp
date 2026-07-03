"""Shared collapse of a raw Unity bridge dict into (text, ok)."""


def unwrap_bridge_result(result: dict) -> "tuple[str, bool]":
    """Collapse {"ok":bool,"data":...,"err":...,"file":...} into (text, ok).

    ok=True: text is data, or "Data saved to: <file>" when file is present
    (combined with data when both are present).
    ok=False: text is the err message (defaults to "Unknown error" if absent).
    """
    ok = bool(result.get("ok"))
    if not ok:
        return result.get("err", "Unknown error"), False
    data = result.get("data", "")
    if "file" in result:
        file_msg = f"Data saved to: {result['file']}"
        return (f"{data}\n{file_msg}" if data else file_msg), True
    return data, True
