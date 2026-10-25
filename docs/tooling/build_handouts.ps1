# Build readable .docx and .pdf copies of the project's Markdown documents.
#
# The Markdown is the record, because it diffs and merges. But nobody should have to read raw
# Markdown to follow a design decision, so this produces a rendered copy of every document that
# is meant to be read rather than parsed.
#
#   powershell -ExecutionPolicy Bypass -File docs/tooling/build_handouts.ps1
#
# Output goes to docs/handouts/, which is gitignored: these rebuild from source in one command.
# PDF export drives the installed Microsoft Word through COM, so it needs Windows with Word.

$ErrorActionPreference = 'Stop'
$repo = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Set-Location $repo

# Source path -> output name. Named explicitly rather than taken from the filename, because
# several source files are called things like "report.md" inside a dated task folder and that
# name means nothing once it is sitting in a handouts directory next to seven other documents.
$docs = [ordered]@{
    'docs\research\CRAZY-TAXI-SUMMARY.md'                  = 'Crazy Taxi pivot - short version'
    'docs\research\CRAZY-TAXI-CONTROLLER-RESEARCH.md'      = 'Crazy Taxi controller - full research'
    'docs\M2-INDIVIDUAL-AKO.md'                            = 'M2 individual documentation'
    'docs\M2-REFLECTION-SCAFFOLD.md'                       = 'M2 reflection - working material'
    'docs\UI-TUTORIAL-BRIEF.md'                            = 'UI and tutorial brief'
    'docs\playtesting\M2-PLAYTEST-WRITEUP.md'              = 'Playtesting and telemetry'
    'docs\DESIGN-OVERVIEW.md'                              = 'Design overview - full reference'
    '.agents\tasks\turning-geometry-2026-10-14\report.md'  = 'Turning circle and corner geometry'
}

$out = Join-Path $repo 'docs\handouts'
New-Item -ItemType Directory -Force -Path $out | Out-Null

# One Word instance for the whole batch. Opening and quitting it per file is slow and leaves
# orphaned processes behind if a conversion throws.
$word = $null
try { $word = New-Object -ComObject Word.Application; $word.Visible = $false; $word.DisplayAlerts = 0 }
catch { Write-Host "Word not available, writing .docx only: $($_.Exception.Message)" }

$made = 0
foreach ($rel in $docs.Keys) {
    $src = Join-Path $repo $rel
    if (-not (Test-Path -LiteralPath $src)) { Write-Host "skip (missing)  $rel"; continue }

    $name = $docs[$rel]
    $docx = Join-Path $out "$name.docx"

    python -B docs/tooling/md_to_docx.py "$rel" --out "docs\handouts\$name.docx" | Out-Null
    if (-not (Test-Path -LiteralPath $docx)) { Write-Host "FAILED docx    $rel"; continue }

    if ($word) {
        # Cast to [string]. Join-Path yields a PSObject here, and Word's SaveAs takes its
        # arguments by reference through COM, which refuses to marshal a PSObject.
        $pdf = [string](Join-Path $out "$name.pdf")
        $docxStr = [string]$docx
        $doc = $null
        try {
            $doc = $word.Documents.Open($docxStr, $false, $true)
            $doc.SaveAs([ref]$pdf, [ref]17)   # 17 = wdFormatPDF
            $pages = $doc.ComputeStatistics(2) # 2 = wdStatisticPages
            Write-Host ("ok  {0,-42} {1,3} pages" -f $name, $pages)
        }
        catch { Write-Host "FAILED pdf     $name : $($_.Exception.Message)" }
        finally { if ($doc) { $doc.Close(0) } }
    }
    else { Write-Host "ok (docx only)  $name" }
    $made++
}

if ($word) { $word.Quit() }
Write-Host ""
Write-Host "$made document(s) written to docs\handouts\"
