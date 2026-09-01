"""Local cache of Source Patch mutation intent (P0-70).

§3.2 forbids probing by writing first, and
test_asset_write_text_cs_sends_exactly_once_regardless_of_route freezes
"exactly one send" per `.cs` write — so `.cs` write routing can never spend a
network round-trip just to ask Unity for the current intent. This module is
therefore a plain, process-local cache: written only by editor_control.editor()
after a successful `mutation_mode` set call, read only by asset.py's `.cs`
write router.

Staleness here is safe by construction, not merely assumed: C#'s
SourcePatchHost.GuardLegacyCsWrite (legacy route) and SourcePatchHost.WriteText
(source_patch_write route) both re-validate the real state pre-effect. A stale
cache can only route to the wrong command, which the C# gate then rejects
loudly — never a silent wrong-route file effect.
"""

_intent_on: bool = False


def set_cached_intent(value: bool) -> None:
    global _intent_on
    _intent_on = value


def get_cached_intent() -> bool:
    return _intent_on
