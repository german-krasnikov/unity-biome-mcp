"""Tests for docs/hooks/transform.py — MkDocs build-time markdown transforms."""

import sys
from pathlib import Path
from types import SimpleNamespace

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "docs" / "hooks"))
from transform import on_page_markdown, _fix_image_paths, _fix_html_img_paths, _add_markdown_attr


# ── helpers ──


def _page(src_path, dest_path=None):
    """Fake MkDocs page object."""
    if dest_path is None:
        parts = Path(src_path).with_suffix("").parts
        if parts[-1] == "index":
            dest_path = str(Path(*parts[:-1]) / "index.html") if len(parts) > 1 else "index.html"
        else:
            dest_path = str(Path(*parts) / "index.html")
    return SimpleNamespace(file=SimpleNamespace(src_path=src_path, dest_path=dest_path))


DIR_URLS_ON = {"use_directory_urls": True}
DIR_URLS_OFF = {"use_directory_urls": False}


# ── image path tests (markdown syntax) ──


class TestFixImagePaths:
    def test_root_index_no_prefix(self):
        page = _page("index.md", "index.html")
        md = "![hero](assets/hero.svg)"
        assert _fix_image_paths(md, page, DIR_URLS_ON) == md

    def test_depth1_file_adds_one_prefix(self):
        page = _page("comparison.md", "comparison/index.html")
        md = "![alt](assets/hero.svg)"
        assert _fix_image_paths(md, page, DIR_URLS_ON) == "![alt](../assets/hero.svg)"

    def test_depth2_file_adds_two_prefixes(self):
        page = _page("install/claude-code.md", "install/claude-code/index.html")
        md = "![alt](assets/img.png)"
        assert _fix_image_paths(md, page, DIR_URLS_ON) == "![alt](../../assets/img.png)"

    def test_section_index_depth1(self):
        page = _page("install/index.md", "install/index.html")
        md = "![alt](assets/img.png)"
        assert _fix_image_paths(md, page, DIR_URLS_ON) == "![alt](../assets/img.png)"

    def test_skips_absolute_url(self):
        page = _page("comparison.md", "comparison/index.html")
        md = "![alt](https://example.com/img.png)"
        assert _fix_image_paths(md, page, DIR_URLS_ON) == md

    def test_skips_root_relative(self):
        page = _page("comparison.md", "comparison/index.html")
        md = "![alt](/assets/img.png)"
        assert _fix_image_paths(md, page, DIR_URLS_ON) == md

    def test_skips_already_prefixed(self):
        page = _page("comparison.md", "comparison/index.html")
        md = "![alt](../assets/hero.svg)"
        assert _fix_image_paths(md, page, DIR_URLS_ON) == md

    def test_skips_data_uri(self):
        page = _page("comparison.md", "comparison/index.html")
        md = "![alt](data:image/png;base64,abc)"
        assert _fix_image_paths(md, page, DIR_URLS_ON) == md

    def test_skips_anchor(self):
        page = _page("comparison.md", "comparison/index.html")
        md = "![alt](#section)"
        assert _fix_image_paths(md, page, DIR_URLS_ON) == md

    def test_directory_urls_off_no_change(self):
        page = _page("comparison.md", "comparison.html")
        md = "![alt](assets/hero.svg)"
        assert _fix_image_paths(md, page, DIR_URLS_OFF) == md

    def test_skips_inside_code_block(self):
        page = _page("comparison.md", "comparison/index.html")
        md = "text\n```\n![alt](assets/hero.svg)\n```\nmore"
        assert "![alt](assets/hero.svg)" in _fix_image_paths(md, page, DIR_URLS_ON)

    def test_skips_inside_inline_code(self):
        page = _page("comparison.md", "comparison/index.html")
        md = "use `![alt](assets/hero.svg)` syntax"
        assert "![alt](assets/hero.svg)" in _fix_image_paths(md, page, DIR_URLS_ON)

    def test_multiple_images(self):
        page = _page("comparison.md", "comparison/index.html")
        md = "![a](assets/one.svg)\n![b](assets/two.png)"
        result = _fix_image_paths(md, page, DIR_URLS_ON)
        assert "![a](../assets/one.svg)" in result
        assert "![b](../assets/two.png)" in result

    def test_empty_alt(self):
        page = _page("comparison.md", "comparison/index.html")
        md = "![](assets/hero.svg)"
        assert _fix_image_paths(md, page, DIR_URLS_ON) == "![](../assets/hero.svg)"


