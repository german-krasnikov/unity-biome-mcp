"""TDD tests for PermissionBroker — mode-aware permission policy."""
import uuid

from unity_mcp.permission_broker import PermissionBroker


def _b(mode):
    return PermissionBroker(mode=mode)


def test_external_mcp_client_no_token_bypasses_chat_policy():
    # mode=None → external client → allow all, including mutations
    assert _b(None).decide("set_property").outcome == "allow_by_saved_policy"


def test_ask_mode_denies_mutation_tools():
    assert _b("ask").decide("set_property").outcome == "deny"


def test_ask_mode_allows_read_tools():
    assert _b("ask").decide("get_hierarchy").outcome == "allow_by_saved_policy"


def test_agent_mode_allows_all_standard_tools():
    assert _b("agent").decide("set_property").outcome == "allow_by_saved_policy"


def test_full_access_allows_all_tools():
    assert _b("full-access").decide("delete_object").outcome == "allow_by_saved_policy"


def test_none_mode_allows_all_tools():
    assert _b(None).decide("delete_object").outcome == "allow_by_saved_policy"


def test_decision_has_unique_id():
    d = _b(None).decide("get_hierarchy")
    uuid.UUID(d.decision_id)  # raises ValueError if not a valid UUID


def test_unknown_plugin_tool_allowed_in_ask_mode_when_not_write():
    # unknown tools not in WRITE_CMDS → not a write → allowed even in ask mode
    assert _b("ask").decide("plugin_custom_tool").outcome == "allow_by_saved_policy"


def test_agent_mode_denies_execute_code():
    assert _b("agent").decide("execute_code").outcome == "deny"


def test_unknown_mode_denied():
    assert _b("typo_mode").decide("get_hierarchy").outcome == "deny"
