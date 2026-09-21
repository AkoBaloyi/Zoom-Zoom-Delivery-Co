"""Hypothesis input strategies from the verification-harness design table."""

from __future__ import annotations

import string
from typing import NamedTuple

from hypothesis import strategies as st
from hypothesis.strategies import SearchStrategy

GENERATED_ROOTS = (
    "Library",
    "Temp",
    "Obj",
    "Build",
    "Builds",
    "Logs",
    "UserSettings",
)
GENERATED_EXT = ("csproj", "sln", "slnx", "unityproj", "userprefs", "pidb")
AI_DIRECTORY_ARTIFACTS = (".kiro", ".cursor", ".claude", ".windsurf", ".continue")
AI_FILE_ARTIFACTS = ("CLAUDE.md", "AGENTS.md")
PLANNING_DIRECTORIES = ("planning", "notes")
PROJECT_ROOTS = ("Assets", "ProjectSettings", "Packages")
LFS_EXT = (
    "fbx",
    "obj",
    "blend",
    "png",
    "jpg",
    "psd",
    "tga",
    "wav",
    "mp3",
    "ogg",
    "ttf",
    "otf",
    "cubemap",
    "unitypackage",
)
TEXT_EXT = ("cs", "shader", "meta", "asset", "prefab", "unity")
FEATURE_BRANCHES = (
    "feature/vehicle-camera",
    "feature/orders-cargo",
    "feature/greybox-ui",
)
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
PRECONDITION_PATHS = (
    ".gitignore",
    ".gitattributes",
    "README.md",
    "docs/CREDITS.md",
    "docs/BUILD-LOG.md",
    "docs/GIT-RULES.md",
    "Assets/",
    "ProjectSettings/",
    "Packages/",
    *tuple(f"{folder}/" for folder in LAYOUT_FOLDERS),
)
LICENCE_NAMES = (
    "LICENSE",
    "LICENCE",
    "LICENSE.md",
    "LICENCE.md",
    "LICENSE.txt",
    "LICENCE.txt",
    "COPYING",
    "UNLICENSE",
)
LICENCE_NEAR_MISSES = ("LICENSE-NOTES.md", "licences/", "MY-LICENSE")
ADVISORY_SIZE = 10_485_760
BLOCK_SIZE = 104_857_600


class StagedFile(NamedTuple):
    """One generated input triple for the two staging-size gates."""

    size_bytes: int
    extension: str
    check_attr_filter: str


class BranchState(NamedTuple):
    """A main tip and zero or more pre-existing feature-branch tips."""

    main_tip: str | None
    feature_tips: tuple[tuple[str, str], ...]


class NarrationInput(NamedTuple):
    """All generated fields consumed by later narration properties."""

    step: int
    action_phrase: str
    paths_or_patterns: tuple[str, ...]
    purpose_clause: str
    removal_count: int
    error_text: str


_SEGMENT = st.builds(
    lambda rest: "repo_" + rest,
    st.text(
        alphabet=string.ascii_letters + string.digits + "_-",
        min_size=0,
        max_size=12,
    ),
)
_EXTENSION_SAFE_PROJECT = st.sampled_from(
    ("cs", "shader", "asset", "prefab", "unity", "json", "inputactions")
)
_SHA = st.text(alphabet="0123456789abcdef", min_size=40, max_size=40)
_WORD = st.text(alphabet=string.ascii_lowercase, min_size=3, max_size=10)
_ACTION_PHRASE = st.lists(_WORD, min_size=2, max_size=6).map(" ".join)
_PURPOSE_CLAUSE = st.lists(_WORD, min_size=4, max_size=8).map(
    lambda words: "so " + " ".join(words)
)
_ERROR_TEXT = st.text(
    alphabet=string.ascii_letters
    + string.digits
    + string.punctuation
    + " \t\r\n",
    min_size=0,
    max_size=4096,
)


@st.composite
def _path_with_extension(
    draw,
    extension_strategy: SearchStrategy[str],
    *,
    min_depth: int = 1,
    max_depth: int = 5,
) -> str:
    depth = draw(st.integers(min_value=min_depth, max_value=max_depth))
    directories = draw(
        st.lists(_SEGMENT, min_size=depth - 1, max_size=depth - 1)
    )
    basename = draw(_SEGMENT)
    extension = draw(extension_strategy)
    return "/".join((*directories, f"{basename}.{extension}"))


@st.composite
def _generated_root_path(draw) -> str:
    root = draw(st.sampled_from(GENERATED_ROOTS))
    spelling = draw(st.sampled_from((root, root.lower())))
    descendants = draw(st.lists(_SEGMENT, min_size=1, max_size=4))
    return "/".join((spelling, *descendants))


