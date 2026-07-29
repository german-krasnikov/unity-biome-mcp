"""Collect and render README facts.

Modes:
  --collect      collect facts and write docs/assets/_meta.json
  --render       render committed outputs from _meta.json
  --all          collect and render (default)
  --check        fail when rendered outputs are stale
  --check-facts  fail when _meta.json differs from a fresh collection
"""

import argparse
import pathlib
import sys


SCRIPTS_DIR = pathlib.Path(__file__).parent
REPO_ROOT = SCRIPTS_DIR.parent
sys.path.insert(0, str(SCRIPTS_DIR))

from readme_render import (  # noqa: E402
    generate_changelog_summary,
    inject_changelog_into_readme,
    make_badge_json,
    parse_latest_changelog,
    read_meta_json,
    render,
    stats_summary,
    substitute_svg_markers,
    update_readme_stats,
)


_FACTS_ATTRS = {
    "count_mcp_tools",
    "count_pytest_python",
    "count_pytest_stress",
    "count_pytest_live",
    "count_unity_tests",
    "read_plugin_version",
    "read_server_version",
}


def __getattr__(name: str):
    """Lazily expose fact collectors without loading them for render-only CI."""
    if name in _FACTS_ATTRS:
        import readme_facts

        return getattr(readme_facts, name)
    raise AttributeError(f"module 'update_readme' has no attribute {name!r}")


def _facts_drift(stored: dict, fresh: dict) -> dict:
    return {
        key: (stored.get(key), value)
        for key, value in fresh.items()
        if stored.get(key) != value
    }


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Collect and render README metadata from source registrations and test discovery."
    )
    modes = parser.add_mutually_exclusive_group()
    modes.add_argument("--collect", action="store_true")
    modes.add_argument("--render", action="store_true")
    modes.add_argument("--all", dest="all_", action="store_true")
    modes.add_argument("--check", action="store_true")
    modes.add_argument("--check-facts", dest="check_facts", action="store_true")
    args = parser.parse_args()

    if args.check_facts:
        from readme_facts import collect_facts, load_meta

        fresh = collect_facts(REPO_ROOT)
        drift = _facts_drift(load_meta(REPO_ROOT), fresh)
        if drift:
            for key, (stored, actual) in drift.items():
                print(f"DRIFT {key}: stored={stored} actual={actual}")
            raise SystemExit(1)
        print("facts OK")
        return

    default_mode = not any((args.collect, args.render, args.all_, args.check))
    collect = args.collect or args.all_ or default_mode
    render_outputs = args.render or args.all_ or default_mode

    if collect:
        from readme_facts import collect_facts, write_meta_json

        print("Collecting facts...")
        facts = collect_facts(REPO_ROOT)
        meta_path = write_meta_json(REPO_ROOT, facts)
        print(
            f"  tools={facts['tools']}  test_inventory={facts['tests_total']} "
            f"(regular={facts['tests_python']} stress={facts['tests_stress']} "
            f"live={facts['tests_live']} unity_source={facts['tests_unity']}) "
            f"server={facts['server_version']} plugin={facts['plugin_version']}"
        )
        print(f"  wrote {meta_path}")
    else:
        facts = read_meta_json(REPO_ROOT)

    if args.check:
        render(REPO_ROOT, facts, check=True)
    elif render_outputs:
        print("Rendering...")
        render(REPO_ROOT, facts)
        print("Done.")


if __name__ == "__main__":
    main()
