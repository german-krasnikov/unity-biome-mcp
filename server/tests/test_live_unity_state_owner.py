"""Unit tests for live Unity state ownership; no Editor is required."""

import base64
from pathlib import Path
from unittest.mock import AsyncMock, patch

import pytest

from tests.live import conftest as live_conftest
from tests.live.unity_state_owner import (
    ObjectState,
    OwnershipPolicy,
    SceneState,
    UnityStateSnapshot,
    build_ownership_plan,
)


def _scene(path, *, name="Scene", handle=1, dirty=False, active=True):
    return SceneState(path, name, handle, dirty, active)


def _obj(gid, transient_id, scene, hierarchy, name):
    return ObjectState(gid, str(transient_id), scene, hierarchy, name)


def _snapshot(*, scenes, objects=(), assets=(), playing=False, time_scale=1.0):
    return UnityStateSnapshot(
        playing,
        tuple(scenes),
        tuple(objects),
        tuple(assets),
        time_scale,
    )


def _b64(value):
    return base64.b64encode(value.encode()).decode()


def test_snapshot_parser_preserves_duplicate_names_by_stable_identity():
    payload = "\n".join([
        "P\t0",
        "T\t1",
        f"S\t{_b64('Assets/Test.unity')}\t{_b64('Test')}\t1\t0\t1\t0",
        (
            f"O\t{_b64('gid-1')}\t101\t{_b64('Assets/Test.unity')}"
            f"\t{_b64('0')}\t{_b64('SameName')}\t0"
        ),
        (
            f"O\t{_b64('gid-2')}\t102\t{_b64('Assets/Test.unity')}"
            f"\t{_b64('1')}\t{_b64('SameName')}\t0"
        ),
    ])

    snapshot = UnityStateSnapshot.parse(payload)

    assert [obj.identity for obj in snapshot.objects] == [
        "global:gid-1", "global:gid-2",
    ]


def test_snapshot_parser_rejects_duplicate_stable_object_identity():
    payload = "\n".join([
        "P\t0",
        "T\t1",
        f"S\t{_b64('Assets/Test.unity')}\t{_b64('Test')}\t1\t0\t1\t0",
        f"O\t{_b64('gid')}\t1\t{_b64('Assets/Test.unity')}\t{_b64('0')}\t{_b64('A')}\t0",
        f"O\t{_b64('gid')}\t2\t{_b64('Assets/Test.unity')}\t{_b64('1')}\t{_b64('B')}\t0",
    ])

    with pytest.raises(ValueError, match="duplicate object identities"):
        UnityStateSnapshot.parse(payload)


def test_snapshot_normalizes_signed_legacy_instance_id_to_uint64_wire_value():
    legacy_id = -33506
    normalized = str((1 << 64) + legacy_id)
    payload = "\n".join([
        "P\t0",
        "T\t1",
        f"S\t{_b64('Assets/Test.unity')}\t{_b64('Test')}\t1\t0\t1\t0",
        (
            f"O\t{_b64('')}\t{legacy_id}\t{_b64('Assets/Test.unity')}"
            f"\t{_b64('0')}\t{_b64('Legacy')}\t0"
        ),
    ])

    snapshot = UnityStateSnapshot.parse(payload)

    assert snapshot.objects[0].transient_id == normalized
    assert snapshot.objects[0].identity == f"transient:{normalized}"


def test_snapshot_preserves_full_uint64_entity_id_without_overflow():
    entity_id = str((1 << 64) - 1)
    payload = "\n".join([
        "P\t0",
        "T\t1",
        f"S\t{_b64('Assets/Test.unity')}\t{_b64('Test')}\t1\t0\t1\t0",
        (
            f"O\t{_b64('')}\t{entity_id}\t{_b64('Assets/Test.unity')}"
            f"\t{_b64('0')}\t{_b64('Entity')}\t0"
        ),
    ])

    snapshot = UnityStateSnapshot.parse(payload)

    assert snapshot.objects[0].transient_id == entity_id


