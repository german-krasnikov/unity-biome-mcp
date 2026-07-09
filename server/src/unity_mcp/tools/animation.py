from ._annotations import RW as _RW
from ._common import bind

_send = None
_args = None


async def animation(action: str, path: str, clip: str | None = None, clip_name: str | None = None,
                    property: str | None = None, keys: str | None = None,
                    time: float | None = None,
                    component_type: str | None = None,
                    binding_path: str | None = None,
                    tangent: str | None = None,
                    function_name: str | None = None,
                    int_param: int | None = None,
                    float_param: float | None = None,
                    string_param: str | None = None) -> str:
    """Animate GameObject properties via AnimationClip. Use when you need to read or author keyframe animation on a specific object (not an Animator state machine — use `animator` for that, not this).
    action: get (list clips/keys) | create (new AnimationClip on object) | edit (add/replace keyframes) | preview (scrub to time) | add_event | remove_event | get_events | set_wrap (keys='loop'|'once'|'pingpong'|'clamp') | set_framerate (keys='30') | get_clip_path (returns asset path).
    clip=clip name, keys='t:0 v:(0,0,0); t:1 v:(0,2,0)', property=e.g. localPosition.x.
    component_type: Unity component to animate (default: Transform). Examples: Light, Camera, Rigidbody.
    binding_path: sub-object path for EditorCurveBinding (e.g. 'Head/Jaw'). Default '' = root.
    tangent: tangent mode for keyframes: auto (default) | smooth | linear | constant.
    function_name: method name for add_event. int_param/float_param/string_param: event parameters."""
    return await _send("animation", _args(action=action, path=path, clip=clip, clip_name=clip_name,
                                          property=property, keys=keys, time=time,
                                          component_type=component_type,
                                          binding_path=binding_path, tangent=tangent,
                                          function_name=function_name, int_param=int_param,
                                          float_param=float_param, string_param=string_param))


async def timeline(path: str, action: str, track: str | None = None, track_type: str | None = None,
                   clip: str | None = None, binding: str | None = None,
                   start: float | None = None, duration: float | None = None,
                   blend_in: float | None = None, blend_out: float | None = None,
                   asset_path: str | None = None, director_path: str | None = None,
                   tracks: str | None = None, time: float | None = None,
                   name: str | None = None,
                   clip_in: float | None = None,
                   index: int | None = None,
                   offset: float | None = None,
                   value: str | None = None) -> str:
    """Unity Timeline (PlayableDirector / TimelineAsset). Use for multi-track cinematic sequences mixing animation, audio, activation, and custom tracks — not for per-object keyframes (use `animation` for that).
    action: get | create | add_track (Animation|Audio|Activation|Signal|Control|Group) | remove_track | add_clip | remove_clip | set_binding | set_timing | mute | unmute | lock | unlock | rename_track | reorder_track | duplicate_clip | add_marker | remove_marker | set_track_offset | set_duration | add_sub_track | set_clip_in | get_bindings | preview.
    track=track name. index=target position for reorder_track. offset=time shift for duplicate_clip. value=offset mode (auto|transform|scene) for set_track_offset."""
    return await _send("timeline", _args(path=path, action=action, track=track, track_type=track_type,
                                         clip=clip, binding=binding, start=start, duration=duration,
                                         blend_in=blend_in, blend_out=blend_out, asset_path=asset_path,
                                         director_path=director_path, tracks=tracks, time=time,
                                         name=name, clip_in=clip_in,
                                         index=index, offset=offset, value=value))


async def animator(action: str, path: str,
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
    """Animator Controller — state machine (use `animation` for keyframe clips, `timeline` for cinematics). action: get|add_param|add_state|add_transition|set_default|remove|add_blend_tree|edit_blend_tree|get_blend_tree|add_layer|remove_layer|rename_layer|set_layer_weight|set_layer_blending|set_state_speed|update_transition|set_avatar|rename_state|rename_param.
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
        duration=duration, exit_time=exit_time, has_exit_time=has_exit_time,
        type=type, name=name, blend_type=blend_type, param=param,
        param_y=param_y, children=children, edit_action=edit_action,
        layer=layer, weight=weight, blending=blending,
        value=value, avatar_path=avatar_path))


async def particle(action: str, path: str,
                   name: str | None = None,
                   module: str | None = None,
                   prop: str | None = None,
                   value: str | None = None,
                   preset: str | None = None) -> str:
    """Particle System. action: get|create|set|apply|play|stop|pause. module=main|emission|shape|colorOverLifetime|sizeOverLifetime|velocityOverLifetime|noise|renderer|trails|collision|rotationOverLifetime. preset: fire|smoke|sparks|rain|snow|explosion|magic|dust|blood|trail."""
    return await _send("particle", _args(
        action=action, path=path, name=name, module=module,
        prop=prop, value=value, preset=preset))


def register(mcp, send, args):
    bind(globals(), send, args)
    mcp.tool(annotations=_RW)(animation)
    mcp.tool(annotations=_RW)(timeline)
    mcp.tool(annotations=_RW)(animator)
    mcp.tool(annotations=_RW)(particle)
