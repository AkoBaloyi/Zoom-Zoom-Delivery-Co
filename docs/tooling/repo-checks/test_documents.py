"""Example-based Markdown contract tests for repository documentation.

Task 16.1 covers only ``README.md``.  The parsing helpers are intentionally
kept document-agnostic so later document and guide contract tasks can append
tests to this module without relying on brittle line numbers or whole-file
string snapshots.
"""

from __future__ import annotations

import json
import re
from dataclasses import dataclass
from pathlib import Path, PurePosixPath

import pytest

_HEADING_RE = re.compile(r"^\s{0,3}(#{1,6})[ \t]+(.+?)\s*$")
_FENCE_RE = re.compile(r"^\s{0,3}(`{3,}|~{3,})(.*)$")
_INLINE_CODE_RE = re.compile(r"(?<!`)`([^`\n]+)`(?!`)")
_LINK_RE = re.compile(
    r"(?<!!)\[([^\]\n]+)\]\(\s*(?:<([^>\n]+)>|([^\s)\n]+))"
    r"(?:\s+(?:\"[^\"]*\"|'[^']*'|\([^)]*\)))?\s*\)"
)
_NUMBERED_ITEM_RE = re.compile(r"^(\s{0,3})(\d+)[.)][ \t]+(.+?)\s*$")
_WORD_RE = re.compile(r"[A-Za-z0-9]+(?:['’\-][A-Za-z0-9]+)*")
_EDITOR_VERSION_RE = re.compile(r"\b\d+(?:\.\d+){2}[A-Za-z]\d+\b")
_REPOSITORY_PATH_PREFIXES = frozenset(
    {"Assets", "ProjectSettings", "Packages", "docs"}
)


@dataclass(frozen=True)
class MarkdownSection:
    """One ATX-heading section, bounded by the next peer/ancestor heading."""

    title: str
    level: int
    body: str
    start_line: int
    end_line: int


@dataclass(frozen=True)
class MarkdownTable:
    """A parsed pipe table with the Markdown delimiter row removed."""

    header: tuple[str, ...]
    rows: tuple[tuple[str, ...], ...]


@dataclass(frozen=True)
class MarkdownLink:
    """An inline Markdown link."""

    label: str
    target: str


@dataclass(frozen=True)
class MarkdownNumberedList:
    """A contiguous ordered-list block."""

    numbers: tuple[int, ...]
    items: tuple[str, ...]


@dataclass(frozen=True)
class MarkdownDocument:
    """A small structural representation suitable for contract assertions."""

    source: str
    sections: tuple[MarkdownSection, ...]

    @classmethod
    def parse(cls, source: str) -> MarkdownDocument:
        lines = source.splitlines()
        headings = _heading_locations(lines)
        sections: list[MarkdownSection] = []

        for position, (start_line, level, title) in enumerate(headings):
            end_line = len(lines)
            for candidate_line, candidate_level, _ in headings[position + 1 :]:
                if candidate_level <= level:
                    end_line = candidate_line
                    break
            sections.append(
                MarkdownSection(
                    title=title,
                    level=level,
                    body="\n".join(lines[start_line + 1 : end_line]).strip(),
                    start_line=start_line,
                    end_line=end_line,
                )
            )

        return cls(source=source, sections=tuple(sections))

    def sections_named(self, title: str) -> tuple[MarkdownSection, ...]:
        wanted = _normalise_text(title)
        return tuple(
            section
            for section in self.sections
            if _normalise_text(section.title) == wanted
        )

    def require_section(
        self, title: str, *, parent: str | None = None
    ) -> MarkdownSection:
        matches = self.sections_named(title)
        if parent is not None:
            parent_section = self.require_section(parent)
            matches = tuple(
                section
                for section in matches
                if parent_section.start_line < section.start_line < parent_section.end_line
            )

        scope = f" under {parent!r}" if parent else ""
        assert len(matches) == 1, (
            f"Expected exactly one {title!r} section{scope}; found {len(matches)}"
        )
        return matches[0]


def _heading_locations(lines: list[str]) -> tuple[tuple[int, int, str], ...]:
    """Find ATX headings while ignoring heading-looking text in code fences."""

    headings: list[tuple[int, int, str]] = []
    fence_character: str | None = None
    fence_length = 0

    for line_number, line in enumerate(lines):
        fence = _FENCE_RE.match(line)
        if fence:
            marker = fence.group(1)
            if fence_character is None:
                fence_character = marker[0]
                fence_length = len(marker)
                continue
            if (
                marker[0] == fence_character
                and len(marker) >= fence_length
                and not fence.group(2).strip()
            ):
                fence_character = None
                fence_length = 0
                continue

        if fence_character is not None:
            continue

        heading = _HEADING_RE.match(line)
        if not heading:
            continue
        raw_title = re.sub(r"[ \t]+#+[ \t]*$", "", heading.group(2)).strip()
        headings.append(
            (line_number, len(heading.group(1)), _plain_markdown(raw_title))
        )

    return tuple(headings)


def _plain_markdown(value: str) -> str:
    """Remove common inline Markdown while preserving its readable text."""

    value = _LINK_RE.sub(lambda match: match.group(1), value)
    value = _INLINE_CODE_RE.sub(lambda match: match.group(1), value)
    value = re.sub(r"<[^>]+>", " ", value)
    value = value.replace("\\|", "|")
    value = re.sub(r"[*_~]", "", value)
    return re.sub(r"\s+", " ", value).strip()


def _normalise_text(value: str) -> str:
    return _plain_markdown(value).casefold()


def _semantic_words(value: str, *, omit: frozenset[str] = frozenset()) -> frozenset[str]:
    return frozenset(
        word.casefold()
        for word in _WORD_RE.findall(_plain_markdown(value))
        if word.casefold() not in omit
    )