def test_hash_reference_parser_preserves_full_uint64_entity_id():
    entity_id = str((1 << 64) - 1)

    assert live_conftest._transient_ref(f"object #{entity_id}") == f"#{entity_id}"


@pytest.mark.parametrize("transient_id", ["0", str(1 << 64), "-2147483649"])
def test_snapshot_rejects_out_of_range_transient_ids(transient_id):
    payload = "\n".join([
        "P\t0",
        "T\t1",
        f"S\t{_b64('Assets/Test.unity')}\t{_b64('Test')}\t1\t0\t1\t0",
        (
            f"O\t{_b64('')}\t{transient_id}\t{_b64('Assets/Test.unity')}"
            f"\t{_b64('0')}\t{_b64('Invalid')}\t0"
        ),
    ])

    with pytest.raises(ValueError, match="invalid Unity snapshot record"):
        UnityStateSnapshot.parse(payload)


def test_snapshot_code_uses_canonical_unity_6000_0_instance_id_api():
    code = live_conftest._build_state_snapshot_code()

    assert "GetInstanceID" in code
    assert "GetEntityId" not in code
    assert "System.Globalization.CultureInfo.InvariantCulture" in code
    assert "Time.timeScale" in code
    assert "FindAssets(" in code
    assert '"", new string[]' in code
    assert '"t:Scene"' not in code
    assert "Directory.EnumerateFiles" in code
    assert live_conftest.RUN_OWNED_ROOT in code


@pytest.mark.asyncio
async def test_cleanup_resolves_transient_id_with_canonical_instance_id_api(
    monkeypatch,
):
    emitted = []

    async def fake_execute(_bridge, code, _operation):
        emitted.append(code)
        return "already-absent"

    monkeypatch.setattr(live_conftest, "_execute_checked", fake_execute)
    obj = _obj("", str((1 << 64) - 1), "Assets/Test.unity", "0", "Owned")

    await live_conftest._destroy_owned_object(object(), obj)

    assert len(emitted) == 1
    assert "InstanceIDToObject" in emitted[0]
    assert "EntityIdToObject" not in emitted[0]
    assert str((1 << 64) - 1) in emitted[0]
    assert "go.name" in emitted[0]
    assert "GetSiblingIndex" in emitted[0]


@pytest.mark.asyncio
async def test_cleanup_never_falls_back_from_stable_to_transient_id(monkeypatch):
    emitted = []

    async def fake_execute(_bridge, code, _operation):
        emitted.append(code)
        return "already-absent"

    monkeypatch.setattr(live_conftest, "_execute_checked", fake_execute)
    obj = _obj("stable-id", 42, "Assets/Test.unity", "0", "Owned")

    await live_conftest._destroy_owned_object(object(), obj)

    stable_branch = emitted[0].split("} else {", 1)[0]
    assert "TryParse(stable" in stable_branch
    assert "InstanceIDToObject" not in stable_branch


def test_owned_test_scene_mutations_are_reset_not_reported_as_user_mutations():
    path = "Assets/TestsTemp/__python_live.unity"
    base = _obj("gid-base", 10, path, "0", "GridPlayer")
    added = _obj("", 20, path, "1", "AnyName")
    before = _snapshot(scenes=[_scene(path)], objects=[base])
    after = _snapshot(scenes=[_scene(path, dirty=True)], objects=[base, added])
    policy = OwnershipPolicy(scene_paths={path}, reset_scene_path=path)

    plan = build_ownership_plan(before, after, policy)

    assert plan.owned_added_objects == (added,)
    assert plan.reset_owned_scene is True
    assert plan.violations == ()


def test_unowned_same_name_object_is_never_treated_as_owned():
    user = "Assets/UserScene.unity"
    before = _snapshot(scenes=[_scene(user)], objects=[])
    intruder = _obj("gid-user", 50, user, "0", "Live_Object")
    after = _snapshot(scenes=[_scene(user, dirty=True)], objects=[intruder])
    policy = OwnershipPolicy(scene_paths={"Assets/TestsTemp/Owned.unity"})

    plan = build_ownership_plan(before, after, policy)

    assert plan.owned_added_objects == ()
    assert plan.unowned_added_objects == (intruder,)
    assert len(plan.violations) == 2


