#!/usr/bin/env bash
# What dsl/ generates (the tool catalog dsl/tools.biome, the decisions dsl/decisions): print it, or check that the generated files are current.
#
# Usage: scripts/dsl.sh generate | check
# Env:   BIOME_DSL_ROOT  a clean checkout of biome-dsl at the TOOLCHAIN commit of dsl/biome.project (Node >= 22).
#        PYTHON          the interpreter of the content lock (default python3; the script needs the standard library only).
#
# generate also refreshes dsl/content-lock.json (scripts/dsl_content_lock.py), the sha256 of every DSL source and generated file that
# server/tests/test_dsl_content_lock.py checks without Node.
# check = `generate --check` (a stale or hand-edited generated file), the examples of the constructs (tool, playtest), which `generate` does not run,
# the reader of the playtest SURFACE against its frozen oracle (dsl/playtest/compare.mjs), and that the content lock states the files as they are.
set -eu

cd "$(dirname "$0")/.."
: "${BIOME_DSL_ROOT:?set BIOME_DSL_ROOT to the biome-dsl checkout named by TOOLCHAIN in dsl/biome.project}"
biome="$BIOME_DSL_ROOT/implementations/typescript/packages/biome/src/bin.mjs"

case "${1:-}" in
    generate)
        node "$biome" generate --project dsl
        "${PYTHON:-python3}" scripts/dsl_content_lock.py generate
        ;;
    check)
        node "$biome" generate --project dsl --check
        node "$biome" verify --construct-examples dsl/constructs/tool.examples
        for examples in dsl/constructs/playtest*.examples; do node "$biome" verify --construct-examples "$examples"; done
        node dsl/playtest/compare.mjs
        "${PYTHON:-python3}" scripts/dsl_content_lock.py check
        ;;
    *)
        echo "usage: scripts/dsl.sh generate | check" >&2
        exit 2
        ;;
esac
