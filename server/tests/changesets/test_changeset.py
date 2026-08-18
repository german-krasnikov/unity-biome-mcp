"""T15 ChangeSet model unit tests (18 tests)."""

import json
import tempfile
import uuid
from pathlib import Path
from unittest.mock import AsyncMock, MagicMock, patch

# ── ContentRef ────────────────────────────────────────────────────────────────

def test_content_ref_of_value_deterministic():
    from unity_mcp.changeset import ContentRef
    r1 = ContentRef.of("100")
    r2 = ContentRef.of("100")
    assert r1 == r2


def test_content_ref_of_none_returns_none():
    from unity_mcp.changeset import ContentRef
    assert ContentRef.of(None) is None


def test_content_ref_hash_is_16_hex_chars():
    from unity_mcp.changeset import ContentRef
    ref = ContentRef.of("some value")
    assert len(ref.hash16) == 16
    assert all(c in "0123456789abcdef" for c in ref.hash16)


def test_content_ref_different_values_differ():
    from unity_mcp.changeset import ContentRef
    assert ContentRef.of("100") != ContentRef.of("200")


# ── ChangeOperation ───────────────────────────────────────────────────────────

def test_change_operation_from_receipt_full():
    from unity_mcp.changeset import ChangeOperation, ContentRef
    receipt = {
        "path": "/Player", "op": "modify", "t": "property",
        "prop": "Health", "before": "100", "after": "50", "rev": True,
    }
    op = ChangeOperation.from_receipt(receipt)
    assert op.kind == "modify"
    assert op.target_type == "property"
    assert op.target_path == "/Player"
    assert op.prop == "Health"
    assert op.before_ref == ContentRef.of("100")
    assert op.after_ref == ContentRef.of("50")
    assert op.reversible is True
    assert len(op.operation_id) == 36  # UUID


def test_change_operation_from_receipt_partial():
    from unity_mcp.changeset import ChangeOperation
    receipt = {"path": "/Obj", "op": "create", "t": "scene_object"}
    op = ChangeOperation.from_receipt(receipt)
    assert op.before_ref is None
    assert op.after_ref is None
    assert op.kind == "create"


def test_change_operation_from_receipt_defaults():
    from unity_mcp.changeset import ChangeOperation
    op = ChangeOperation.from_receipt({})
    assert op.kind == "modify"
    assert op.target_type == "scene_object"
    assert op.target_path == ""
    assert op.prop is None
    assert op.reversible is True


def test_change_operation_has_iso_timestamp():
    from unity_mcp.changeset import ChangeOperation
    op = ChangeOperation.from_receipt({"path": "/P"})
    assert "T" in op.timestamp
    assert "+" in op.timestamp or op.timestamp.endswith("Z")


# ── ChangeSet ─────────────────────────────────────────────────────────────────

def test_changeset_initial_status_open():
    from unity_mcp.changeset import ChangeSet
    cs = ChangeSet(
        changeset_id=str(uuid.uuid4()),
        session_id="sess-1",
    )
    assert cs.status == "open"


def test_changeset_turn_id_zero_by_default():
    """turn_id is always 0; no T16 wiring planned."""
    from unity_mcp.changeset import ChangeSet
    cs = ChangeSet(changeset_id=str(uuid.uuid4()), session_id="sess-1")
    assert cs.turn_id == 0


# ── ChangeSetCoordinator ──────────────────────────────────────────────────────

def test_coordinator_creates_on_first_receipt():
    from unity_mcp.changeset_coordinator import ChangeSetCoordinator
    coord = ChangeSetCoordinator(get_session_id=lambda: "sess-1", _no_journal=True)
    assert coord.get_current() is None
    coord.append("set_property", {"path": "/P", "op": "modify", "t": "property"})
    cs = coord.get_current()
    assert cs is not None
    assert cs.status == "open"


def test_coordinator_appends_multiple():
    from unity_mcp.changeset_coordinator import ChangeSetCoordinator
    coord = ChangeSetCoordinator(get_session_id=lambda: "sess-1", _no_journal=True)
    for _ in range(3):
        coord.append("set_property", {"path": "/P", "op": "modify", "t": "property"})
    assert len(coord.get_current().operations) == 3


def test_coordinator_finalize_sets_status():
    from unity_mcp.changeset_coordinator import ChangeSetCoordinator
    coord = ChangeSetCoordinator(get_session_id=lambda: "sess-1", _no_journal=True)
    coord.append("set_property", {"path": "/P"})
    coord.finalize()
    cs = coord.get_current()
    assert cs.status == "finalized"
    assert cs.finalized_at is not None


def test_coordinator_new_session_opens_new_cs():
    from unity_mcp.changeset_coordinator import ChangeSetCoordinator
    session = ["sess-A"]
    coord = ChangeSetCoordinator(get_session_id=lambda: session[0], _no_journal=True)
    coord.append("set_property", {"path": "/P"})
    first_id = coord.get_current().changeset_id

    session[0] = "sess-B"
    coord.append("set_property", {"path": "/Q"})
    assert coord.get_current().changeset_id != first_id
    assert coord.get_current().session_id == "sess-B"


