# ============================================================
#  LiteBright — Setup Builder Script
#  Publishes the Release build and compiles the Inno Setup installer.
# ============================================================

$ErrorActionPreference = "Stop"

Write-Host "==> Publishing LiteBright (Release win-x64)..." -ForegroundColor Cyan
dotnet publish -c Release -r win-x64 --self-contained false -o "bin\publish-setup"

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
& $iscc "installer\LiteBright.iss"

$outputInstaller = "bin\installer-output\LiteBright-Setup-1.2.0.exe"
if (Test-Path $outputInstaller) {
    $hash = (Get-FileHash $outputInstaller -Algorithm SHA256).Hash
    $size = (Get-Item $outputInstaller).Length / 1MB
    Write-Host "`n[SUCCESS] Installer built successfully!" -ForegroundColor Green
    Write-Host "File:   $outputInstaller ($([Math]::Round($size, 2)) MB)"
    Write-Host "SHA256: $hash"
}
