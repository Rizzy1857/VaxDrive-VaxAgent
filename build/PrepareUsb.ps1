param (
    [Parameter(Mandatory=$true)]
    [ValidatePattern("^[A-Za-z]$")]
    [string]$DriveLetter
)

# Show all errors in the terminal
$ErrorActionPreference = "Continue"

$envFile = Join-Path -Path $PSScriptRoot -ChildPath "..\.env"
if (Test-Path $envFile) {
    Get-Content $envFile | Where-Object { $_ -match '^([^#=]+)=(.*)$' } | ForEach-Object {
        [Environment]::SetEnvironmentVariable($Matches[1].Trim(), $Matches[2].Trim())
    }
}

if ([string]::IsNullOrWhiteSpace($env:VAXDRIVE_BUILD_KEY)) {
    Write-Error "VAXDRIVE_BUILD_KEY environment variable is not set."
    exit 1
}

$drivePath = "$DriveLetter`:"

# 1. Confirm drive is not the system drive
$sysDrive = $env:SystemDrive.Substring(0,1)
if ($DriveLetter -eq $sysDrive) {
    Write-Error "Cannot prepare the system drive! Aborting."
    exit 1
}

# 2. Format volume and ensure clean access (handles OS boot drives)
try {
    Write-Host "Attempting to clean the entire disk for clean access..."
    $partition = Get-Partition -DriveLetter $DriveLetter -ErrorAction Stop
    $disk = Get-Disk -Number $partition.DiskNumber -ErrorAction Stop
    
    # Safety check to avoid wiping system or boot disk
    if ($disk.IsBoot -or $disk.IsSystem) {
        Write-Error "The specified drive is a system or boot disk! Aborting."
        exit 1
    }
    
    Write-Host "Clearing disk $($disk.Number)..."
    Clear-Disk -Number $disk.Number -RemoveData -RemoveOEM -Confirm:$false -ErrorAction Stop
    
    Write-Host "Initializing disk $($disk.Number)..."
    Initialize-Disk -Number $disk.Number -PartitionStyle MBR -ErrorAction Stop
    
    Write-Host "Creating new active partition..."
    New-Partition -DiskNumber $disk.Number -UseMaximumSize -IsActive -DriveLetter $DriveLetter -ErrorAction Stop | Format-Volume -FileSystem exFAT -NewFileSystemLabel "VAXDRIVE" -Confirm:$false -Force -ErrorAction Stop
} catch {
    Write-Warning "Disk clean failed or not supported: $_"
    Write-Host "Falling back to formatting the existing volume..."
    try {
        Format-Volume -DriveLetter $DriveLetter -FileSystem exFAT -NewFileSystemLabel "VAXDRIVE" -Confirm:$false -Force -ErrorAction Stop
    } catch {
        Write-Error "Failed to format volume: $_"
        exit 1
    }
}

# 3. Create folder structure
Write-Host "Creating folder structure..."
$folders = @("VaxAgent", "VaxDock", "logs", "reports", "updates")
foreach ($folder in $folders) {
    New-Item -ItemType Directory -Force -Path "$drivePath\$folder"
}

# 4. Copy deploy_payload to drive root
$deployDir = "deploy_payload"
if (!(Test-Path $deployDir)) {
    Write-Error "deploy_payload directory not found."
    exit 1
}
Write-Host "Copying payload to $drivePath..."
Copy-Item -Path "$deployDir\*" -Destination "$drivePath\" -Recurse -Force

# 5. Verify SHA-256 matches manifest
$manifestPath = "$drivePath\manifest.json"
if (!(Test-Path $manifestPath)) {
    Write-Error "manifest.json not found on the prepared drive."
    Remove-Item -Path "$drivePath\*" -Recurse -Force
    exit 1
}

Write-Host "Verifying manifest hashes..."
$manifestJson = Get-Content -Path $manifestPath -Raw | ConvertFrom-Json
$mismatch = $false

foreach ($fileRecord in $manifestJson.files) {
    $filePath = "$drivePath\$($fileRecord.filename)".Replace('/', '\')
    if (!(Test-Path $filePath)) {
        Write-Error "Missing file: $($fileRecord.filename)"
        $mismatch = $true
        continue
    }
    $actualHash = (Get-FileHash -Path $filePath -Algorithm SHA256).Hash.ToLower()
    if ($actualHash -ne $fileRecord.sha256) {
        Write-Error "Hash mismatch for file: $($fileRecord.filename). Expected $($fileRecord.sha256), got $actualHash"
        $mismatch = $true
    }
}

# 6. Mismatch handling
if ($mismatch) {
    Write-Error "Verification failed. Cleaning up drive."
    Remove-Item -Path "$drivePath\*" -Recurse -Force
    exit 1
}

# 7. Write prep log
Write-Host "Writing preparation log..."
$utcNow = (Get-Date).ToUniversalTime().ToString("yyyyMMdd_HHmmss")
$logPath = "$drivePath\logs\prep_log_$utcNow.txt"
Set-Content -Path $logPath -Value "USB PREP SUCCESS: $utcNow UTC"

# 8. Stdout
$manifestHash = (Get-FileHash -Path $manifestPath -Algorithm SHA256).Hash.ToLower()
Write-Host "USB READY $DriveLetter sha256=$manifestHash"
exit 0
