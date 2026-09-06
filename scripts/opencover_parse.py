"""opencover_parse.py — pure OpenCover XML -> MethodRecord parser.

Schema grounded in a real capture (CI run 33976250848, coverage-data-Linux
artifact), not from memory of the OpenCover spec: File `uid` is scoped to its
enclosing <Module>, method name comes from the mangled <Name> text
("ReturnType Namespace.Class::Method(args)"), and cyclomaticComplexity/seq/
branch counts live on <Method>/@cyclomaticComplexity and <Method>/<Summary>.

No ranking, no git, no rendering here -- see coverage_hotspots.py for that.
"""
import pathlib
import xml.etree.ElementTree as ET
from dataclasses import dataclass


@dataclass(frozen=True)
class MethodRecord:
    file: str
    class_name: str
    method_name: str
    cyclomatic_complexity: int | None
    seq_covered: int
    seq_total: int  # 0 => coverage is unknown, not 0%
    branch_covered: int
    branch_total: int
    line_start: int
    line_end: int


def _method_name(mangled_name: str) -> str:
    """'System.Void NS.Class::Method(args)' -> 'Method'; '...::Foo[T](x)' -> 'Foo[T]'."""
    after_scope = mangled_name.rsplit("::", 1)[-1]
    return after_scope.split("(", 1)[0]


def _line_range(sequence_points: ET.Element) -> tuple[int, int]:
    points = sequence_points.findall("SequencePoint")
    if not points:
        return 0, 0
    starts = [int(p.get("sl", 0)) for p in points]
    ends = [int(p.get("el", 0)) for p in points]
    return min(starts), max(ends)


def parse_opencover(path: pathlib.Path) -> list[MethodRecord]:
    path = pathlib.Path(path)
    tree = ET.parse(path)  # raises FileNotFoundError for a missing path
    root = tree.getroot()

    records: list[MethodRecord] = []
    for module in root.find("Modules").findall("Module"):
        file_by_uid = {f.get("uid"): f.get("fullPath") for f in module.find("Files").findall("File")}
        classes_el = module.find("Classes")
        if classes_el is None:
            continue
        for cls in classes_el.findall("Class"):
            class_name = cls.findtext("FullName", default="")
            methods_el = cls.find("Methods")
            if methods_el is None:
                continue
            for method in methods_el.findall("Method"):
                file_ref = method.find("FileRef")
                file_path = file_by_uid.get(file_ref.get("uid")) if file_ref is not None else ""
                summary = method.find("Summary")
                cc_attr = method.get("cyclomaticComplexity")
                line_start, line_end = _line_range(method.find("SequencePoints"))
                records.append(MethodRecord(
                    file=file_path or "",
                    class_name=class_name,
                    method_name=_method_name(method.findtext("Name", default="")),
                    cyclomatic_complexity=int(cc_attr) if cc_attr is not None else None,
                    seq_covered=int(summary.get("visitedSequencePoints", 0)),
                    seq_total=int(summary.get("numSequencePoints", 0)),
                    branch_covered=int(summary.get("visitedBranchPoints", 0)),
                    branch_total=int(summary.get("numBranchPoints", 0)),
                    line_start=line_start,
                    line_end=line_end,
                ))
    return records