def _split_table_row(line: str) -> tuple[str, ...] | None:
    """Split a Markdown pipe row, respecting escapes and inline code spans."""

    stripped = line.strip()
    if "|" not in stripped:
        return None
    if stripped.startswith("|"):
        stripped = stripped[1:]
    if stripped.endswith("|") and not stripped.endswith("\\|"):
        stripped = stripped[:-1]

    cells: list[str] = []
    cell: list[str] = []
    code_delimiter = 0
    saw_separator = False
    index = 0

    while index < len(stripped):
        character = stripped[index]
        if character == "\\" and index + 1 < len(stripped):
            following = stripped[index + 1]
            if following in {"|", "\\"}:
                cell.append(following)
                index += 2
                continue
        if character == "`":
            run_end = index
            while run_end < len(stripped) and stripped[run_end] == "`":
                run_end += 1
            run_length = run_end - index
            if code_delimiter == 0:
                code_delimiter = run_length
            elif code_delimiter == run_length:
                code_delimiter = 0
            cell.extend(stripped[index:run_end])
            index = run_end
            continue
        if character == "|" and code_delimiter == 0:
            cells.append("".join(cell).strip())
            cell = []
            saw_separator = True
            index += 1
            continue
        cell.append(character)
        index += 1

    if not saw_separator:
        return None
    cells.append("".join(cell).strip())
    return tuple(cells)


def _is_table_delimiter(cells: tuple[str, ...]) -> bool:
    return bool(cells) and all(
        re.fullmatch(r":?-{3,}:?", cell.replace(" ", "")) is not None
        for cell in cells
    )


def parse_tables(markdown: str) -> tuple[MarkdownTable, ...]:
    """Parse all structurally valid pipe tables in a Markdown fragment."""

    lines = markdown.splitlines()
    tables: list[MarkdownTable] = []
    index = 0

    while index + 1 < len(lines):
        header = _split_table_row(lines[index])
        delimiter = _split_table_row(lines[index + 1])
        if (
            header is None
            or delimiter is None
            or len(header) != len(delimiter)
            or not _is_table_delimiter(delimiter)
        ):
            index += 1
            continue

        rows: list[tuple[str, ...]] = []
        index += 2
        while index < len(lines):
            row = _split_table_row(lines[index])
            if row is None or _is_table_delimiter(row):
                break
            assert len(row) == len(header), (
                f"Markdown table row has {len(row)} cells; expected {len(header)}: "
                f"{lines[index]!r}"
            )
            rows.append(row)
            index += 1
        tables.append(MarkdownTable(header=header, rows=tuple(rows)))

    return tuple(tables)


def require_single_table(section: MarkdownSection) -> MarkdownTable:
    tables = parse_tables(section.body)
    assert len(tables) == 1, (
        f"Expected exactly one table in {section.title!r}; found {len(tables)}"
    )
    return tables[0]


def parse_numbered_lists(markdown: str) -> tuple[MarkdownNumberedList, ...]:
    """Parse contiguous top-level numbered lists, including indented continuations."""

    lines = markdown.splitlines()
    lists: list[MarkdownNumberedList] = []
    index = 0

    while index < len(lines):
        first = _NUMBERED_ITEM_RE.match(lines[index])
        if not first:
            index += 1
            continue

        base_indent = len(first.group(1))
        numbers: list[int] = []
        items: list[str] = []

        while index < len(lines):
            item_match = _NUMBERED_ITEM_RE.match(lines[index])
            if not item_match or len(item_match.group(1)) != base_indent:
                break

            numbers.append(int(item_match.group(2)))
            item_parts = [item_match.group(3).strip()]
            index += 1
            while index < len(lines):
                continuation = lines[index]
                if not continuation.strip():
                    break
                next_item = _NUMBERED_ITEM_RE.match(continuation)
                if next_item and len(next_item.group(1)) == base_indent:
                    break
                indentation = len(continuation) - len(continuation.lstrip())
                if indentation <= base_indent:
                    break
                item_parts.append(continuation.strip())
                index += 1
            items.append(" ".join(item_parts))

            if index < len(lines) and not lines[index].strip():
                break

        lists.append(
            MarkdownNumberedList(numbers=tuple(numbers), items=tuple(items))
        )

    return tuple(lists)


def parse_links(markdown: str) -> tuple[MarkdownLink, ...]:
    """Return inline Markdown links in source order, excluding images."""

    return tuple(
        MarkdownLink(
            label=_plain_markdown(match.group(1)),
            target=(match.group(2) or match.group(3)).strip(),
        )
        for match in _LINK_RE.finditer(markdown)
    )


def _is_repository_relative_path(value: str) -> bool:
    if not value or "\\" in value or re.match(r"^[A-Za-z][A-Za-z0-9+.-]*:", value):
        return False
    path = PurePosixPath(value)
    return (
        not path.is_absolute()
        and bool(path.parts)
        and path.parts[0] in _REPOSITORY_PATH_PREFIXES
        and "." not in path.parts
        and ".." not in path.parts
    )


def _resolve_repository_path(repo_root: Path, value: str) -> Path:
    assert _is_repository_relative_path(value), (
        f"Expected a safe repository-relative path, got {value!r}"
    )
    resolved_root = repo_root.resolve()
    resolved = resolved_root.joinpath(*PurePosixPath(value).parts).resolve()
    assert resolved.is_relative_to(resolved_root), f"Path escapes repository: {value}"
    return resolved


def repository_path_occurrences(
    markdown: str, *, suffix: str
) -> tuple[str, ...]:
    """Find path occurrences in code spans, link targets, and bare Markdown text."""

    occurrences: list[str] = []
    occupied: list[tuple[int, int]] = []

    for match in _INLINE_CODE_RE.finditer(markdown):
        occupied.append(match.span())
        candidate = match.group(1).strip()
        if candidate.casefold().endswith(suffix.casefold()) and _is_repository_relative_path(
            candidate
        ):
            occurrences.append(candidate)

    for match in _LINK_RE.finditer(markdown):
        occupied.append(match.span())
        candidate = (match.group(2) or match.group(3)).strip()
        candidate = candidate.split("#", 1)[0].split("?", 1)[0]
        if candidate.casefold().endswith(suffix.casefold()) and _is_repository_relative_path(
            candidate
        ):
            occurrences.append(candidate)

    masked = list(markdown)
    for start, end in occupied:
        for index in range(start, end):
            if masked[index] not in "\r\n":
                masked[index] = " "

    suffix_pattern = re.escape(suffix)
    bare_path = re.compile(
        rf"(?<![\w./-])((?:Assets|ProjectSettings|Packages|docs)/"
        rf"[^\s<>()|`]*?{suffix_pattern})(?![\w.-])",
        re.IGNORECASE,
    )
    occurrences.extend(match.group(1) for match in bare_path.finditer("".join(masked)))
    return tuple(occurrences)