def test_only_explicit_object_identity_authorizes_cleanup():
    user = "Assets/UserScene.unity"
    before = _snapshot(scenes=[_scene(user)], objects=[])
    owned = _obj("gid-owned", 70, user, "0", "PyOwned_123")
    after = _snapshot(scenes=[_scene(user, dirty=True)], objects=[owned])
    policy = OwnershipPolicy(object_ids={owned.identity})

    plan = build_ownership_plan(before, after, policy)

    assert plan.owned_added_objects == (owned,)
    assert plan.owned_added_objects[0].identity == "global:gid-owned"
    assert plan.unowned_dirty_scenes == (_scene(user, dirty=True),)


def test_removed_or_renamed_baseline_object_is_a_loud_violation():
    user = "Assets/UserScene.unity"
    original = _obj("gid-existing", 1, user, "0", "Original")
    renamed = _obj("gid-existing", 1, user, "0", "Renamed")
    policy = OwnershipPolicy()

    removed = build_ownership_plan(
        _snapshot(scenes=[_scene(user)], objects=[original]),
        _snapshot(scenes=[_scene(user)], objects=[]),
        policy,
    )
    changed = build_ownership_plan(
        _snapshot(scenes=[_scene(user)], objects=[original]),
        _snapshot(scenes=[_scene(user)], objects=[renamed]),
        policy,
    )

    assert any("baseline object removed" in item for item in removed.violations)
    assert any("moved or renamed" in item for item in changed.violations)


def test_pathless_added_scene_cannot_be_owned_by_a_similar_registered_path():
    base = _snapshot(scenes=[_scene("Assets/User.unity")])
    untitled = _scene("", name="LiveMS_fake", handle=9, active=False)
    after = _snapshot(scenes=[*base.scenes, untitled])
    policy = OwnershipPolicy(
        scene_paths={"Assets/TestsTemp/LiveMS_fake.unity"}
    )

    plan = build_ownership_plan(base, after, policy)

    assert plan.owned_added_scenes == ()
    assert plan.unowned_added_scenes == (untitled,)


def test_asset_cleanup_requires_an_exact_registered_path():
    before = _snapshot(
        scenes=[_scene("Assets/TestsTemp/__python_live.unity")],
        assets=["Assets/TestsTemp/__python_live.unity"],
    )
    after = _snapshot(
        scenes=before.scenes,
        assets=[
            *before.assets,
            "Assets/TestsTemp/LiveMS_owned.unity",
            "Assets/TestsTemp/LiveMS_owned.asset",
            "Assets/UserScene.unity",
        ],
    )
    policy = OwnershipPolicy(
        asset_paths={"Assets/TestsTemp/LiveMS_owned.unity"}
    )

    plan = build_ownership_plan(before, after, policy)

    assert plan.owned_added_assets == ("Assets/TestsTemp/LiveMS_owned.unity",)
    assert plan.unowned_added_assets == (
        "Assets/TestsTemp/LiveMS_owned.asset",
        "Assets/UserScene.unity",
    )


def test_production_policy_rejects_registered_paths_outside_run_root():
    root = "Assets/TestsTemp/PythonLive/run-1"
    outside = "Assets/UserScene.unity"
    policy = OwnershipPolicy(
        scene_paths={outside},
        asset_paths={outside},
        object_ids={"global:user-object"},
        allowed_path_root=root,
    )
    user_object = _obj("user-object", 7, outside, "0", "User")

    assert policy.owns_scene_path(outside) is False
    assert policy.owns_asset_path(outside) is False
    assert policy.owns_object(user_object) is False
    assert policy.owns_scene_path(f"{root}/../UserScene.unity") is False


