#!/usr/bin/env python3
"""Make `python docs/tooling/compile_check.py` runnable inside the git worktree.

WHY THIS IS NEEDED
compile_check.py borrows the assembly references out of Unity's generated
Assembly-CSharp.csproj / Assembly-CSharp-Editor.csproj. Both are gitignored (`*.csproj` in
.gitignore), so a `git worktree` checkout does not have them, and compile_check exits with
"Assembly-CSharp.csproj not found". Copying them in is not enough on its own: 65 (runtime) and
77 (editor) of their <HintPath> entries are RELATIVE paths into `Library\\PackageCache` and
`Library\\ScriptAssemblies`, compile_check resolves a relative hint path against the current
repo root, and the worktree has no Library folder. Every one of those would then fail to
resolve, which dotnet reports as MSB3245 warnings and compile_check counts as warnings.

So this copies both csproj files into the worktree and rewrites just the relative `Library\\...`
hint paths to absolute paths inside the MAIN workspace, which does have Library. Nothing else in
either file is touched, and both destinations are gitignored, so this can never reach a commit.

Run once per worktree, before the first compile_check. Re-run after Unity regenerates the csproj
files in the main workspace (it does that whenever scripts change there).

Usage:
    python .agents/tasks/reverse-and-recovery-2026-10-14/prepare_compile_refs.py
    python ...prepare_compile_refs.py --main <main workspace> --worktree <worktree>
"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path

PROJECTS = ("Assembly-CSharp.csproj", "Assembly-CSharp-Editor.csproj")
MAIN_DEFAULT = Path(__file__).resolve().parents[3]
WORKTREE_DEFAULT = MAIN_DEFAULT / ".worktrees" / "reverse-recovery"


def main() -> int:
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--main", type=Path, default=MAIN_DEFAULT, help="main workspace (has Library/)")
    p.add_argument("--worktree", type=Path, default=WORKTREE_DEFAULT, help="worktree to prepare")
    a = p.parse_args()

    main_root: Path = a.main.resolve()
    worktree: Path = a.worktree.resolve()

    if not (main_root / "Library").is_dir():
        return print(f"no Library folder in {main_root}; open the project in Unity once") or 1
    if not worktree.is_dir():
        return print(f"worktree not found: {worktree}") or 1

    for name in PROJECTS:
        src = main_root / name
        if not src.is_file():
            return print(f"{src} not found; let Unity generate it in the main workspace once") or 1

        text = src.read_text(encoding="utf-8")
        needle = "<HintPath>Library" + "\\"
        replacement = "<HintPath>" + str(main_root) + "\\Library" + "\\"
        rewritten = text.replace(needle, replacement)
        rewrote = text.count(needle)

        dest = worktree / name
        dest.write_text(rewritten, encoding="utf-8")
        print(f"{name}: wrote {dest}, {rewrote} relative Library hint path(s) made absolute, "
              f"{rewritten.count(needle)} left relative")

    print("Done. Now run, from the worktree root: python docs/tooling/compile_check.py")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