@pytest.fixture(scope="module")
def readme_document(repo_root: Path) -> MarkdownDocument:
    return MarkdownDocument.parse((repo_root / "README.md").read_text(encoding="utf-8"))


def test_readme_game_description_contract(readme_document: MarkdownDocument) -> None:
    """Requirements 5.1: description length, identity, genre, and themes."""

    description = _plain_markdown(
        readme_document.require_section("Game Description").body
    )
    word_count = len(_WORD_RE.findall(description))

    assert 40 <= word_count <= 150, (
        f"Game Description has {word_count} words; expected 40 to 150"
    )
    assert "Zoom Zoom Delivery Co" in description
    normalised = description.casefold()
    assert "solo arcade delivery game" in normalised
    for concept in ("pressure", "mistakes", "recovery"):
        assert re.search(rf"\b{concept}\b", normalised), (
            f"Game Description must name {concept!r}"
        )


def test_readme_design_question_contract(readme_document: MarkdownDocument) -> None:
    """Requirement 5.2: one question with all three required design ideas."""

    section = readme_document.require_section("Design Question")
    non_empty_blocks = tuple(
        block.strip() for block in re.split(r"\n\s*\n", section.body) if block.strip()
    )
    assert len(non_empty_blocks) == 1, "Design Question must be one prose block"

    question = _plain_markdown(non_empty_blocks[0])
    assert question.endswith("?"), "Design Question must end with '?'"
    terminal_marks = re.findall(r"[.!?](?=(?:[\"'”’\])}]*)?(?:\s|$))", question)
    assert terminal_marks == ["?"], (
        f"Design Question must be one sentence; found terminators {terminal_marks}"
    )

    normalised = question.casefold()
    assert re.search(r"\bautomatic\s+order\s+pressure\b", normalised)
    assert re.search(r"\bdemanding\s+but\s+predictable\s+driving\b", normalised)
    assert re.search(r"\brecovery\b.*\bsatisfying\b", normalised)


def test_readme_unity_version_matches_project_exactly_once(
    repo_root: Path, readme_document: MarkdownDocument
) -> None:
    """Requirement 5.3: one verbatim editor-version occurrence."""

    version_file = (repo_root / "ProjectSettings" / "ProjectVersion.txt").read_text(
        encoding="utf-8"
    )
    matches = re.findall(r"(?m)^m_EditorVersion:\s*(\S+)\s*$", version_file)
    assert len(matches) == 1, "ProjectVersion.txt must contain one m_EditorVersion"
    expected_version = matches[0]
    assert expected_version == "6000.5.4f1"

    assert readme_document.source.count(expected_version) == 1, (
        f"README.md must contain {expected_version!r} exactly once"
    )
    assert _EDITOR_VERSION_RE.findall(readme_document.source) == [expected_version], (
        "README.md must contain no second or different Unity editor version"
    )


def test_readme_open_steps_contract(readme_document: MarkdownDocument) -> None:
    """Requirement 5.5: concise Unity Hub-to-Play numbered instructions."""

    section = readme_document.require_section("How to Open")
    numbered_lists = parse_numbered_lists(section.body)
    assert len(numbered_lists) == 1, (
        f"How to Open must contain one numbered list; found {len(numbered_lists)}"
    )
    open_steps = numbered_lists[0]

    assert 1 <= len(open_steps.items) <= 8
    assert open_steps.numbers == tuple(range(1, len(open_steps.items) + 1))

    first_step = _normalise_text(open_steps.items[0])
    assert re.search(r"\badd\b.*\bproject\s+folder\b.*\bunity\s+hub\b", first_step)

    version_steps = [
        _normalise_text(item)
        for item in open_steps.items
        if "6000.5.4f1" in item
    ]
    assert len(version_steps) == 1, "Exactly one open step must name 6000.5.4f1"
    assert re.search(r"\bselect\b.*\b6000\.5\.4f1\b", version_steps[0])

    final_step = _normalise_text(open_steps.items[-1])
    assert re.search(r"\benter\b.*\bplay\s+mode\b", final_step)


def test_readme_ownership_tables_contract(readme_document: MarkdownDocument) -> None:
    """Requirements 5.6 and 9.2: exact system and branch ownership maps."""

    system_section = readme_document.require_section(
        "System Ownership", parent="Ownership"
    )
    system_table = require_single_table(system_section)
    assert tuple(_normalise_text(cell) for cell in system_table.header) == (
        "system",
        "owner",
    )
    assert len(system_table.rows) == 3

    system_mapping = {
        _semantic_words(system, omit=frozenset({"and"})): _normalise_text(owner)
        for system, owner in system_table.rows
    }
    assert len(system_mapping) == 3, "System ownership rows must be unique"
    assert system_mapping == {
        frozenset({"vehicle", "camera"}): "ako",
        frozenset({"orders", "timers", "cargo"}): "kyuri",
        frozenset({"greybox", "level", "ui"}): "zubuhle",
    }

    branch_section = readme_document.require_section(
        "Branch Ownership", parent="Ownership"
    )
    branch_table = require_single_table(branch_section)
    assert tuple(_normalise_text(cell) for cell in branch_table.header) == (
        "branch",
        "owner",
    )
    assert len(branch_table.rows) == 3

    branch_mapping = {
        _plain_markdown(branch): _plain_markdown(owner)
        for branch, owner in branch_table.rows
    }
    assert len(branch_mapping) == 3, "Branch ownership rows must be unique"
    assert branch_mapping == {
        "feature/vehicle-camera": "Ako",
        "feature/orders-cargo": "Kyuri",
        "feature/greybox-ui": "Zubuhle",
    }


