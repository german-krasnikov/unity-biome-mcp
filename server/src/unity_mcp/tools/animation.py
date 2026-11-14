from ._common import bind

# Wrappers the tool catalog (dsl/tools.biome) generates; server.py and the tests import them from here.
from .generated.catalog_tools import animation, animator, particle, timeline, tools  # noqa: F401

_send = None
_args = None


async def _animator_body(action: str, path: str,
                   state: str | None = None, states: str | None = None,
                   params: str | None = None,
                   source: str | None = None, target: str | None = None,
                   conditions: str | None = None,
                   duration: float | None = None,
                   exit_time: float | None = None,
                   has_exit_time: bool | None = None,
                   type: str | None = None, name: str | None = None,
                   blend_type: str | None = None,
                   param: str | None = None,
                   param_y: str | None = None,
                   children: str | None = None,
                   edit_action: str | None = None,
                   layer: int | str | None = None,
                   weight: float | None = None,
                   blending: str | None = None,
                   value: str | None = None,
                   avatar_path: str | None = None) -> str:
    """Animator Controller — state machine. Modifies animator assets. No confirmation required. (use `animation` for keyframe clips, `timeline` for cinematics). action: get|add_param|add_state|add_transition|set_default|remove|add_blend_tree|edit_blend_tree|get_blend_tree|add_layer|remove_layer|rename_layer|set_layer_weight|set_layer_blending|set_state_speed|update_transition|set_avatar|rename_state|rename_param.
    params='Speed:float:0; Jump:trigger'. states='Idle:Idle.anim; Walk'.
    conditions='Speed>0.1; IsGrounded'. source/target=state names (*=AnyState).
    blend_type: 1d|2d_simple|2d_freeform|2d_cartesian|direct.
    param/param_y: blend parameters (auto-created as float if missing).
    children: '(1D) Idle:0; Walk:0.5; Run:1' or '(2D) Idle:0,0; Walk:0,1'.
    edit_action: add_child|remove_child|set_thresholds|set_param|set_type.
    layer: layer index (int) for add_state/add_transition/set_default, or name/index string for CRUD ops.
    weight: defaultWeight for add_layer/set_layer_weight (0.0–1.0).
    blending: Override|Additive for add_layer/set_layer_blending.
    value: speed multiplier for set_state_speed. avatar_path: asset path for set_avatar."""
    return await _send("animator", _args(
        action=action, path=path, state=state, states=states, params=params,
        source=source, target=target, conditions=conditions,
        duration=duration, exit_time=exit_time,
        has_exit_time=("true" if has_exit_time is True else "false" if has_exit_time is False else None),
        type=type, name=name, blend_type=blend_type, param=param,
        param_y=param_y, children=children, edit_action=edit_action,
        layer=layer, weight=weight, blending=blending,
        value=value, avatar_path=avatar_path))


def register(mcp, send, args):
    bind(globals(), send, args)
