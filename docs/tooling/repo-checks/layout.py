"""Pure layout/index rules plus the isolated ``Assets/_Project`` builder.

The builder is the only function in this module that writes to disk.  Index
reconciliation is deliberately a pure state transformation: callers can apply
its result to a scratch Git index, while the function itself cannot delete or
alter a working-tree file.
"""

from __future__ import annotations

import os
import re
from collections.abc import Callable, Iterable
from pathlib import Path, PurePosixPath
from typing import NamedTuple, TypeAlias

LAYOUT_FOLDERS = (
    "Assets/_Project/Scenes",
    "Assets/_Project/Scripts/Vehicle",
    "Assets/_Project/Scripts/Orders",
    "Assets/_Project/Scripts/UI",
    "Assets/_Project/Prefabs",
    "Assets/_Project/Materials",
    "Assets/_Project/Audio",
    "Assets/_Project/ThirdParty",
)
LAYOUT_GITKEEPS = tuple(f"{folder}/.gitkeep" for folder in LAYOUT_FOLDERS)
INTERMEDIATE_FOLDERS = ("Assets/_Project", "Assets/_Project/Scripts")
PROJECT_ROOTS = ("Assets", "ProjectSettings", "Packages")
FIXED_ALLOW_LIST = (
    ".gitignore",
    ".gitattributes",
    "README.md",
    "docs/CREDITS.md",
    "docs/BUILD-LOG.md",
    "docs/GIT-RULES.md",
)
PRECONDITION_PATHS = (
    *FIXED_ALLOW_LIST,
    *(f"{root}/" for root in PROJECT_ROOTS),
    *(f"{folder}/" for folder in LAYOUT_FOLDERS),
)

if len(PRECONDITION_PATHS) != 17:  # Keep the R10.7 contract visible in code.
    raise AssertionError("The first-commit precondition set must contain 17 paths")

_PathValue: TypeAlias = str | os.PathLike[str]
_IgnoredInput: TypeAlias = Iterable[_PathValue] | Callable[[str], bool]
_WINDOWS_DRIVE = re.compile(r"^[A-Za-z]:/")


class RemovalRecord(NamedTuple):
    """One path removed from the modelled index, never from the working tree."""

    path: str
    reason: str
    still_on_disk: bool


class ReconciliationResult(NamedTuple):
    """The converged index and deterministic records for every removed path."""

    target_index: frozenset[str]
    removals: tuple[RemovalRecord, ...]

    @property
    def removed_paths(self) -> frozenset[str]:
        """Return the exact set represented by :attr:`removals`."""

        return frozenset(record.path for record in self.removals)


class LayoutBuildResult(NamedTuple):
    """Paths created by one builder run; existing paths are intentionally absent."""

    created_folders: tuple[str, ...]
    created_gitkeeps: tuple[str, ...]


class LayoutBuildError(OSError):
    """Identify the repository-relative path whose creation failed."""

    def __init__(self, path: str, operation: str, error: OSError) -> None:
        self.path = path
        self.operation = operation
        self.original_error = error
        super().__init__(f"Could not {operation} {path}: {error}")


def _normalise_repository_path(path: _PathValue) -> str:
    raw = os.fspath(path)
    if not isinstance(raw, str):
        raise TypeError("repository paths must be strings, not bytes")
    raw = raw.replace("\\", "/")
    if raw.startswith("/") or _WINDOWS_DRIVE.match(raw):
        raise ValueError(f"repository path must be relative: {raw!r}")

    parts: list[str] = []
    for part in raw.split("/"):
        if part in ("", "."):
            continue
        if part == "..":
            raise ValueError(f"repository path must not escape its root: {raw!r}")
        parts.append(part)
    if not parts:
        raise ValueError("repository path must not be empty")
    return "/".join(parts)


def _path_set(paths: Iterable[_PathValue]) -> frozenset[str]:
    return frozenset(_normalise_repository_path(path) for path in paths)


def _ignored_matcher(ignored_paths: _IgnoredInput) -> Callable[[str], bool]:
    if callable(ignored_paths):
        return ignored_paths
    ignored = _path_set(ignored_paths)
    return ignored.__contains__


def is_allow_list_path(path: _PathValue) -> bool:
    """Return whether a file path belongs to the R10.2 first-commit allow-list.

    The three project roots admit descendants, not directory entries (Git does
    not index directories).  Outside those roots only the six fixed files are
    admitted.
    """

    candidate = _normalise_repository_path(path)
    return candidate in FIXED_ALLOW_LIST or any(
        candidate.startswith(f"{root}/") for root in PROJECT_ROOTS
    )


def build_target_index(
    working_tree_paths: Iterable[_PathValue],
    *,
    ignored_paths: _IgnoredInput = (),
) -> frozenset[str]:
    """Expand the allow-list over existing files and exclude every ignored path."""

    ignored = _ignored_matcher(ignored_paths)
    return frozenset(
        path
        for path in _path_set(working_tree_paths)
        if is_allow_list_path(path) and not ignored(path)
    )


