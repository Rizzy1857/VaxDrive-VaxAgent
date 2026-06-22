param (
    [Parameter(Mandatory=$true)]
    [ValidatePattern("^[A-Za-z]$")]
    [string]$DriveLetter,

    [Parameter(Mandatory=$false)]
    [int]$DiskNumber = -1
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

# 2. Format volume and ensure clean access (handles write-protection and OS boot drives)
try {
    if ($DiskNumber -ge 0) {
        $disk = Get-Disk -Number $DiskNumber -ErrorAction Stop
    } else {
        try {
            $partition = Get-Partition -DriveLetter $DriveLetter -ErrorAction Stop
            $disk = Get-Disk -Number $partition.DiskNumber -ErrorAction Stop
        } catch {
            Write-Error "Could not find a partition for Drive Letter $DriveLetter. The USB drive might be wiped or corrupted."
            Write-Error "Please run 'Get-Disk' in PowerShell to find your USB disk number, then run this script with: .\PrepareUsb.ps1 -DriveLetter $DriveLetter -DiskNumber <YourDiskNumber>"
            exit 1
        }
    }
    
    # Safety check to avoid wiping system or boot disk
    if ($disk.IsBoot -or $disk.IsSystem) {
        Write-Error "The specified drive is a system or boot disk! Aborting."
        exit 1
    }
    
    Write-Host "Step A: Removing registry-level USB write protection if present..."
    $regPath = "HKLM:\SYSTEM\CurrentControlSet\Control\StorageDevicePolicies"
    if (Test-Path $regPath) {
        Set-ItemProperty -Path $regPath -Name "WriteProtect" -Value 0 -ErrorAction SilentlyContinue
    }

    Write-Host "Step B: Running diskpart sequence to remove disk write protection and format disk $($disk.Number)..."
    $diskpartScript = @"
select disk $($disk.Number)
attributes disk clear readonly
clean
convert mbr
create partition primary
select partition 1
active
format fs=exfat label="VAXDRIVE" quick
assign letter=$DriveLetter
exit
"@
    $tempScriptPath = Join-Path $env:TEMP "vaxdrive_diskpart_$([guid]::NewGuid()).txt"
    $diskpartScript | Set-Content -Path $tempScriptPath -Encoding Ascii
    
    $dpOutput = diskpart /s $tempScriptPath
    Write-Host $dpOutput
    Remove-Item -Path $tempScriptPath -Force -ErrorAction SilentlyContinue
    
    # Give the OS a moment to mount the new volume
    Start-Sleep -Seconds 2
    
    if (-not (Test-Path "$DriveLetter`:\")) {
        Write-Error "Diskpart sequence completed, but drive $DriveLetter is not accessible."
        exit 1
    }
} catch {
    Write-Error "Failed to prepare the drive: $_"
    exit 1
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
