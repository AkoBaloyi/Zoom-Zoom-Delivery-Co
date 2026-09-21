# Repository verification harness

This harness verifies the version-control setup for **Zoom Zoom Delivery Co**. It is tracked as submission evidence on `feature/vehicle-camera`, outside both the root commit and `Assets/`, so Unity does not import or compile it.

## Install

Run from the repository root. Dependencies are installed in the current user's Python site rather than in the Unity project:

```powershell
pip install --user pytest hypothesis
```

## Run

Use this command for every harness run:

```powershell
python -B -m pytest -p no:cacheprovider docs/tooling/repo-checks
```

`-B` prevents Python bytecode caches and `-p no:cacheprovider` prevents pytest's cache directory. Do not add an ignore rule for either cache: the command keeps them off disk.

## Safety model

- Checks against the real repository are read-only. The plumbing layer permits only `git check-attr`, `git check-ignore --no-index`, `git ls-files`, and `git rev-parse`.
- Generated path lists are sent to `git check-attr --stdin` or `git check-ignore --stdin` as one NUL-delimited batch per example. The paths do not need to exist.
- Any later test that stages, commits, creates branches, or builds folders must use the `scratch_repo` fixture. Each scratch repository is created with `tempfile.mkdtemp()`, receives copies of the real `.gitignore` and `.gitattributes`, and is deleted in fixture teardown.
- The harness performs no GitHub, remote, Unity Editor, or other network operation.

## Module map

Task 13.1 creates the scaffold modules marked **present**. The remaining entries describe the modules reserved for later spec tasks; they are intentionally not implemented by this task.

| Path | Status | What it proves or supports |
| --- | --- | --- |
| `README.md` | present | Records the only supported install/run commands, cache policy, and real-repository safety boundary. |
| `conftest.py` | present | Locates the repository root, provides batched read-only helpers, and proves mutating tests can be isolated in seeded scratch repositories that are always removed. |
| `gitplumbing.py` | present | Provides the four read-only Git queries used to prove ignore coverage, attribute routing, tracked-path membership, and ref identity without changing the real repository. |
| `generators.py` | present | Defines every input domain and boundary cluster from the design table for later Hypothesis properties. |
| `gates.py` | later task | Will prove the 10 MiB advisory and 100 MiB hard-stop decisions are distinct and exact. |
| `narration.py` | later task | Will prove narration lines satisfy numbering, naming, ordering, failure, and 30–140 character rules. |
| `layout.py` | later task | Will prove the eight-folder builder is minimal/idempotent and index reconciliation converges without deleting working-tree files. |
| `rights.py` | later task | Will prove licence-name matching is exact and third-party files require complete attribution. |
| `tags.py` | later task | Will prove checkpoint tags are well formed and uniquely numbered for each date. |
| `fixtures/` | later task | Will preserve the actual narration and presented guide text for contract comparison. |
| `test_properties.py` | later task | Will hold one Hypothesis test for each of the thirteen named correctness properties. |
| `test_documents.py` | later task | Will prove the README, credits, build log, Git rules, and captured guides satisfy their literal contracts. |
| `test_smoke.py` | later task | Will prove the installed LFS version, merge-driver registration, editor-setting verdicts, and zero-remote state. |
| `test_error_paths.py` | later task | Will prove each specified failure reports the right evidence while preserving repository state. |
