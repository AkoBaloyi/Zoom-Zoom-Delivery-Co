"""Checkpoint-build tag construction and semantic validation."""

from __future__ import annotations

import re
from dataclasses import dataclass
from datetime import date, datetime

CHECKPOINT_TAG_GRAMMAR = (
    r"^build-\d{4}-\d{2}-\d{2}(-[2-9]|-[1-9]\d+)?$"
)
CHECKPOINT_TAG_PATTERN = re.compile(CHECKPOINT_TAG_GRAMMAR, re.ASCII)
_DATE_PATTERN = re.compile(r"^\d{4}-\d{2}-\d{2}$", re.ASCII)


@dataclass(frozen=True, slots=True)
class ParsedCheckpointTag:
    """The calendar date and one-based build ordinal encoded by a tag."""

    build_date: date
    ordinal: int


def _coerce_date(value: date | str) -> date:
    if isinstance(value, datetime):
        raise TypeError("checkpoint build date must be a date, not a datetime")
    if isinstance(value, date):
        return value
    if not isinstance(value, str):
        raise TypeError("checkpoint build date must be a date or YYYY-MM-DD string")
    if _DATE_PATTERN.fullmatch(value) is None:
        raise ValueError(f"checkpoint build date must use YYYY-MM-DD: {value!r}")
    try:
        parsed = date.fromisoformat(value)
    except ValueError as error:
        raise ValueError(f"invalid checkpoint build date: {value!r}") from error
    if parsed.isoformat() != value:
        raise ValueError(f"checkpoint build date must use YYYY-MM-DD: {value!r}")
    return parsed


def _validate_ordinal(ordinal: int) -> int:
    if type(ordinal) is not int:
        raise TypeError("checkpoint build ordinal must be an integer")
    if ordinal < 1:
        raise ValueError("checkpoint build ordinal must be at least 1")
    return ordinal


def checkpoint_tag(build_date: date | str, ordinal: int = 1) -> str:
    """Return ``build-YYYY-MM-DD`` and add ``-k`` exactly when ``k > 1``."""

    day = _coerce_date(build_date)
    build_ordinal = _validate_ordinal(ordinal)
    base = f"build-{day.isoformat()}"
    return base if build_ordinal == 1 else f"{base}-{build_ordinal}"


def parse_checkpoint_tag(value: str) -> ParsedCheckpointTag:
    """Parse a grammatically and semantically valid checkpoint tag.

    Besides the regex grammar, this rejects impossible calendar dates and any
    suffix representing zero, one, a negative value, or a leading-zero value.
    """

    if not isinstance(value, str):
        raise TypeError("checkpoint tag must be a string")
    match = CHECKPOINT_TAG_PATTERN.fullmatch(value)
    if match is None:
        raise ValueError(f"invalid checkpoint tag grammar: {value!r}")

    day = _coerce_date(value[len("build-") : len("build-YYYY-MM-DD")])
    suffix = match.group(1)
    ordinal = 1 if suffix is None else int(suffix[1:])
    _validate_ordinal(ordinal)
    return ParsedCheckpointTag(build_date=day, ordinal=ordinal)


def is_valid_checkpoint_tag(value: object) -> bool:
    """Return whether ``value`` satisfies the grammar, date, and ordinal rules."""

    if not isinstance(value, str):
        return False
    try:
        parse_checkpoint_tag(value)
    except (TypeError, ValueError):
        return False
    return True


__all__ = [
    "CHECKPOINT_TAG_GRAMMAR",
    "CHECKPOINT_TAG_PATTERN",
    "ParsedCheckpointTag",
    "checkpoint_tag",
    "is_valid_checkpoint_tag",
    "parse_checkpoint_tag",
]
