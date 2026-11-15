from unity_mcp.som.extract import build_path_pool


def test_build_path_pool_deterministic():
    rects = [{"path": f"/obj{i}"} for i in range(10)]
    results = [build_path_pool(rects) for _ in range(20)]
    assert len(set(tuple(r) for r in results)) == 1


def test_build_path_pool_capped():
    rects = [{"path": f"/obj{i}"} for i in range(40)]
    assert len(build_path_pool(rects)) == 30


def test_build_path_pool_union():
    before = [{"path": "/A"}]
    after = [{"path": "/B"}]
    pool = build_path_pool(before, after)
    assert "/A" in pool and "/B" in pool


def test_build_path_pool_dedup():
    rects = [{"path": "/A"}, {"path": "/A"}, {"path": "/B"}]
    pool = build_path_pool(rects)
    assert len(pool) == 2


def test_build_path_pool_none_after():
    rects = [{"path": "/A"}, {"path": "/B"}]
    pool = build_path_pool(rects, None)
    assert pool == build_path_pool(rects, rects)


def test_build_path_pool_empty_after():
    """Empty list ≠ None: empty after contributes no paths."""
    rects = [{"path": "/A"}, {"path": "/B"}]
    pool = build_path_pool(rects, [])
    # Only rects contribute paths (after=[] has none)
    assert set(pool) == {"/A", "/B"}