def test_readme_names_main_and_one_tracked_scene(
    repo_root: Path, repo_git, readme_document: MarkdownDocument
) -> None:
    """Requirements 5.7 and 5.9: marker branch and sole playable scene."""

    open_section = _plain_markdown(
        readme_document.require_section("How to Open").body
    ).casefold()
    assert re.search(r"\bmain\b.{0,40}\bbranch\b|\bbranch\b.{0,40}\bmain\b", open_section)

    scene_occurrences = repository_path_occurrences(
        readme_document.source, suffix=".unity"
    )
    assert len(scene_occurrences) == 1, (
        "README.md must name exactly one repository-relative .unity path; "
        f"found {scene_occurrences}"
    )
    scene_path = scene_occurrences[0]
    assert scene_path.startswith("Assets/")
    assert _resolve_repository_path(repo_root, scene_path).is_file()
    assert repo_git.ls_files(scene_path) == (scene_path,), (
        f"README scene must be tracked: {scene_path}"
    )


def test_readme_document_links_resolve_and_are_tracked(
    repo_root: Path, repo_git, readme_document: MarkdownDocument
) -> None:
    """Requirement 5.8: exactly the three relative documentation links."""

    links = parse_links(readme_document.require_section("Links").body)
    expected_targets = {
        "docs/CREDITS.md",
        "docs/BUILD-LOG.md",
        "docs/GIT-RULES.md",
    }
    targets = tuple(link.target for link in links)

    assert len(targets) == 3
    assert set(targets) == expected_targets
    assert len(set(targets)) == len(targets), "README documentation links must be unique"

    for target in targets:
        assert _is_repository_relative_path(target), (
            f"Documentation link must be repository-relative: {target}"
        )
        assert _resolve_repository_path(repo_root, target).is_file(), (
            f"README link target is missing: {target}"
        )

    tracked_targets = repo_git.ls_files(targets)
    assert set(tracked_targets) == expected_targets
    assert len(tracked_targets) == len(expected_targets)


def test_readme_controls_have_required_actions_and_bindings(
    repo_root: Path, readme_document: MarkdownDocument
) -> None:
    """Requirement 5.10: every playable control has its action and binding."""

    controls_table = require_single_table(readme_document.require_section("Controls"))
    assert tuple(_normalise_text(cell) for cell in controls_table.header) == (
        "game control",
        "input binding",
        "player action",
    )
    assert len(controls_table.rows) == 5

    expectations = {
        frozenset({"drive", "forward", "backward"}): (
            "move",
            (r"\bw\b", r"\bs\b", r"\bup\s+arrow\b", r"\bdown\s+arrow\b"),
        ),
        frozenset({"steer", "left", "right"}): (
            "move",
            (r"\ba\b", r"\bd\b", r"\bleft\s+arrow\b", r"\bright\s+arrow\b"),
        ),
        frozenset({"handbrake"}): ("jump", (r"\bspace\b",)),
        frozenset({"pick", "up", "drop", "off", "order"}): (
            "interact",
            (r"\be\b",),
        ),
        frozenset({"camera"}): ("look", (r"\bmouse\b",)),
    }

    seen_controls: set[frozenset[str]] = set()
    for control, binding, action in controls_table.rows:
        assert _plain_markdown(control)
        assert _plain_markdown(binding), f"Control has no binding: {control}"
        assert _plain_markdown(action), f"Control has no Input System action: {control}"

        control_key = _semantic_words(control)
        assert control_key in expectations, f"Unexpected control row: {control}"
        assert control_key not in seen_controls, f"Duplicate control row: {control}"
        seen_controls.add(control_key)

        expected_action, binding_patterns = expectations[control_key]
        assert _normalise_text(action) == expected_action
        normalised_binding = _normalise_text(binding)
        for pattern in binding_patterns:
            assert re.search(pattern, normalised_binding), (
                f"{control!r} binding must match {pattern!r}: {binding!r}"
            )

    assert seen_controls == set(expectations)

    input_actions = json.loads(
        (repo_root / "Assets" / "InputSystem_Actions.inputactions").read_text(
            encoding="utf-8"
        )
    )
    player_maps = [item for item in input_actions["maps"] if item["name"] == "Player"]
    assert len(player_maps) == 1
    player_map = player_maps[0]
    action_names = {item["name"].casefold() for item in player_map["actions"]}
    assert {expected[0] for expected in expectations.values()} <= action_names

    binding_paths: dict[str, set[str]] = {}
    for binding in player_map["bindings"]:
        action_name = binding["action"].casefold()
        binding_paths.setdefault(action_name, set()).add(binding["path"].casefold())
    assert {
        "<keyboard>/w",
        "<keyboard>/s",
        "<keyboard>/uparrow",
        "<keyboard>/downarrow",
        "<keyboard>/a",
        "<keyboard>/d",
        "<keyboard>/leftarrow",
        "<keyboard>/rightarrow",
    } <= binding_paths["move"]
    assert "<keyboard>/space" in binding_paths["jump"]
    assert "<keyboard>/e" in binding_paths["interact"]
    assert "<pointer>/delta" in binding_paths["look"]


def test_readme_rights_contract(readme_document: MarkdownDocument) -> None:
    """Requirements 13.2 and 13.7: coursework rights and use prohibition."""

    rights_sections = readme_document.sections_named("Rights")
    assert len(rights_sections) == 1, (
        f"README.md must contain exactly one Rights section; found {len(rights_sections)}"
    )
    rights = _plain_markdown(rights_sections[0].body).casefold()

    assert re.search(r"\buniversity\s+coursework\b", rights)
    assert "reserve all rights" in rights
    for owner in ("Ako", "Kyuri", "Zubuhle"):
        assert re.search(rf"\b{owner.casefold()}\b", rights)
    for prohibited_use in ("copy", "modify", "redistribute", "resubmit"):
        assert re.search(rf"\b{prohibited_use}\b", rights)
    assert re.search(r"\bwritten\s+permission\b.*\ball\s+three\b", rights)


