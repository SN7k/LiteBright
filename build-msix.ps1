# ==============================================================================
# LiteBright MSIX Builder (Self-Contained + Native Auto-Start)
# ==============================================================================
$ErrorActionPreference = "Stop"

$ProjectRoot = $PSScriptRoot
$BuildDir    = Join-Path $ProjectRoot "bin\msix-build"
$OutputDir   = Join-Path $ProjectRoot "bin\msix-output"
$PackageName = "LiteBright_1.2.1.0_x64.msix"
$OutputMsix  = Join-Path $OutputDir $PackageName
$AssetsDir   = Join-Path $ProjectRoot "msix-assets"

# Locate makeappx.exe from Windows SDK BuildTools
$MakeAppx = "C:\Users\shomb\.nuget\packages\microsoft.windows.sdk.buildtools\10.0.28000.2705\bin\10.0.28000.0\x64\makeappx.exe"
if (-not (Test-Path $MakeAppx)) {
    $found = Get-ChildItem -Path "C:\Users\shomb\.nuget\packages\microsoft.windows.sdk.buildtools" -Recurse -Filter "makeappx.exe" -ErrorAction SilentlyContinue |
             Where-Object { $_.FullName -match "\\x64\\makeappx\.exe$" } | Select-Object -First 1
    if ($found) { $MakeAppx = $found.FullName }
    else { throw "makeappx.exe not found! Please check Windows SDK BuildTools package." }
}

Write-Host ">>> Using MakeAppx: $MakeAppx" -ForegroundColor Cyan

# 1. Clean and prepare output directories
if (-not (Test-Path $OutputDir)) { New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null }
if (Test-Path $BuildDir) {
    dotnet build-server shutdown | Out-Null
    Remove-Item -Path $BuildDir -Recurse -Force -ErrorAction SilentlyContinue
}
New-Item -ItemType Directory -Path $BuildDir -Force | Out-Null

# 2. Publish Self-Contained Release for win-x64
Write-Host ">>> Publishing self-contained win-x64 binaries..." -ForegroundColor Cyan
dotnet publish (Join-Path $ProjectRoot "BrightnessController.csproj") `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=false `
    -o $BuildDir

if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed!" }

# 3. Copy visual assets
$TargetAssets = Join-Path $BuildDir "Assets"
New-Item -ItemType Directory -Path $TargetAssets -Force | Out-Null
Copy-Item -Path (Join-Path $AssetsDir "*") -Destination $TargetAssets -Recurse -Force
Write-Host ">>> Copied Store visual assets." -ForegroundColor Green

# 4. Generate AppxManifest.xml with native windows.startupTask
$ManifestXml = @"
<?xml version="1.0" encoding="utf-8"?>
<Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
         xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
         xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities"
         xmlns:desktop="http://schemas.microsoft.com/appx/manifest/desktop/windows10"
         IgnorableNamespaces="uap rescap desktop">

  <Identity Name="SNKDEVWORKS.LiteBright"
            ProcessorArchitecture="x64"
            Publisher="CN=D6D1B1CC-C565-4B68-BDD7-5C3D7CC9A623"
            Version="1.2.1.0" />

  <Properties>
    <DisplayName>LiteBright</DisplayName>
    <PublisherDisplayName>SNK DEVWORKS</PublisherDisplayName>
    <Logo>Assets\StoreLogo.png</Logo>
  </Properties>

  <Dependencies>
    <TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.17763.0" MaxVersionTested="10.0.26100.0" />
  </Dependencies>

  <Resources>
    <Resource Language="en-US" />
  </Resources>

  <Applications>
    <Application Id="LiteBright"
                 Executable="BrightnessController.exe"
                 EntryPoint="Windows.FullTrustApplication">
      <uap:VisualElements DisplayName="LiteBright"
                          Description="Seamless monitor brightness control for Windows"
                          BackgroundColor="transparent"
                          Square150x150Logo="Assets\Square150x150Logo.png"
                          Square44x44Logo="Assets\Square44x44Logo.png">
        <uap:DefaultTile Wide310x150Logo="Assets\Wide310x150Logo.png"
                         Square71x71Logo="Assets\Square71x71Logo.png" />
      </uap:VisualElements>
      <Extensions>
        <desktop:Extension Category="windows.startupTask" Executable="BrightnessController.exe" EntryPoint="Windows.FullTrustApplication">
          <desktop:StartupTask TaskId="LiteBrightStartupTask"
                               Enabled="true"
                               DisplayName="LiteBright" />
        </desktop:Extension>
      </Extensions>
    </Application>
  </Applications>

  <Capabilities>
    <rescap:Capability Name="runFullTrust" />
  </Capabilities>
</Package>
"@

$ManifestPath = Join-Path $BuildDir "AppxManifest.xml"
Set-Content -Path $ManifestPath -Value $ManifestXml -Encoding UTF8
Write-Host ">>> Generated AppxManifest.xml with windows.startupTask." -ForegroundColor Green

# 5. Pack MSIX with makeappx.exe
Write-Host ">>> Packing MSIX package..." -ForegroundColor Cyan
if (Test-Path $OutputMsix) { Remove-Item $OutputMsix -Force }
& $MakeAppx pack /d $BuildDir /p $OutputMsix /o /v

if ($LASTEXITCODE -ne 0) { throw "makeappx pack failed!" }

# 6. Verify and display results
$Hash = (Get-FileHash -Path $OutputMsix -Algorithm SHA256).Hash
$SizeMB = [math]::Round(((Get-Item $OutputMsix).Length / 1MB), 2)

Write-Host "`n========================================================" -ForegroundColor Green
Write-Host "  SUCCESS! MSIX Package Ready for Microsoft Store" -ForegroundColor Green
Write-Host "========================================================" -ForegroundColor Green
Write-Host "  File:    $OutputMsix" -ForegroundColor White
Write-Host "  Size:    $SizeMB MB" -ForegroundColor White
Write-Host "  SHA256:  $Hash" -ForegroundColor Yellow
Write-Host "========================================================`n" -ForegroundColor Green
