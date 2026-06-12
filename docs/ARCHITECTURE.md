# VaxDrive System Architecture
> **Revision:** 1.0 · **Status:** Pre-implementation reference · **Owner:** Engineering

---

## Table of Contents

1. [System Overview](#1-system-overview)
2. [Component Map](#2-component-map)
3. [Monorepo Layout](#3-monorepo-layout)
4. [VaxDrive — Physical Hardware](#4-vaxdrive--physical-hardware)
5. [VaxAgent — Scanner Binary](#5-vaxagent--scanner-binary)
6. [VaxDock — Laptop Application](#6-vaxdock--laptop-application)
7. [Cryptography Specification](#7-cryptography-specification)
8. [.vax File Format](#8-vax-file-format)
9. [Definitions Pack](#9-definitions-pack)
10. [Launcher Subsystem](#10-launcher-subsystem)
11. [Data Flow — End to End](#11-data-flow--end-to-end)
12. [Threat Model](#12-threat-model)
13. [Engineering Rules — Non-negotiable](#13-engineering-rules--non-negotiable)
14. [Open Decisions](#14-open-decisions)
15. [Dependency Registry](#15-dependency-registry)

---

## 1. System Overview

VaxDrive is a **portable OT vulnerability vaccine**. It enables non-technical plant floor staff to perform cryptographically sealed, passive vulnerability scans of Windows and PLC-adjacent devices with **zero network dependency, zero writes to the target, and zero operator expertise required**.

```
┌──────────────────────────────────────────────────────────────────────────────┐
│                         FIELD OPERATION (AIR-GAPPED)                         │
│                                                                              │
│   Staff plugs VaxDrive → HID spoof auto-launches VaxAgent.exe               │
│   VaxAgent runs 90s passive scan → writes SCAN_[HASH]_[TS].vax to /results  │
│   Staff unplugs drive                                                        │
└──────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      │ (physical transport)
                                      ▼
┌──────────────────────────────────────────────────────────────────────────────┐
│                       SECURITY LAPTOP (VaxDock)                              │
│                                                                              │
│   Drive plugged in → auto-detect VAXDRIVE volume label                      │
│   Decrypt .vax → verify HMAC → ingest to SQLite                             │
│   Dashboard: triage / remediation cards / trend / export                    │
└──────────────────────────────────────────────────────────────────────────────┘
```

### Core Constraints (Immutable)

| Property | Value |
|---|---|
| Network dependency | **ZERO** — fully air-gapped both halves |
| Writes to target host | **NEVER** |
| Phones home | **NEVER** |
| Elevation required on target | **No** |
| Scan duration | **90 seconds** |
| Operator skill required | **None** — plug and pull |

---

## 2. Component Map

```
VaxDrive Monorepo
│
├── VaxAgent          C# .NET 8 / .NET 3.5 dual-target scanner binary
│   ├── Checks        9 individual check modules (one file = one check)
│   ├── Crypto        AES-256-GCM encrypt + HMAC-SHA256 sign (agent side)
│   ├── Definitions   CVE pack loader + matcher engine
│   └── Output        .vax file writer + progress reporter
│
├── VaxDock           WPF .NET 8 companion laptop application
│   ├── Views         Dashboard / DeviceDetail / RemediationCard / TrendView
│   ├── Data          SQLite repository (raw parameterized SQL — NO ORM)
│   ├── Crypto        .vax decrypt + HMAC verify (dock side)
│   └── Models        Shared: ScanResult / Finding / Device / PlcNeighbor
│
├── Definitions       OT CVE JSON pack source + build & signing scripts
├── Launcher          HID stub (AutoHotkey compiled OR ATtiny85 firmware)
└── Tests             xUnit — VaxAgent.Tests + VaxDock.Tests
```

**Why monorepo?** VaxAgent and VaxDock share `Models` and `Crypto` contracts. Splitting forces shared-library sync hell across two CI pipelines. One `.sln` at root, one pipeline, one version tag.

---

## 3. Monorepo Layout

```
VaxDrive-VaxAgent/
├── VaxDrive.sln                         # Solution — all projects
│
├── VaxAgent/
│   ├── Checks/
│   │   ├── OsCheck.cs                   # WMI Win32_OperatingSystem
│   │   ├── InstalledSoftwareCheck.cs    # HKLM Uninstall registry
│   │   ├── ServicesCheck.cs             # WMI sc query
│   │   ├── OpenPortsCheck.cs            # netstat -ano snapshot
│   │   ├── UsbHistoryCheck.cs           # USBSTOR — unknown VIDs
│   │   ├── ScheduledTasksCheck.cs       # schtasks /query
│   │   ├── FirmwareCheck.cs             # WMI Win32_BIOS
│   │   ├── CveMatchCheck.cs             # Definition pack cross-ref
│   │   └── RogueProcessCheck.cs         # Known-bad IOC process list
│   ├── Crypto/
│   │   ├── VaxEncryptor.cs              # AES-256-GCM encrypt
│   │   └── VaxSigner.cs                 # HMAC-SHA256 sign
│   ├── Definitions/
│   │   ├── DefinitionLoader.cs          # Load + verify signed JSON pack
│   │   └── CveMatcher.cs               # Match findings against pack
│   ├── Output/
│   │   ├── VaxFileWriter.cs             # Assemble + write .vax
│   │   └── ProgressReporter.cs          # OLED / console progress bar
│   ├── Models/                          # ← consumed from shared Models/
│   ├── Program.cs                       # Entry point + orchestrator
│   ├── ScanOrchestrator.cs              # Runs all checks in sequence
│   └── VaxAgent.csproj                  # net8.0 + net3.5 conditional
│
├── VaxDock/
│   ├── Views/
│   │   ├── Dashboard.xaml / .cs         # Device list + severity summary
│   │   ├── DeviceDetail.xaml / .cs      # Per-device findings deep dive
│   │   ├── RemediationCard.xaml / .cs   # Step-by-step operator fix card
│   │   └── TrendView.xaml / .cs         # Per-device vuln history
│   ├── Data/
│   │   ├── DatabaseBootstrap.cs         # Schema init + migration stubs
│   │   ├── ScanRepository.cs            # Raw SQL: scans + findings
│   │   ├── DeviceRepository.cs          # Raw SQL: device registry
│   │   └── RemediationRepository.cs     # Raw SQL: remediation state
│   ├── Crypto/
│   │   ├── VaxDecryptor.cs              # AES-256-GCM decrypt
│   │   └── HmacVerifier.cs              # HMAC-SHA256 verify on import
│   ├── Services/
│   │   ├── DriveDetector.cs             # Watch for VAXDRIVE volume label
│   │   ├── IngestPipeline.cs            # Decrypt → verify → ingest
│   │   ├── CadenceTracker.cs            # Flag overdue devices
│   │   └── ExportService.cs             # PDF + CSV export
│   ├── Models/                          # ScanResult / Finding / Device / PlcNeighbor
│   ├── App.xaml / .cs
│   └── VaxDock.csproj                   # net8.0-windows WPF
│
├── Definitions/
│   ├── packs/
│   │   └── ot-cve-pack-YYYY-WNN.json    # Versioned weekly pack
│   ├── scripts/
│   │   ├── build-pack.ps1               # Assemble + sign pack
│   │   └── verify-pack.ps1              # Validate signature
│   └── schema/
│       └── cve-pack.schema.json         # JSON schema for pack validation
│
├── Launcher/
│   ├── ahk/
│   │   ├── VaxLauncher.ahk              # AutoHotkey source
│   │   └── VaxLauncher.exe              # Compiled stub (committed artifact)
│   └── attiny85/
│       ├── vax_hid.ino                  # Arduino firmware
│       └── README.md                    # Flash instructions
│
├── Tests/
│   ├── VaxAgent.Tests/
│   │   ├── Checks/                      # Unit tests per check module
│   │   ├── Crypto/                      # Encrypt/sign/verify round-trip
│   │   └── VaxAgent.Tests.csproj
│   └── VaxDock.Tests/
│       ├── IngestPipeline/              # HMAC fail + quarantine path
│       ├── Data/                        # Repository SQL tests (in-memory SQLite)
│       └── VaxDock.Tests.csproj
│
├── .env.example                         # Template — NO keys, NO secrets
├── .gitignore
├── README.md
└── VaxDrive.sln
```

---

## 4. VaxDrive — Physical Hardware

### Hardware Class

| Property | Spec |
|---|---|
| Target device class | IronKey S1000 or Kingston Vault Privacy 80 (or equivalent) |
| Onboard encryption | AES-256 XTS — hardware enforced |
| Host write access | **NONE** — drive is read-only to the target host |
| Agent write access | `/results` partition only |
| Display | Optional OLED on casing (progress bar) — falls back to console window |

### Partition Layout

```
VAXDRIVE (volume label)
├── /boot         Read-only · signed
│                 Autorun shim + HID launcher stub
│                 Note: Autorun disabled since Win7 SP1 — shim supports
│                 manual and HID-triggered launch only
│
├── /engine       Read-only · signed · self-verifying
│                 VaxAgent.exe — single-file self-contained
│                 Verifies own SHA-256 hash against manifest on every launch
│
├── /definitions  Read-only · signed · weekly-updated
│                 OT CVE pack (signed JSON) — updated offline before field rounds
│
├── /results      Write-only from VaxAgent, read-only from VaxDock
│                 SCAN_[DEVICE-HASH]_[TIMESTAMP].vax
│                 AES-256-GCM encrypted JSON + HMAC-SHA256 per file
│
└── /logs         Append-only
                  Tamper audit log — launch timestamp, check count, file written
```

### Partition Access Matrix

| Partition | VaxAgent (on target) | VaxDock (on laptop) | Target host OS |
|---|---|---|---|
| /boot | Read | Read | Read |
| /engine | Read (execute) | Read | Read |
| /definitions | Read | Read | No access |
| /results | **Write** | Read | No access |
| /logs | Append | Read | No access |

---

## 5. VaxAgent — Scanner Binary

### Build Targets

```xml
<!-- VaxAgent.csproj — dual target -->
<TargetFrameworks>net8.0;net35</TargetFrameworks>
<PublishSingleFile Condition="'$(TargetFramework)'=='net8.0'">true</PublishSingleFile>
<SelfContained Condition="'$(TargetFramework)'=='net8.0'">true</SelfContained>
<RuntimeIdentifier Condition="'$(TargetFramework)'=='net8.0'">win-x64</RuntimeIdentifier>
```

- **net8.0** → Single-file self-contained. No runtime install on target. Ships as `VaxAgent.exe`.
- **net35** → Conditional compilation (`#if NET35`) for Windows XP / Server 2003 targets. WMI-only paths, no LINQ, no async. Built as a separate artifact `VaxAgent_xp.exe`.

### Self-Verification on Launch

```
Program.cs → SelfVerifier.VerifyBinaryIntegrity()
  ├── Read /engine/VaxAgent.manifest (SHA-256 hash embedded at build time)
  ├── Compute running binary SHA-256
  ├── Compare
  └── MISMATCH → log to /logs/tamper.log → Environment.Exit(1)
```

No check proceeds if self-verification fails. This is not suppressible.

### Check Execution — Sequence & Isolation

Each check is a class implementing `ICheck`:

```csharp
public interface ICheck
{
    string Name { get; }
    CheckResult Run(ScanContext context);  // No async — deterministic 90s budget
}
```

`ScanOrchestrator` runs all checks sequentially (not parallel — deterministic timing, simpler fault isolation):

```
ScanOrchestrator.RunAll()
  1. OsCheck            → ScanResult.Os, PatchLevel
  2. InstalledSoftwareCheck → ScanResult.InstalledSoftware[]
  3. ServicesCheck      → ScanResult.Services[]
  4. OpenPortsCheck     → ScanResult.OpenPorts[]
  5. UsbHistoryCheck    → ScanResult.UsbAnomalies[]
  6. ScheduledTasksCheck → ScanResult.ScheduledTasks[]
  7. FirmwareCheck      → ScanResult.BiosString
  8. CveMatchCheck      → ScanResult.Findings[]  (uses output of 1-7)
  9. RogueProcessCheck  → ScanResult.Findings[] appended
  10. PlcNeighborCheck  → ScanResult.PlcNeighbors[] (conditional — if networked)
```

Each check catches its own exceptions, records partial failure into `CheckResult.Error`, and returns — the orchestrator never aborts on a single check failure.

### Check Specifications

#### OsCheck.cs
- **Method:** `ManagementObject` query on `Win32_OperatingSystem`
- **Extracts:** `Caption`, `Version`, `ServicePackMajorVersion`, `LastBootUpTime`
- **net35:** Same WMI path — `System.Management` available in .NET 3.5

#### InstalledSoftwareCheck.cs
- **Method:** Registry read — `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*`
- **Also checks:** `HKLM\SOFTWARE\WOW6432Node\...` for 32-bit software on 64-bit hosts
- **Extracts:** `DisplayName`, `DisplayVersion`, `Publisher`, `InstallDate`
- **Note:** No registry writes. `RegistryKey.OpenSubKey` with read-only flag.

#### ServicesCheck.cs
- **Method:** `Win32_Service` WMI query
- **Extracts:** `Name`, `State`, `StartMode`, `PathName`
- **Flag:** Services running from unusual paths (temp dirs, removable drives)

#### OpenPortsCheck.cs
- **Method:** `netstat -ano` process execution, stdout capture, parse
- **Extracts:** Protocol, local address, local port, remote address, state, PID
- **net35 note:** `Process.Start` available — same path

#### UsbHistoryCheck.cs
- **Method:** Registry read — `HKLM\SYSTEM\CurrentControlSet\Enum\USBSTOR`
- **Logic:** Enumerate all sub-keys. Extract VID/PID. Cross-reference against known-good allowlist in definitions pack. Unknown VIDs go into `usb_anomalies[]`.
- **Allowlist source:** `Definitions/packs/ot-cve-pack-*.json` → `usb_allowlist[]` field

#### ScheduledTasksCheck.cs
- **Method:** `schtasks /query /fo CSV /v` — stdout capture + CSV parse
- **Flag:** Tasks with `SYSTEM` run-as + external executable paths
- **net35:** Same process execution path

#### FirmwareCheck.cs
- **Method:** `Win32_BIOS` WMI + `Win32_ComputerSystem`
- **Extracts:** `SMBIOSBIOSVersion`, `Manufacturer`, `ReleaseDate`, `SerialNumber`
- **Purpose:** Baseline fingerprint for supply-chain firmware anomaly detection

#### CveMatchCheck.cs
- **Input:** Outputs from all preceding checks
- **Method:** Load definition pack via `DefinitionLoader` → iterate `SoftwareCveRule[]` → match installed software by name + version range
- **Produces:** `Finding[]` with `id`, `severity`, `component`, `status`, `remediation_id`
- **Version matching:** Semantic version comparison — no external NuGet. Implemented in `Definitions/SemVer.cs`.

#### RogueProcessCheck.cs
- **Method:** `Win32_Process` WMI query
- **IOC list source:** `Definitions/packs/ot-cve-pack-*.json` → `process_iocs[]` field
- **Extracts:** Running process names + exe paths
- **Flag:** Name or path hash matches IOC list → `Finding` with severity HIGH, status `EXPLOITABLE`

#### PlcNeighborCheck.cs (conditional)
- **Trigger condition:** `NetworkInterface.GetAllNetworkInterfaces()` finds active non-loopback adapter
- **Step 1:** Passive ARP sweep via SharpPcap — listen-only, no probe packets sent
- **Step 2:** For each live host — attempt banner grab on PLC-specific ports:

  | Protocol | Port | Library |
  |---|---|---|
  | Siemens S7 | TCP 102 | Raw socket ISO-TSAP + S7comm handshake |
  | Modbus TCP | TCP 502 | Raw socket Modbus read-device-id (FC 43) |
  | Mitsubishi MELSEC | TCP 5007 | Raw TCP banner read |
  | Toyopuc | TCP 1024 | Raw TCP banner read |

- **Read-only guarantee:** No write function codes. Function code 43 subfunction 14 (Read Device Identification) only.
- **Timeout:** 3 seconds per host per port
- **Output:** `PlcNeighbor { ip, banner, open_ports[] }`

### Progress Reporting

```
ProgressReporter
  ├── If OLED detected (via /dev/i2c or custom driver signal) → draw progress bar
  └── Else → Console.Write progress ticker (visible if console window present)
```

Progress ticks after each check completes (10 ticks = 10 checks). Final tick = file written.

---

## 6. VaxDock — Laptop Application

### Technology

| Property | Value |
|---|---|
| Framework | WPF .NET 8 (`net8.0-windows`) |
| Database | SQLite via `Microsoft.Data.Sqlite` |
| SQL style | Raw parameterized SQL — **no ORM, no Dapper** |
| Network | None — fully air-gapped |

### Drive Detection Pipeline

```
DriveDetector (background service, runs on app start)
  │
  ├── Poll DriveInfo.GetDrives() every 2 seconds
  ├── Match volume label == "VAXDRIVE"
  │
  └── On detect → IngestPipeline.RunAsync(drivePath)
        │
        ├── Enumerate /results/*.vax
        ├── For each .vax file:
        │     ├── VaxDecryptor.Decrypt(file, hardwareKey)  → raw JSON bytes
        │     ├── HmacVerifier.Verify(jsonBytes, signature) → bool
        │     │     └── FAIL → quarantine file → log → skip (NEVER silent ingest)
        │     └── PASS → deserialize ScanResult → ScanRepository.Upsert()
        │
        └── Emit IngestCompleted event → refresh Dashboard
```

### Database Schema

```sql
-- DatabaseBootstrap.cs creates these on first run

CREATE TABLE IF NOT EXISTS Devices (
    Id              TEXT PRIMARY KEY,   -- device_fingerprint (stable identifier)
    LastSeen        TEXT NOT NULL,      -- ISO 8601
    OsVersion       TEXT,
    BiosString      TEXT
);

CREATE TABLE IF NOT EXISTS Scans (
    ScanId          TEXT PRIMARY KEY,   -- UUID from .vax
    DeviceId        TEXT NOT NULL REFERENCES Devices(Id),
    Timestamp       TEXT NOT NULL,      -- ISO 8601 UTC
    PatchLevel      TEXT,
    RawJson         TEXT NOT NULL,      -- Full scan JSON (compressed)
    Quarantined     INTEGER DEFAULT 0,  -- 1 = HMAC failed
    IngestedAt      TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS Findings (
    Id              INTEGER PRIMARY KEY AUTOINCREMENT,
    ScanId          TEXT NOT NULL REFERENCES Scans(ScanId),
    DeviceId        TEXT NOT NULL,
    CveId           TEXT NOT NULL,
    Severity        TEXT NOT NULL,      -- CRITICAL|HIGH|MEDIUM|LOW
    Component       TEXT NOT NULL,
    Status          TEXT NOT NULL,      -- EXPLOITABLE|PATCH_AVAILABLE|MITIGATED
    RemediationId   TEXT,
    ResolvedAt      TEXT,               -- NULL = open
    EscalatedAt     TEXT
);

CREATE TABLE IF NOT EXISTS PlcNeighbors (
    Id              INTEGER PRIMARY KEY AUTOINCREMENT,
    ScanId          TEXT NOT NULL REFERENCES Scans(ScanId),
    Ip              TEXT NOT NULL,
    Banner          TEXT,
    OpenPorts       TEXT               -- JSON array stored as text
);

CREATE TABLE IF NOT EXISTS UsbAnomalies (
    Id              INTEGER PRIMARY KEY AUTOINCREMENT,
    ScanId          TEXT NOT NULL REFERENCES Scans(ScanId),
    DeviceId        TEXT NOT NULL,
    Description     TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS QuarantinedFiles (
    Id              INTEGER PRIMARY KEY AUTOINCREMENT,
    Filename        TEXT NOT NULL,
    FailureReason   TEXT NOT NULL,
    DetectedAt      TEXT NOT NULL
);

-- Indexes for common query patterns
CREATE INDEX IF NOT EXISTS idx_findings_device ON Findings(DeviceId);
CREATE INDEX IF NOT EXISTS idx_findings_severity ON Findings(Severity);
CREATE INDEX IF NOT EXISTS idx_scans_device ON Scans(DeviceId, Timestamp DESC);
```

### Views — Specification

#### Dashboard.xaml
- **Left panel:** Device list — sorted by severity (CRITICAL first)
- **Per device row:** DeviceId · LastScan date · CRITICAL count · HIGH count · Status badge
- **Status badge logic:**
  - 🔴 RED: Any CRITICAL finding OR overdue scan
  - 🟡 AMBER: HIGH findings, no CRITICAL, scan within window
  - 🟢 GREEN: No open findings, scan within window
- **Top bar:** Total devices · Total CRITICAL · New since last session · Drive ingest status
- **Cadence alert banner:** "X devices not scanned in 7 days" — dismissible per session

#### DeviceDetail.xaml
- **Header:** DeviceId · OS · BIOS string · Last scan timestamp
- **Findings table:** CVE ID · Severity · Component · Status · Remediation link
- **USB anomalies list:** Unknown VID entries with first/last seen dates
- **Open ports list:** Port number · Protocol · PID (if captured)
- **PLC Neighbors panel:** IP · Banner · Open ports — visible only if PlcNeighbors present
- **Trend mini-chart:** CRITICAL/HIGH count per scan date (last 12 scans)

#### RemediationCard.xaml
- **Triggered by:** Clicking a finding row
- **Header:** CVE ID · Severity badge · Component
- **Body:** Pre-authored plain-language steps (sourced from definitions pack `remediation_id` → `RemediationCard` JSON)
- **Actions:**
  - `[Mark Resolved]` → sets `Findings.ResolvedAt = UTC now`, updates badge
  - `[Escalate to Supervisor]` → sets `Findings.EscalatedAt`, exports single finding to PDF
  - `[Export]` → full device report PDF/CSV

#### TrendView.xaml
- **Scope:** Single device, configurable date range
- **Chart:** CRITICAL + HIGH counts per scan cycle (line chart — no third-party charting lib; drawn via WPF `Path`/`Polyline` on `Canvas`)
- **Annotation:** Mark Resolved events shown as green dots on timeline
- **Purpose:** Show security posture improving or degrading over scan cycles

### Cadence Tracking Logic

```csharp
// CadenceTracker.cs
public IEnumerable<DeviceCadenceAlert> GetOverdueDevices(int maxDaysSinceLastScan)
{
    // Raw SQL — parameterized
    const string sql = @"
        SELECT d.Id, d.LastSeen,
               julianday('now') - julianday(d.LastSeen) AS DaysSince
        FROM Devices d
        WHERE julianday('now') - julianday(d.LastSeen) > @threshold
        ORDER BY DaysSince DESC";
    // ... execute with @threshold = maxDaysSinceLastScan
}
```

Default threshold: 7 days (configurable in `appsettings.json` — no hardcoding).

### Export Service

```
ExportService
  ├── ExportDevicePdf(deviceId)     → uses WPF PrintDialog + FlowDocument render
  └── ExportDeviceCsv(deviceId)     → StreamWriter, RFC 4180 compliant
```

No external PDF library. WPF built-in `XpsDocument` → convert to PDF via Windows print infrastructure.

---

## 7. Cryptography Specification

**Library:** `System.Security.Cryptography` — built-in. **No external crypto libraries.**

### Key Derivation

```
Drive hardware token  →  raw bytes (IronKey SDK or Kingston API — see OPEN-1)
        │
        └──→  HKDF-SHA256(secret=token_bytes, info="VAXDRIVE-V1", length=32)
                        │
                        └──→  AES-256-GCM encryption key (per-file)
```

Until OPEN-1 is resolved: key derivation uses a **hardware-bound stub** — reads a unique token file from the `/boot` partition signed by the drive at manufacture time. Production replaces this with the drive SDK call.

### .vax File Encryption (VaxAgent side)

```
1. Serialize ScanResult → UTF-8 JSON bytes
2. Generate random 96-bit nonce  (RandomNumberGenerator.GetBytes(12))
3. AesGcm.Encrypt(key, nonce, plaintext, ciphertext, tag)
4. Compute HMAC-SHA256(key=hmac_key, data=nonce || ciphertext || tag)
   where hmac_key = HKDF-SHA256(token_bytes, info="VAXDRIVE-HMAC-V1")
5. Write .vax file:
   [ 4 bytes: magic "VAX1" ]
   [ 12 bytes: nonce ]
   [ 16 bytes: GCM auth tag ]
   [ 4 bytes: ciphertext length (big-endian uint32) ]
   [ N bytes: ciphertext ]
   [ 32 bytes: HMAC-SHA256 ]
```

### .vax File Decryption + Verify (VaxDock side)

```
1. Read file — validate magic "VAX1"
2. Extract nonce (bytes 4-15), tag (bytes 16-31), length (bytes 32-35), ciphertext, hmac
3. Recompute HMAC-SHA256 over nonce || ciphertext || tag
4. Constant-time compare with stored HMAC
   └── MISMATCH → QuarantineFile() → log → DO NOT PROCEED
5. AesGcm.Decrypt(key, nonce, ciphertext, tag, plaintext)
6. Deserialize JSON → ScanResult
```

**HMAC is verified BEFORE decryption.** This prevents oracle attacks and ensures integrity check is never skipped.

### Key Storage Principle

```
VaxAgent:  key derived at runtime from drive hardware token — never written to host
VaxDock:   same derivation — drive must be present to decrypt
At rest:   key exists nowhere — derived on demand — drive = key material
```

---

## 8. .vax File Format

### Schema

```json
{
  "scan_id": "a3f9b2c1-...",
  "device_fingerprint": "PLANT-HMI-07",
  "timestamp": "2026-06-12T09:14:22Z",
  "os": "Windows 7 Embedded SP1",
  "patch_level": "KB2999226 missing",
  "findings": [
    {
      "id": "CVE-2017-0144",
      "severity": "CRITICAL",
      "component": "SMBv1",
      "status": "EXPLOITABLE",
      "remediation_id": "REM-SMB-001"
    }
  ],
  "usb_anomalies": [
    "Unknown VID:0x1234 PID:0x5678 first seen 2026-06-01"
  ],
  "open_ports": [445, 139, 3389],
  "plc_neighbors": [
    {
      "ip": "192.168.10.44",
      "banner": "Siemens S7-300 v3.2.1",
      "open": [102]
    }
  ],
  "signature": "HMAC:9c3a7f..."
}
```

### Severity Enum

| Value | Meaning |
|---|---|
| `CRITICAL` | CVSS ≥ 9.0 — immediate risk |
| `HIGH` | CVSS 7.0–8.9 — elevated risk |
| `MEDIUM` | CVSS 4.0–6.9 — addressable in next window |
| `LOW` | CVSS < 4.0 — informational |

### Status Enum

| Value | Meaning |
|---|---|
| `EXPLOITABLE` | Vulnerable, no patch, or patch not applied |
| `PATCH_AVAILABLE` | Patch exists, not yet applied |
| `MITIGATED` | Vulnerable version present but compensating control found |

### Device Fingerprint Construction

```csharp
// Stable across reboots — does not change unless hardware changes
string fingerprint = SHA256(
    Win32_ComputerSystem.Name +
    Win32_BIOS.SerialNumber +
    Win32_OperatingSystem.InstallDate
).ToHex()[..16];
```

This becomes the filename component `DEVICE-HASH` and the SQLite `Devices.Id`.

---

## 9. Definitions Pack

### Purpose

The OT CVE pack is the intelligence layer consumed by:
1. `CveMatchCheck` — matches installed software against known-vulnerable version ranges
2. `UsbHistoryCheck` — known-good USB VID allowlist
3. `RogueProcessCheck` — IOC process name list
4. `VaxDock RemediationCard` — pre-authored plain-language fix steps per CVE

### Pack Scope

- **NOT** the full NVD. OT-scoped subset only.
- Focus: Windows XP/7/Embedded CVEs, HMI software CVEs, SCADA platform CVEs, ICS protocol vulnerabilities.
- Approximate target: top 200–500 OT-relevant CVEs per weekly pack.

### Pack Format

```json
{
  "pack_version": "2026-W24",
  "generated": "2026-06-09T00:00:00Z",
  "signature": "PACK-HMAC:...",
  "software_cve_rules": [
    {
      "id": "CVE-2017-0144",
      "severity": "CRITICAL",
      "component": "SMBv1",
      "match": {
        "type": "os_feature",
        "feature": "SMBv1",
        "detection": "registry_key",
        "key": "HKLM\\SYSTEM\\CurrentControlSet\\Services\\LanmanServer\\Parameters",
        "value": "SMB1",
        "match_value": "1"
      },
      "status": "EXPLOITABLE",
      "remediation_id": "REM-SMB-001"
    },
    {
      "id": "CVE-2019-0708",
      "severity": "CRITICAL",
      "component": "Remote Desktop Services",
      "match": {
        "type": "missing_patch",
        "kb": "KB4499175"
      },
      "status": "PATCH_AVAILABLE",
      "remediation_id": "REM-RDP-001"
    }
  ],
  "remediation_cards": [
    {
      "id": "REM-SMB-001",
      "title": "Disable SMBv1",
      "steps": [
        "Open Control Panel",
        "Click Programs → Turn Windows features on or off",
        "Uncheck 'SMB 1.0/CIFS File Sharing Support'",
        "Click OK and reboot",
        "Verify: open Command Prompt and run: sc query mrxsmb10"
      ]
    }
  ],
  "usb_allowlist": [
    { "vid": "0x046D", "description": "Logitech" },
    { "vid": "0x045E", "description": "Microsoft" }
  ],
  "process_iocs": [
    { "name": "mimikatz.exe", "severity": "CRITICAL" },
    { "name": "psexec.exe", "severity": "HIGH" }
  ]
}
```

### Pack Signing

```
build-pack.ps1:
  1. Assemble JSON pack
  2. HMAC-SHA256(pack_bytes, pack_signing_key) → embed as "signature" field
  3. Output: ot-cve-pack-YYYY-WNN.json

verify-pack.ps1 / DefinitionLoader.cs:
  1. Read pack
  2. Extract + recompute HMAC
  3. MISMATCH → abort scan with error logged to /logs/
```

Pack signing key: stored in `.env` / CI secret — never committed.

### Update Cadence

- Weekly on Mondays (automated CI job)
- Drive updated by security team before each field round
- `DefinitionLoader` checks `pack_version` on launch — warns (does not abort) if pack > 14 days old

---

## 10. Launcher Subsystem

### Primary: AutoHotkey HID Stub

```
/Launcher/ahk/VaxLauncher.ahk

Flow:
  Device presents as HID keyboard + mass storage
  On plug-in → OS assigns drive letter
  AHK stub executes:
    1. Send Win+R  (open Run dialog)
    2. Wait 500ms
    3. Type drive letter + "\engine\VaxAgent.exe"
    4. Send Enter
    5. AHK process exits
```

Compiled to `VaxLauncher.exe` — committed to repo as build artifact.

**Limitations:**
- Group Policy `HID Keyboard Block` → falls back to button
- Windows Defender SmartScreen on first run → covered by code signing
- UAC prompt → VaxAgent requests no elevation, prompt should not appear

### Fallback: Physical SCAN Button

```
/Launcher/attiny85/vax_hid.ino

ATtiny85 microcontroller on custom PCB:
  - Physical button on drive casing labelled "SCAN"
  - Press → ATtiny85 sends identical HID keystroke sequence
  - USB device descriptor: composite HID + mass storage
```

Decision: **OPEN-2** — budget and target environment determine which ships.

---

## 11. Data Flow — End to End

```
┌─────────────────────────────────────────────────────────────────────────────┐
│  FIELD (target device)                                                      │
│                                                                             │
│  Staff plugs VaxDrive USB                                                   │
│       │                                                                     │
│       ▼                                                                     │
│  HID stub fires → VaxAgent.exe launches from /engine                       │
│       │                                                                     │
│       ▼                                                                     │
│  SelfVerifier.VerifyBinaryIntegrity() ── FAIL → exit + log               │
│       │ PASS                                                                │
│       ▼                                                                     │
│  DefinitionLoader.Load(/definitions/ot-cve-pack-*.json)                    │
│       │  Verify pack HMAC ── FAIL → warn, proceed with last-known-good     │
│       │                                                                     │
│       ▼                                                                     │
│  ScanOrchestrator.RunAll()  (sequential, 90s budget)                       │
│    OsCheck → SoftwareCheck → ServicesCheck → PortsCheck                    │
│    → UsbCheck → TasksCheck → FirmwareCheck → CveMatch → RogueProcess       │
│    → PlcNeighbors (conditional)                                             │
│       │                                                                     │
│       ▼                                                                     │
│  VaxFileWriter                                                              │
│    serialize JSON → AES-256-GCM encrypt → HMAC-SHA256 sign                 │
│    write SCAN_[HASH]_[TS].vax → /results                                   │
│    append to /logs/audit.log                                                │
│       │                                                                     │
│       ▼                                                                     │
│  ProgressReporter → "DONE" (OLED or console)                               │
│                                                                             │
│  Staff unplugs drive                                                        │
└─────────────────────────────────────────────────────────────────────────────┘
                        │ (physical transport, later)
                        ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│  SECURITY LAPTOP (VaxDock)                                                  │
│                                                                             │
│  Staff plugs VaxDrive into security laptop                                  │
│       │                                                                     │
│       ▼                                                                     │
│  DriveDetector detects "VAXDRIVE" volume label                              │
│       │                                                                     │
│       ▼                                                                     │
│  IngestPipeline.RunAsync()                                                  │
│    For each /results/*.vax:                                                 │
│      VaxDecryptor.Decrypt() → raw JSON                                      │
│      HmacVerifier.Verify() ── FAIL → QuarantinedFiles table + alert UI     │
│        │ PASS                                                               │
│        └─→ ScanRepository.Upsert(scanResult)                               │
│            DeviceRepository.Upsert(device)                                 │
│            FindingRepository.BulkInsert(findings)                          │
│       │                                                                     │
│       ▼                                                                     │
│  Dashboard refreshes — new scans highlighted                                │
│  CadenceTracker re-evaluates all devices                                    │
│                                                                             │
│  User triages → RemediationCard → Mark Resolved / Escalate / Export        │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 12. Threat Model

| Threat | Attack Scenario | VaxDrive Coverage |
|---|---|---|
| Unpatched legacy devices | CVEs on XP/Win7 HMIs active in the wild | OS/patch enum + CVE match every scan |
| Insider threat / rogue USB | Employee plugs unauthorized USB — persistence or exfil | USBSTOR registry → unknown VIDs flagged |
| External lateral movement | Attacker pivoting via open SMB/RDP ports | Port snapshot + rogue process IOC list |
| Supply chain / firmware | Compromised BIOS or PLC firmware installed | BIOS string + PLC banner grab |
| Drive weaponization | Attacker attempts to corrupt or weaponize VaxDrive | AES-256 XTS onboard + no host writes + signed binaries |
| Result tampering | Attacker flips bits in .vax to hide findings | HMAC-SHA256 per file — any corruption = quarantine |
| Malicious definition pack | Corrupted CVE pack injected | Pack HMAC verified on every load |
| VaxAgent binary swap | Agent binary replaced with malicious payload | Self-verification against signed manifest on launch |

### Out of Scope (by design)

- Active exploitation
- PLC logic modification
- Patch deployment (remediation is human-executed)
- Real-time monitoring (scan-based, not continuous)

---

## 13. Engineering Rules — Non-negotiable

These are enforced via code review and CI checks. No exceptions.

| Rule | Enforcement |
|---|---|
| Type hints on ALL function signatures | Roslyn analyzer — CS8600 warnings as errors |
| Zero hardcoded credentials | `SecretScanner` CI step (gitleaks) |
| Zero ORMs — raw parameterized SQL only | Code review gate |
| Zero writes to target host filesystem | VaxAgent output path validated to drive letter at startup |
| Zero network calls from VaxAgent | `HttpClient` not referenced in VaxAgent.csproj |
| All drive binaries code-signed | CI sign step — unsigned binary fails deploy |
| VaxAgent self-verifies binary hash on launch | `SelfVerifier` runs before all checks — not bypassable |
| HMAC fail = quarantine — never silent ingest | `HmacVerifier` returns void; throws `HmacVerificationException` caught at pipeline level |
| Single repo shared Models and Crypto | Project references in `.csproj` — not NuGet |
| No full NVD pull | Definition pack build script scope-gates to OT CVE list |

---

## 14. Open Decisions

Items marked **OPEN** require investigation before the relevant phase starts.

| ID | Phase Blocks | Topic | Decision Needed |
|---|---|---|---|
| **OPEN-1** | Phase 2 | Hardware token key derivation | IronKey SDK vs Kingston API vs fallback boot-partition token. Investigate per drive model. Fallback: signed token file approach documented in §7. |
| **OPEN-2** | Phase 6 | ATtiny85 vs AutoHotkey HID | Budget + target-environment determination. AHK stub ships first as phase 6 deliverable. ATtiny85 is custom PCB — separate hardware sprint. |
| **OPEN-3** | Phase 7 | OLED display | Custom hardware decision. Off-shelf drives use console progress bar fallback. `ProgressReporter` abstracts both paths — no code change needed either way. |
| **OPEN-4** | Post-v1 | VaxDock + SentryShield merge | Keep clean boundary for v1. Compatible architecture. Merge via shared Models NuGet package when boundary is ready to cross. |

---

## 15. Dependency Registry

All dependencies must be approved here before use. No transitive surprises.

### VaxAgent Dependencies

| Package | Version | Purpose | Source |
|---|---|---|---|
| `SharpPcap` | Latest stable | Passive network capture for PLC ARP sweep | NuGet |
| `System.Security.Cryptography` | BCL | AES-GCM + HMAC-SHA256 | .NET BCL — no NuGet |
| `System.Management` | BCL / NuGet (net8) | WMI access | NuGet for net8 target |
| `Microsoft.Win32.Registry` | BCL | Registry reads | .NET BCL |

> ⚠️ `SharpPcap` requires WinPcap / Npcap on target — investigate whether Npcap can be bundled on `/engine` partition or if raw socket fallback is needed for locked HMIs.

### VaxDock Dependencies

| Package | Version | Purpose | Source |
|---|---|---|---|
| `Microsoft.Data.Sqlite` | Latest stable | SQLite access | NuGet |
| `System.Security.Cryptography` | BCL | AES-GCM + HMAC-SHA256 | .NET BCL |
| WPF | BCL | UI framework | `net8.0-windows` BCL |

### Test Dependencies

| Package | Version | Purpose |
|---|---|---|
| `xunit` | Latest stable | Unit test framework |
| `xunit.runner.visualstudio` | Latest stable | VS Test Explorer |
| `Microsoft.NET.Test.Sdk` | Latest stable | Test host |
| `Moq` | Latest stable | Mocking for check interfaces |

### Explicitly Prohibited

| Package | Reason |
|---|---|
| Entity Framework Core | ORM — prohibited |
| Dapper | ORM-adjacent — prohibited |
| BouncyCastle / NSec | External crypto — use BCL only |
| Newtonsoft.Json | Use `System.Text.Json` (BCL) |
| Any telemetry SDK | Phones home — prohibited |

---

*Document maintained by the VaxDrive engineering team. Update this file when any architectural decision changes. Do not let this drift from the code.*