# Task 16.2: CREDITS.md, BUILD-LOG.md, and GIT-RULES.md contracts.
_BUILD_TAG_RE = re.compile(
    r"^build-\d{4}-\d{2}-\d{2}(?:-[2-9]|-[1-9]\d+)?$"
)
_CALENDAR_DATE_RE = re.compile(r"^\d{4}-\d{2}-\d{2}$")
_GENERIC_BUILD_DETAIL_WORDS = frozenset(
    {
        "bug",
        "defect",
        "description",
        "example",
        "feature",
        "here",
        "none",
        "placeholder",
        "something",
        "tbd",
        "text",
        "todo",
        "unknown",
    }
)
_BUILD_DETAIL_STOP_WORDS = frozenset(
    {
        "a",
        "an",
        "and",
        "are",
        "at",
        "for",
        "in",
        "is",
        "it",
        "of",
        "on",
        "the",
        "to",
        "was",
        "were",
        "with",
    }
)


@dataclass(frozen=True)
class MarkdownNumberedRule:
    """One top-level numbered rule, including its continuation paragraphs."""

    number: int
    source: str


def _markdown_prose(markdown: str) -> str:
    """Return readable prose with pipe tables and fenced code removed."""

    lines = markdown.splitlines()
    prose_lines: list[str] = []
    fence_character: str | None = None
    fence_length = 0
    index = 0

    while index < len(lines):
        fence = _FENCE_RE.match(lines[index])
        if fence:
            marker = fence.group(1)
            if fence_character is None:
                fence_character = marker[0]
                fence_length = len(marker)
            elif (
                marker[0] == fence_character
                and len(marker) >= fence_length
                and not fence.group(2).strip()
            ):
                fence_character = None
                fence_length = 0
            index += 1
            continue

        if fence_character is not None:
            index += 1
            continue

        if index + 1 < len(lines):
            header = _split_table_row(lines[index])
            delimiter = _split_table_row(lines[index + 1])
            if (
                header is not None
                and delimiter is not None
                and len(header) == len(delimiter)
                and _is_table_delimiter(delimiter)
            ):
                index += 2
                while index < len(lines):
                    row = _split_table_row(lines[index])
                    if (
                        row is None
                        or len(row) != len(header)
                        or _is_table_delimiter(row)
                    ):
                        break
                    index += 1
                continue

        prose_lines.append(lines[index])
        index += 1

    return _plain_markdown("\n".join(prose_lines))


def _parse_top_level_numbered_rules(
    markdown: str,
) -> tuple[MarkdownNumberedRule, ...]:
    """Parse top-level numbered items while ignoring item-looking fenced code."""

    lines = markdown.splitlines()
    candidates: list[tuple[int, int, int]] = []
    fence_character: str | None = None
    fence_length = 0

    for line_number, line in enumerate(lines):
        fence = _FENCE_RE.match(line)
        if fence:
            marker = fence.group(1)
            if fence_character is None:
                fence_character = marker[0]
                fence_length = len(marker)
            elif (
                marker[0] == fence_character
                and len(marker) >= fence_length
                and not fence.group(2).strip()
            ):
                fence_character = None
                fence_length = 0
            continue

        if fence_character is not None:
            continue

        item = _NUMBERED_ITEM_RE.match(line)
        if item:
            candidates.append(
                (line_number, len(item.group(1)), int(item.group(2)))
            )

    if not candidates:
        return ()

    base_indent = min(indent for _, indent, _ in candidates)
    starts = tuple(
        (line_number, number)
        for line_number, indent, number in candidates
        if indent == base_indent
    )
    rules: list[MarkdownNumberedRule] = []
    for position, (start_line, number) in enumerate(starts):
        end_line = starts[position + 1][0] if position + 1 < len(starts) else len(lines)
        rules.append(
            MarkdownNumberedRule(
                number=number,
                source="\n".join(lines[start_line:end_line]).strip(),
            )
        )
    return tuple(rules)


def _fenced_code_lines(markdown: str) -> tuple[str, ...]:
    """Return non-empty fenced-code lines, with list indentation removed."""

    commands: list[str] = []
    fence_character: str | None = None
    fence_length = 0

    for line in markdown.splitlines():
        fence = _FENCE_RE.match(line)
        if fence:
            marker = fence.group(1)
            if fence_character is None:
                fence_character = marker[0]
                fence_length = len(marker)
            elif (
                marker[0] == fence_character
                and len(marker) >= fence_length
                and not fence.group(2).strip()
            ):
                fence_character = None
                fence_length = 0
            continue
        if fence_character is not None and line.strip():
            commands.append(line.strip())

    assert fence_character is None, "Unclosed fenced code block"
    return tuple(commands)


def _inline_code_literals(markdown: str) -> tuple[str, ...]:
    return tuple(match.group(1).strip() for match in _INLINE_CODE_RE.finditer(markdown))


def _require_rule(
    rules: tuple[MarkdownNumberedRule, ...], number: int
) -> MarkdownNumberedRule:
    matches = tuple(rule for rule in rules if rule.number == number)
    assert len(matches) == 1, (
        f"Expected exactly one Git rule {number}; found {len(matches)}"
    )
    return matches[0]


def _assert_specific_build_detail(value: str, *, column: str) -> None:
    plain = _plain_markdown(value)
    normalised = plain.casefold()
    assert not re.search(r"\b(?:placeholder|tbd|todo|unknown)\b", normalised), (
        f"{column} must contain a specific example, not placeholder text: {value!r}"
    )
    meaningful = _semantic_words(
        value, omit=_BUILD_DETAIL_STOP_WORDS
    ) - _GENERIC_BUILD_DETAIL_WORDS
    assert len(meaningful) >= 2, (
        f"{column} must name a specific feature or defect: {value!r}"
    )


@pytest.fixture(scope="module")
def credits_document(repo_root: Path) -> MarkdownDocument:
    return MarkdownDocument.parse(
        (repo_root / "docs" / "CREDITS.md").read_text(encoding="utf-8")
    )


