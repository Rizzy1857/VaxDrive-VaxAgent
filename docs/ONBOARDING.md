# VaxDrive Developer Onboarding

Welcome to the VaxDrive ecosystem! This guide will systematically orient you to the project's unique constraints, physical hardware integration, and the overarching architecture required for Operating Technology (OT) deployments.

---

## 1. Core Philosophy

Developing for heavy industry (Power Plants, Manufacturing, Utilities) requires a completely different mindset than developing for the modern web.

1. **Air-Gapped First**: We assume absolute zero internet connectivity. No cloud APIs, no external telemetry, no remote crash reporting.
2. **Zero Installation**: HMIs (Human Machine Interfaces) are highly regulated. You cannot run traditional installers. The `VaxAgent` must execute strictly from the USB `VAXDRIVE` volume and leave absolutely no footprint behind.
3. **No ORMs or UI Magic**: We use raw parameterized SQL (via `Microsoft.Data.Sqlite`) in `VaxDock`. This provides absolute control over memory performance and lock contention via SQLite's WAL mode. We do not use Entity Framework.

---

## 2. System Architecture

The VaxDrive system is composed of three interconnected nodes, each handling a distinct phase of the pipeline:

### Node A: Hardware Path (ATtiny85 Emulator)
- **Role**: Bypasses dead Windows AutoRun features on modern operating systems.
- **Mechanism**: Plugs in as a fake USB keyboard, waits 3 seconds, opens the `Win+R` dialog, and types a raw `cmd` loop.
- **Execution**: The loop scans drives `D:` through `Z:` searching for a `.vaxdrive` marker file. When it finds it, it silently launches the agent payload.

### Node B: VaxAgent (C# Console Payload)
- **Role**: Gathers vulnerabilities, network data, and processes from the target machine.
- **Mechanism**: Dual-targeted for both modern (`net8.0`) and legacy (`net3.5` for Windows XP/7) platforms. It performs non-intrusive WMI queries, pure registry reads, and passive `SharpPcap` ARP sweeps.
- **Output**: Generates a strictly structured `.vax` JSON payload, encrypts it using `AES-256-GCM`, signs it with an `HMAC-SHA256` key, and writes it back to the physical USB drive.

### Node C: VaxDock (C# WPF Analyzer)
- **Role**: The secure air-gapped analyst workstation that ingests the payloads.
- **Mechanism**: Decrypts the `.vax` files, validates the cryptographic signatures to ensure the payload hasn't been tampered with, and logs the findings into a centralized SQLite database for historical reporting and cadence alerts.

---

## 3. Directory Layout

- `/VaxAgent`: The deployment payload (Dual Target).
- `/VaxDock`: The WPF Analyst application.
- `/Models`: Shared domain definitions (CVE schemas, Finding statuses).
- `/Hardware`: The ATtiny85 C++ sketches and the batch script launchers.
- `/Tests`: xUnit suites validating the cryptography and OT network parsers.
- `/scripts`: PowerShell and Bash automation for building production releases.
- `/docs`: Central repository for architectural decisions.

---

## 4. Critical Reading

> [!IMPORTANT]  
> **Mandatory Reading**: Before writing a single line of code, you must read [`/docs/SENIOR_ENGINEER_TRAPS.md`](file:///Users/rizzy/Documents/GitHub/VaxDrive-VaxAgent/docs/SENIOR_ENGINEER_TRAPS.md).  
> It contains hard-learned, real-world lessons about SQLite concurrency, WPF threading, and OT network fragility (e.g., why you never send write PDUs to a 1990s Siemens PLC).