# ── image path tests (HTML <img> syntax) ──


class TestFixHtmlImgPaths:
    def test_root_index_no_prefix(self):
        page = _page("index.md", "index.html")
        md = '<img src="assets/hero.svg" width="100%">'
        assert _fix_html_img_paths(md, page, DIR_URLS_ON) == md

    def test_depth1_adds_prefix(self):
        page = _page("comparison.md", "comparison/index.html")
        md = '<img src="assets/hero.svg" width="100%">'
        assert _fix_html_img_paths(md, page, DIR_URLS_ON) == '<img src="../assets/hero.svg" width="100%">'

    def test_depth2_adds_two_prefixes(self):
        page = _page("install/claude-code.md", "install/claude-code/index.html")
        md = '<img src="assets/img.png" alt="test">'
        assert _fix_html_img_paths(md, page, DIR_URLS_ON) == '<img src="../../assets/img.png" alt="test">'

    def test_single_quotes(self):
        page = _page("comparison.md", "comparison/index.html")
        md = "<img src='assets/hero.svg'>"
        assert _fix_html_img_paths(md, page, DIR_URLS_ON) == "<img src='../assets/hero.svg'>"

    def test_skips_absolute_url(self):
        page = _page("comparison.md", "comparison/index.html")
        md = '<img src="https://example.com/img.png">'
        assert _fix_html_img_paths(md, page, DIR_URLS_ON) == md

    def test_skips_already_prefixed(self):
        page = _page("comparison.md", "comparison/index.html")
        md = '<img src="../assets/hero.svg">'
        assert _fix_html_img_paths(md, page, DIR_URLS_ON) == md

    def test_skips_data_uri(self):
        page = _page("comparison.md", "comparison/index.html")
        md = '<img src="data:image/svg+xml;base64,abc">'
        assert _fix_html_img_paths(md, page, DIR_URLS_ON) == md

    def test_directory_urls_off(self):
        page = _page("comparison.md", "comparison.html")
        md = '<img src="assets/hero.svg">'
        assert _fix_html_img_paths(md, page, DIR_URLS_OFF) == md

    def test_skips_inside_code_block(self):
        page = _page("comparison.md", "comparison/index.html")
        md = '```html\n<img src="assets/hero.svg">\n```'
        assert 'src="assets/hero.svg"' in _fix_html_img_paths(md, page, DIR_URLS_ON)

    def test_multiple_img_tags(self):
        page = _page("comparison.md", "comparison/index.html")
        md = '<img src="assets/a.svg">\n<img src="assets/b.png">'
        result = _fix_html_img_paths(md, page, DIR_URLS_ON)
        assert '../assets/a.svg' in result
        assert '../assets/b.png' in result

    def test_preserves_other_attributes(self):
        page = _page("comparison.md", "comparison/index.html")
        md = '<img src="assets/hero.svg" width="100%" alt="Hero image" class="full">'
        result = _fix_html_img_paths(md, page, DIR_URLS_ON)
        assert 'width="100%"' in result
        assert 'alt="Hero image"' in result
        assert 'src="../assets/hero.svg"' in result


# ── markdown attr tests ──


