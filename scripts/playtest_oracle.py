"""Print dsl/playtest/oracle.rows, the frozen oracle of the hand-written PlaytestParser (step 7.5 O0).

The rows are what the repository had at 86eca971, before any playtest construct existed: the command lines of the tracked .playtest files and the string literals that the
tests pass to PlaytestParser.Parse, with what those tests say about them. They are read from git, not from the working tree, and this script only prints them: the file is
never regenerated to make a difference disappear (an intended difference of the new reader is an INTENDED entry with its reason). The format is in the header it prints.

Usage (from the repository root):
  python scripts/playtest_oracle.py | diff - dsl/playtest/oracle.rows
"""

import re
import subprocess
import sys
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
BASE = "86eca9710822ab7b1c8d8a5cbed8929ee3ef873e"
NOT_STEPS = frozenset({"INCLUDE", "VAL", "VAR", "SETUP", "TEARDOWN"})

HEADER = """\
# Frozen oracle of the hand-written PlaytestParser, taken at {rev} (master) before any playtest construct existed (step 7.5 O0).
# It holds the inputs of the parser and what the tests of the project say about them, not the steps the parser built.
# Do not regenerate this file to remove a difference: an intended difference of the new reader is an INTENDED entry with its reason.
# scripts/playtest_oracle.py prints it again from that commit: python scripts/playtest_oracle.py | diff - dsl/playtest/oracle.rows
# A row is `<kind> <file>:<line> <field> <text>` (1-based line; for a call, the line of `PlaytestParser.Parse(`); an empty text ends the row.
# A text is escaped: \\\\ \\n \\r \\t \\xNN \\uNNNN, and a last space is \\x20.
# line   <VERB> <rest>           a command line of a git-tracked .playtest file, `<VERB> <rest>` (comments and blank lines are not rows)
# accept <StepTypes or -> <text> a test passes the literal <text> to PlaytestParser.Parse outside Assert.Throws; StepTypes (comma-separated) are those it asserts after the call
# reject <Exception> <text>      the call is inside Assert.Throws<Exception>: the test expects it to throw
# A call is a row only when its first argument is one plain "..." literal (not a variable, concatenation, interpolation or @"..."); other arguments are not recorded.
# {files} files, {commands} commands ({steps} steps without INCLUDE VAL VAR SETUP TEARDOWN), {verb_count} verbs: {verbs}
# {calls} calls ({accepts} accept, {rejects} reject, {typed} of the accepts with StepTypes), {literals} different literals
"""

_LEX = re.compile(r"""//[^\n]*|/\*.*?\*/|(?:\$@|@\$|@)"(?:[^"]|"")*"|\$?"(?:[^"\\\n]|\\.)*"|'(?:[^'\\\n]|\\.)+'""", re.S)
_ARGUMENT = re.compile(r'\s*"( *)"\s*[,)]')  # a plain literal as the first argument, as _mask leaves it (the body blank)
_MEMBER = re.compile(r"\n\s*(?:\[|(?:public|private|protected|internal)\b)")  # an attribute or a member: the next test starts
_THROWS = re.compile(r"\bThrows<([\w.]+)>$")
_C_SHARP = {"n": "\n", "r": "\r", "t": "\t", "0": "\0", "a": "\a", "b": "\b", "f": "\f", "v": "\v", "\\": "\\", '"': '"', "'": "'"}
_ROW = {"\\": r"\\", "\n": r"\n", "\r": r"\r", "\t": r"\t"}


def _git(*args: str) -> str:
    return subprocess.run(["git", *args], cwd=ROOT, check=True, capture_output=True).stdout.decode("utf-8")


def _mask(text: str) -> str:
    """The C# text with comments blank and the body of each string and char blank: same length, same lines, so a position in it is a position in `text`."""
    def blank(token: str) -> str:
        return re.sub(r"[^\n]", " ", token)

    def mask(match: re.Match) -> str:
        token = match.group()
        if token[0] == "/":
            return blank(token)
        opening = token.index(token[-1]) + 1
        return token[:opening] + blank(token[opening:-1]) + token[-1]
    return _LEX.sub(mask, text)


def _unescape(body: str) -> str:
    """The value of the body of a C# string literal; an escape that C# does not have is a KeyError (the oracle must not guess)."""
    return re.sub(r"\\(?:u([0-9a-fA-F]{4})|x([0-9a-fA-F]{1,4})|(.))", lambda m: _C_SHARP[m.group(3)] if m.group(3) else chr(int(m.group(1) or m.group(2), 16)), body)


