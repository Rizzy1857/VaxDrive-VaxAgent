<#
.SYNOPSIS
Orchestrates the complete build, code-signing, and packaging of the VaxDrive ecosystem.

.DESCRIPTION
This script performs a production-hardened build of VaxAgent (net8.0 and net35) and VaxDock.
It stages the output into a deployment directory mimicking the physical VAXDRIVE USB partition structure.
In a real production environment, the SignTool block should be uncommented and configured with a valid EV certificate.

.EXAMPLE
.\build_release.ps1
#>

$ErrorActionPreference = "Stop"

$RepoRoot = (Resolve-Path "$PSScriptRoot\..").ProviderPath
$DeployDir = "$RepoRoot\deploy_payload"

Write-Host "[1] Cleaning deployment directory..."
if (Test-Path $DeployDir) { Remove-Item -Recurse -Force $DeployDir }
New-Item -ItemType Directory -Force -Path $DeployDir | Out-Null
New-Item -ItemType Directory -Force -Path "$DeployDir\logs" | Out-Null

Write-Host "[2] Building VaxAgent (net8.0-windows, Single File)..."
dotnet publish "$RepoRoot\VaxAgent\VaxAgent.csproj" -f net8.0-windows -c Release -r win-x64 --self-contained -o "$DeployDir\Agent_Net8"

Write-Host "[3] Building VaxDock (net8.0-windows)..."
dotnet publish "$RepoRoot\VaxDock\VaxDock.csproj" -c Release -o "$DeployDir\VaxDock"

Write-Host "[5] Staging VAXDRIVE USB Root Structure..."
# Copy primary net8.0 agent to the root (assumed modern target by default)
Copy-Item "$DeployDir\Agent_Net8\VaxAgent.exe" "$DeployDir\VaxAgent.exe"
# Copy batch launcher
Copy-Item "$RepoRoot\Hardware\launcher.bat" "$DeployDir\launcher.bat"
# Create marker file
New-Item -ItemType File -Force -Path "$DeployDir\.vaxdrive" | Out-Null

Write-Host "[6] (Skipped) Code Signing..."
<#
$CertThumbprint = "<YOUR_CERT_THUMBPRINT_HERE>"
$SigntoolPath = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.19041.0\x64\signtool.exe"
& $SigntoolPath sign /sha1 $CertThumbprint /t http://timestamp.digicert.com "$DeployDir\VaxAgent.exe"
& $SigntoolPath sign /sha1 $CertThumbprint /t http://timestamp.digicert.com "$DeployDir\VaxDock\VaxDock.exe"
#>

Write-Host "============================================="
Write-Host "✅ Production Deployment Package Staged"
Write-Host "Payload located at: $DeployDir"
Write-Host "Copy contents of '$DeployDir' strictly to the root of the exFAT VAXDRIVE USB volume."
Write-Host "============================================="
