"""Compile every runtime script in the project without opening Unity.

Unity's generated Assembly-CSharp.csproj cannot be used for this directly: it enumerates
source files explicitly and goes stale, so newly added scripts are silently left out and a
green build there means nothing. This borrows that project's assembly references, which are
correct, and pairs them with a fresh glob of Assets so nothing can be missed.

CS0649 and CS0169 are suppressed. Those fire on [SerializeField] private fields, which
Unity assigns through the serialiser and the plain C# compiler cannot see, so outside Unity
they are false positives on almost every component in the project.

Usage:
    python docs/tooling/compile_check.py
Exit status is non-zero if anything fails to compile.
"""
import glob
import os
import re
import subprocess
import sys
from pathlib import Path
from xml.sax.saxutils import escape

SOURCE_PROJECT = "Assembly-CSharp.csproj"
# Editor-only scripts reference UnityEditor, which is not in the runtime reference set.
SKIP_SEGMENTS = (f"{os.sep}Editor{os.sep}",)


def main():
    root = Path.cwd()
    if not (root / SOURCE_PROJECT).exists():
        sys.exit(f"{SOURCE_PROJECT} not found. Run from the repository root, and let Unity "
                 "generate it at least once.")

    src = (root / SOURCE_PROJECT).read_text(encoding="utf-8")
    references = []
    for block in re.findall(r"<Reference Include=.*?</Reference>", src, re.S):
        block = re.sub(
            r"<HintPath>(?!\w:)([^<]+)</HintPath>",
            lambda m: f"<HintPath>{escape(str((root / m.group(1)).resolve()))}</HintPath>",
            block,
        )
        references.append(block)
    if not references:
        sys.exit(f"No assembly references found in {SOURCE_PROJECT}.")

    files = []
    for path in glob.glob("Assets/**/*.cs", recursive=True):
        native = os.sep + path.replace("/", os.sep)
        if any(segment in native for segment in SKIP_SEGMENTS):
            continue
        files.append(path)
    if not files:
        sys.exit("No runtime scripts found under Assets.")

    team = [f for f in files if "TextMesh Pro" not in f]
    print(f"{len(references)} assembly references, {len(files)} runtime scripts "
          f"({len(team)} excluding imported TextMesh Pro samples)")

    compiles = "\n".join(
        f'    <Compile Include="{escape(str((root / f).resolve()))}" />' for f in files)
    project = f"""<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.1</TargetFramework>
    <LangVersion>9.0</LangVersion>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <AssemblyName>ZZCompileCheck</AssemblyName>
    <NoWarn>CS0649;CS0169</NoWarn>
  </PropertyGroup>
  <ItemGroup>
{compiles}
  </ItemGroup>
  <ItemGroup>
{chr(10).join('    ' + r for r in references)}
  </ItemGroup>
</Project>
"""
    out = Path(os.environ.get("TEMP", "/tmp")) / "zz-compile-check"
    out.mkdir(parents=True, exist_ok=True)
    project_file = out / "zz-compile-check.csproj"
    project_file.write_text(project, encoding="utf-8")

    result = subprocess.run(
        ["dotnet", "build", str(project_file), "-v", "q", "--nologo"],
        capture_output=True, text=True,
    )
    lines = [l.strip() for l in (result.stdout + result.stderr).splitlines() if l.strip()]
    errors = sorted({l for l in lines if ": error " in l})
    warnings = sorted({l for l in lines if ": warning " in l})

    print(f"errors {len(errors)}, warnings {len(warnings)}")
    for line in errors[:40]:
        print("  error  ", line)
    for line in warnings[:20]:
        print("  warning", line)

    if result.returncode != 0 or errors:
        return 1
    print("Compiles clean.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
