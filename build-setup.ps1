# ============================================================
#  LiteBright — Setup Builder Script (Self-Contained)
#  Publishes self-contained Release and compiles Inno Setup installer.
# ============================================================

$ErrorActionPreference = "Stop"

$ProjectRoot = $PSScriptRoot
$PublishDir  = Join-Path $ProjectRoot "bin\publish-setup"
$InstallerScript = Join-Path $ProjectRoot "installer\LiteBright.iss"

Write-Host "==> Publishing LiteBright (Self-Contained win-x64)..." -ForegroundColor Cyan
dotnet build-server shutdown | Out-Null
if (Test-Path $PublishDir) {
    Remove-Item -Path $PublishDir -Recurse -Force -ErrorAction SilentlyContinue
}

dotnet publish (Join-Path $ProjectRoot "BrightnessController.csproj") `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=false `
    -o $PublishDir

if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed!" }

# Locate Inno Setup Compiler (ISCC.exe)
$isccCandidates = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
)

$iscc = $isccCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc) {
    Write-Error "ISCC.exe (Inno Setup 6) was not found. Please install Inno Setup 6."
    exit 1
}

Write-Host "==> Compiling installer with Inno Setup ($iscc)..." -ForegroundColor Cyan
& $iscc $InstallerScript

$outputInstaller = Join-Path $ProjectRoot "bin\installer-output\LiteBright-Setup-1.2.2.exe"
if (Test-Path $outputInstaller) {
    $hash = (Get-FileHash $outputInstaller -Algorithm SHA256).Hash
    $size = (Get-Item $outputInstaller).Length / 1MB
    Write-Host "`n[SUCCESS] Self-contained installer built successfully!" -ForegroundColor Green
    Write-Host "File:   $outputInstaller ($([Math]::Round($size, 2)) MB)"
    Write-Host "SHA256: $hash"
}
