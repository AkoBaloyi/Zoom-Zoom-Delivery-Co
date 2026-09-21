"""Shared pytest fixtures for safe repository verification."""

from __future__ import annotations

import os
import shutil
import subprocess
import tempfile
from collections.abc import Iterable
from contextlib import contextmanager
from dataclasses import dataclass
from pathlib import Path
from typing import Iterator

import pytest

from gitplumbing import check_attr, check_ignore, ls_files, rev_parse

_REPO_ROOT = Path(__file__).resolve().parents[3]
_SEED_FILES = (".gitignore", ".gitattributes")


@dataclass(frozen=True)
class BatchedGit:
    """Bind the four read-only plumbing wrappers to one repository."""

    repository: Path

    def check_attr(
        self,
        paths: str | Iterable[str],
        attributes: str | Iterable[str] = ("filter", "diff", "merge", "text"),
    ) -> dict[str, dict[str, str]]:
        return check_attr(self.repository, paths, attributes)

    def check_ignore(self, paths: str | Iterable[str]) -> frozenset[str]:
        return check_ignore(self.repository, paths)

    def ls_files(self, pathspecs: str | Iterable[str] = ()) -> tuple[str, ...]:
        return ls_files(self.repository, pathspecs)

    def rev_parse(self, revisions: str | Iterable[str]) -> dict[str, str]:
        return rev_parse(self.repository, revisions)


def _run_scratch_git(repository: Path, *arguments: str) -> None:
    """Run setup-only Git commands, refusing to target the real repository."""

    root = repository.resolve()
    real_root = _REPO_ROOT.resolve()
    if root == real_root or real_root in root.parents:
        raise RuntimeError(
            "Scratch Git setup must never target the real repository or its children"
        )

    environment = os.environ.copy()
    environment["GIT_TERMINAL_PROMPT"] = "0"
    completed = subprocess.run(
        ("git", *arguments),
        cwd=root,
        env=environment,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        check=False,
    )
    if completed.returncode:
        stderr = completed.stderr.decode("utf-8", errors="replace").strip()
        command = " ".join(("git", *arguments))
        raise RuntimeError(
            f"Scratch command {command} exited {completed.returncode}: "
            f"{stderr or '<no stderr>'}"
        )


@contextmanager
def _scratch_repository(repo_root: Path) -> Iterator[Path]:
    """Create, seed, and unconditionally remove a disposable Git repository."""

    scratch = Path(tempfile.mkdtemp(prefix="zoom-zoom-repo-checks-"))
    try:
        _run_scratch_git(scratch, "init", "--initial-branch=main")
        _run_scratch_git(scratch, "config", "--local", "user.name", "Repo Checks")
        _run_scratch_git(
            scratch,
            "config",
            "--local",
            "user.email",
            "repo-checks@example.invalid",
        )
        for filename in _SEED_FILES:
            source = repo_root / filename
            if not source.is_file():
                raise FileNotFoundError(f"Scratch seed file is missing: {source}")
            shutil.copy2(source, scratch / filename)
        yield scratch
    finally:
        shutil.rmtree(scratch, ignore_errors=False)


@pytest.fixture(scope="session")
def repo_root() -> Path:
    """Return the real repository root without invoking or changing Git."""

    if not (_REPO_ROOT / ".git").exists():
        raise RuntimeError(f"Expected a Git repository at {_REPO_ROOT}")
    for filename in _SEED_FILES:
        if not (_REPO_ROOT / filename).is_file():
            raise RuntimeError(f"Expected repository seed file: {filename}")
    return _REPO_ROOT


@pytest.fixture(scope="session")
def repo_git(repo_root: Path) -> BatchedGit:
    """Provide batched, read-only Git helpers for the real repository."""

    return BatchedGit(repo_root)


@pytest.fixture
def scratch_repo(repo_root: Path) -> Iterator[Path]:
    """Yield a seeded temporary repository and always delete it afterwards."""

    with _scratch_repository(repo_root) as repository:
        yield repository


@pytest.fixture
def scratch_git(scratch_repo: Path) -> BatchedGit:
    """Provide the same read-only plumbing API for a scratch repository."""

    return BatchedGit(scratch_repo)
