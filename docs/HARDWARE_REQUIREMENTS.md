# Hardware Requirements

This document outlines the physical hardware requirements for deploying the VaxDrive ecosystem in an Operational Technology (OT) context.

## VaxDrive — Physical Device

Requirements for the hardened USB unit itself:

- **Hardened USB Drive Body**: IronKey S1000 or Kingston Vault Privacy 80 (or equivalent).
  - AES-256 XTS onboard hardware encryption.
  - Hardware write-protect on `/engine` and `/definitions` partitions.
  - Agent-only write access to `/results` partition.
  - Industrial temp range preferred: -20C to 85C.
- **Physical SCAN Button (Required Fallback)**:
  - Tactile momentary push button on casing with debounce circuit.
  - Manual launch path in environments where Group Policy blocks HID keystroke injection.
- **HID Microcontroller (Optional)**:
  - ATtiny85 on custom PCB.
  - USB Full Speed capable (DigiKeyboard or V-USB firmware).
  - Enumerate as composite USB (HID + mass storage).
  - *Must feature a 2000ms+ boot delay before emitting keystrokes to allow Windows to settle.*
- **OLED Display Module (Optional — OPEN-3)**:
  - SSD1306 or equivalent I2C/SPI OLED driven by MCU.
  - Small form factor for progress bar visual feedback.

## VaxDock — Security Laptop

Minimum specs for the companion Windows triage laptop:

- **OS**: Windows 10 64-bit (minimum) / Windows 11 Pro 64-bit (recommended). ARM is currently unsupported due to SharpPcap limitations.
- **CPU**: Dual-core x64 minimum (AES-GCM + HMAC ingest is CPU bound but light).
- **RAM**: 4 GB minimum / 8 GB recommended (WPF + SQLite).
- **Storage**: 10 GB free space (SQLite database grows at approx ~1 MB per 20 scans).
- **Network**: Fully air-gapped operation supported. No NIC required post-install.
- **Dependencies**:
  - **Npcap**: Version 1.60+ must be installed with Admin rights prior to air-gapping (required for SharpPcap).

## Target Scanned Machine (HMI / Engineering Station)

The endpoints where VaxAgent.exe will execute:

- **OS Supported**: Windows XP SP3 (`.NET 3.5` build), Windows 7 SP1+, Windows 10, Windows 11.
- **Execution Profile**:
  - Network required: **NO**
  - Elevation required: **NO** (graceful fallback expected if UAC triggers)
  - Writes to host: **NO**
  - Runtime install required: **NO** (self-contained binaries)
