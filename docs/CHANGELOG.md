# Changelog

All notable changes to this project will be documented in this file.

## [3.2.0] - Phase 11 (Reporting & Operator Console)
### Added
- **ReportExporter**: Generates fully offline, HMAC-signed HTML and JSON topology & vulnerability reports.
- **AlertDispatcher**: Emits SIEM-ready CEF logs to UDP Syslog, Windows Event Log, and daily rolling flat files.
- **OperatorConsole**: Replaced the static background daemon wait with a live, 5s-tick TUI displaying YARA hits, NVD syncs, and network topology mapping.

## [3.1.0] - Phase 10 (Production Packager & Updater)
### Added
- **AgentCli**: Added fully functional CLI harness wrapping the `AgentBootstrap` with flags for `--scan`, `--sync-nvd`, `--topology-export`, and `--daemon`.
- **PackagePayload.ps1**: Developed a silent PowerShell deployment packager that strips PDBs, structures native DLLs, and generates an HMAC-signed `manifest.json`.
- **SelfUpdateService**: Enabled the VaxAgent to securely update itself from the USB root, verifying manifest HMACS and all file SHA-256 hashes before replacing binaries.

## [3.0.0] - Phase 9 (Enterprise Edition Core)
### Added
- **Hardware Token Integration (Fixes OPEN-1)**: Replaced deterministic `/boot/device.token` stub with PKCS#11/CNG integration for Kingston and IronKey hardware security modules. Tokens are pinned, copied to `SecureString`, and instantly zeroized.
- **OT Protocol Parsers**: Extended the passive sweeper with deep-packet decoding for DNP3, EtherNet/IP (CIP), and BACnet/IP.
- **YARA Engine Integration**: Integrated `libyara.NET` memory scanning (`ProcessMemoryReader`) capable of iterating Win32 process memory in safe 4MB chunks to search for Industroyer2, Triton, and Stuxnet payloads.
- **NVD Pagination Engine**: Rebuilt the NVD Sync pipeline into a fully paginated, token-bucket rate-limited, exponentially backoff-capable offline caching engine using SQLite WAL mode.
