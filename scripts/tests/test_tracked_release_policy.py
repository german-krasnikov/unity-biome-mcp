
import re
import sys
from pathlib import Path

SCRIPTS = Path(__file__).resolve().parents[1]
REPO_ROOT = SCRIPTS.parent
sys.path.insert(0, str(SCRIPTS))

from gauntlet.contract_catalog import load_contract_catalog  # noqa: E402
from gauntlet.release_policy import load_release_policy  # noqa: E402

POLICY = SCRIPTS / "gauntlet" / "release-policy.json"
CATALOG = SCRIPTS / "gauntlet" / "contracts.json"
PYPROJECT = REPO_ROOT / "server" / "pyproject.toml"


def _product_version() -> str:
    match = re.search(
        r'^version = "([^"]+)"$',
        PYPROJECT.read_text(encoding="utf-8"),
        re.MULTILINE,
    )
    if match is None:
        raise AssertionError("server/pyproject.toml does not define a package version")
    return match.group(1)


def test_tracked_release_policy_binds_tracked_contract_catalog() -> None:
    policy = load_release_policy(POLICY)
    catalog = load_contract_catalog(CATALOG)

    assert policy.contract_catalog_path == "scripts/gauntlet/contracts.json"
    assert policy.contract_catalog_sha == catalog.catalog_sha
    assert policy.activation_product_version == _product_version()


def test_tracked_release_policy_defines_public_and_unity_profiles() -> None:
    policy = load_release_policy(POLICY)

    assert tuple(profile.profile_id for profile in policy.active_profiles) == (
        "public-stdio-linux-py314",
        "unity-editor-macos-py314",
    )
    assert policy.active_profiles[0].required_workers == 0
    assert policy.active_profiles[1].required_workers == 1


def test_tracked_release_policy_pytest_nodes_exist() -> None:
    policy = load_release_policy(POLICY)
    nodes = {
        node
        for profile in policy.active_profiles
        for node in profile.pytest_node_ids
    }

    for node in nodes:
        path_text, _, _selector = node.partition("::")
        assert (REPO_ROOT / path_text).is_file()