def test_mode_and_time_scale_drift_are_loud_violations():
    scene = _scene("Assets/TestsTemp/Owned.unity")
    before = _snapshot(scenes=[scene], playing=False, time_scale=1.0)
    after = _snapshot(scenes=[scene], playing=True, time_scale=0.25)

    plan = build_ownership_plan(
        before,
        after,
        OwnershipPolicy(scene_paths={scene.path}),
    )

    assert plan.play_mode_changed is True
    assert plan.time_scale_changed is True
    assert "Play/Edit mode changed during the test" in plan.violations
    assert "Time.timeScale changed during the test" in plan.violations


def test_expected_play_mode_transition_is_restored_without_policy_violation():
    scene = _scene("Assets/TestsTemp/Owned.unity")
    before = _snapshot(scenes=[scene], playing=False)
    after = _snapshot(scenes=[scene], playing=True)

    plan = build_ownership_plan(
        before,
        after,
        OwnershipPolicy(
            scene_paths={scene.path},
            allowed_play_mode_target=True,
        ),
    )

    assert plan.play_mode_changed is True
    assert plan.play_mode_transition_allowed is True
    assert "Play/Edit mode changed during the test" not in plan.violations


def test_playmode_policy_does_not_hide_unexpected_exit_to_edit_mode():
    scene = _scene("Assets/TestsTemp/Owned.unity")
    before = _snapshot(scenes=[scene], playing=True)
    after = _snapshot(scenes=[scene], playing=False)

    plan = build_ownership_plan(
        before,
        after,
        OwnershipPolicy(
            scene_paths={scene.path},
            allowed_play_mode_target=True,
        ),
    )

    assert plan.play_mode_changed is True
    assert plan.play_mode_transition_allowed is False
    assert "Play/Edit mode changed during the test" in plan.violations


@pytest.mark.asyncio
async def test_restore_is_fail_closed_when_unowned_dirty_scene_exists(monkeypatch):
    owned_path = "Assets/TestsTemp/Owned.unity"
    before = _snapshot(
        scenes=[_scene(owned_path)],
        assets=[owned_path],
    )
    unsafe = _snapshot(
        scenes=[
            _scene(owned_path, dirty=True),
            _scene("Assets/User.unity", handle=2, dirty=True, active=False),
        ],
        assets=[owned_path],
    )
    resets = []

    async def fake_capture(_bridge):
        return unsafe

    async def fake_reset(*args):
        resets.append(args)

    monkeypatch.setattr(live_conftest, "_capture_unity_state", fake_capture)
    monkeypatch.setattr(live_conftest, "_reset_owned_scene", fake_reset)

    with pytest.raises(AssertionError, match="owned scene reset was blocked"):
        await live_conftest._restore_owned_state(
            object(),
            before,
            OwnershipPolicy(
                scene_paths={owned_path},
                asset_paths={owned_path},
                reset_scene_path=owned_path,
            ),
        )

    assert resets == []


@pytest.mark.asyncio
async def test_restore_resets_time_scale_but_reports_test_drift(monkeypatch):
    path = "Assets/TestsTemp/Owned.unity"
    before = _snapshot(scenes=[_scene(path)], assets=[path], time_scale=1.0)
    drifted = _snapshot(scenes=[_scene(path)], assets=[path], time_scale=0.5)
    captures = iter((drifted, before, before))
    restored = []

    async def fake_capture(_bridge):
        return next(captures)

    async def fake_restore(_bridge, expected):
        restored.append(expected)

    monkeypatch.setattr(live_conftest, "_capture_unity_state", fake_capture)
    monkeypatch.setattr(live_conftest, "_restore_time_scale", fake_restore)

    with pytest.raises(AssertionError, match="Time.timeScale changed"):
        await live_conftest._restore_owned_state(
            object(),
            before,
            OwnershipPolicy(
                scene_paths={path},
                asset_paths={path},
                reset_scene_path=path,
            ),
        )

    assert restored == [1.0]