# ── ChangeSetJournal ──────────────────────────────────────────────────────────

def test_journal_writes_header_on_open():
    from unity_mcp.changeset import ChangeSet
    from unity_mcp.changeset_journal import ChangeSetJournal
    with tempfile.TemporaryDirectory() as tmpdir:
        journal = ChangeSetJournal(session_id="sess-1", _dir=Path(tmpdir))
        cs = ChangeSet(
            changeset_id=str(uuid.uuid4()),
            session_id="sess-1",
            created_at="2026-08-14T00:00:00+00:00",
        )
        journal.write_header(cs)
        journal.close()
        lines = (Path(tmpdir) / "sess-1.jsonl").read_text(encoding="utf-8").strip().splitlines()
        record = json.loads(lines[0])
        assert record["_t"] == "cs"
        assert record["id"] == cs.changeset_id


def test_journal_writes_op_line():
    from unity_mcp.changeset import ChangeOperation, ChangeSet
    from unity_mcp.changeset_journal import ChangeSetJournal
    with tempfile.TemporaryDirectory() as tmpdir:
        journal = ChangeSetJournal(session_id="sess-1", _dir=Path(tmpdir))
        cs = ChangeSet(changeset_id=str(uuid.uuid4()), session_id="sess-1")
        op = ChangeOperation.from_receipt(
            {"path": "/Player", "op": "modify", "t": "property",
             "prop": "Health", "before": "100", "after": "50"}
        )
        journal.write_operation(op, cs.changeset_id)
        journal.close()
        lines = (Path(tmpdir) / "sess-1.jsonl").read_text(encoding="utf-8").strip().splitlines()
        record = json.loads(lines[0])
        assert record["_t"] == "op"
        assert record["kind"] == "modify"
        assert "bh" in record
        assert "ah" in record


def test_journal_dedup_same_op_id():
    from unity_mcp.changeset import ChangeOperation, ChangeSet
    from unity_mcp.changeset_journal import ChangeSetJournal
    with tempfile.TemporaryDirectory() as tmpdir:
        journal = ChangeSetJournal(session_id="sess-1", _dir=Path(tmpdir))
        cs = ChangeSet(changeset_id=str(uuid.uuid4()), session_id="sess-1")
        op = ChangeOperation.from_receipt({"path": "/P"})
        # Write twice — dedup guard should prevent second write
        journal.write_operation(op, cs.changeset_id)
        journal.write_operation(op, cs.changeset_id)
        journal.close()
        lines = (Path(tmpdir) / "sess-1.jsonl").read_text(encoding="utf-8").strip().splitlines()
        assert len(lines) == 1


# ── Middleware integration ────────────────────────────────────────────────────

def _make_test_mw():
    """Minimal Middleware mock for wrap_send unit tests."""
    mw = MagicMock()
    mw._alias_cache = {}        # falsy → skip alias resolution
    mw._prefetch_cache = None   # None  → skip prefetch/invalidation branches
    mw.hinter = None
    mw.schema_guard = None
    mw.watchdog = None
    mw.speculation = None
    mw.lessons = None
    mw.inferrer = None
    mw.session = None
    mw.recorder = None
    mw.scene_brief = None
    mw._negative_path_cache = None
    mw.call_count = 0
    mw.circuit.allow_request.return_value = True
    mw.circuit._probe_in_flight = False
    mw.check_retry.return_value = None
    mw.check_taint.return_value = None
    mw.check_dead_write.return_value = None
    mw.check_blast_radius.return_value = None
    mw.check_verification_needed.return_value = None
    mw.check_play_mode_required.return_value = None
    mw.check_read_only.return_value = None
    mw.check_alive.return_value = True
    mw.transition.return_value = None
    mw.categorize_console_errors.side_effect = lambda r: r
    mw.check_starvation.side_effect = lambda r: r
    mw.update_confidence.side_effect = lambda c, a, r: r
    mw.maybe_inject_state = AsyncMock(side_effect=lambda sf, r, c, a: r)
    mw.maybe_verify_visual = AsyncMock(side_effect=lambda c, a, r: r)
    mw._maybe_distill = AsyncMock(side_effect=lambda c, a, r, **kw: r)
    return mw


async def test_middleware_extracts_receipt_and_feeds_coordinator():
    from unity_mcp.changeset_coordinator import ChangeSetCoordinator
    from unity_mcp.middleware_pipeline import wrap_send

    coord = ChangeSetCoordinator(get_session_id=lambda: "sess-1", _no_journal=True)
    raw = {"ok": True, "data": "Health = 50", "receipt": {"path": "/P", "op": "modify"}}
    send_fn = AsyncMock(return_value=raw)

    mw = _make_test_mw()
    # _explicit_path skips live path resolution; _no_reflect skips asymmetric reflection
    mw.reroute_cmd.return_value = ("set_property", {"path": "/P"})

    with patch("unity_mcp.changeset_coordinator.get_coordinator", return_value=coord):
        wrapped = wrap_send(send_fn, mw=mw)
        await wrapped("set_property", {"path": "/P", "_explicit_path": True, "_no_reflect": True})

    cs = coord.get_current()
    assert cs is not None
    assert len(cs.operations) == 1
    assert cs.operations[0].target_path == "/P"


