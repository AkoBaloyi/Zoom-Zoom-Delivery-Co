"""Compile every script in the project without opening Unity: runtime first, then editor.

Unity's generated csproj files cannot be used for this directly: they enumerate source
files explicitly and go stale, so newly added scripts are silently left out and a green
build there means nothing. This borrows their assembly references, which are correct, and
pairs them with a fresh glob of Assets so nothing can be missed.

Two passes, because Unity builds two assemblies:
  1. Runtime: every .cs under Assets except Editor folders, against Assembly-CSharp's
     references. This is what ships.
  2. Editor: every .cs inside an Editor folder, against Assembly-CSharp-Editor's references
     (which include UnityEditor.dll) plus the runtime assembly from pass 1, since editor
     tools reference runtime types.

CS0649 and CS0169 are suppressed. Those fire on [SerializeField] private fields, which
Unity assigns through the serialiser and the plain C# compiler cannot see, so outside Unity
they are false positives on almost every component in the project.

Usage:
    python docs/tooling/compile_check.py
Exit status is non-zero if either pass fails.
"""
import glob
import os
import re
import subprocess
import sys
from pathlib import Path
from xml.sax.saxutils import escape

RUNTIME_PROJECT = "Assembly-CSharp.csproj"
EDITOR_PROJECT = "Assembly-CSharp-Editor.csproj"
EDITOR_SEGMENT = f"{os.sep}Editor{os.sep}"


def references_from(csproj: Path, root: Path):
    src = csproj.read_text(encoding="utf-8")
    refs = []
    for block in re.findall(r"<Reference Include=.*?</Reference>", src, re.S):
        block = re.sub(
            r"<HintPath>(?!\w:)([^<]+)</HintPath>",
            lambda m: f"<HintPath>{escape(str((root / m.group(1)).resolve()))}</HintPath>",
            block,
        )
        refs.append(block)
    return refs


def build(name: str, files, references, extra_refs, out_dir: Path, root: Path):
    compiles = "\n".join(
        f'    <Compile Include="{escape(str((root / f).resolve()))}" />' for f in files)
    extras = "\n".join(
        f'    <Reference Include="{Path(p).stem}"><HintPath>{escape(str(p))}</HintPath></Reference>'
        for p in extra_refs)
    project = f"""<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.1</TargetFramework>
    <LangVersion>9.0</LangVersion>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <AssemblyName>{name}</AssemblyName>
    <NoWarn>CS0649;CS0169</NoWarn>
    <OutputPath>bin</OutputPath>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
  </PropertyGroup>
  <ItemGroup>
{compiles}
  </ItemGroup>
  <ItemGroup>
{chr(10).join('    ' + r for r in references)}
{extras}
  </ItemGroup>
</Project>
"""
    proj_dir = out_dir / name
    proj_dir.mkdir(parents=True, exist_ok=True)
    project_file = proj_dir / f"{name}.csproj"
    project_file.write_text(project, encoding="utf-8")

    result = subprocess.run(
        ["dotnet", "build", str(project_file), "-v", "q", "--nologo"],
        capture_output=True, text=True,
    )
    lines = [l.strip() for l in (result.stdout + result.stderr).splitlines() if l.strip()]
    errors = sorted({l for l in lines if ": error " in l})
    warnings = sorted({l for l in lines if ": warning " in l})

    print(f"  errors {len(errors)}, warnings {len(warnings)}")
    for line in errors[:40]:
        print("    error  ", line)
    for line in warnings[:20]:
        print("    warning", line)

    ok = result.returncode == 0 and not errors
    return ok, proj_dir / "bin" / f"{name}.dll"


def main():
    root = Path.cwd()
    for needed in (RUNTIME_PROJECT, EDITOR_PROJECT):
        if not (root / needed).exists():
            sys.exit(f"{needed} not found. Run from the repository root, and let Unity "
                     "generate it at least once.")

    runtime_files, editor_files = [], []
    for path in glob.glob("Assets/**/*.cs", recursive=True):
        native = os.sep + path.replace("/", os.sep)
        (editor_files if EDITOR_SEGMENT in native else runtime_files).append(path)
    if not runtime_files:
        sys.exit("No runtime scripts found under Assets.")

    out = Path(os.environ.get("TEMP", "/tmp")) / "zz-compile-check"

    team = [f for f in runtime_files if "TextMesh Pro" not in f]
    runtime_refs = references_from(root / RUNTIME_PROJECT, root)
    print(f"RUNTIME  {len(runtime_refs)} references, {len(runtime_files)} scripts "
          f"({len(team)} excluding imported TextMesh Pro samples)")
    ok_runtime, runtime_dll = build("ZZRuntime", runtime_files, runtime_refs, [], out, root)
    if not ok_runtime:
        print("Runtime assembly failed; editor pass skipped.")
        return 1

    if editor_files:
        # The editor csproj's own framework references (mscorlib, netstandard and the facades)
        # collide with the .NET SDK's and produce "System.String is not defined" and a type
        # forwarder cycle on System.Attribute. The runtime set is known to work, so the editor
        # pass uses that and adds only what the editor project has and the runtime does not:
        # UnityEditor.dll and the editor-side package assemblies.
        runtime_names = {re.search(r'Include="([^"]+)"', r).group(1) for r in runtime_refs}
        editor_only = [r for r in references_from(root / EDITOR_PROJECT, root)
                       if re.search(r'Include="([^"]+)"', r).group(1) not in runtime_names
                       and not re.search(r'Include="(System|Microsoft)[."]', r)]
        editor_refs = runtime_refs + editor_only
        print(f"EDITOR   {len(editor_refs)} references ({len(editor_only)} editor-only), "
              f"{len(editor_files)} scripts")
        ok_editor, _ = build("ZZEditor", editor_files, editor_refs, [runtime_dll], out, root)
        if not ok_editor:
            return 1
    else:
        print("EDITOR   no scripts in Editor folders")

    print("Compiles clean.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
