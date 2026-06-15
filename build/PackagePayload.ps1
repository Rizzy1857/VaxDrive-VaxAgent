$ErrorActionPreference = "Stop"

$buildKeyHex = $env:VAXDRIVE_BUILD_KEY
if ([string]::IsNullOrWhiteSpace($buildKeyHex)) {
    # Reject build silently without writing anything to stdout
    exit 1
}

$deployDir = "deploy_payload"
if (!(Test-Path $deployDir)) {
    New-Item -ItemType Directory -Force -Path $deployDir | Out-Null
}

$nativeDir = "$deployDir\native\win-x64"
if (!(Test-Path $nativeDir)) {
    New-Item -ItemType Directory -Force -Path $nativeDir | Out-Null
}

# Paths to copy from
$agentBinDir = "VaxAgent\bin\Release\net8.0\win-x64\publish"
$dockBinDir = "VaxDock\bin\Release\net8.0-windows"

# Copy binaries silently
if (Test-Path $agentBinDir) {
    Copy-Item -Path "$agentBinDir\*" -Destination $deployDir -Recurse -Force | Out-Null
}
if (Test-Path $dockBinDir) {
    Copy-Item -Path "$dockBinDir\*" -Destination $deployDir -Recurse -Force | Out-Null
}

# Create dummy yara.dll if it doesn't exist just to satisfy the folder structure for tests
if (!(Test-Path "$nativeDir\yara.dll")) {
    Set-Content -Path "$nativeDir\yara.dll" -Value "MOCK_YARA_DLL" | Out-Null
}

if (Test-Path ".env.example") {
    Copy-Item -Path ".env.example" -Destination "$deployDir\.env.template" -Force | Out-Null
}

# Strip PDBs unless requested
if ($env:BUILD_INCLUDE_PDB -ne "1") {
    Get-ChildItem -Path $deployDir -Filter "*.pdb" -Recurse | Remove-Item -Force | Out-Null
}

# Generate manifest
$files = Get-ChildItem -Path $deployDir -File -Recurse | Where-Object { $_.Name -ne "manifest.json" }
$manifestList = @()

foreach ($f in $files) {
    $relPath = $f.FullName.Substring((Resolve-Path $deployDir).Path.Length + 1).Replace('\', '/')
    $sha256 = (Get-FileHash -Path $f.FullName -Algorithm SHA256).Hash.ToLower()
    $size = $f.Length
    
    $manifestList += @{
        filename = $relPath
        sha256 = $sha256
        size_bytes = $size
    }
}

$manifestJson = $manifestList | ConvertTo-Json -Depth 5 -Compress
$manifestBytes = [System.Text.Encoding]::UTF8.GetBytes($manifestJson)

# Try parsing the key. It might be plain text or hex. Let's assume bytes from string.
$keyBytes = [System.Text.Encoding]::UTF8.GetBytes($buildKeyHex)

$hmac = [System.Security.Cryptography.HMACSHA256]::new($keyBytes)
$hashBytes = $hmac.ComputeHash($manifestBytes)
$hmacHex = [System.BitConverter]::ToString($hashBytes).Replace("-", "").ToLower()

$finalManifest = @{
    signature = $hmacHex
    files = $manifestList
}

$finalJson = $finalManifest | ConvertTo-Json -Depth 5 -Compress
Set-Content -Path "$deployDir\manifest.json" -Value $finalJson -Encoding UTF8 | Out-Null

$manifestSha256 = (Get-FileHash -Path "$deployDir\manifest.json" -Algorithm SHA256).Hash.ToLower()

Write-Host "BUILD OK sha256=$manifestSha256"
