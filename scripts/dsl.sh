#!/usr/bin/env bash
# What dsl/ generates (the tool catalog dsl/tools.biome, the decisions dsl/decisions, the chat turn dsl/chat, the playtest grammar dsl/constructs/playtest.construct): print it, or check that the generated files are current.
#
# Usage: scripts/dsl.sh generate | check
# Env:   BIOME_DSL_ROOT  a clean checkout of biome-dsl at the TOOLCHAIN commit of dsl/biome.project (Node >= 22).
#        PYTHON          the interpreter of the content lock (default python3; the script needs the standard library only).
#
# generate also refreshes dsl/content-lock.json (scripts/dsl_content_lock.py), the sha256 of every DSL source and generated file that
# server/tests/test_dsl_content_lock.py checks without Node.
# check = `generate --check` (a stale or hand-edited generated file), the examples of the constructs (tool, record, playtest), which `generate` does not run,
# the reader of the playtest SURFACE against its frozen oracle (dsl/playtest/compare.mjs), the tests of the tools of dsl/playtest (node --test), the playtest scripts
# of the repository read through that SURFACE (dsl/playtest/check.mjs), and that the content lock states the files as they are.
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
        pin_count="$(grep -c '^TOOLCHAIN ' dsl/biome.project || true)"
        if [ "$pin_count" -ne 1 ]; then
            echo "error: TOOLCHAIN_PIN_MISSING dsl/biome.project must have exactly one '^TOOLCHAIN ' line, found $pin_count" >&2
            exit 1
        fi
        pin="$(grep '^TOOLCHAIN ' dsl/biome.project)"
        pin="${pin#TOOLCHAIN }"
        if ! toolchain_head="$(git -C "$BIOME_DSL_ROOT" rev-parse HEAD 2>/dev/null)"; then
            echo "error: TOOLCHAIN_ROOT_NOT_GIT $BIOME_DSL_ROOT is not a git checkout (BIOME_DSL_ROOT must be a biome-dsl checkout at the TOOLCHAIN commit)" >&2
            exit 1
        fi
        if [ "$pin" != "$toolchain_head" ]; then
            echo "error: TOOLCHAIN_PIN_MISMATCH dsl/biome.project pins $pin, $BIOME_DSL_ROOT is at $toolchain_head (re-pin: biome generate --pin)" >&2
            exit 1
        fi
        node "$biome" generate --project dsl --check
        node "$biome" verify --construct-examples dsl/constructs/tool.examples
        node "$biome" verify --construct-examples dsl/constructs/record.examples
        node "$biome" verify --construct-examples dsl/constructs/event.examples
        for examples in dsl/constructs/playtest*.examples; do node "$biome" verify --construct-examples "$examples"; done
        node dsl/playtest/compare.mjs
        node --test dsl/playtest/*.test.mjs
        node dsl/playtest/check.mjs
        "${PYTHON:-python3}" scripts/dsl_content_lock.py check
        ;;
    *)
        echo "usage: scripts/dsl.sh generate | check" >&2
        exit 2
        ;;
esac