def csharp_calls(text: str) -> list[tuple[int, str | None, list[str], str]]:
    """(line, exception, step types, literal) of each PlaytestParser.Parse call of a C# file whose first argument is one plain string literal. `exception` is the type of the
    Assert.Throws that holds the call, else None. The step types are those of the Assert statements from the call's statement to the next call or test."""
    code, stack, found = _mask(text), [], []
    for match in re.finditer(r"(PlaytestParser\.Parse)?\(|\)", code):  # the stack holds the text before each open parenthesis
        if match.group() == ")":
            stack = stack[:-1]
            continue
        if match.group(1):
            found.append((match, stack))
        stack = [*stack, code[max(0, match.start() - 80):match.start()].rstrip()]
    calls = []
    for index, (match, enclosing) in enumerate(found):
        if not (argument := _ARGUMENT.match(code, match.end())):
            continue
        throws = [t.group(1).rpartition(".")[2] for frame in enclosing if (t := _THROWS.search(frame))]
        stops = [len(code) if index + 1 == len(found) else found[index + 1][0].start()]
        if boundary := _MEMBER.search(code, argument.end()):
            stops.append(boundary.start())
        start = max(code.rfind(mark, 0, match.start()) for mark in ";{}") + 1
        types = [name for statement in re.findall(r"Assert\.[^;]*;", code[start:min(stops)]) for name in re.findall(r"StepType\.(\w+)", statement)]
        literal = _unescape(text[argument.start(1):argument.end(1)])
        calls.append((text.count("\n", 0, match.start()) + 1, throws[-1] if throws else None, types, literal))
    return calls


def escape(text: str) -> str:
    """The text as one line of a row: `\\`, line ends and tabs as escapes, the other control characters as \\xNN or \\uNNNN, a last space as \\x20 (a row never ends in one)."""
    def one(match: re.Match) -> str:
        char = match.group()
        return _ROW.get(char) or (f"\\x{ord(char):02x}" if ord(char) < 256 else f"\\u{ord(char):04x}")
    return re.sub(r" $", r"\\x20", re.sub(r"[\\\x00-\x1f\x7f-\x9f\u2028\u2029]", one, text))


def render(rev: str = BASE) -> str:
    """The whole file as `rev` had it: the header and the rows, .playtest lines first, then the calls by file and position."""
    tracked = _git("ls-tree", "-r", "-z", "--name-only", rev).split("\0")
    lines, calls = [], []  # (kind, file:line, field, text)
    for path in sorted(name for name in tracked if name.endswith(".playtest")):
        for number, line in enumerate(_git("show", f"{rev}:{path}").split("\n"), 1):
            if line.strip() and not line.lstrip().startswith("#"):
                verb, _, rest = line.partition(" ")
                lines.append(("line", f"{path}:{number}", verb, rest))
    for name in sorted(filter(None, _git("grep", "-lzF", "PlaytestParser.Parse(", rev, "--", "*.cs").split("\0"))):
        path = name.partition(":")[2]
        for number, exception, types, literal in csharp_calls(_git("show", f"{rev}:{path}")):
            calls.append(("reject", f"{path}:{number}", exception, literal) if exception else ("accept", f"{path}:{number}", ",".join(types) or "-", literal))
    verbs = Counter(verb for _, _, verb, _ in lines)
    header = HEADER.format(
        rev=rev[:8], files=len({where.rpartition(":")[0] for _, where, _, _ in lines}), commands=len(lines), verb_count=len(verbs),
        steps=sum(n for verb, n in verbs.items() if verb not in NOT_STEPS), verbs=" ".join(f"{verb} {n}" for verb, n in verbs.most_common()), calls=len(calls),
        accepts=sum(kind == "accept" for kind, _, _, _ in calls), rejects=sum(kind == "reject" for kind, _, _, _ in calls),
        typed=sum(kind == "accept" and field != "-" for kind, _, field, _ in calls), literals=len({text for _, _, _, text in calls}))
    return header + "".join(f"{kind} {where} {field} {escape(text)}".rstrip() + "\n" for kind, where, field, text in [*lines, *calls])


def main() -> int:
    sys.stdout.buffer.write(render().encode("utf-8"))
    return 0


if __name__ == "__main__":
    sys.exit(main())
