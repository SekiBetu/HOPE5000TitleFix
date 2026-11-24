param(
    [string]$Tag = "v1.0.0",
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

if ($Tag -notmatch '^[A-Za-z0-9._-]+$') {
    throw "Tag contains characters that are not safe for a file name: $Tag"
}

$root = $PSScriptRoot
$releaseRoot = [IO.Path]::GetFullPath((Join-Path $root "release"))
$distExe = [IO.Path]::GetFullPath((Join-Path $root "dist\HOPE5000TitleFix.exe"))

if (-not $SkipBuild) {
    & (Join-Path $root "build.cmd")
    if ($LASTEXITCODE -ne 0) {
        throw "build.cmd failed with exit code $LASTEXITCODE."
    }
}

if (-not (Test-Path -LiteralPath $distExe)) {
    throw "Built executable not found: $distExe"
}

$packageName = "HOPE5000TitleFix-$Tag-win"
$packageDir = Join-Path $releaseRoot $packageName
$zipPath = Join-Path $releaseRoot "$packageName.zip"
$checksumsPath = Join-Path $releaseRoot "SHA256SUMS.txt"

New-Item -ItemType Directory -Force -Path $packageDir | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $packageDir "docs") | Out-Null

Copy-Item -LiteralPath $distExe -Destination $packageDir
Copy-Item -Path (Join-Path $root "README.md"), (Join-Path $root "LICENSE"), (Join-Path $root "THIRD-PARTY-NOTICES.txt"), (Join-Path $root "CHANGELOG.md") -Destination $packageDir
Copy-Item -Path (Join-Path $root "docs\*") -Destination (Join-Path $packageDir "docs")

Compress-Archive -Path (Join-Path $packageDir "*") -DestinationPath $zipPath -Force

$exeHash = (Get-FileHash -LiteralPath $distExe -Algorithm SHA256).Hash
$zipHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash
@(
    "$exeHash  HOPE5000TitleFix.exe"
    "$zipHash  $packageName.zip"
) | Set-Content -LiteralPath $checksumsPath -Encoding ascii

Write-Host ""
Write-Host "Package : $zipPath"
Write-Host "EXE     : $distExe"
Write-Host "SHA256  : $checksumsPath"
Write-Host "EXE hash: $exeHash"
Write-Host "ZIP hash: $zipHash"
