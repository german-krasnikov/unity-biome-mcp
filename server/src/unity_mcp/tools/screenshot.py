"""Screenshot capture + optional Haiku description (B2: split from scene.py)."""
from ._annotations import RW as _RW
from ._common import bind

_send = None
_args = None


def _get_describer_safe():
    """Return ScreenshotDescriber or None on import/init error."""
    try:
        from ..screenshot_describe.describer import get_describer
        return get_describer()
    except Exception:
        return None


async def screenshot(width: int = 640, height: int = 480, camera: str | None = None,
                     path: str | None = None, output_path: str | None = None,
                     describe: str | None = None,
                     raw: bool = False, zoom: float | None = None,
                     angles: str | None = None, supersample: int | None = None,
                     offset: str | None = None, fixed_size: float | None = None,
                     highlight: str | None = None,
                     show_colliders: bool | None = None,
                     angle: str | None = None,
                     annotation_id: str | None = None) -> str:
    """Capture screenshot (file path); describe= -> Haiku text (15-100x fewer tokens), raw=True forces path.
    camera: scene_view|scene_view_frame|multi_view|single_view|overview|overview_game. angle (single_view): front|left|top|iso|ex,ey,ez.
    zoom: higher=closer. angles: per-view Euler "ex,ey,ez|..." (_=skip). supersample 1-4. offset/fixed_size: framing.
    highlight: paths[:#RRGGBB] for bbox. show_colliders: wireframes.
    annotation_id: frame + highlight annotation by id (auto sets camera=annotation_frame).
    warn:ScreenSpaceOverlay canvas not captured in Edit Mode (bypasses camera pipeline).
    Workarounds: switch to ScreenSpace-Camera, use Play Mode, or camera=scene_view."""
    if annotation_id is not None:
        camera = "annotation_frame"
    result = await _send("screenshot", _args(width=width, height=height, camera=camera,
                                             path=path, output_path=output_path,
                                             zoom=zoom, angles=angles,
                                             supersample=supersample,
                                             offset=offset, fixed_size=fixed_size,
                                             highlight=highlight,
                                             show_colliders="true" if show_colliders else None,
                                             angle=angle,
                                             annotation_id=annotation_id))
    if raw or describe is None or "Data saved to:" not in result:
        return result
    png_path = result.split("Data saved to: ")[-1].strip()
    key = "multi_view" if camera == "multi_view" else describe
    describer = _get_describer_safe()
    if describer is None:
        return result
    try:
        try:
            fp = await _send("fingerprint", _args(path=path, depth=2))
        except Exception:
            fp = None
        desc = await describer.describe(png_path, key, fp)
        if desc is None:
            return result
        return f"{desc}\n[img:{png_path}]"
    except Exception:
        return result


def register(mcp, send, args):
    bind(globals(), send, args)
    mcp.tool(annotations=_RW)(screenshot)