async def test_middleware_no_receipt_no_coordinator_call():
    from unity_mcp.middleware_pipeline import wrap_send

    mock_coord = MagicMock()
    raw = {"ok": True, "data": "no change"}  # no receipt key
    send_fn = AsyncMock(return_value=raw)

    mw = _make_test_mw()
    mw.reroute_cmd.return_value = ("set_property", {"path": "/Q"})

    with patch("unity_mcp.changeset_coordinator.get_coordinator", return_value=mock_coord):
        wrapped = wrap_send(send_fn, mw=mw)
        await wrapped("set_property", {"path": "/Q", "_explicit_path": True, "_no_reflect": True})

    mock_coord.append.assert_not_called()


# ── Backward compat ───────────────────────────────────────────────────────────

def test_old_unwrap_ignores_receipt_field():
    from unity_mcp.bridge_result import unwrap_bridge_result
    data, ok = unwrap_bridge_result(
        {"ok": True, "data": "x", "receipt": {"path": "/P"}}
    )
    assert data == "x"
    assert ok is True


# ── paths ─────────────────────────────────────────────────────────────────────

def test_journals_dir_under_unity_mcp_dir():
    from unity_mcp.paths import journals_dir, unity_mcp_dir
    assert journals_dir() == unity_mcp_dir() / "journals"


# ── M2: gate changeset op on actual file change ───────────────────────────────

async def test_write_text_no_record_when_file_unchanged():
    """No changeset op when write fails (before_ref == after_ref — file unchanged)."""
    import unity_mcp.tools.asset as asset_mod
    from unity_mcp.changeset import ContentRef

    ref = ContentRef.of("original content")
    coord = MagicMock()

    with patch("unity_mcp.changeset_file_capture.snapshot_file", return_value=ref), \
         patch("unity_mcp.changeset_coordinator.get_coordinator", return_value=coord), \
         patch("unity_mcp.changeset_store.get_store", return_value=MagicMock()), \
         patch.object(asset_mod, "_send", AsyncMock(return_value="Error: write failed")), \
         patch.object(asset_mod, "_args", lambda **kw: kw):
        await asset_mod._write_text_with_capture("Assets/Foo.cs", "new content")

    coord.append_file_op.assert_not_called()


# ── M3: finalized changeset blocks further mutations ─────────────────────────

def test_coordinator_append_after_finalize_starts_new_cs():
    """After finalize(), append() opens a fresh changeset instead of mutating the old one."""
    from unity_mcp.changeset_coordinator import ChangeSetCoordinator

    coord = ChangeSetCoordinator(get_session_id=lambda: "sess-1", _no_journal=True)
    coord.append("set_property", {"path": "/A"})
    coord.finalize()
    first_id = coord.get_current().changeset_id

    coord.append("set_property", {"path": "/B"})

    cs = coord.get_current()
    assert cs.changeset_id != first_id
    assert cs.status == "open"
    assert len(cs.operations) == 1  # only the post-finalize op


# ── M4: derive kind from before/after refs ────────────────────────────────────

def test_append_file_op_kind_create_when_no_before():
    """before_ref=None → kind='create'."""
    from unity_mcp.changeset import ContentRef
    from unity_mcp.changeset_coordinator import ChangeSetCoordinator

    coord = ChangeSetCoordinator(get_session_id=lambda: "sess-1", _no_journal=True)
    coord.append_file_op("asset.write_text", "/Foo.cs",
                         before_ref=None, after_ref=ContentRef.of("content"))
    assert coord.get_current().operations[0].kind == "create"


def test_append_file_op_kind_delete_when_no_after():
    """after_ref=None → kind='delete'."""
    from unity_mcp.changeset import ContentRef
    from unity_mcp.changeset_coordinator import ChangeSetCoordinator

    coord = ChangeSetCoordinator(get_session_id=lambda: "sess-1", _no_journal=True)
    coord.append_file_op("asset.write_text", "/Foo.cs",
                         before_ref=ContentRef.of("original"), after_ref=None)
    assert coord.get_current().operations[0].kind == "delete"


def test_append_file_op_kind_modify_when_both_present():
    """before_ref and after_ref both present → kind='modify'."""
    from unity_mcp.changeset import ContentRef
    from unity_mcp.changeset_coordinator import ChangeSetCoordinator

    coord = ChangeSetCoordinator(get_session_id=lambda: "sess-1", _no_journal=True)
    coord.append_file_op("asset.write_text", "/Foo.cs",
                         before_ref=ContentRef.of("old"), after_ref=ContentRef.of("new"))
    assert coord.get_current().operations[0].kind == "modify"


# ── ChangeSetJournal path traversal ──────────────────────────────────────────

def test_journal_session_id_path_traversal_rejected(tmp_path):
    """session_id with directory separators must not escape the base dir."""
    from unity_mcp.changeset_journal import ChangeSetJournal
    journal = ChangeSetJournal("../../../etc/evil", _dir=tmp_path)
    # The file must land inside tmp_path, not outside
    assert journal._path.parent == tmp_path
    journal.close()
