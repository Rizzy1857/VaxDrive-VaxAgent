# VAXDRIVE
> **Portable OT Vulnerability Assessment** — Offline · Resilient · Low-Forensic-Footprint

[![status](https://img.shields.io/badge/status-production-brightgreen)]()
[![version](https://img.shields.io/badge/version-v3.7.0-blue)]()
[![stack](https://img.shields.io/badge/stack-.NET%208%20%7C%20WPF%20%7C%20SQLite-blue)]()
[![security](https://img.shields.io/badge/crypto-AES--256--GCM%20%2B%20HMAC--SHA256-green)]()
[![target](https://img.shields.io/badge/target-OT%20%7C%20ICS%20%7C%20HMI-red)]()

---

## What Is It

A hardened USB drive running a self-contained assessment scanner.  
Plugs into Windows / HMI devices on the plant floor.  
Fingerprints the device. Writes encrypted results to itself. No persistence. No registry modifications. No configuration changes. Any artifacts are limited to normal operating-system execution telemetry.  
Comes home to a companion Windows app for triage, remediation, and reporting.

**Operated by floor staff. Portable asset assessment. Done.**

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
| Launch method | Explicit operator execution from the drive |

> **Operator-Driven.** The drive does not use autonomous HID spoofing or hardware injection in order to remain safe and compliant in strict industrial environments. An operator simply plugs it in and explicitly runs the scanner.

### Drive Partition Layout

```
VAXDRIVE/
├── /engine       → VaxAgent.exe (signed, self-verifying, single-file binary)
├── /definitions  → OT-scoped CVE/signature pack (weekly-updated, signed)
├── /results      → Encrypted JSON scan outputs (write-only from agent)
└── /logs         → Tamper audit log
```

---

## VaxAgent.exe — Scanner Binary

**Resilient Collection. No installs. No registry writes. No network calls. Runs from drive in memory.**

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

### Features

| Feature | Description |
|---|---|
| Comprehensive NVD Sync | Pulls and deduplicates the latest OT-specific vulnerabilities (Siemens, Mitsubishi, Toyopuc, Windows) directly from NIST NVD API. |
| Resilient Collection | If definition checks fail or expire, baseline host inventory is still reliably gathered. |
| Scan Completeness | Reports exact module failures so analysts know precisely how much of the target was actually covered (e.g. `80% Complete`). |
| Dynamic Risk Score | Normalized scoring based on operator-assigned Asset Criticality (`CRITICAL` down to `LOW`). |
| Mandatory Suppression Expiry | Security findings cannot be ignored forever. Suppressions enforce a 30-day default expiry to prevent silent decay. |
| Assessment Status | Gives immediate visual feedback on scan freshness, definitions age, and risk. |
| Zero network | Fully air-gapped agent operations. Dock only uses network for NVD syncs. |

---

## Threat Model

| Threat | Coverage |
|---|---|
| Unpatched legacy devices | OS/patch enumeration + CVE match every scan |
| Insider threat / rogue USB | USBSTOR registry read → unknown VIDs flagged |
| External lateral movement | Open port snapshot + rogue process heuristics |
| Supply chain / firmware | BIOS string capture + PLC banner grab via network |
| Drive weaponization | Hardware encryption + HID-only on target, no host write (Development builds derive keys from device.token. Production builds will use hardware-backed key derivation.) |
| Result tampering | HMAC-SHA256 per scan file, verified on every import |

---

## Build Stack

| Component | Tech |
|---|---|
| VaxAgent.exe | C# .NET 8, single-file self-contained publish |
| XP/legacy fallback | .NET 3.5 conditional compilation build target |
| Encryption / signing | `System.Security.Cryptography` — AES-GCM + HMAC-SHA256 |
| VaxDock app | WPF .NET 8 |
| Local database | SQLite via `Microsoft.Data.Sqlite` — parameterized raw SQL only |
| CVE definitions | OT-scoped JSON pack (NVD API integrated) |

---

## Repo Structure

```
vaxdrive/
├── VaxAgent/                  # Scanner binary (C# .NET 8 / 3.5 conditional)
├── VaxDock/                   # Laptop app (WPF .NET 8)
├── Definitions/               # CVE/signature pack source + build scripts
├── Tests/                     # xUnit test suites
├── .gitignore
├── .env.example               # No keys hardcoded — env vars only
└── README.md
```

---

## Security Constraints

- **No credentials hardcoded** — all keys via environment variables or hardware token derivation
- **No intentional writes to target host** — results to drive only, acknowledging unavoidable OS-level artifacts (Prefetch, Event ID 4688, WMI traces)
- **No network calls from agent** — fully air-gapped design, zero telemetry
- **No external ORMs** — parameterized raw SQL throughout VaxDock
- **Signed binaries** — all drive-resident executables code-signed; agent self-verifies on launch
- **HMAC integrity** — every `.vax` file verified before import; any corruption = quarantine

---

## What This Is Not

- Not a network scanner (not Nessus-lite)
- Not an active exploit framework
- Does not touch PLC logic, ladder diagrams, or process memory
- Does not intentionally write files to target devices (native OS logging excluded)
- Does not phone home

---

## Naming

| Name | What It Is |
|---|---|
| **VaxDrive** | The physical USB drive |
| **VaxAgent** | Scanner binary living on the drive |
| **VaxDock** | Laptop app — where the drive comes home to report |

> Periodic offline rounds. Plug in. Run. Review. Action.  
> A highly-focused assessment workflow for OT machines that can never go offline for a full audit.