def reconcile_index(
    indexed_paths: Iterable[_PathValue],
    working_tree_paths: Iterable[_PathValue],
    *,
    ignored_paths: _IgnoredInput = (),
) -> ReconciliationResult:
    """Converge any modelled index on the non-ignored R10.2 allow-list.

    This function performs no filesystem or Git operation.  Consequently a
    path reported as removed from the index remains on disk exactly when it was
    present in ``working_tree_paths``.  Ignored removals take precedence over
    the more general ``outside_allow_list`` reason.
    """

    indexed = _path_set(indexed_paths)
    working_tree = _path_set(working_tree_paths)
    ignored = _ignored_matcher(ignored_paths)
    target = frozenset(
        path
        for path in working_tree
        if is_allow_list_path(path) and not ignored(path)
    )
    removals = tuple(
        RemovalRecord(
            path=path,
            reason="ignored" if ignored(path) else "outside_allow_list",
            still_on_disk=path in working_tree,
        )
        for path in sorted(indexed - target)
    )
    return ReconciliationResult(target_index=target, removals=removals)


def absent_preconditions(
    present_paths: Iterable[_PathValue],
) -> frozenset[str]:
    """Return exactly the absent members of the fixed 17-path R10.7 set.

    Directory inputs may include or omit a trailing slash.  Returned directory
    names retain the specification's trailing slash for exact reporting.
    """

    present = _path_set(present_paths)
    return frozenset(
        required
        for required in PRECONDITION_PATHS
        if _normalise_repository_path(required) not in present
    )


def absent_preconditions_on_disk(repository: _PathValue) -> frozenset[str]:
    """Read the 17 prerequisites beneath ``repository`` without changing them."""

    root = Path(repository)
    if not root.is_dir():
        raise ValueError(f"repository directory does not exist: {root}")

    missing: set[str] = set()
    for required in PRECONDITION_PATHS:
        destination = root / PurePosixPath(required.rstrip("/"))
        exists = destination.is_dir() if required.endswith("/") else destination.is_file()
        if not exists:
            missing.add(required)
    return frozenset(missing)


def build_asset_layout(repository: _PathValue) -> LayoutBuildResult:
    """Create only the eight required folders and their missing ``.gitkeep`` files.

    Existing directories, all of their contents, and existing ``.gitkeep``
    bytes are left untouched.  New ``.gitkeep`` files are created atomically as
    zero-byte files.  No marker is created in either intermediate directory.
    """

    root = Path(repository)
    if not root.is_dir():
        raise ValueError(f"repository directory does not exist: {root}")

    created_folders: list[str] = []
    created_gitkeeps: list[str] = []

    for folder in LAYOUT_FOLDERS:
        destination = root / PurePosixPath(folder)
        existed = destination.exists()
        try:
            destination.mkdir(parents=True, exist_ok=True)
            if not destination.is_dir():
                raise NotADirectoryError(f"path exists but is not a directory: {destination}")
        except OSError as error:
            raise LayoutBuildError(folder, "create folder", error) from error
        if not existed:
            created_folders.append(folder)

        gitkeep_path = f"{folder}/.gitkeep"
        gitkeep = destination / ".gitkeep"
        if gitkeep.exists():
            if not gitkeep.is_file():
                error = IsADirectoryError(f"path exists but is not a file: {gitkeep}")
                raise LayoutBuildError(gitkeep_path, "create file", error) from error
            continue

        try:
            with gitkeep.open("xb"):
                pass
        except FileExistsError:
            # A concurrent creator is safe only if it produced the required file.
            if not gitkeep.is_file():
                error = IsADirectoryError(f"path exists but is not a file: {gitkeep}")
                raise LayoutBuildError(gitkeep_path, "create file", error) from error
        except OSError as error:
            raise LayoutBuildError(gitkeep_path, "create file", error) from error
        else:
            created_gitkeeps.append(gitkeep_path)

    return LayoutBuildResult(
        created_folders=tuple(created_folders),
        created_gitkeeps=tuple(created_gitkeeps),
    )


# A discoverable synonym for callers phrasing R10.7 as "missing" paths.
missing_preconditions = absent_preconditions


__all__ = [
    "FIXED_ALLOW_LIST",
    "INTERMEDIATE_FOLDERS",
    "LAYOUT_FOLDERS",
    "LAYOUT_GITKEEPS",
    "PRECONDITION_PATHS",
    "PROJECT_ROOTS",
    "LayoutBuildError",
    "LayoutBuildResult",
    "ReconciliationResult",
    "RemovalRecord",
    "absent_preconditions",
    "absent_preconditions_on_disk",
    "build_asset_layout",
    "build_target_index",
    "is_allow_list_path",
    "missing_preconditions",
    "reconcile_index",
]
