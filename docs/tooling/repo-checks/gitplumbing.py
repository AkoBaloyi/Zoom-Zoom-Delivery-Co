"""Read-only, batched Git plumbing for repository verification.

Every subprocess is selected from a small allowlist. In particular, this module
cannot add, remove, commit, branch, check out, or contact a remote repository.
"""

from __future__ import annotations

import os
import subprocess
from collections.abc import Iterable, Sequence
from pathlib import Path

DEFAULT_ATTRIBUTES = ("filter", "diff", "merge", "text")
_READ_ONLY_SUBCOMMANDS = frozenset(
    {"check-attr", "check-ignore", "ls-files", "rev-parse"}
)


class GitPlumbingError(RuntimeError):
    """Raised when a read-only Git plumbing command cannot be completed."""


def _values(value: str | Iterable[str], *, label: str) -> tuple[str, ...]:
    if isinstance(value, str):
        result = (value,)
    else:
        result = tuple(value)

    for item in result:
        if not isinstance(item, str):
            raise TypeError(f"{label} entries must be strings, got {type(item)!r}")
        if not item:
            raise ValueError(f"{label} entries must not be empty")
        if "\0" in item:
            raise ValueError(f"{label} entries must not contain NUL bytes")
    return result


def _nul_input(values: Sequence[str]) -> bytes:
    return b"".join(value.encode("utf-8") + b"\0" for value in values)


def _nul_output(payload: bytes) -> tuple[str, ...]:
    fields = payload.split(b"\0")
    if fields and fields[-1] == b"":
        fields.pop()
    return tuple(field.decode("utf-8", errors="surrogateescape") for field in fields)


def _run_git(
    repository: str | Path,
    arguments: Sequence[str],
    *,
    stdin: bytes | None = None,
    allowed_returncodes: frozenset[int] = frozenset({0}),
) -> subprocess.CompletedProcess[bytes]:
    if not arguments or arguments[0] not in _READ_ONLY_SUBCOMMANDS:
        subcommand = arguments[0] if arguments else "<missing>"
        raise ValueError(f"Git subcommand is not read-only or allowed: {subcommand}")

    root = Path(repository).resolve()
    if not root.is_dir():
        raise ValueError(f"Repository directory does not exist: {root}")

    environment = os.environ.copy()
    environment["GIT_OPTIONAL_LOCKS"] = "0"
    environment["GIT_TERMINAL_PROMPT"] = "0"

    command = ("git", *arguments)
    completed = subprocess.run(
        command,
        cwd=root,
        env=environment,
        input=stdin,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        check=False,
    )
    if completed.returncode not in allowed_returncodes:
        stderr = completed.stderr.decode("utf-8", errors="replace").strip()
        rendered = " ".join(command)
        raise GitPlumbingError(
            f"{rendered} exited {completed.returncode}: {stderr or '<no stderr>'}"
        )
    return completed


def check_attr(
    repository: str | Path,
    paths: str | Iterable[str],
    attributes: str | Iterable[str] = DEFAULT_ATTRIBUTES,
) -> dict[str, dict[str, str]]:
    """Resolve attributes for all paths with one ``git check-attr --stdin`` call."""

    path_batch = _values(paths, label="path")
    attribute_batch = _values(attributes, label="attribute")
    if not path_batch:
        return {}

    completed = _run_git(
        repository,
        ("check-attr", "-z", "--stdin", *attribute_batch),
        stdin=_nul_input(path_batch),
    )
    fields = _nul_output(completed.stdout)
    if len(fields) % 3:
        raise GitPlumbingError(
            "git check-attr returned a malformed NUL-delimited response"
        )

    result = {path: {} for path in path_batch}
    for offset in range(0, len(fields), 3):
        path, attribute, value = fields[offset : offset + 3]
        result.setdefault(path, {})[attribute] = value

    expected = len(path_batch) * len(attribute_batch)
    if len(fields) // 3 != expected:
        raise GitPlumbingError(
            f"git check-attr returned {len(fields) // 3} values; expected {expected}"
        )
    return result


def check_ignore(
    repository: str | Path, paths: str | Iterable[str]
) -> frozenset[str]:
    """Return ignored paths after one ``git check-ignore --stdin`` call.

    ``--no-index`` is mandatory so tracked paths and generated path strings are
    evaluated against the ignore rules in the same way.
    """

    path_batch = _values(paths, label="path")
    if not path_batch:
        return frozenset()

    completed = _run_git(
        repository,
        ("check-ignore", "--no-index", "-z", "--stdin"),
        stdin=_nul_input(path_batch),
        allowed_returncodes=frozenset({0, 1}),
    )
    return frozenset(_nul_output(completed.stdout))


def ls_files(
    repository: str | Path, pathspecs: str | Iterable[str] = ()
) -> tuple[str, ...]:
    """Return tracked paths using NUL-delimited ``git ls-files`` output."""

    pathspec_batch = _values(pathspecs, label="pathspec")
    arguments: tuple[str, ...] = ("ls-files", "-z")
    if pathspec_batch:
        arguments += ("--", *pathspec_batch)
    completed = _run_git(repository, arguments)
    return _nul_output(completed.stdout)


def rev_parse(
    repository: str | Path, revisions: str | Iterable[str]
) -> dict[str, str]:
    """Resolve one or more simple revisions in a single read-only Git call."""

    revision_batch = _values(revisions, label="revision")
    if not revision_batch:
        return {}
    if any(revision.startswith("-") for revision in revision_batch):
        raise ValueError("revision entries must not begin with '-' characters")

    completed = _run_git(repository, ("rev-parse", *revision_batch))
    resolved = tuple(
        line
        for line in completed.stdout.decode(
            "utf-8", errors="surrogateescape"
        ).splitlines()
        if line
    )
    if len(resolved) != len(revision_batch):
        raise GitPlumbingError(
            f"git rev-parse returned {len(resolved)} revisions; "
            f"expected {len(revision_batch)}"
        )
    return dict(zip(revision_batch, resolved))


__all__ = [
    "DEFAULT_ATTRIBUTES",
    "GitPlumbingError",
    "check_attr",
    "check_ignore",
    "ls_files",
    "rev_parse",
]