class TestAddMarkdownAttr:
    def test_details_gets_markdown(self):
        assert _add_markdown_attr("<details>") == "<details markdown>"

    def test_details_with_class_gets_markdown(self):
        assert _add_markdown_attr('<details class="note">') == '<details class="note" markdown>'

    def test_details_already_has_markdown(self):
        assert _add_markdown_attr("<details markdown>") == "<details markdown>"

    def test_summary_gets_markdown(self):
        assert _add_markdown_attr("<summary>Text</summary>") == "<summary markdown>Text</summary>"

    def test_div_with_class_gets_markdown(self):
        assert _add_markdown_attr('<div class="ubm-hero">') == '<div class="ubm-hero" markdown>'

    def test_div_without_class_unchanged(self):
        md = "<div>"
        assert _add_markdown_attr(md) == md

    def test_div_already_has_markdown(self):
        md = '<div class="ubm-hero" markdown>'
        assert _add_markdown_attr(md) == md

    def test_multiple_details(self):
        md = "<details>\n<summary>A</summary>\ncontent\n</details>\n<details>\n<summary>B</summary>"
        result = _add_markdown_attr(md)
        assert result.count("markdown>") == 4  # 2 details + 2 summary

    def test_nested_div(self):
        md = '<div class="ubm-features">\n<div class="ubm-feature">\ncontent\n</div>\n</div>'
        result = _add_markdown_attr(md)
        assert result.count("markdown>") == 2

    def test_closing_tags_unchanged(self):
        md = "</details>\n</div>"
        assert _add_markdown_attr(md) == md

    def test_no_false_positive_on_div_id(self):
        md = '<div id="test">'
        assert _add_markdown_attr(md) == md

    def test_details_markdown_equals_1(self):
        md = '<details markdown="1">'
        assert _add_markdown_attr(md) == md


# ── integration: on_page_markdown ──


class TestOnPageMarkdown:
    def test_full_pipeline_depth1(self):
        page = _page("comparison.md", "comparison/index.html")
        md = (
            "# Title\n\n"
            '<img src="assets/hero.svg" width="100%">\n\n'
            "<details>\n<summary>Info</summary>\n\n"
            "- **Bold** item\n\n"
            "</details>\n"
        )
        result = on_page_markdown(md, page, DIR_URLS_ON, files=None)
        assert '../assets/hero.svg' in result
        assert '<details markdown>' in result
        assert '<summary markdown>' in result

    def test_full_pipeline_root(self):
        page = _page("index.md", "index.html")
        md = (
            '<div class="ubm-hero">\n\n'
            "# Title\n\n"
            "![hero](assets/hero.svg)\n\n"
            "</div>\n"
        )
        result = on_page_markdown(md, page, DIR_URLS_ON, files=None)
        assert "![hero](assets/hero.svg)" in result  # no prefix at root
        assert '<div class="ubm-hero" markdown>' in result

    def test_full_pipeline_depth2(self):
        page = _page("install/claude-code.md", "install/claude-code/index.html")
        md = "![setup](assets/setup.png)\n\n<details>\n<summary>Help</summary>\ntext\n</details>"
        result = on_page_markdown(md, page, DIR_URLS_ON, files=None)
        assert "![setup](../../assets/setup.png)" in result
        assert "<details markdown>" in result

    def test_code_blocks_protected(self):
        page = _page("comparison.md", "comparison/index.html")
        md = (
            "![real](assets/a.svg)\n\n"
            "```markdown\n![fake](assets/b.svg)\n```\n\n"
            '<img src="assets/c.svg">\n\n'
            '```html\n<img src="assets/d.svg">\n```\n'
        )
        result = on_page_markdown(md, page, DIR_URLS_ON, files=None)
        assert "![real](../assets/a.svg)" in result
        assert "![fake](assets/b.svg)" in result  # protected
        assert '../assets/c.svg' in result
        assert 'src="assets/d.svg"' in result  # protected

    def test_mixed_absolute_and_relative(self):
        page = _page("comparison.md", "comparison/index.html")
        md = (
            "![a](assets/local.svg)\n"
            "![b](https://cdn.example.com/remote.svg)\n"
            "![c](/absolute/path.svg)\n"
            "![d](../already/prefixed.svg)\n"
        )
        result = on_page_markdown(md, page, DIR_URLS_ON, files=None)
        assert "![a](../assets/local.svg)" in result
        assert "![b](https://cdn.example.com/remote.svg)" in result
        assert "![c](/absolute/path.svg)" in result
        assert "![d](../already/prefixed.svg)" in result

    def test_directory_urls_off_passthrough(self):
        page = _page("comparison.md", "comparison.html")
        md = '![a](assets/x.svg)\n<img src="assets/y.svg">\n<details>\ntext\n</details>'
        result = on_page_markdown(md, page, DIR_URLS_OFF, files=None)
        assert "![a](assets/x.svg)" in result  # no prefix
        assert 'src="assets/y.svg"' in result  # no prefix
        assert "<details markdown>" in result  # attrs still added