@st.composite
def _artifact_path(draw) -> str:
    kind = draw(
        st.sampled_from(
            ("ai-directory", "aider", "ai-file", "planning-directory", "plan-file")
        )
    )
    if kind == "ai-directory":
        root = draw(st.sampled_from(AI_DIRECTORY_ARTIFACTS))
        descendants = draw(st.lists(_SEGMENT, min_size=1, max_size=4))
        return "/".join((root, *descendants))
    if kind == "aider":
        prefix = draw(st.lists(_SEGMENT, min_size=0, max_size=3))
        suffix = draw(
            st.text(
                alphabet=string.ascii_letters + string.digits + "._-",
                min_size=0,
                max_size=12,
            )
        )
        return "/".join((*prefix, f".aider{suffix}"))
    if kind == "ai-file":
        prefix = draw(st.lists(_SEGMENT, min_size=0, max_size=3))
        filename = draw(st.sampled_from(AI_FILE_ARTIFACTS))
        return "/".join((*prefix, filename))
    if kind == "planning-directory":
        root = draw(st.sampled_from(PLANNING_DIRECTORIES))
        descendants = draw(st.lists(_SEGMENT, min_size=1, max_size=4))
        return "/".join((root, *descendants))

    prefix = draw(st.lists(_SEGMENT, min_size=0, max_size=4))
    basename = draw(_SEGMENT)
    return "/".join((*prefix, f"{basename}.plan.md"))


@st.composite
def ignored_path(draw) -> str:
    """Generate every excluded path family from Requirements 1 and 2."""

    return draw(
        st.one_of(
            _generated_root_path(),
            _path_with_extension(st.sampled_from(GENERATED_EXT)),
            _artifact_path(),
        )
    )


@st.composite
def project_path(draw) -> str:
    """Generate protected project paths and their optional ``.meta`` companions."""

    root = draw(st.sampled_from(PROJECT_ROOTS))
    directories = draw(st.lists(_SEGMENT, min_size=0, max_size=3))
    basename = draw(_SEGMENT)
    extension = draw(_EXTENSION_SAFE_PROJECT)
    path = "/".join((root, *directories, f"{basename}.{extension}"))
    if draw(st.booleans()):
        path += ".meta"
    return path


@st.composite
def lfs_path(draw) -> str:
    """Generate binary-extension paths at random depth in lower or upper case."""

    extension = draw(st.sampled_from(LFS_EXT))
    if draw(st.booleans()):
        extension = extension.upper()
    return draw(_path_with_extension(st.just(extension)))


def text_path() -> SearchStrategy[str]:
    """Generate paths over all six text extensions at random depth."""

    return _path_with_extension(st.sampled_from(TEXT_EXT))


_BOUNDARY_SIZES = st.sampled_from(
    (0, ADVISORY_SIZE - 1, ADVISORY_SIZE, BLOCK_SIZE, BLOCK_SIZE + 1)
)
_BETWEEN_SIZES = st.one_of(
    st.integers(min_value=1, max_value=ADVISORY_SIZE - 2),
    st.integers(min_value=ADVISORY_SIZE + 1, max_value=BLOCK_SIZE - 1),
    st.integers(min_value=BLOCK_SIZE + 2, max_value=BLOCK_SIZE * 2),
)
_STAGED_EXTENSION = st.one_of(
    st.sampled_from(LFS_EXT),
    st.sampled_from(TEXT_EXT),
    st.sampled_from(("bin", "dat", "json", "zip")),
)
_STAGED_FILTER = st.sampled_from(("lfs", "unspecified", "custom"))


def staged_file() -> SearchStrategy[StagedFile]:
    """Generate gate triples clustered exactly around both byte thresholds."""

    return st.builds(
        StagedFile,
        st.one_of(_BOUNDARY_SIZES, _BETWEEN_SIZES),
        _STAGED_EXTENSION,
        _STAGED_FILTER,
    )


_ALLOW_LIST_FIXED = (
    ".gitignore",
    ".gitattributes",
    "README.md",
    "docs/CREDITS.md",
    "docs/BUILD-LOG.md",
    "docs/GIT-RULES.md",
    *tuple(f"{folder}/.gitkeep" for folder in LAYOUT_FOLDERS),
)


@st.composite
def _stray_path(draw) -> str:
    if draw(st.booleans()):
        return ".vscode/settings.json"
    directory = draw(_SEGMENT)
    basename = draw(_SEGMENT)
    extension = draw(st.sampled_from(("cfg", "dat", "md", "bak")))
    return f"{directory}/{basename}.{extension}"


