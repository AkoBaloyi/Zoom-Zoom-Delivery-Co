"""Read-only rights checks for licence names and third-party attribution."""

from __future__ import annotations

import os
import re
from collections.abc import Iterable, Mapping, Sequence
from dataclasses import dataclass
from pathlib import Path, PurePosixPath
from typing import TypeAlias

LICENCE_NAMES = frozenset(
    {
        "LICENSE",
        "LICENCE",
        "LICENSE.md",
        "LICENCE.md",
        "LICENSE.txt",
        "LICENCE.txt",
        "COPYING",
        "UNLICENSE",
    }
)
THIRD_PARTY_ROOT = "Assets/_Project/ThirdParty"
CREDITS_COLUMNS = (
    "Asset",
    "Author",
    "Source URL",
    "Licence",
    "Where it is used",
)
ATTRIBUTION_REQUEST_FIELDS = ("Author", "Source URL", "Licence")

_PathValue: TypeAlias = str | os.PathLike[str]
_CreditsInput: TypeAlias = (
    "CreditsRow | Mapping[str, object] | Sequence[object]"
)
_KEY_CHARACTERS = re.compile(r"[^a-z0-9]")


@dataclass(frozen=True, slots=True)
class CreditsRow:
    """The five cells of one credits-table row, in document order."""

    asset: str
    author: str
    source_url: str
    licence: str
    where_used: str

    @property
    def cells(self) -> tuple[str, str, str, str, str]:
        return (
            self.asset,
            self.author,
            self.source_url,
            self.licence,
            self.where_used,
        )


@dataclass(frozen=True, slots=True)
class AttributionIssue:
    """A ThirdParty file for which no matching complete five-cell row exists."""

    path: str
    asset: str
    missing_fields: tuple[str, ...]
    matching_row_count: int

    @property
    def requested_fields(self) -> tuple[str, ...]:
        """Return the missing Author/Source URL/Licence values R6.8 requests."""

        return tuple(
            field
            for field in ATTRIBUTION_REQUEST_FIELDS
            if field in self.missing_fields
        )

    @property
    def missing_author(self) -> bool:
        return "Author" in self.missing_fields

    @property
    def missing_source_url(self) -> bool:
        return "Source URL" in self.missing_fields

    @property
    def missing_licence(self) -> bool:
        return "Licence" in self.missing_fields


def _path_text(path: _PathValue) -> str:
    raw = os.fspath(path)
    if not isinstance(raw, str):
        raise TypeError("paths must be strings, not bytes")
    return raw.replace("\\", "/")


def _normalise_relative_path(path: _PathValue) -> str:
    raw = _path_text(path)
    parts: list[str] = []
    for part in raw.split("/"):
        if part in ("", "."):
            continue
        if part == "..":
            raise ValueError(f"path must not escape its root: {raw!r}")
        parts.append(part)
    return "/".join(parts)


def _basename(path: _PathValue) -> str:
    raw = _path_text(path)
    if not raw or raw.endswith("/"):
        return ""
    return raw.rsplit("/", 1)[-1]


def is_licence_name(path: _PathValue) -> bool:
    """Match exactly the eight licence names on the whole basename.

    Parent directories are irrelevant, matching is case-insensitive, and
    near-misses such as ``LICENSE-NOTES.md`` or ``MY-LICENSE`` are false.
    """

    return _basename(path).casefold() in {
        name.casefold() for name in LICENCE_NAMES
    }


def find_licence_paths(paths: Iterable[_PathValue]) -> tuple[str, ...]:
    """Return matching paths in deterministic order without touching them."""

    matches = {
        _normalise_relative_path(path)
        for path in paths
        if is_licence_name(path)
    }
    return tuple(sorted(matches))


def _cell_text(value: object) -> str:
    if value is None:
        return ""
    return str(value).strip()


def _mapping_key(value: object) -> str:
    return _KEY_CHARACTERS.sub("", str(value).casefold())


def coerce_credits_row(row: _CreditsInput) -> CreditsRow:
    """Convert a row record, mapping, or five-value sequence to ``CreditsRow``."""

    if isinstance(row, CreditsRow):
        return CreditsRow(*(_cell_text(cell) for cell in row.cells))

    if isinstance(row, Mapping):
        cells = {_mapping_key(key): value for key, value in row.items()}

        def get(*keys: str) -> str:
            for key in keys:
                if key in cells:
                    return _cell_text(cells[key])
            return ""

        return CreditsRow(
            asset=get("asset"),
            author=get("author"),
            source_url=get("sourceurl", "source"),
            licence=get("licence", "license"),
            where_used=get("whereitisused", "whereused"),
        )

    if isinstance(row, Sequence) and not isinstance(row, (str, bytes, bytearray)):
        if len(row) != len(CREDITS_COLUMNS):
            raise ValueError("a credits row must contain exactly five cells")
        return CreditsRow(*(_cell_text(cell) for cell in row))

    raise TypeError("credits rows must be CreditsRow, mappings, or five-cell sequences")


