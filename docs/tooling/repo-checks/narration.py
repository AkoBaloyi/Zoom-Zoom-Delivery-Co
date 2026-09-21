"""Pure formatting and ordering helpers for setup step narration."""

from __future__ import annotations

import posixpath
import re
from collections.abc import Iterable, Sequence
from pathlib import PurePosixPath, PureWindowsPath
from typing import Final

NARRATION_MIN: Final = 30
NARRATION_MAX: Final = 140
MIN_STEP: Final = 1
MAX_STEP: Final = 13

AI_AND_PLANNING_PATTERNS: Final = (
    ".kiro/",
    ".cursor/",
    ".claude/",
    ".windsurf/",
    ".continue/",
    ".aider*",
    "CLAUDE.md",
    "AGENTS.md",
    "planning/",
    "notes/",
    "*.plan.md",
)

_FAILURE_MARKER: Final = "Failed because the shell reported:"
_STEP_RE: Final = re.compile(r"^(1[0-3]|[1-9])\s")


class NarrationFormatError(ValueError):
    """Raised when mandatory verbatim content cannot fit the line contract."""


def _validate_step(step: int) -> int:
    if isinstance(step, bool) or not isinstance(step, int):
        raise TypeError("step must be an integer")
    if not MIN_STEP <= step <= MAX_STEP:
        raise ValueError(f"step must be between {MIN_STEP} and {MAX_STEP}")
    return step


def _normalize_prose(value: str, *, label: str, default: str | None = None) -> str:
    if not isinstance(value, str):
        raise TypeError(f"{label} must be a string")

    printable = "".join(character if character.isprintable() else " " for character in value)
    normalized = " ".join(printable.split())
    if normalized:
        return normalized
    if default is not None:
        return default
    raise ValueError(f"{label} must contain printable text")


def _validate_repository_entry(entry: str) -> str:
    if not isinstance(entry, str):
        raise TypeError("path and pattern entries must be strings")
    if not entry:
        raise ValueError("path and pattern entries must not be empty")
    if not entry.isprintable() or "\r" in entry or "\n" in entry:
        raise ValueError("path and pattern entries must be printable single-line text")

    windows_path = PureWindowsPath(entry)
    normalized_for_check = entry.replace("\\", "/").rstrip("/")
    posix_path = PurePosixPath(normalized_for_check)
    if (
        entry.startswith(("/", "\\"))
        or bool(windows_path.drive)
        or posix_path.is_absolute()
        or ".." in posix_path.parts
    ):
        raise ValueError(f"entry must be repository-relative: {entry!r}")
    return entry


def _shared_parent(entries: Sequence[str]) -> str:
    parents: list[str] = []
    for entry in entries:
        normalized = entry.replace("\\", "/").rstrip("/")
        parent = posixpath.dirname(normalized)
        parents.append(parent or ".")

    common = posixpath.commonpath(parents)
    return "repository root" if common in ("", ".") else common


def _is_ai_pattern_run(entries: Sequence[str]) -> bool:
    return (
        len(entries) == len(AI_AND_PLANNING_PATTERNS)
        and frozenset(entries) == frozenset(AI_AND_PLANNING_PATTERNS)
    )


def _ellipsize(value: str, width: int) -> str:
    if len(value) <= width:
        return value
    if width <= 3:
        return value[:width]
    head = value[: width - 3].rstrip()
    return f"{head}..." if head else value[:width]


def _fit_components(
    render,
    action_phrase: str,
    purpose_clause: str,
) -> str:
    """Shorten prose only, never a repository entry, to meet the hard cap."""

    action_width = len(action_phrase)
    purpose_width = len(purpose_clause)
    minimum_action = min(action_width, 4)
    minimum_purpose = min(purpose_width, 4)

    while True:
        action = _ellipsize(action_phrase, action_width)
        purpose = _ellipsize(purpose_clause, purpose_width)
        line = render(action, purpose)
        if len(line) <= NARRATION_MAX:
            break

        can_shorten_action = action_width > minimum_action
        can_shorten_purpose = purpose_width > minimum_purpose
        if not can_shorten_action and not can_shorten_purpose:
            raise NarrationFormatError(
                "mandatory verbatim narration content cannot fit within 140 characters"
            )
        if can_shorten_action and (
            not can_shorten_purpose or action_width >= purpose_width
        ):
            action_width -= 1
        else:
            purpose_width -= 1

    if len(line) < NARRATION_MIN:
        line += " for a clear repository record"
    if len(line) > NARRATION_MAX:
        raise NarrationFormatError("narration line exceeds 140 characters")
    if not line.isprintable() or "\r" in line or "\n" in line:
        raise NarrationFormatError("narration line must be printable and single-line")
    return line


