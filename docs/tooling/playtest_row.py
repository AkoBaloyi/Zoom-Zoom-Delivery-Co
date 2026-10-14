#!/usr/bin/env python3
"""Turn a shift_log.csv row into a playtest_sessions.csv row.

The measured columns come from the log the game writes. The asked columns stay blank, because
they are the tester's words and nobody else can supply them. This script exists so that the
thirty seconds after a tester stands up is spent asking questions, not retyping numbers.

Usage, after a tester has played one shift:

    python docs/tooling/playtest_row.py --session S01 --tester T01 \
        --experience sometimes --build $(git rev-parse --short HEAD) --play-minutes 6

That prints the row. Add --append to write it into the sheet. Then open the sheet and type the
tester's answers into the q_ columns, which are the only cells left blank.

The script reads the sheet's own header rather than carrying its own copy of the column list, so
it cannot drift out of step if a column is added.
"""
from __future__ import annotations

import argparse
import csv
import datetime as _dt
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
SHEET = REPO / "docs" / "playtesting" / "playtest_sessions.csv"
DEFAULT_LOG = REPO / "PlaytestLogs" / "shift_log.csv"

# Sheet column <- log column. Only these are machine-measured; everything else is asked or typed.
MEASURED = {
    "delivered": "delivered",
    "late": "late",
    "value": "value",
    "boost_activations": "boost_activations",
    "boost_seconds": "boost_seconds",
    "boost_earned": "boost_earned",
    "handbrake_seconds": "handbrake_seconds",
    "wall_hits": "wall_hits",
    "flips": "flips",
    "time_above_supersonic_s": "time_above_supersonic_s",
    "longest_recovery_s": "longest_recovery_s",
}

SESSION_TYPES = ("human", "machine-log", "ai-qa", "mixed-ai-user")
EXPERIENCE = ("never", "sometimes", "often")
# From QUESTIONS.md. Kept here only to validate --variant, so a typo does not silently create a
# variant that no other row shares and therefore cannot be compared with anything.
VARIANTS = (
    "baseline",
    "boost-off",
    "boost-regen",
    "flip-off",
    "grippy",
    "loose",
    "camera-far",
)


def read_sheet_header(path: Path) -> list[str]:
    if not path.exists():
        sys.exit(f"sheet not found: {path}")
    with path.open(newline="", encoding="utf-8") as f:
        header = next(csv.reader(f), None)
    if not header:
        sys.exit(f"sheet has no header row: {path}")
    return header


def read_log_rows(path: Path) -> list[dict[str, str]]:
    if not path.exists():
        sys.exit(
            f"shift log not found: {path}\n"
            "PlaytestLogger writes it at shift end. If it never ran, leave the measured cells "
            "blank rather than estimating them."
        )
    # utf-8-sig, not utf-8: PlaytestLogger writes the log with a BOM, and plain utf-8 decoding
    # names the first column "\ufefftimestamp", so every row looks like it has no timestamp and
    # the whole file silently reads as empty.
    with path.open(newline="", encoding="utf-8-sig") as f:
        rows = [r for r in csv.DictReader(f) if r.get("timestamp")]
    if not rows:
        sys.exit(f"shift log has a header but no rows: {path}")
    return rows