@pytest.mark.asyncio
async def test_restore_returns_to_baseline_mode_before_scene_reset(monkeypatch):
    path = "Assets/TestsTemp/Owned.unity"
    before = _snapshot(scenes=[_scene(path)], assets=[path], playing=False)
    drifted = _snapshot(scenes=[_scene(path)], assets=[path], playing=True)
    captures = iter((drifted, before, before))
    restored_modes = []
    resets = []

    async def fake_capture(_bridge):
        return next(captures)

    async def fake_restore_mode(_bridge, expected):
        restored_modes.append(expected)

    async def fake_reset(_bridge, reset_path, playing):
        resets.append((reset_path, playing))

    monkeypatch.setattr(live_conftest, "_capture_unity_state", fake_capture)
    monkeypatch.setattr(live_conftest, "_restore_play_mode", fake_restore_mode)
    monkeypatch.setattr(live_conftest, "_reset_owned_scene", fake_reset)

    with pytest.raises(AssertionError, match="Play/Edit mode changed"):
        await live_conftest._restore_owned_state(
            object(),
            before,
            OwnershipPolicy(
                scene_paths={path},
                asset_paths={path},
                reset_scene_path=path,
            ),
        )

    assert restored_modes == [False]
    assert resets == [(path, False)]


@pytest.mark.asyncio
async def test_expected_playmode_fixture_transition_restores_without_error(monkeypatch):
    path = "Assets/TestsTemp/Owned.unity"
    before = _snapshot(scenes=[_scene(path)], assets=[path], playing=False)
    drifted = _snapshot(scenes=[_scene(path)], assets=[path], playing=True)
    captures = iter((drifted, before, before))
    restored_modes = []
    resets = []

    async def fake_capture(_bridge):
        return next(captures)

    async def fake_restore_mode(_bridge, expected):
        restored_modes.append(expected)

    async def fake_reset(_bridge, reset_path, playing):
        resets.append((reset_path, playing))

    monkeypatch.setattr(live_conftest, "_capture_unity_state", fake_capture)
    monkeypatch.setattr(live_conftest, "_restore_play_mode", fake_restore_mode)
    monkeypatch.setattr(live_conftest, "_reset_owned_scene", fake_reset)

    await live_conftest._restore_owned_state(
        object(),
        before,
        OwnershipPolicy(
            scene_paths={path},
            asset_paths={path},
            reset_scene_path=path,
            allowed_play_mode_target=True,
        ),
    )

    assert restored_modes == [False]
    assert resets == [(path, False)]


@pytest.mark.asyncio
async def test_failed_playmode_setup_stops_play_before_failing(monkeypatch):
    events = []

    class FakeBridge:
        async def close(self):
            events.append("close")

    async def fake_connect(_bridge):
        events.append("connect")

    async def fake_enter(_bridge):
        events.append("enter")
        raise RuntimeError("setup failed")

    async def fake_stop(_bridge):
        events.append("stop")

    monkeypatch.setattr(live_conftest, "make_live_bridge", FakeBridge)
    monkeypatch.setattr(live_conftest, "_connect_with_retry", fake_connect)
    monkeypatch.setattr(live_conftest, "_enter_play", fake_enter)
    monkeypatch.setattr(live_conftest, "_stop_play", fake_stop)
    fixture = live_conftest._play_mode_session.__wrapped__(object())

    with pytest.raises(pytest.fail.Exception, match="Could not enter Play Mode"):
        await anext(fixture)

    assert events == ["connect", "enter", "stop", "close"]


@pytest.mark.asyncio
async def test_every_playmode_test_reloads_owned_primary_scene(monkeypatch):
    path = "Assets/TestsTemp/Owned.unity"
    before_runtime = _obj("", 101, path, "1", "RuntimeObject")
    final_runtime = _obj("", 202, path, "1", "RuntimeObject")
    stable = _obj("stable-player", 10, path, "0", "GridPlayer")
    before = _snapshot(
        scenes=[_scene(path, handle=10)],
        objects=[stable, before_runtime],
        assets=[path],
        playing=True,
    )
    final = _snapshot(
        scenes=[_scene(path, handle=11)],
        objects=[stable, final_runtime],
        assets=[path],
        playing=True,
    )
    captures = iter((before, final))
    resets = []

    async def fake_capture(_bridge):
        return next(captures)

    async def fake_reset(_bridge, reset_path, playing):
        resets.append((reset_path, playing))

    monkeypatch.setattr(live_conftest, "_capture_unity_state", fake_capture)
    monkeypatch.setattr(live_conftest, "_reset_owned_scene", fake_reset)

    await live_conftest._restore_owned_state(
        object(),
        before,
        OwnershipPolicy(
            scene_paths={path},
            asset_paths={path},
            reset_scene_path=path,
        ),
    )

    assert resets == [(path, True)]


