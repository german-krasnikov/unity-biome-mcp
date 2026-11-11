from ..debug import snapshots as snapshot_tool
from . import (
    animation,
    animator_intent_tool,
    ask_tool,
    asset,
    autobatch,
    batch,
    brief_tool,
    budget_tool,
    changeset_tool,
    checkpoint_tool,
    code_intel,
    codegen,
    connection,
    console,
    debug_tool,
    diagnose,
    do_tool,
    editor_control,
    meta,
    objects,
    permission_prompt_tool,
    profiling,
    runtime,
    scene,
    screenshot,
    skills,
    spatial,
    sync_module,
    testing,
    transaction,
    ui_intent_tool,
    uitk,
    uitk_intent_tool,
    verify,
    vfx_intent_tool,
    watch,
)
from .generated import catalog_tools
from .metrics_tool import register as register_metrics


def register_all(mcp, send, args, *, get_slot, get_middleware=None,
                  stdio_alive=None, refresh_tools_cache=None, push_catalog=None):
    for mod in [scene, objects, asset, animation, runtime, watch, code_intel,
                batch, codegen, skills, spatial, uitk, sync_module, diagnose,
                debug_tool, snapshot_tool, profiling,
                console, screenshot, editor_control, verify, transaction]:
        mod.register(mcp, send, args)
    testing.register(mcp, send, args, get_slot=get_slot)
    connection.register(mcp, send, args, get_slot=get_slot,
                        stdio_alive=stdio_alive,
                        get_middleware=get_middleware,
                        refresh_tools_cache=refresh_tools_cache,
                        push_catalog=push_catalog)
    meta.register(mcp, send, args, get_slot=get_slot)
    autobatch.register(mcp, send, args)
    do_tool.register(mcp, send, args)
    ask_tool.register(mcp, send, args)
    permission_prompt_tool.register(mcp, send, args)
    for mod in [animator_intent_tool, vfx_intent_tool, ui_intent_tool, uitk_intent_tool]:
        mod.register(mcp, send, args)
    register_metrics(mcp, send, args)
    budget_tool.register(mcp, send, args)
    changeset_tool.register(mcp, send, args)
    checkpoint_tool.register(mcp, send, args)
    brief_tool.register(mcp, send, args)
    catalog_tools.register(mcp, send, args)  # last: it checks the functions that the modules above provided
