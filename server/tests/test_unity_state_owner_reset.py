"""Unit tests for the conditional owned-scene reset (no Editor required).

`_restore_owned_state` used to fire `_reset_owned_scene` unconditionally
whenever `policy.reset_scene_path` was set, even when the ownership plan
shows the scene never mutated. These tests pin the new behavior: skip the
reset when nothing changed, still reset when the plan-derived
`OwnershipPlan.reset_owned_scene` signal says it did.
"""

import pytest

from tests.live import conftest as live_conftest
from tests.live import unity_state_owner as state_owner_module
from tests.live.unity_state_owner import ObjectState, OwnershipPolicy, SceneState, UnityStateSnapshot


def _scene(path, *, dirty=False):
    return SceneState(path=path, name="Scene", handle=1, is_dirty=dirty, is_active=True)


def _obj(global_id, transient_id, scene, hierarchy, name):
    return ObjectState(global_id, str(transient_id), scene, hierarchy, name)


def _snapshot(*, scenes, objects=(), assets=()):
    return UnityStateSnapshot(
        is_playing=False,
        scenes=tuple(scenes),
        objects=tuple(objects),
        assets=tuple(assets),
    )


def _spy_on_build_ownership_plan(monkeypatch):
    """Count every `build_ownership_plan` call, from either module's own
    reference to it, and still delegate to the real implementation.

    Proves M1's fix: the reset decision reuses the plan already built at
    the call site instead of asking `_needs_owned_scene_reset` to rebuild
    an identical one internally.
    """
    calls = []
    real_build = live_conftest.build_ownership_plan

    def counting_build(before_, after_, policy_):
        calls.append(1)
        return real_build(before_, after_, policy_)

    monkeypatch.setattr(live_conftest, "build_ownership_plan", counting_build)
    monkeypatch.setattr(state_owner_module, "build_ownership_plan", counting_build)
    return calls


@pytest.mark.asyncio
async def test_restore_owned_state_skips_reset_when_plan_shows_no_scene_mutation(monkeypatch):
    path = "Assets/TestsTemp/Owned.unity"
    snapshot = _snapshot(
        scenes=[_scene(path)],
        objects=[_obj("stable-player", 10, path, "0", "GridPlayer")],
        assets=[path],
    )
    captures = iter((snapshot, snapshot))
    resets = []
    plan_calls = _spy_on_build_ownership_plan(monkeypatch)

    async def fake_capture(_bridge):
        return next(captures)

    async def fake_reset(_bridge, reset_path, playing):
        resets.append((reset_path, playing))

    monkeypatch.setattr(live_conftest, "_capture_unity_state", fake_capture)
    monkeypatch.setattr(live_conftest, "_reset_owned_scene", fake_reset)

    await live_conftest._restore_owned_state(
        object(),
        snapshot,
        OwnershipPolicy(scene_paths={path}, asset_paths={path}, reset_scene_path=path),
    )

    assert resets == []
    assert len(plan_calls) == 2, "reset decision must reuse the already-built plan, not rebuild it"


@pytest.mark.asyncio
async def test_restore_owned_state_resets_when_plan_shows_scene_mutation(monkeypatch):
    path = "Assets/TestsTemp/Owned.unity"
    stable = _obj("stable-player", 10, path, "0", "GridPlayer")
    before = _snapshot(scenes=[_scene(path)], objects=[stable], assets=[path])
    mutated = _snapshot(scenes=[_scene(path, dirty=True)], objects=[stable], assets=[path])
    captures = iter((mutated, before))
    resets = []
    plan_calls = _spy_on_build_ownership_plan(monkeypatch)

    async def fake_capture(_bridge):
        return next(captures)

    async def fake_reset(_bridge, reset_path, playing):
        resets.append((reset_path, playing))

    monkeypatch.setattr(live_conftest, "_capture_unity_state", fake_capture)
    monkeypatch.setattr(live_conftest, "_reset_owned_scene", fake_reset)

    await live_conftest._restore_owned_state(
        object(),
        before,
        OwnershipPolicy(scene_paths={path}, asset_paths={path}, reset_scene_path=path),
    )

    assert resets == [(path, False)]
    assert len(plan_calls) == 2, "reset decision must reuse the already-built plan, not rebuild it"