@pytest.mark.asyncio
async def test_clean_editmode_test_skips_redundant_scene_reset(monkeypatch):
    # A17: an EditMode run with zero plan-derived scene mutation no longer
    # pays for a reload — see test_unity_state_owner_reset.py for the
    # dedicated skip/reset pair this behavior change was built against.
    path = "Assets/TestsTemp/Owned.unity"
    snapshot = _snapshot(
        scenes=[_scene(path)],
        objects=[_obj("stable-player", 10, path, "0", "GridPlayer")],
        assets=[path],
    )
    captures = iter((snapshot, snapshot))
    resets = []

    async def fake_capture(_bridge):
        return next(captures)

    async def fake_reset(_bridge, reset_path, playing):
        resets.append((reset_path, playing))

    monkeypatch.setattr(live_conftest, "_capture_unity_state", fake_capture)
    monkeypatch.setattr(live_conftest, "_reset_owned_scene", fake_reset)

    await live_conftest._restore_owned_state(
        object(),
        snapshot,
        OwnershipPolicy(
            scene_paths={path},
            asset_paths={path},
            reset_scene_path=path,
        ),
    )

    assert resets == []


@pytest.mark.asyncio
async def test_editmode_reset_discards_memory_and_saved_asset_mutations(monkeypatch):
    emitted = []

    async def fake_execute(_bridge, code, _operation):
        emitted.append(code)
        return live_conftest.OWNED_LIVE_SCENE

    monkeypatch.setattr(live_conftest, "_execute_checked", fake_execute)

    await live_conftest._reset_owned_scene(
        object(),
        live_conftest.OWNED_LIVE_SCENE,
        playing=False,
    )

    assert "CloseScene" in emitted[0]
    assert "File.ReadAllBytes" in emitted[0]
    assert "File.Copy" in emitted[0]
    assert live_conftest.GRIDTEST_SCENE in emitted[0]
    assert "unsafe-loaded-scene" in emitted[0]
    assert "activeGuard.handle != guard.handle" in emitted[0]
    assert "OpenSceneMode.Single" in emitted[0]


def test_global_transitions_reject_pathless_dirty_and_unowned_scenes():
    owned = "Assets/TestsTemp/Owned.unity"
    state = _snapshot(scenes=[
        _scene(owned, dirty=True),
        _scene("", name="Untitled", handle=2, active=False),
        _scene("Assets/User.unity", handle=3, active=False),
    ])

    blockers = live_conftest._global_transition_blockers(
        state,
        OwnershipPolicy(scene_paths={owned}),
    )

    assert any("dirty scene loaded" in blocker for blocker in blockers)
    assert any("pathless scene loaded" in blocker for blocker in blockers)
    assert any("unowned scene loaded" in blocker for blocker in blockers)


@pytest.mark.asyncio
async def test_generic_owned_asset_delete_handles_files_and_directories(monkeypatch):
    emitted = []

    async def fake_execute(_bridge, code, _operation):
        emitted.append(code)
        return "already-absent"

    monkeypatch.setattr(live_conftest, "_execute_checked", fake_execute)

    path = f"{live_conftest.RUN_OWNED_ROOT}/data.bin"
    await live_conftest._delete_owned_asset(object(), path)

    assert "AssetDatabase.DeleteAsset" in emitted[0]
    assert "File.Delete" in emitted[0]
    assert "Directory.Delete" in emitted[0]
    assert "SceneAsset" not in emitted[0]


