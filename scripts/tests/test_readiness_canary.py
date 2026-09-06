import os

import pytest
from gauntlet.readiness_canary import OwnedFileEdit, OwnershipConflict, render_dsl


def test_owned_file_round_trip_restores_exact_bytes_and_mtime(tmp_path):
    path = tmp_path / "manifest.json"
    baseline = b'\xef\xbb\xbf{ "unrelated": "keep" }\r\n'
    path.write_bytes(baseline)
    os.utime(path, ns=(100_000_000, 200_000_000))
    edit = OwnedFileEdit(path, tmp_path / "evidence")
    edit.replace(b'{"provider":"pinned"}\n')
    edit.restore()
    assert path.read_bytes() == baseline
    assert path.stat().st_mtime_ns == 200_000_000
    assert (edit.evidence / "before.bin").read_bytes() == baseline


def test_external_same_size_same_mtime_edit_is_never_overwritten(tmp_path):
    path = tmp_path / "Target.cs"
    path.write_bytes(b"101")
    edit = OwnedFileEdit(path, tmp_path / "evidence")
    edit.replace(b"202")
    after = path.stat()
    path.write_bytes(b"303")
    os.utime(path, ns=(after.st_atime_ns, after.st_mtime_ns))
    with pytest.raises(OwnershipConflict):
        edit.restore()
    assert path.read_bytes() == b"303"


def test_conflict_before_first_write_has_zero_effect(tmp_path):
    path = tmp_path / "manifest.json"
    path.write_bytes(b"old")
    edit = OwnedFileEdit(path, tmp_path / "evidence")
    path.write_bytes(b"external")
    with pytest.raises(OwnershipConflict):
        edit.replace(b"ours")
    assert path.read_bytes() == b"external"


def test_created_file_is_removed_but_ambient_sibling_survives(tmp_path):
    sibling = tmp_path / "scene.unity"
    sibling.write_bytes(b"user scene")
    path = tmp_path / "Owned.cs"
    edit = OwnedFileEdit(path, tmp_path / "evidence")
    edit.replace(b"owned")
    edit.restore()
    assert not path.exists()
    assert sibling.read_bytes() == b"user scene"


def test_symlink_target_is_rejected_without_following_it(tmp_path):
    external = tmp_path / "external"
    external.write_bytes(b"user")
    link = tmp_path / "owned"
    link.symlink_to(external)
    with pytest.raises(OwnershipConflict):
        OwnedFileEdit(link, tmp_path / "evidence")
    assert external.read_bytes() == b"user"


def test_retargeted_parent_symlink_is_rejected_during_restore(tmp_path):
    owned = tmp_path / "owned"
    owned.mkdir()
    path = owned / "Target.cs"
    edit = OwnedFileEdit(path, tmp_path / "evidence")
    edit.replace(b"ours")
    owned.rename(tmp_path / "moved")
    external = tmp_path / "external"
    external.mkdir()
    (external / "Target.cs").write_bytes(b"ours")
    owned.symlink_to(external, target_is_directory=True)
    with pytest.raises(OwnershipConflict):
        edit.restore()
    assert (external / "Target.cs").read_bytes() == b"ours"


def test_dsl_samples_before_asserting_actual_value():
    path = "/__BiomeReadiness_" + "a" * 32
    lines = render_dsl(path, 202).splitlines()
    assert lines == [
        "# @needs editmode",
        f"INVOKE {path} BuildReadinessCanaryProbe Sample",
        f"ASSERT {path}|BuildReadinessCanaryProbe|Observed == 202",
    ]


def test_preview_dsl_uses_exact_instance_id_and_retains_name_ownership():
    script = render_dsl("/__BiomeReadiness_" + "a" * 32, 101, instance_id=-712)
    assert "INVOKE #-712 BuildReadinessCanaryProbe Sample" in script
    assert "ASSERT #-712|BuildReadinessCanaryProbe|Observed == 101" in script


@pytest.mark.parametrize("instance_id", [0, True, "-712\nDELETE /Camera"])
def test_preview_id_rejects_zero_bool_and_injection(instance_id):
    with pytest.raises(OwnershipConflict):
        render_dsl("/__BiomeReadiness_" + "a" * 32, 101, instance_id=instance_id)


@pytest.mark.parametrize("path", ["/Camera", "/__BiomeReadiness_abc\nDELETE /Camera", "../scene"])
def test_dsl_cannot_target_unowned_or_injected_path(path):
    with pytest.raises(OwnershipConflict):
        render_dsl(path, 101)
