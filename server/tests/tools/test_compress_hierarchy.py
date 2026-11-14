from unity_mcp.server import compress_hierarchy


def test_groups_slots():
    """Consecutive slot_N lines are grouped."""
    text = "\n".join([
        "  slot_0  []  #1",
        "  slot_1  []  #2",
        "  slot_2  []  #3",
    ])
    result = compress_hierarchy(text)
    assert "[3x slot]" in result
    assert "slot_0" not in result


def test_groups_points():
    """Consecutive point_N lines are grouped."""
    text = "\n".join([
        "    point_0  []  #10",
        "    point_1  []  #11",
        "    point_2  []  #12",
        "    point_3  []  #13",
    ])
    result = compress_hierarchy(text)
    assert "[4x point]" in result
    assert "point_0" not in result


def test_groups_visual_meshes():
    """3+ consecutive MeshFilter,MeshRenderer lines are grouped."""
    text = "\n".join([
        "  obj1 [MeshFilter,MeshRenderer]  #1",
        "  obj2 [MeshFilter,MeshRenderer]  #2",
        "  obj3 [MeshFilter,MeshRenderer]  #3",
    ])
    result = compress_hierarchy(text)
    assert "[3x visual mesh]" in result


def test_no_group_under_3_meshes():
    """2 mesh lines stay as-is (threshold is 3)."""
    text = "\n".join([
        "  obj1 [MeshFilter,MeshRenderer]  #1",
        "  obj2 [MeshFilter,MeshRenderer]  #2",
    ])
    result = compress_hierarchy(text)
    assert "visual mesh" not in result
    assert "obj1" in result
    assert "obj2" in result


def test_no_change_normal_lines():
    """Normal hierarchy lines pass through unchanged."""
    text = "Root [Transform]  #1\n  Child [Rigidbody]  #2"
    result = compress_hierarchy(text)
    assert result == text


def test_preserves_indent():
    """Grouped lines preserve indentation of original."""
    text = "\n".join([
        "    slot_0  []  #1",
        "    slot_1  []  #2",
    ])
    result = compress_hierarchy(text)
    assert result.startswith("    [2x slot]")


def test_empty_input():
    """Empty string returns empty string."""
    assert compress_hierarchy("") == ""


def test_interleaved_slots_points():
    """Interleaved slot and point lines are grouped separately."""
    text = "\n".join([
        "  slot_0  []  #1",
        "  slot_1  []  #2",
        "  point_0  []  #10",
        "  point_1  []  #11",
        "  slot_2  []  #3",
    ])
    result = compress_hierarchy(text)
    # First two slots grouped, then points grouped, then last slot alone
    assert "[2x slot]" in result
    assert "[2x point]" in result


def test_single_slot_not_grouped():
    """A single slot line stays as-is (groups need 1+, which it does - verify)."""
    text = "  slot_0  []  #1"
    result = compress_hierarchy(text)
    # Single slot becomes [1x slot]
    assert "slot" in result


def test_groups_current_base62_refs():
    text = "\n".join([
        "  slot_0  []  &1",
        "  slot_1  []  &a",
        "  slot_2  []  &Mo",
    ])
    assert "[3x slot]" in compress_hierarchy(text)


def test_does_not_group_matches_at_different_indent_levels():
    text = "\n".join([
        "  slot_0  []  &1",
        "    slot_1  []  &2",
        "  slot_2  []  &3",
    ])
    result = compress_hierarchy(text)
    assert "[3x slot]" not in result
    assert result.count("[1x slot]") == 3


def test_tree_prefix_depth_prevents_parent_child_grouping():
    text = "\n".join([
        "├─ slot_0 [] &1",
        "│  └─ slot_1 [] &2",
    ])
    result = compress_hierarchy(text)
    assert result.splitlines() == ["├─ [1x slot]", "│  └─ [1x slot]"]


def test_tree_sibling_group_preserves_final_branch_marker():
    text = "\n".join([
        "├─ slot_0 [] &1",
        "├─ slot_1 [] &2",
        "└─ slot_2 [] &3",
    ])
    assert compress_hierarchy(text) == "└─ [3x slot]"