def test_live_conftest_has_mandatory_owner_and_never_saves_user_scenes():
    source = (Path(__file__).parent / "live" / "conftest.py").read_text(
        encoding="utf-8"
    )

    assert "async def unity_state_owner" in source
    assert "@pytest_asyncio.fixture(autouse=True)" in source
    assert "GlobalObjectId" in source
    assert "_orphan_guard" not in source
    assert "SaveScene(" not in source
    assert "TransientObjectId" not in source
    assert "pkill" not in source
    assert "subprocess" not in source
    assert "async def _live_suite_lease" in source
    assert "_ensure_gridtest_scene(_live_suite_lease)" in source


@pytest.mark.parametrize(
    ("host", "expected"),
    [
        ("127.0.0.1", True),
        ("::1", True),
        ("localhost", True),
        ("192.0.2.1", False),
        ("unity.example.test", False),
    ],
)
def test_live_lease_local_process_proof_only_for_loopback(host, expected):
    assert live_conftest._is_loopback_host(host) is expected


def test_live_lease_acquire_is_atomic_and_never_reclaims_a_live_owner():
    code = live_conftest._build_live_lease_acquire_code()

    assert "UnityEditor.SessionState.GetString(ownerKey" in code
    assert "System.Diagnostics.Process.GetProcessById(ownerPid)" in code
    assert "process.StartTime.ToUniversalTime().Ticks" in code
    assert "held-live-owner:" in code
    assert "held-remote-owner:" in code
    assert "held-unverifiable-owner:" in code
    assert "reclaimed-dead-owner" in code
    assert code.count("SessionState.SetString(ownerKey, token)") == 1
    assert code.index("held-live-owner:") < code.index(
        "SessionState.SetString(ownerKey, token)"
    )


@pytest.mark.asyncio
async def test_live_lease_acquire_uses_one_execute_code_request(monkeypatch):
    emitted = []

    async def fake_execute(_bridge, code, operation):
        emitted.append((code, operation))
        return "acquired"

    monkeypatch.setattr(live_conftest, "_execute_checked", fake_execute)

    outcome = await live_conftest._acquire_live_suite_lease(object())

    assert outcome == "acquired"
    assert len(emitted) == 1
    assert emitted[0][1] == "acquire live-suite lease"


def test_live_lease_release_is_owner_fenced_and_retry_idempotent():
    code = live_conftest._build_live_lease_release_code()
    owner_guard = code.index('if (owner != token) return "not-owner:"')
    owner_clear = code.index('SessionState.SetString(ownerKey, "")')

    assert "already-released" in code
    assert owner_guard < owner_clear
    assert code.count('SessionState.SetString(ownerKey, "")') == 1


@pytest.mark.parametrize(
    ("operation", "outcome"),
    [
        ("acquire", "held-live-owner:other"),
        ("renew", "not-owner:other"),
        ("release", "not-owner:other"),
    ],
)
def test_live_lease_refuses_operations_without_ownership(operation, outcome):
    with pytest.raises(AssertionError, match="Another live suite may own"):
        live_conftest._assert_lease_owned(outcome, operation)


def test_live_lease_accepts_idempotent_transport_retries():
    live_conftest._assert_lease_owned("renewed", "acquire")
    live_conftest._assert_lease_owned("already-released", "release")


def test_multiscene_live_tests_use_canonical_transient_id_expression():
    live_dir = Path(__file__).parent / "live"
    for filename in ("test_multiscene_live.py", "test_multiscene_stress_live.py"):
        source = (live_dir / filename).read_text(encoding="utf-8")
        assert "_transient_id_expression" in source
        assert "GetInstanceID" not in source
        assert "GetEntityId" not in source
        assert "RUN_OWNED_ROOT" in source
        assert ".scene_paths" in source
        assert ".asset_paths" in source


