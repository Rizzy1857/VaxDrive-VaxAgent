param (
    [Parameter(Mandatory=$true)]
    [string]$DriveLetter
)

$ErrorActionPreference = "Stop"

# Ensure Admin
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (!$isAdmin) {
    Write-Host "ABORT: Script must be run as Administrator." -ForegroundColor Red
    exit 1
}

$drivePath = "$DriveLetter`:"

if (!(Test-Path "$drivePath\.env.template")) {
    Write-Host "ABORT: .env.template not found on drive $DriveLetter" -ForegroundColor Red
    exit 1
}

# 1. Read .env.template and prompt for missing values
$envTemplate = Get-Content -Path "$drivePath\.env.template"
foreach ($line in $envTemplate) {
    if ([string]::IsNullOrWhiteSpace($line) -or $line.StartsWith("#")) { continue }
    
    $parts = $line.Split('=', 2)
    if ($parts.Length -eq 2) {
        $key = $parts[0].Trim()
        $val = $parts[1].Trim()
        
        if ([string]::IsNullOrWhiteSpace($val)) {
            $val = Read-Host "Enter value for $key"
        }
        
        # 2. Write to Machine-level env vars
        [Environment]::SetEnvironmentVariable($key, $val, "Machine")
    }
}

# 3. Register VaxAgent as Windows Service
$serviceName = "VaxDriveAgent"
$binPath = "$drivePath\VaxAgent.exe --daemon"

# Check if exists and stop/delete if so
if (Get-Service -Name $serviceName -ErrorAction SilentlyContinue) {
    Stop-Service -Name $serviceName -Force -ErrorAction SilentlyContinue
    sc.exe delete $serviceName | Out-Null
    Start-Sleep -Seconds 2
}

# Perform Atomic Swap if staging is ready
$flagPath = "$drivePath\staged_ready.flag"
$backupDir = $null
if (Test-Path $flagPath) {
    $utcNow = (Get-Date).ToUniversalTime().ToString("yyyyMMdd_HHmmss")
    $agentDir = "$drivePath\VaxAgent"
    $backupDir = "$drivePath\VaxAgent_backup_$utcNow"
    $stagingDir = "$drivePath\updates_staging"

    if (Test-Path $stagingDir) {
        for ($i=0; $i -lt 3; $i++) {
            try {
                if (Test-Path $agentDir) {
                    Rename-Item -Path $agentDir -NewName "VaxAgent_backup_$utcNow" -ErrorAction Stop
                }
                Rename-Item -Path $stagingDir -NewName "VaxAgent" -ErrorAction Stop
                break
            } catch {
                if ($i -eq 2) {
                    # Abort and Restore
                    if (Test-Path $backupDir) {
                        Rename-Item -Path $backupDir -NewName "VaxAgent" -ErrorAction SilentlyContinue
                    }
                    Write-Host "ABORT: Failed to perform atomic swap due to locked files." -ForegroundColor Red
                    exit 1
                }
                Start-Sleep -Milliseconds 500
            }
        }
    }
    Remove-Item -Path $flagPath -Force -ErrorAction SilentlyContinue
}

# Create service using sc.exe to specify exact account (NT SERVICE\VaxDriveAgent is a virtual account)
# Virtual accounts are implicitly created by the SCM when the service is created.
$scResult = sc.exe create $serviceName binPath= "`"$drivePath\VaxAgent.exe`" --daemon" start= auto obj= "NT SERVICE\$serviceName"
if ($LASTEXITCODE -ne 0) {
    Write-Host "ABORT: Failed to register service. sc.exe returned $LASTEXITCODE" -ForegroundColor Red
    exit 1
}

# Grant the virtual account "Log on as a service" right (SeServiceLogonRight)
# (Note: In a pure automated script, ntrights.exe or a PowerShell equivalent module is needed,
# but sc.exe create with NT SERVICE\ usually provisions it locally. We rely on standard SCM behavior here.)

# 5. Start service
Start-Service -Name $serviceName
$running = $false

for ($i = 0; $i -lt 10; $i++) {
    $svc = Get-Service -Name $serviceName
    if ($svc.Status -eq 'Running') {
        $running = $true
        break
    }
    Start-Sleep -Seconds 1
}

if (!$running) {
    Write-Host "ABORT: Service failed to enter Running state within 10s." -ForegroundColor Red
    exit 1
}

if ($null -ne $backupDir -and (Test-Path $backupDir)) {
    Remove-Item -Path $backupDir -Recurse -Force -ErrorAction SilentlyContinue
}

# 6. Write install log
$utcNow = (Get-Date).ToUniversalTime().ToString("yyyyMMdd_HHmmss")
$logPath = "$drivePath\logs\install_log_$utcNow.txt"
Set-Content -Path $logPath -Value "INSTALL SUCCESS: $utcNow UTC" | Out-Null

Write-Host "Service $serviceName installed and running successfully."
exit 0