@st.composite
def index_state(draw) -> frozenset[str]:
    """Generate arbitrary indexed mixes for later reconciliation properties."""

    allow_list = draw(
        st.sets(
            st.one_of(st.sampled_from(_ALLOW_LIST_FIXED), project_path()),
            min_size=0,
            max_size=12,
        )
    )
    generated = draw(
        st.sets(
            st.one_of(
                _generated_root_path(),
                _path_with_extension(st.sampled_from(GENERATED_EXT)),
            ),
            min_size=0,
            max_size=8,
        )
    )
    artifacts = draw(st.sets(_artifact_path(), min_size=0, max_size=8))
    strays = draw(st.sets(_stray_path(), min_size=0, max_size=8))
    return frozenset((*allow_list, *generated, *artifacts, *strays))


def precondition_state() -> SearchStrategy[frozenset[str]]:
    """Generate every possible absent subset of the 17 prerequisites."""

    return st.sets(
        st.sampled_from(PRECONDITION_PATHS),
        min_size=0,
        max_size=len(PRECONDITION_PATHS),
    ).map(frozenset)


@st.composite
def _committed_branch_state(draw) -> BranchState:
    main_tip = draw(_SHA)
    existing = draw(
        st.sets(
            st.sampled_from(FEATURE_BRANCHES),
            min_size=0,
            max_size=len(FEATURE_BRANCHES),
        )
    )
    tips = tuple(sorted((branch, draw(_SHA)) for branch in existing))
    return BranchState(main_tip=main_tip, feature_tips=tips)


def branch_state() -> SearchStrategy[BranchState]:
    """Generate committed-main states plus the explicit empty-main case."""

    return st.one_of(
        st.just(BranchState(main_tip=None, feature_tips=())),
        _committed_branch_state(),
    )


@st.composite
def layout_state(draw) -> dict[str, bytes]:
    """Generate pre-existing layout subsets with non-empty sentinel contents."""

    existing = draw(
        st.sets(
            st.sampled_from(LAYOUT_FOLDERS),
            min_size=0,
            max_size=len(LAYOUT_FOLDERS),
        )
    )
    return {
        folder: draw(st.binary(min_size=1, max_size=64))
        for folder in sorted(existing)
    }


_SHORT_PATH = st.one_of(
    _path_with_extension(st.sampled_from(("cs", "asset", "md", "png")), max_depth=3),
    st.sampled_from(
        (
            ".kiro/",
            ".cursor/",
            "CLAUDE.md",
            "planning/",
            "notes/",
            "*.plan.md",
        )
    ),
)


@st.composite
def narration_input(draw) -> NarrationInput:
    """Generate the complete narration formatter/error input domain."""

    return NarrationInput(
        step=draw(st.integers(min_value=1, max_value=13)),
        action_phrase=draw(_ACTION_PHRASE),
        paths_or_patterns=tuple(
            draw(st.lists(_SHORT_PATH, min_size=0, max_size=20))
        ),
        purpose_clause=draw(_PURPOSE_CLAUSE),
        removal_count=draw(st.integers(min_value=0, max_value=999)),
        error_text=draw(_ERROR_TEXT),
    )


def _mixed_case(value: str) -> SearchStrategy[str]:
    characters = tuple(
        st.sampled_from((character.lower(), character.upper()))
        if character.isalpha()
        else st.just(character)
        for character in value
    )
    return st.tuples(*characters).map("".join)


def licence_name() -> SearchStrategy[str]:
    """Generate all eight case-insensitive licence names and named near misses."""

    exact = st.sampled_from(LICENCE_NAMES).flatmap(_mixed_case)
    near_miss = st.sampled_from(LICENCE_NEAR_MISSES).flatmap(_mixed_case)
    return st.one_of(exact, near_miss)


__all__ = [
    "ADVISORY_SIZE",
    "AI_DIRECTORY_ARTIFACTS",
    "AI_FILE_ARTIFACTS",
    "BLOCK_SIZE",
    "BranchState",
    "FEATURE_BRANCHES",
    "GENERATED_EXT",
    "GENERATED_ROOTS",
    "LAYOUT_FOLDERS",
    "LFS_EXT",
    "LICENCE_NAMES",
    "LICENCE_NEAR_MISSES",
    "NarrationInput",
    "PLANNING_DIRECTORIES",
    "PRECONDITION_PATHS",
    "PROJECT_ROOTS",
    "StagedFile",
    "TEXT_EXT",
    "branch_state",
    "ignored_path",
    "index_state",
    "layout_state",
    "lfs_path",
    "licence_name",
    "narration_input",
    "precondition_state",
    "project_path",
    "staged_file",
    "text_path",
]