async def test_wait_compile_idle_returns_true_immediately_when_idle():
    """Read-only poll: an already-idle compile_status returns True on the
    first check, with no sleep and no reconnect."""

    class _FakeBridge:
        async def send(self, cmd, args):  # noqa: ARG002
            assert cmd == "compile_status"
            return {"ok": True, "data": "idle"}

    result = await live_conftest._wait_compile_idle(
        _FakeBridge(), budget_s=5.0, interval_s=0.01
    )
    assert result is True


async def test_wait_compile_idle_polls_until_compiling_clears():
    """Poll compile_status until 'compiling' clears, bounded by budget_s —
    entirely in-place, never a bridge.close()/reconnect cycle."""
    responses = iter([
        {"ok": True, "data": "compiling"},
        {"ok": True, "data": "compiling"},
        {"ok": True, "data": "idle"},
    ])
    calls = []

    class _FakeBridge:
        async def send(self, cmd, args):  # noqa: ARG002
            calls.append(cmd)
            return next(responses)

    result = await live_conftest._wait_compile_idle(
        _FakeBridge(), budget_s=5.0, interval_s=0.01
    )
    assert result is True
    assert calls == ["compile_status"] * 3


async def test_wait_compile_idle_returns_false_when_budget_exhausted():
    """Still-compiling past the budget returns False instead of hanging
    forever — domain reload is documented up to 90s; the caller decides
    what to do next."""

    class _FakeBridge:
        async def send(self, cmd, args):  # noqa: ARG002
            return {"ok": True, "data": "compiling"}

    result = await live_conftest._wait_compile_idle(
        _FakeBridge(), budget_s=0.03, interval_s=0.02
    )
    assert result is False


async def test_capture_unity_state_reload_failure_waits_instead_of_reconnecting():
    """UncertainDeliveryError / 'Domain reload in progress' must retry via a
    bounded read-only re-probe (_wait_compile_idle + a fresh op_id from the
    next _execute_checked call), never bridge.close()/reconnect — repeated
    reconnect churn during an active Unity compile was measured to lengthen
    downstream test timing (commit d41bc6e0)."""
    responses = iter([
        ConnectionError("Domain reload in progress — retry after recompile"),
        {"ok": True, "data": "P\t0\nT\t1\n"},
    ])

    class _FakeBridge:
        async def send(self, cmd, args):  # noqa: ARG002
            item = next(responses)
            if isinstance(item, Exception):
                raise item
            return item

        async def close(self):
            raise AssertionError("must not close the bridge for a reload-related failure")

    async def _forbid_reconnect(bridge, retries=15, delay=1.0):  # noqa: ARG001
        raise AssertionError("must not reconnect for a reload-related failure")

    with patch.object(live_conftest, "_connect_with_retry", _forbid_reconnect), \
         patch.object(
             live_conftest, "_wait_compile_idle", AsyncMock(return_value=True)
         ) as wait_mock:
        state = await live_conftest._capture_unity_state(_FakeBridge())

    assert state.is_playing is False
    assert wait_mock.await_count == 1


async def test_capture_unity_state_non_reload_failure_still_reconnects():
    """A non-reload connection failure keeps the original reconnect
    fallback — only the reload-specific path skips bridge.close()/reconnect."""
    responses = iter([
        ConnectionError("connection reset by peer"),
        {"ok": True, "data": "P\t0\nT\t1\n"},
    ])
    reconnected = []

    class _FakeBridge:
        async def send(self, cmd, args):  # noqa: ARG002
            item = next(responses)
            if isinstance(item, Exception):
                raise item
            return item

        async def close(self):
            pass

    async def _fake_connect_with_retry(bridge, retries=15, delay=1.0):  # noqa: ARG001
        reconnected.append(retries)

    async def _forbid_wait(bridge, budget_s=90.0, interval_s=2.0):  # noqa: ARG001
        raise AssertionError("must not poll compile_status for a non-reload failure")

    with patch.object(live_conftest, "_connect_with_retry", _fake_connect_with_retry), \
         patch.object(live_conftest, "_wait_compile_idle", _forbid_wait):
        state = await live_conftest._capture_unity_state(_FakeBridge())

    assert state.is_playing is False
    assert reconnected == [10]
