# Builds the per-user installer for Autodesk Access Nudge.
# Requires: .NET 8 SDK and Inno Setup 6.
param(
    [string]$Version = "0.1.0"
)

$ErrorActionPreference = "Stop"
$root = (Split-Path $PSScriptRoot)
$app = Join-Path $root "src\SmsNudge"
$publishDir = Join-Path $app "publish"
$distDir = Join-Path $root "dist"
$outputDir = Join-Path $root "installer\output"

Write-Host "== Publishing self-contained exe (win-x64) =="
dotnet publish $app -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:IncludeNativeLibraries=true -p:DebugType=None -p:DebugSymbols=false `
    /p:InformationalVersion=$Version -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

$exePath = Join-Path $publishDir "SmsNudge.exe"
if (-not (Test-Path $exePath)) { throw "Expected publish output not found: $exePath" }

Write-Host "== Compiling installer =="
$scriptPath = Join-Path $root "installer\Setup.iss"
$isccCandidates = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
)
$iscc = $isccCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc -or -not (Test-Path $iscc)) {
    $iscc = (Get-Command iscc -ErrorAction SilentlyContinue).Source
}
if (-not $iscc -or -not (Test-Path $iscc)) { throw "Inno Setup compiler (ISCC.exe) not found. Install Inno Setup 6." }

& $iscc "/DAppVersion=$Version" "/DOutputDir=$outputDir" "/Qp" $scriptPath
if ($LASTEXITCODE -ne 0) { throw "ISCC failed" }

$setupName = "Setup-SmsNudge-v$Version.exe"
$distSetup = Join-Path $distDir $setupName
New-Item $distDir -ItemType Directory -Force | Out-Null
Copy-Item (Join-Path $outputDir $setupName) $distSetup -Force

$sha = (Get-FileHash $distSetup -Algorithm SHA256).Hash.ToLower()
$shaFile = Join-Path $distDir "$setupName.sha256"
"$($sha)  $setupName" | Set-Content -Encoding ASCII $shaFile

Write-Host "Installer: $distSetup"
Write-Host "SHA-256:   $sha"