@pytest.fixture(scope="module")
def build_log_document(repo_root: Path) -> MarkdownDocument:
    return MarkdownDocument.parse(
        (repo_root / "docs" / "BUILD-LOG.md").read_text(encoding="utf-8")
    )


@pytest.fixture(scope="module")
def git_rules_document(repo_root: Path) -> MarkdownDocument:
    return MarkdownDocument.parse(
        (repo_root / "docs" / "GIT-RULES.md").read_text(encoding="utf-8")
    )


@pytest.fixture(scope="module")
def git_rules(
    git_rules_document: MarkdownDocument,
) -> tuple[MarkdownNumberedRule, ...]:
    return _parse_top_level_numbered_rules(git_rules_document.source)


def test_credits_table_has_only_the_complete_example_row(
    credits_document: MarkdownDocument,
) -> None:
    """Requirements 6.1, 6.2, and 6.6: exact table and setup rows."""

    tables = parse_tables(credits_document.source)
    assert len(tables) == 1, f"CREDITS.md must contain one table; found {len(tables)}"
    table = tables[0]
    assert tuple(_plain_markdown(cell) for cell in table.header) == (
        "Asset",
        "Author",
        "Source URL",
        "Licence",
        "Where it is used",
    )
    assert len(table.rows) == 1, (
        "CREDITS.md must start with exactly one example row and zero real rows"
    )

    example_rows = tuple(
        row
        for row in table.rows
        if re.match(r"(?i)^example\b", _plain_markdown(row[0]))
    )
    real_rows = tuple(row for row in table.rows if row not in example_rows)
    assert len(example_rows) == 1
    assert real_rows == (), f"CREDITS.md must contain zero real asset rows: {real_rows}"
    assert len(example_rows[0]) == 5
    assert all(_plain_markdown(cell) for cell in example_rows[0]), (
        "Every cell in the CREDITS.md example row must be non-empty"
    )


def test_credits_same_day_and_before_commit_rule(
    credits_document: MarkdownDocument,
) -> None:
    """Requirement 6.3: the downloader records attribution promptly."""

    prose = _markdown_prose(credits_document.source).casefold()
    assert re.search(
        r"\bteam member\b.*\bdownloads?\b.*\badd\b.*\bone row\b"
        r".*\bsame calendar day\b.*\bdownload\b",
        prose,
    )
    assert re.search(
        r"\bsame calendar day\b.*\bbefore\b.*\basset\b.*\bcommitted\b",
        prose,
    )


def test_credits_third_party_path_and_asset_naming_rule(
    credits_document: MarkdownDocument,
) -> None:
    """Requirement 6.4: exact storage path and table naming convention."""

    prose = _markdown_prose(credits_document.source).casefold()
    assert "Assets/_Project/ThirdParty" in _inline_code_literals(
        credits_document.source
    )
    assert re.search(r"\bevery asset recorded\b.*\bstored under\b", prose)
    assert re.search(
        r"\basset cell\b.*\bname\b.*\bfile or folder\b.*\bexactly as\b"
        r".*\bappears under that path\b",
        prose,
    )


def test_credits_completeness_rule(credits_document: MarkdownDocument) -> None:
    """Requirement 6.7: all five cells are required before commit."""

    prose = _markdown_prose(credits_document.source).casefold()
    assert re.search(
        r"\brow counts as complete only when\b.*\beach\b.*\bfive cells\b"
        r".*\bnon[- ]empty text\b",
        prose,
    )
    assert re.search(
        r"\bcomplete\b.*\bincomplete row\b.*\bbefore committing\b.*\basset\b",
        prose,
    )


def test_credits_attribution_guard_behavior(
    credits_document: MarkdownDocument,
) -> None:
    """Requirement 6.8: unattributed ThirdParty files remain unstaged."""

    guard = credits_document.require_section("Attribution guard")
    prose = _markdown_prose(guard.body).casefold()
    literals = _inline_code_literals(guard.body)

    assert "Assets/_Project/ThirdParty" in literals
    assert ".gitkeep" in literals
    assert re.search(r"\bbefore staging any path under\b", prose)
    assert re.search(
        r"\bevery file other than\b.*\bwith a complete credits row\b", prose
    )
    assert re.search(
        r"\breport its path\b.*\bmissing author\b.*\bsource url\b.*\blicence\b",
        prose,
    )
    assert re.search(r"\brequest those values from ako before staging\b", prose)
    assert re.search(
        r"\bleave every unattributed file unstaged and unchanged on disk\b", prose
    )
    assert re.search(r"\bcontains only\b.*\bguard reports nothing\b", prose)


def test_build_log_table_shape_setup_rows_and_date_order(
    build_log_document: MarkdownDocument,
) -> None:
    """Requirements 7.1 and 7.7: exact table, date order, and no real rows."""

    tables = parse_tables(build_log_document.source)
    assert len(tables) == 1, (
        f"BUILD-LOG.md must contain one table; found {len(tables)}"
    )
    table = tables[0]
    assert tuple(_plain_markdown(cell) for cell in table.header) == (
        "Date",
        "Build tag",
        "What is in it",
        "What broke",
        "Who tested it",
    )
    assert all(len(row) == 5 for row in table.rows), (
        "BUILD-LOG.md rows must have exactly five cells and no extras"
    )

    real_rows = tuple(
        row
        for row in table.rows
        if not re.match(r"(?i)^example\b", _plain_markdown(row[0]))
    )
    assert real_rows == (), f"BUILD-LOG.md must contain zero real rows: {real_rows}"
    real_dates = tuple(_plain_markdown(row[0]) for row in real_rows)
    assert all(_CALENDAR_DATE_RE.fullmatch(value) for value in real_dates)
    assert real_dates == tuple(sorted(real_dates)), (
        "Real build rows must run from oldest Date to newest Date"
    )

    prose = _markdown_prose(build_log_document.source).casefold()
    assert re.search(r"\boldest date first\b.*\bnewest date last\b", prose)