def incomplete_credits_fields(row: _CreditsInput) -> tuple[str, ...]:
    """Return empty-cell column names in the table's left-to-right order."""

    credits_row = coerce_credits_row(row)
    return tuple(
        column
        for column, cell in zip(CREDITS_COLUMNS, credits_row.cells)
        if not _cell_text(cell)
    )


def is_complete_credits_row(row: _CreditsInput) -> bool:
    """A row is complete exactly when all five cells contain non-whitespace text."""

    return not incomplete_credits_fields(row)


def _asset_reference(value: str, third_party_root: str) -> str:
    reference = _normalise_relative_path(value.strip())
    root = _normalise_relative_path(third_party_root)
    if reference.startswith(f"{root}/"):
        return reference[len(root) + 1 :]
    return reference


def _matches_asset(reference: str, relative_file: str) -> bool:
    """Match an exact file or a folder ancestor named by the Asset cell."""

    reference = reference.rstrip("/")
    return bool(reference) and (
        relative_file == reference or relative_file.startswith(f"{reference}/")
    )


def _third_party_file(
    path: _PathValue, third_party_root: str
) -> tuple[str, str] | None:
    candidate = _normalise_relative_path(path)
    root = _normalise_relative_path(third_party_root)
    if not candidate:
        return None

    if candidate.startswith(f"{root}/"):
        relative = candidate[len(root) + 1 :]
    elif candidate == root or candidate.startswith("Assets/"):
        return None
    else:
        # A bare relative path is interpreted relative to ThirdParty for tests
        # and callers that have already enumerated that directory.
        relative = candidate

    if not relative or relative.rsplit("/", 1)[-1] == ".gitkeep":
        return None
    return f"{root}/{relative}", relative


def find_attribution_issues(
    files: Iterable[_PathValue],
    credits_rows: Iterable[_CreditsInput],
    *,
    third_party_root: str = THIRD_PARTY_ROOT,
) -> tuple[AttributionIssue, ...]:
    """Report exactly ThirdParty files lacking a matching complete row.

    A row may name either the file's path relative to ``ThirdParty`` or a
    folder ancestor.  Matching is case-sensitive, as repository paths are.
    The function is pure: it stages, moves, opens, and alters no file.
    """

    rows = tuple(coerce_credits_row(row) for row in credits_rows)
    candidates = {
        candidate
        for path in files
        if (candidate := _third_party_file(path, third_party_root)) is not None
    }
    issues: list[AttributionIssue] = []

    for reported_path, relative_file in sorted(candidates):
        matching = tuple(
            row
            for row in rows
            if _matches_asset(
                _asset_reference(row.asset, third_party_root), relative_file
            )
        )
        if any(is_complete_credits_row(row) for row in matching):
            continue

        if matching:
            # Do not merge complementary incomplete rows: R6.7 requires one
            # row whose five cells are all complete.  Report the closest row.
            missing = min(
                (incomplete_credits_fields(row) for row in matching),
                key=lambda fields: (len(fields), fields),
            )
        else:
            missing = CREDITS_COLUMNS

        issues.append(
            AttributionIssue(
                path=reported_path,
                asset=relative_file,
                missing_fields=tuple(missing),
                matching_row_count=len(matching),
            )
        )

    return tuple(issues)


def scan_third_party_attribution(
    repository: _PathValue,
    credits_rows: Iterable[_CreditsInput],
    *,
    third_party_root: str = THIRD_PARTY_ROOT,
) -> tuple[AttributionIssue, ...]:
    """Enumerate ThirdParty files and run the read-only attribution guard."""

    root = Path(repository)
    if not root.is_dir():
        raise ValueError(f"repository directory does not exist: {root}")
    relative_root = _normalise_relative_path(third_party_root)
    third_party = root / PurePosixPath(relative_root)
    if not third_party.exists():
        return ()
    if not third_party.is_dir():
        raise ValueError(f"ThirdParty path is not a directory: {third_party}")

    files = (
        path.relative_to(root).as_posix()
        for path in third_party.rglob("*")
        if path.is_file()
    )
    return find_attribution_issues(
        files,
        credits_rows,
        third_party_root=relative_root,
    )


__all__ = [
    "ATTRIBUTION_REQUEST_FIELDS",
    "CREDITS_COLUMNS",
    "LICENCE_NAMES",
    "THIRD_PARTY_ROOT",
    "AttributionIssue",
    "CreditsRow",
    "coerce_credits_row",
    "find_attribution_issues",
    "find_licence_paths",
    "incomplete_credits_fields",
    "is_complete_credits_row",
    "is_licence_name",
    "scan_third_party_attribution",
]
