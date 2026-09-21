"""Pure staging-size gate decisions for repository verification.

The advisory and hard-stop gates intentionally use different predicates.  The
hard stop is evaluated first because a blocked file must never be downgraded to
an advisory.
"""

from __future__ import annotations

from enum import Enum
from typing import Final

ADVISORY_SIZE: Final = 10_485_760
BLOCK_SIZE: Final = 104_857_600
LFS_EXT: Final = frozenset(
    {
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
    }
)


class GateDecision(str, Enum):
    """The three possible outcomes of the two staging-size gates."""

    PASS = "PASS"
    ADVISE = "ADVISE"
    BLOCK = "BLOCK"


PASS: Final = GateDecision.PASS
ADVISE: Final = GateDecision.ADVISE
BLOCK: Final = GateDecision.BLOCK


def normalize_extension(extension: str) -> str:
    """Return a comparison-safe extension without changing its meaning.

    Callers may provide either ``"png"`` or ``".PNG"``.  Exactly one leading
    dot is ignored and ASCII extension matching is case-insensitive.  Embedded
    dots and path separators are retained/rejected rather than interpreted as
    a filename, keeping this function conservative when given malformed input.
    """

    if not isinstance(extension, str):
        raise TypeError("extension must be a string")

    normalized = extension.strip()
    if "\0" in normalized or any(separator in normalized for separator in "/\\"):
        raise ValueError("extension must not contain NUL bytes or path separators")
    if normalized.startswith("."):
        normalized = normalized[1:]
    return normalized.casefold()


def classify_gate(
    size_bytes: int,
    extension: str,
    check_attr_filter: str,
) -> GateDecision:
    """Classify one staged file as :data:`PASS`, :data:`ADVISE`, or :data:`BLOCK`.

    ``BLOCK_SIZE`` is exclusive for the hard stop and inclusive for the upper
    edge of the advisory range.  The resolved filter value is compared to the
    literal value Git reports, ``"lfs"``; only extension membership is
    normalized.
    """

    if isinstance(size_bytes, bool) or not isinstance(size_bytes, int):
        raise TypeError("size_bytes must be an integer")
    if size_bytes < 0:
        raise ValueError("size_bytes must not be negative")
    if not isinstance(check_attr_filter, str):
        raise TypeError("check_attr_filter must be a string")

    normalized_extension = normalize_extension(extension)

    if size_bytes > BLOCK_SIZE and check_attr_filter != "lfs":
        return BLOCK
    if (
        ADVISORY_SIZE <= size_bytes <= BLOCK_SIZE
        and normalized_extension not in LFS_EXT
    ):
        return ADVISE
    return PASS


# A concise alias is useful when a property maps generated triples directly.
classify = classify_gate


__all__ = [
    "ADVISORY_SIZE",
    "BLOCK_SIZE",
    "LFS_EXT",
    "GateDecision",
    "PASS",
    "ADVISE",
    "BLOCK",
    "normalize_extension",
    "classify_gate",
    "classify",
]