def test_build_log_example_row_contract(
    build_log_document: MarkdownDocument,
) -> None:
    """Requirement 7.4: one complete, specific, unmistakable example."""

    table = parse_tables(build_log_document.source)[0]
    example_rows = tuple(
        row
        for row in table.rows
        if re.match(r"(?i)^example\b", _plain_markdown(row[0]))
    )
    assert len(example_rows) == 1
    row = example_rows[0]
    assert len(row) == 5
    assert all(_plain_markdown(cell) for cell in row)

    date_cell, build_tag, what_is_in_it, what_broke, who_tested = row
    assert _plain_markdown(date_cell).startswith("Example")
    assert _BUILD_TAG_RE.fullmatch(_plain_markdown(build_tag))
    _assert_specific_build_detail(what_is_in_it, column="What is in it")
    _assert_specific_build_detail(what_broke, column="What broke")
    assert _plain_markdown(who_tested) in {"Ako", "Kyuri", "Zubuhle"}


def test_build_log_responsibility_and_tag_date_rules(
    build_log_document: MarkdownDocument,
) -> None:
    """Requirements 7.2 and 7.3: recorder, timing, and matching dates."""

    prose = _markdown_prose(build_log_document.source).casefold()
    literals = _inline_code_literals(build_log_document.source)
    assert re.search(
        r"\bako records exactly one row for each checkpoint\s*build\b"
        r".*\bsame calendar day\b.*\bbuild tag is created\b",
        prose,
    )
    assert re.search(r"\bwhile ako is unavailable\b.*\bkyuri records the row\b", prose)
    assert "build-YYYY-MM-DD" in literals
    assert "YYYY-MM-DD" in literals
    assert re.search(
        r"\bbuild tag\b.*\bformat\b.*\bsame\b.*\bdate\b.*\bdate cell\b",
        prose,
    )


def test_build_log_completeness_rule(
    build_log_document: MarkdownDocument,
) -> None:
    """Requirement 7.5: blanks are missing records, never clean results."""

    prose = _markdown_prose(build_log_document.source).casefold()
    assert re.search(
        r"\bevery cell\b.*\bcheckpoint\s*build row\b.*\bnon[- ]empty\b",
        prose,
    )
    assert re.search(r"\bnothing to report\b.*\bnone\b", prose)
    assert re.search(r"\bblank cell\b.*\bmissing\b.*\bclean result\b", prose)


def test_build_log_failed_build_rule(build_log_document: MarkdownDocument) -> None:
    """Requirement 7.6: failed tagged builds are logged but not checkpoints."""

    prose = _markdown_prose(build_log_document.source).casefold()
    assert re.search(
        r"\btagged build\b.*\bfails to open\b.*\bconsole errors\b"
        r".*\bstill logged as a row\b",
        prose,
    )
    assert re.search(r"\bfailure named\b.*\bwhat broke cell\b", prose)
    assert re.search(
        r"\bnot a checkpoint\s*build until\b.*\blater build\b"
        r".*\bopens and plays\b.*\bwithout console errors\b",
        prose,
    )


def test_git_rules_are_exactly_eleven_numbered_rules(
    git_rules: tuple[MarkdownNumberedRule, ...],
) -> None:
    """Requirement 8: one numbered rule for each criterion R8.1-R8.11."""

    assert tuple(rule.number for rule in git_rules) == tuple(range(1, 12))


def test_git_rule_8_1_session_fetch_and_merge(
    git_rules: tuple[MarkdownNumberedRule, ...],
) -> None:
    """Requirement 8.1: update the owned branch before opening Unity."""

    rule = _require_rule(git_rules, 1)
    prose = _markdown_prose(rule.source).casefold()
    assert re.search(r"\bat the start of every work session\b", prose)
    assert re.search(r"\bon your own branch\b", prose)
    assert re.search(r"\bfetch\b.*\bmain\b.*\bmerge\b.*\bmain\b", prose)
    assert re.search(r"\bbefore opening\b.*\bproject\b.*\bunity editor\b", prose)
    assert _fenced_code_lines(rule.source) == (
        "git fetch origin",
        "git merge origin/main",
    )


def test_git_rule_8_2_branch_owners_and_branch_check(
    git_rules: tuple[MarkdownNumberedRule, ...],
) -> None:
    """Requirement 8.2: exact ownership map and checked-out branch command."""

    rule = _require_rule(git_rules, 2)
    prose = _markdown_prose(rule.source).casefold()
    for owner, branch in (
        ("ako", "feature/vehicle-camera"),
        ("kyuri", "feature/orders-cargo"),
        ("zubuhle", "feature/greybox-ui"),
    ):
        assert re.search(rf"\b{owner}\s+owns\s+{re.escape(branch)}\b", prose)
    assert re.search(r"\bcommit only to the branch you own\b", prose)
    assert re.search(r"\bbefore the first commit of every work session\b", prose)
    assert _inline_code_literals(rule.source).count("git branch --show-current") == 1
    assert re.search(r"\bdo not commit to main\b.*\banother team member", prose)


def test_git_rule_8_3_small_commit_definition(
    git_rules: tuple[MarkdownNumberedRule, ...],
) -> None:
    """Requirement 8.3: one system, concise what-subject, and why-body."""

    prose = _markdown_prose(_require_rule(git_rules, 3).source).casefold()
    assert re.search(r"\bsmall commit changes one system\b", prose)
    assert re.search(
        r"\bsubject line\b.*\bat most 72 characters\b.*\bwhat changed\b", prose
    )
    assert re.search(r"\bbody\b.*\bwhy the change was made\b", prose)


def test_git_rule_8_4_library_precommit_check(
    git_rules: tuple[MarkdownNumberedRule, ...],
) -> None:
    """Requirement 8.4: .gitignore mechanism and exact staged-path check."""

    rule = _require_rule(git_rules, 4)
    prose = _markdown_prose(rule.source).casefold()
    assert re.search(r"\bkeep\b.*library/.*\bout of version control\b", prose)
    assert re.search(r"\broot \.gitignore\b.*\bmechanism\b.*\bexcludes\b.*library/", prose)
    assert re.search(r"\bbefore every commit\b", prose)
    assert _fenced_code_lines(rule.source) == (
        "git diff --cached --name-only | Select-String '^Library/'",
    )
    assert re.search(r"\bconfirm\b.*\breturns no output\b", prose)