def main() -> int:
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--log", type=Path, default=DEFAULT_LOG, help=f"shift log (default {DEFAULT_LOG})")
    p.add_argument("--row", type=int, default=-1, help="which log row; default -1, the last one")
    p.add_argument("--session", help="session_id, e.g. S01; one per sitting")
    p.add_argument("--tester", help="tester_id, e.g. T01; never a name")
    p.add_argument("--experience", choices=EXPERIENCE, help="answer to the before-play question")
    p.add_argument("--variant", default="baseline", help=f"one of {', '.join(VARIANTS)}")
    p.add_argument("--build", help="commit hash or tag of the build that was played")
    p.add_argument("--play-minutes", type=float, help="wall clock including restarts")
    p.add_argument("--type", dest="stype", default="human", choices=SESSION_TYPES)
    p.add_argument("--notes", default="", help="observer_notes; quote it")
    p.add_argument("--append", action="store_true", help="write the row into the sheet")
    p.add_argument("--list", action="store_true", help="list the log rows and exit")
    a = p.parse_args()

    log_rows = read_log_rows(a.log)

    if a.list:
        for i, r in enumerate(log_rows):
            print(
                f"[{i}] {r['timestamp']}  scene={r.get('scene','?')}  "
                f"play_s={r.get('play_seconds','?')}  shift_s={r.get('shift_seconds','?')}  "
                f"delivered={r.get('delivered','?')} late={r.get('late','?')}"
            )
        return 0

    try:
        log = log_rows[a.row]
    except IndexError:
        return print(f"no log row at index {a.row}; there are {len(log_rows)}") or 1

    if a.variant not in VARIANTS:
        print(f"warning: variant {a.variant!r} is not in QUESTIONS.md: {', '.join(VARIANTS)}", file=sys.stderr)

    # The README's rule, enforced rather than described: a row that cannot be tied to a version
    # cannot be compared with any other row, so a human session without a build is not evidence.
    if a.stype == "human":
        missing = [n for n, v in (("--session", a.session), ("--tester", a.tester), ("--build", a.build)) if not v]
        if missing:
            return print(
                f"refusing to build a human row without {', '.join(missing)}.\n"
                "A row without a build hash cannot be tied to a version, and a row without a "
                "tester id cannot be followed across sessions. Use --type ai-qa or --type "
                "machine-log for a run that had no participant."
            ) or 1

    shift_s = log.get("shift_seconds", "")
    play_s = log.get("play_seconds", "")
    warnings = []
    try:
        if float(shift_s) == 0:
            warnings.append(
                f"shift_seconds is 0 against {play_s} logged play_seconds, so this is an exit "
                "record rather than a completed shift; its totals are not comparable"
            )
        elif float(play_s) < float(shift_s) * 0.9:
            warnings.append(
                f"incomplete shift: {play_s} logged play_seconds against a {shift_s}-second shift"
            )
    except (TypeError, ValueError):
        warnings.append("could not read shift_seconds/play_seconds from the log")

    stamp = log["timestamp"]
    date = stamp.split(" ")[0]
    try:
        _dt.date.fromisoformat(date)
    except ValueError:
        return print(f"log timestamp {stamp!r} does not start with an ISO date") or 1

    notes = a.notes
    provenance = (
        f"Source: {a.log.as_posix()} row {stamp}, scene {log.get('scene','?')}. "
        f"Logged play_seconds={play_s}; shift_seconds={shift_s}."
    )
    notes = f"{notes} {provenance}".strip() if notes else provenance
    for w in warnings:
        notes += f" {w[0].upper()}{w[1:]}."
    if a.stype != "human":
        notes += " Not a human-research session; measured columns only, no participant answers."

    header = read_sheet_header(SHEET)
    filled = {
        "session_id": a.session or f"LOG-{stamp.replace('-','').replace(':','').replace(' ','-')}",
        "session_type": a.stype,
        "date": date,
        "build": a.build or "",
        "variant": a.variant,
        "tester_id": a.tester or "",
        "tester_racing_experience": a.experience or "",
        "play_minutes": "" if a.play_minutes is None else f"{a.play_minutes:g}",
        "observer_notes": notes,
    }
    for sheet_col, log_col in MEASURED.items():
        filled[sheet_col] = log.get(log_col, "")

    unknown = [k for k in filled if k not in header]
    if unknown:
        return print(f"sheet header has no column(s) {unknown}; update this script") or 1

    row = [filled.get(c, "") for c in header]

    blank_q = [c for c in header if c.startswith("q_") and not filled.get(c)]

    buf = csv.writer(sys.stdout, quoting=csv.QUOTE_MINIMAL, lineterminator="\n")
    if a.append:
        with SHEET.open("a", newline="", encoding="utf-8") as f:
            csv.writer(f, quoting=csv.QUOTE_MINIMAL, lineterminator="\n").writerow(row)
        print(f"appended to {SHEET.relative_to(REPO).as_posix()}")
    else:
        buf.writerow(row)

    print(file=sys.stderr)
    for w in warnings:
        print(f"warning: {w}", file=sys.stderr)
    if a.stype == "human":
        print(
            f"{len(blank_q)} asked columns are blank and must be typed from the tester's answers:\n  "
            + "\n  ".join(blank_q),
            file=sys.stderr,
        )
        print("Read docs/playtesting/QUESTIONS.md as written. Do not paraphrase.", file=sys.stderr)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