def format_narration(
    step: int,
    action_phrase: str,
    paths_or_patterns: Iterable[str],
    purpose_clause: str,
    *,
    guide_or_report: str | None = None,
    removal_count: int | None = None,
) -> str:
    """Format one completed setup step as a printable 30--140 character line.

    Up to three repository entries are preserved verbatim.  Four or more paths
    collapse to their shared parent and count.  The documented step-2 pattern
    run is the sole exception: when its removal count is supplied, all eleven
    required patterns are named in the compact form from Tension 1.

    For a step with no path, pass ``guide_or_report`` to name the presented
    artifact.  A deterministic generic report name is used when omitted so the
    function remains total over generated zero-entry inputs.
    """

    _validate_step(step)
    action = _normalize_prose(action_phrase, label="action_phrase")
    purpose = _normalize_prose(purpose_clause, label="purpose_clause")

    if isinstance(paths_or_patterns, str):
        raise TypeError("paths_or_patterns must be an iterable of entries, not a string")
    entries = tuple(_validate_repository_entry(entry) for entry in paths_or_patterns)

    if removal_count is not None:
        if isinstance(removal_count, bool) or not isinstance(removal_count, int):
            raise TypeError("removal_count must be an integer")
        if not 0 <= removal_count <= 999:
            raise ValueError("removal_count must be between 0 and 999")

    if _is_ai_pattern_run(entries) and removal_count is not None:
        pattern_text = " ".join(entries)

        def render(current_action: str, current_purpose: str) -> str:
            return (
                f"{step} {current_action} {pattern_text}; "
                f"{removal_count} unstaged, {current_purpose}"
            )

        return _fit_components(render, action, purpose)

    if not entries:
        subject = _normalize_prose(
            guide_or_report or "",
            label="guide_or_report",
            default="the step guide/report",
        )
    elif len(entries) <= 3:
        subject = " ".join(entries)
    else:
        subject = f"{len(entries)} paths under {_shared_parent(entries)}"

    def render(current_action: str, current_purpose: str) -> str:
        return f"{step} {current_action} {subject}, {current_purpose}"

    return _fit_components(render, action, purpose)


def format_failure(step: int, shell_error: str) -> str:
    """Format a normalized shell failure and truncate it to the 140-char cap."""

    _validate_step(step)
    error = _normalize_prose(
        shell_error,
        label="shell_error",
        default="<no shell error text>",
    )
    prefix = f"{step} {_FAILURE_MARKER} "
    available = NARRATION_MAX - len(prefix)
    line = prefix + _ellipsize(error, available)
    if not NARRATION_MIN <= len(line) <= NARRATION_MAX:
        raise NarrationFormatError("failure narration is outside the length budget")
    return line


def _line_step(line: str) -> int:
    if not isinstance(line, str):
        raise TypeError("summary lines must be strings")
    if not line.isprintable() or "\r" in line or "\n" in line:
        raise ValueError("summary lines must be printable single-line text")
    if not NARRATION_MIN <= len(line) <= NARRATION_MAX:
        raise ValueError("summary lines must contain 30 to 140 characters")

    match = _STEP_RE.match(line)
    if match is None:
        raise ValueError("summary lines must open with a step number from 1 to 13")
    return int(match.group(1))


def sort_summary(
    lines: Iterable[str],
    failure_line: str | None = None,
) -> tuple[str, ...]:
    """Return ascending, duplicate-free completed lines with one failure last.

    Repeated completed steps use the last emitted line.  If malformed input
    contains multiple failure emissions, the last one wins, matching the same
    deterministic rule while preserving the setup contract's single failure.
    """

    if isinstance(lines, str):
        raise TypeError("lines must be an iterable of narration lines, not a string")

    completed: dict[int, str] = {}
    failure: tuple[int, str] | None = None
    supplied = list(lines)
    if failure_line is not None:
        supplied.append(failure_line)

    for line in supplied:
        step = _line_step(line)
        is_failure = line.startswith(f"{step} {_FAILURE_MARKER}")
        if is_failure:
            failure = (step, line)
            completed.pop(step, None)
        else:
            completed[step] = line

    if failure is not None:
        completed.pop(failure[0], None)

    ordered = [completed[step] for step in sorted(completed)]
    if failure is not None:
        ordered.append(failure[1])
    return tuple(ordered)


# Concise aliases keep future property tests readable without adding behavior.
format_step = format_narration
order_summary = sort_summary


__all__ = [
    "NARRATION_MIN",
    "NARRATION_MAX",
    "AI_AND_PLANNING_PATTERNS",
    "NarrationFormatError",
    "format_narration",
    "format_step",
    "format_failure",
    "sort_summary",
    "order_summary",
]
