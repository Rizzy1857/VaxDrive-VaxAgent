param (
    [Parameter(Mandatory=$true)]
    [ValidatePattern("^[A-Za-z]$")]
    [string]$DriveLetter
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($env:VAXDRIVE_BUILD_KEY)) {
    exit 1
}

$drivePath = "$DriveLetter`:"

# 1. Confirm drive is removable
$disk = Get-WmiObject Win32_LogicalDisk -Filter "DeviceID='$drivePath'"
if ($null -eq $disk -or $disk.DriveType -ne 2) {
    # 2 is Removable Disk
    exit 1
}

# 2. Format volume
try {
    # WMI format approach or modern Format-Volume. Format-Volume is better for Win10+
    Format-Volume -DriveLetter $DriveLetter -FileSystem NTFS -NewFileSystemLabel "VAXDRIVE" -Confirm:$false -Force | Out-Null
} catch {
    exit 1
}

# 3. Create folder structure
$folders = @("VaxAgent", "VaxDock", "logs", "reports", "updates")
foreach ($folder in $folders) {
    New-Item -ItemType Directory -Force -Path "$drivePath\$folder" | Out-Null
}

# 4. Copy deploy_payload to drive root
$deployDir = "deploy_payload"
if (!(Test-Path $deployDir)) {
    exit 1
}
Copy-Item -Path "$deployDir\*" -Destination "$drivePath\" -Recurse -Force | Out-Null

# 5. Verify SHA-256 matches manifest
$manifestPath = "$drivePath\manifest.json"
if (!(Test-Path $manifestPath)) {
    Remove-Item -Path "$drivePath\*" -Recurse -Force | Out-Null
    exit 1
}

$manifestJson = Get-Content -Path $manifestPath -Raw | ConvertFrom-Json
$mismatch = $false

foreach ($fileRecord in $manifestJson.files) {
    $filePath = "$drivePath\$($fileRecord.filename)".Replace('/', '\')
    if (!(Test-Path $filePath)) {
        $mismatch = $true
        break
    }
    $actualHash = (Get-FileHash -Path $filePath -Algorithm SHA256).Hash.ToLower()
    if ($actualHash -ne $fileRecord.sha256) {
        $mismatch = $true
        break
    }
}

# 6. Mismatch handling
if ($mismatch) {
    Remove-Item -Path "$drivePath\*" -Recurse -Force | Out-Null
    exit 1
}

# 7. Write prep log
$utcNow = (Get-Date).ToUniversalTime().ToString("yyyyMMdd_HHmmss")
$logPath = "$drivePath\logs\prep_log_$utcNow.txt"
Set-Content -Path $logPath -Value "USB PREP SUCCESS: $utcNow UTC" | Out-Null

# 8. Stdout
$manifestHash = (Get-FileHash -Path $manifestPath -Algorithm SHA256).Hash.ToLower()
Write-Host "USB READY $DriveLetter sha256=$manifestHash"
exit 0
