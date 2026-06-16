# VAXDRIVE
> **Portable OT Vulnerability Vaccine** — Passive · Offline · Zero-write · Cryptographically sealed

[![status](https://img.shields.io/badge/status-production-brightgreen)]()
[![version](https://img.shields.io/badge/version-v3.7.0-blue)]()
[![stack](https://img.shields.io/badge/stack-.NET%208%20%7C%20WPF%20%7C%20SQLite-blue)]()
[![security](https://img.shields.io/badge/crypto-AES--256--GCM%20%2B%20HMAC--SHA256-green)]()
[![target](https://img.shields.io/badge/target-OT%20%7C%20ICS%20%7C%20HMI-red)]()

---

## What Is It

A hardened USB drive running a self-contained passive scanner.  
Plugs into Windows / HMI devices on the plant floor.  
Fingerprints the device. Writes encrypted results to itself. Leaves zero trace.  
Comes home to a companion Windows app for triage, remediation, and reporting.

**Operated by non-technical floor staff. One plug. 90 seconds. Done.**

---

## The Two Halves

```
┌─────────────────────────────┐     plug into laptop     ┌──────────────────────────────┐
│       HALF 1 — VaxDrive     │ ────────────────────────▶ │      HALF 2 — VaxDock        │
│       (field USB device)    │    decrypt + verify HMAC   │     (security laptop app)    │
└─────────────────────────────┘                           └──────────────────────────────┘
```

---

## HALF 1 — The Drive

### Hardware Baseline

| Property | Spec |
|---|---|
| Encryption | AES-256 XTS onboard (IronKey S1000 / Kingston Vault Privacy 80 class) |
| Host write access | None — drive is read-only to host by default |
| Agent write access | `/results` partition only |
| Launch method | USB HID keyboard spoof (types hotkey on plug-in) OR physical SCAN button fallback |

> **BadUSB used defensively.** On plug-in the drive presents as HID + mass storage. Types a pre-staged hotkey that launches `VaxAgent.exe` from the drive. No operator action beyond plugging it in. For hardened environments: physical button on casing labelled **SCAN**.

### Drive Partition Layout

```
VAXDRIVE/
├── /boot         → Autorun shim + HID launcher (read-only, signed)
├── /engine       → VaxAgent.exe (signed, self-verifying, single-file binary)
├── /definitions  → OT-scoped CVE/signature pack (weekly-updated, signed)
├── /results      → Encrypted JSON scan outputs (write-only from agent)
└── /logs         → Tamper audit log
```

---

## VaxAgent.exe — Scanner Binary

**Passive. Read-only. No installs. No registry writes. No network calls. Runs from drive in memory.**

### Windows Checks (XP / 7 / Embedded / 10 / 11)

| Check | Method | Elevation Needed |
|---|---|---|
| OS version + patch level | `WMI Win32_OperatingSystem` | No |
| Installed software | `HKLM\SOFTWARE\...\Uninstall` registry read | No |
| Running services | `sc query` via WMI | No |
| Open ports | `netstat -ano` snapshot | No |
| USB history / insider threat | `HKLM\SYSTEM\CurrentControlSet\Enum\USBSTOR` | No |
| Scheduled tasks | `schtasks /query` | No |
| Firmware / BIOS string | `Win32_BIOS` via WMI | No |
| CVE match | Local definition pack cross-reference | No |
| Rogue process heuristics | Known-bad process name list (lateral movement IOCs) | No |

### PLC-Adjacent / Networked HMI Checks

- Passive ARP sweep of local subnet (if networked node detected)
- **Read-only** Modbus/S7comm banner grab → firmware version strings, open service fingerprints
- Supports: Siemens S7, Mitsubishi MELSEC, Toyopuc
- No direct USB-to-PLC connection (PLCs have no USB host)

### Scan Output

**Duration:** 90 seconds flat. Progress bar on embedded OLED display or console window.

Each scan writes one file:
```
/results/SCAN_[DEVICE-HASH]_[TIMESTAMP].vax
```

`.vax` = AES-256-GCM encrypted JSON, key derived from drive hardware token.  
Each file is HMAC-SHA256 signed. Any bit flip = invalid on import.

---

## HALF 2 — VaxDock (Laptop App)

Standalone WPF app. Separate repo, clean boundary from other tooling.

### On Drive Plug-in to Laptop

1. Auto-detects drive via volume label `VAXDRIVE`
2. Decrypts all `.vax` files using drive hardware key
3. Verifies every HMAC signature (any failure = quarantined, flagged)
4. Ingests into local SQLite database

### Features

| Feature | Description |
|---|---|
| Comprehensive NVD Sync | Pulls and deduplicates the latest OT-specific vulnerabilities (Siemens, Mitsubishi, Toyopuc, Windows) directly from NIST NVD API. |
| Remediation cards | Pre-authored per CVE — plain language, operator-readable, no jargon |
| Cadence tracking | Flags devices not scanned within configured window (weekly/monthly) |
| Trend view | Per-device vuln history across scan cycles — posture improving or degrading |
| Export | PDF / CSV report for supervisor or audit trail |
| Zero network | Fully air-gapped agent operations. Dock only uses network for NVD syncs. |

---

## Threat Model

| Threat | Coverage |
|---|---|
| Unpatched legacy devices | OS/patch enumeration + CVE match every scan |
| Insider threat / rogue USB | USBSTOR registry read → unknown VIDs flagged |
| External lateral movement | Open port snapshot + rogue process heuristics |
| Supply chain / firmware | BIOS string capture + PLC banner grab via network |
| Drive weaponization | Hardware encryption + HID-only on target, no host write |
| Result tampering | HMAC-SHA256 per scan file, verified on every import |

---

## Build Stack

| Component | Tech |
|---|---|
| VaxAgent.exe | C# .NET 8, single-file self-contained publish |
| XP/legacy fallback | .NET 3.5 conditional compilation build target |
| Encryption / signing | `System.Security.Cryptography` — AES-GCM + HMAC-SHA256 |
| PLC banner grab | SharpPcap passive + raw TCP S7/Modbus probe (read-only) |
| VaxDock app | WPF .NET 8 |
| Local database | SQLite via `Microsoft.Data.Sqlite` — parameterized raw SQL only |
| CVE definitions | OT-scoped JSON pack (NVD API integrated) |
| HID launcher | AutoHotkey-compiled stub OR ATtiny85 microcontroller on custom drive |

---

## Repo Structure

```
vaxdrive/
├── VaxAgent/                  # Scanner binary (C# .NET 8 / 3.5 conditional)
├── VaxDock/                   # Laptop app (WPF .NET 8)
├── Definitions/               # CVE/signature pack source + build scripts
├── Launcher/                  # HID stub source (AHK or ATtiny85 firmware)
├── Tests/                     # xUnit test suites
├── .gitignore
├── .env.example               # No keys hardcoded — env vars only
└── README.md
```

---

## Security Constraints

- **No credentials hardcoded** — all keys via environment variables or hardware token derivation
- **No writes to target host** — agent runs in-memory, results to drive only
- **No network calls from agent** — fully air-gapped design, zero telemetry
- **No external ORMs** — parameterized raw SQL throughout VaxDock
- **Signed binaries** — all drive-resident executables code-signed; agent self-verifies on launch
- **HMAC integrity** — every `.vax` file verified before import; any corruption = quarantine

---

## What This Is Not

- Not a network scanner (not Nessus-lite)
- Not an active exploit framework
- Does not touch PLC logic, ladder diagrams, or process memory
- Does not write anything to target devices
- Does not phone home

---

## Naming

| Name | What It Is |
|---|---|
| **VaxDrive** | The physical USB drive |
| **VaxAgent** | Scanner binary living on the drive |
| **VaxDock** | Laptop app — where the drive comes home to report |

> Weekly or monthly rounds. Plug in. Pull out. Review. Fix. Repeat.  
> A vaccination schedule for machines that can never go offline for a full audit.
