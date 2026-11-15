"""Tests for Inferrer + SessionContext."""
from unity_mcp.inference import SessionContext, Inferrer, infer_primitive, infer_parent


# ── infer_primitive() ────────────────────────────────────────────────────────

def test_infer_primitive_cube():
    assert infer_primitive("Cube") == "Cube"


def test_infer_primitive_capsule_suffix():
    assert infer_primitive("PlayerCapsule") == "Capsule"


def test_infer_primitive_sphere_suffix():
    assert infer_primitive("EnemySphere") == "Sphere"


def test_infer_primitive_no_match():
    assert infer_primitive("Goblin") is None


def test_infer_primitive_case_insensitive():
    assert infer_primitive("floorcube") == "Cube"


# ── infer_parent() ───────────────────────────────────────────────────────────

def test_infer_parent_3_creates_same_parent():
    ctx = SessionContext()
    ctx.recent_creates.append(("A", "/Root/A", "Root"))
    ctx.recent_creates.append(("B", "/Root/B", "Root"))
    ctx.recent_creates.append(("C", "/Root/C", "Root"))
    assert infer_parent(ctx) == "Root"


def test_infer_parent_less_than_3_returns_none():
    ctx = SessionContext()
    ctx.recent_creates.append(("A", "/Root/A", "Root"))
    ctx.recent_creates.append(("B", "/Root/B", "Root"))
    assert infer_parent(ctx) is None


def test_infer_parent_mixed_parents_returns_none():
    ctx = SessionContext()
    ctx.recent_creates.append(("A", "/Root/A", "Root"))
    ctx.recent_creates.append(("B", "/Other/B", "Other"))
    ctx.recent_creates.append(("C", "/Third/C", "Third"))
    assert infer_parent(ctx) is None


# ── Inferrer ─────────────────────────────────────────────────────────────────

def test_inferrer_create_object_adds_primitive():
    inf = Inferrer()
    ctx = SessionContext()
    new_args, tags = inf.infer("create_object", {"name": "WallCube"}, ctx)
    assert new_args["primitive"] == "Cube"
    assert any("primitive" in t for t in tags)


def test_inferrer_explicit_primitive_not_overridden():
    inf = Inferrer()
    ctx = SessionContext()
    new_args, tags = inf.infer("create_object", {"name": "WallCube", "primitive": "Sphere"}, ctx)
    assert new_args["primitive"] == "Sphere"
    assert not any("primitive" in t for t in tags)


def test_inferrer_infers_parent_from_context():
    inf = Inferrer()
    ctx = SessionContext()
    ctx.recent_creates.append(("A", "/Env/A", "Env"))
    ctx.recent_creates.append(("B", "/Env/B", "Env"))
    ctx.recent_creates.append(("C", "/Env/C", "Env"))
    new_args, tags = inf.infer("create_object", {"name": "Wall"}, ctx)
    assert new_args["parent"] == "Env"
    assert any("parent" in t for t in tags)


def test_inferrer_explicit_parent_not_overridden():
    inf = Inferrer()
    ctx = SessionContext()
    ctx.recent_creates.append(("A", "/Env/A", "Env"))
    ctx.recent_creates.append(("B", "/Env/B", "Env"))
    ctx.recent_creates.append(("C", "/Env/C", "Env"))
    new_args, tags = inf.infer("create_object", {"name": "X", "parent": "Custom"}, ctx)
    assert new_args["parent"] == "Custom"


def test_inferrer_non_create_unchanged():
    inf = Inferrer()
    ctx = SessionContext()
    args = {"path": "/X", "component": "Health", "prop": "hp", "value": "50"}
    new_args, tags = inf.infer("set_property", args, ctx)
    assert new_args == args
    assert tags == []


# ── SessionContext.record() ──────────────────────────────────────────────────

def test_session_context_records_path():
    ctx = SessionContext()
    ctx.record("get_component", {"path": "/Player", "type": "Health"}, "ok")
    assert ctx.last_path == "/Player"


def test_session_context_records_last_component():
    ctx = SessionContext()
    ctx.record("get_component", {"path": "/X", "type": "Rigidbody"}, "ok")
    assert ctx.last_component == "Rigidbody"


def test_session_context_records_create():
    ctx = SessionContext()
    ctx.record("create_object", {"name": "Wall", "parent": "Env"}, "ok")
    assert len(ctx.recent_creates) == 1
    assert ctx.recent_creates[0][0] == "Wall"