def test_git_rule_8_5_scene_claim_and_prefab_fallback(
    git_rules: tuple[MarkdownNumberedRule, ...],
) -> None:
    """Requirement 8.5: announce, hold, release, wait or isolate via prefabs."""

    rule = _require_rule(git_rules, 5)
    prose = _markdown_prose(rule.source).casefold()
    literals = _inline_code_literals(rule.source)
    assert re.search(
        r"\bbefore opening a shared scene\b.*\bannounce your claim\b"
        r".*\bother two team members\b",
        prose,
    )
    assert re.search(
        r"\bhold the claim until\b.*\bpush the scene change\b"
        r".*\bannounce its release\b",
        prose,
    )
    assert re.search(
        r"\bwhile another team member holds the claim\b.*\bwait for the release\b"
        r".*\bseparate test scene\b",
        prose,
    )
    assert "Assets/_Project/Scenes" in literals
    assert "Assets/_Project/Prefabs" in literals
    assert re.search(r"\bcombine the work through prefabs\b", prose)


def test_git_rule_8_6_merge_owners_and_24_hour_fallback(
    git_rules: tuple[MarkdownNumberedRule, ...],
) -> None:
    """Requirement 8.6: Ako merges, with Kyuri's defined fallback."""

    prose = _markdown_prose(_require_rule(git_rules, 6).source).casefold()
    assert re.search(r"\bako merges into main\b", prose)
    assert re.search(r"\bkyuri merges into main while ako is unavailable\b", prose)
    assert re.search(
        r"\bunavailable means\b.*\bako has not replied\b.*\bmerge request\b"
        r".*\bwithin 24 hours\b",
        prose,
    )


def test_git_rule_8_7_checkpoint_tag_commands_and_suffixes(
    git_rules: tuple[MarkdownNumberedRule, ...],
) -> None:
    """Requirement 8.7: exact tag/push commands and same-day numbering."""

    rule = _require_rule(git_rules, 7)
    prose = _markdown_prose(rule.source).casefold()
    assert re.search(
        r"\bako tags each checkpoint build\b.*\bcalendar date of the build\b",
        prose,
    )
    assert _fenced_code_lines(rule.source) == (
        "git tag build-YYYY-MM-DD",
        "git push origin build-YYYY-MM-DD",
    )
    assert re.search(
        r"\bsecond checkpoint build\b.*\bsame calendar date\b"
        r".*\bbuild-yyyy-mm-dd-2\b",
        prose,
    )
    assert re.search(r"\bincrement the suffix by one for each further build\b", prose)


def test_git_rule_8_8_main_is_playable(
    git_rules: tuple[MarkdownNumberedRule, ...],
) -> None:
    """Requirement 8.8: main opens in the pinned Unity and plays cleanly."""

    prose = _markdown_prose(_require_rule(git_rules, 8).source).casefold()
    assert re.search(
        r"\bmain\b.*\bholds only a version\b.*\bopens in unity 6000\.5\.4f1\b"
        r".*\bplays with zero unity console errors\b",
        prose,
    )


def test_git_rule_8_9_premerge_check_matches_readme_scene(
    readme_document: MarkdownDocument,
    git_rules: tuple[MarkdownNumberedRule, ...],
) -> None:
    """Requirement 8.9: complete pre-merge play check on the README scene."""

    rule = _require_rule(git_rules, 9)
    prose = _markdown_prose(rule.source).casefold()
    assert re.search(r"\bbefore pushing a merge to main\b", prose)
    assert re.search(
        r"\bmerging owner opens the merged project\b.*\bunity 6000\.5\.4f1\b",
        prose,
    )
    assert re.search(r"\bloads the scene named in README\.md\b", prose, re.IGNORECASE)
    assert re.search(r"\bplays for at least 60 seconds\b", prose)
    assert re.search(r"\bunity console reports zero errors\b", prose)

    rule_scenes = repository_path_occurrences(rule.source, suffix=".unity")
    readme_scenes = repository_path_occurrences(
        readme_document.source, suffix=".unity"
    )
    assert len(rule_scenes) == 1
    assert len(readme_scenes) == 1
    assert rule_scenes[0] == readme_scenes[0]


def test_git_rule_8_10_failed_merge_stays_local(
    git_rules: tuple[MarkdownNumberedRule, ...],
) -> None:
    """Requirement 8.10: errors block push without losing the local merge."""

    prose = _markdown_prose(_require_rule(git_rules, 10).source).casefold()
    assert re.search(r"\bpre-merge check reports one or more console errors\b", prose)
    assert re.search(r"\bdo not push the merge to main\b", prose)
    assert re.search(
        r"\bleave the merge unpushed on a local branch\b.*\bno work is lost\b",
        prose,
    )
    assert re.search(
        r"\breport the failing check\b.*\bteam member who owns the affected system\b"
        r".*\bwithin 24 hours\b",
        prose,
    )


def test_git_rule_8_11_unityyamlmerge_escalation(
    git_rules: tuple[MarkdownNumberedRule, ...],
) -> None:
    """Requirement 8.11: smart-merge Unity files, then escalate at 30 minutes."""

    rule = _require_rule(git_rules, 11)
    prose = _markdown_prose(rule.source).casefold()
    literals = _inline_code_literals(rule.source)
    assert ".unity" in literals
    assert ".prefab" in literals
    assert "unityyamlmerge" in literals
    assert re.search(
        r"\bmerge conflict\b.*\.unity.*\.prefab.*\bmerge tool registered as\b"
        r".*\bunityyamlmerge\b",
        prose,
    )
    assert re.search(r"\bremains unresolved after 30 minutes\b", prose)
    assert re.search(
        r"\bhand it to the team member who owns the affected system\b", prose
    )
