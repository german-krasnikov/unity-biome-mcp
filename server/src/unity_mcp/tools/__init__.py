from ..debug import snapshots as snapshot_tool
from . import (
    animation,
    animator_intent_tool,
    ask_tool,
    ask_user_tool,
    asset,
    auto_wire,
    autobatch,
    batch,
    brief_tool,
    budget_tool,
    build,
    changeset_tool,
    checkpoint_tool,
    code_intel,
    codegen,
    connection,
    console,
    debug_tool,
    diagnose,
    diagnostics,
    do_tool,
    editor_control,
    meta,
    objects,
    packages,
    permission_prompt_tool,
    profiling,
    rendering,
    runtime,
    scene,
    scene_health,
    screenshot,
    skills,
    spatial,
    sync,
    testing,
    transaction,
    ui,
    ui_intent_tool,
    uitk,
    verify,
    vfx_intent_tool,
    watch,
)
from .metrics_tool import register as register_metrics


def register_all(mcp, send, args, *, get_slot, get_middleware=None,
                  refresh_tools_cache=None, push_catalog=None):
    for mod in [scene, objects, asset, animation, runtime, watch, code_intel,
                batch, codegen, skills, spatial, ui, uitk, sync, diagnose,
                debug_tool, diagnostics, snapshot_tool, profiling, rendering, scene_health, auto_wire, meta, build, packages,
                console, screenshot, testing, editor_control, verify, transaction]:
        mod.register(mcp, send, args)
    connection.register(mcp, send, args, get_slot=get_slot,
                        get_middleware=get_middleware,
                        refresh_tools_cache=refresh_tools_cache,
                        push_catalog=push_catalog)
    autobatch.register(mcp, send, args)
    do_tool.register(mcp, send, args)
    ask_tool.register(mcp, send, args)
    ask_user_tool.register(mcp, send, args)
    permission_prompt_tool.register(mcp, send, args)
    for mod in [animator_intent_tool, vfx_intent_tool, ui_intent_tool]:
        mod.register(mcp, send, args)
    register_metrics(mcp, send, args)
    budget_tool.register(mcp, send, args)
    changeset_tool.register(mcp, send, args)
    checkpoint_tool.register(mcp, send, args)
    brief_tool.register(mcp, send, args)
    uitk.register(mcp, send, args)
