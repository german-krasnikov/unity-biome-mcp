"""Helper: annotate before/after images with SoM circles, call sampling."""
import os
import tempfile

from ..sampling import SamplingService  # noqa: TC001


async def diff_with_annotation(
    before: str,
    after: str,
    rects: list,
    rects_after: list | None,
    prompt: str,
    sampling: SamplingService,
    feature: str,
) -> str | None:
    """Annotate both frames with SoM circles, call sampling, cleanup."""
    from PIL import Image

    from .extract import build_path_pool
    from .overlay import annotate

    # Build canonical pool once from union of both frames — ensures circles and
    # legend share the same index space (fixes index mismatch on subset frames).
    effective_after = rects_after or rects
    pool = build_path_pool(rects, effective_after)

    with tempfile.TemporaryDirectory() as tmp:
        ann_before = os.path.join(tmp, "before.png")
        ann_after = os.path.join(tmp, "after.png")

        img_b = Image.open(before)
        annotated_b, _ = annotate(img_b, rects, path_pool=pool)
        annotated_b.save(ann_before)

        img_a = Image.open(after)
        annotated_a, _ = annotate(img_a, effective_after, path_pool=pool)
        annotated_a.save(ann_after)

        return await sampling.verify_visual_diff(ann_before, ann_after, prompt, feature=feature)
