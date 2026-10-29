#!/usr/bin/env bash
# The tool catalog (dsl/tools.biome): print what is generated from it, or check that the generated files are current.
#
# Usage: scripts/dsl.sh generate | check
# Env:   BIOME_DSL_ROOT  a clean checkout of biome-dsl at the TOOLCHAIN commit of dsl/biome.project (Node >= 22).
#
# check = `generate --check` (a stale or hand-edited generated file) and the examples of the construct, which `generate` does not run.
set -eu

cd "$(dirname "$0")/.."
: "${BIOME_DSL_ROOT:?set BIOME_DSL_ROOT to the biome-dsl checkout named by TOOLCHAIN in dsl/biome.project}"
biome="$BIOME_DSL_ROOT/implementations/typescript/packages/biome/src/bin.mjs"

case "${1:-}" in
    generate)
        node "$biome" generate --project dsl
        ;;
    check)
        node "$biome" generate --project dsl --check
        node "$biome" verify --construct-examples dsl/constructs/tool.examples
        ;;
    *)
        echo "usage: scripts/dsl.sh generate | check" >&2
        exit 2
        ;;
esac
