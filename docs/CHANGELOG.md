# Changelog

All notable changes to this project will be documented in this file.

## [3.7.0] - 2026-06-15
### Fixed
- [Sprint D] SharpPcap DllNotFoundException caught, raw socket fallback
- [Sprint D] FirmwareCheck 4-field confidence score, UNKNOWN substitution
- [Sprint D] LegacyEncryptor RNG IV, PBKDF2 10k, key zeroize in finally

## [3.6.0] - 2026-06-15
### Fixed
- [Sprint C] netstat foreign address column replaces localized string match
- [Sprint C] schtasks RFC 4180 parser handles escaped quotes and commas
- [Sprint C] USB FILETIME via IRegistryReader, NET35 P/Invoke + NET8 native
- [Sprint C] InstalledSoftwareCheck queries both hives, ARM aware, deduped

## [3.4.0] - 2026-06-15
### Fixed
- [Sprint A] AES-CBC/HMAC-SHA256 fallback for .NET 3.5 (LegacyEncryptor.cs)
- [Sprint A] Crash logger writes HMAC-signed file when no console attached
- [Sprint A] LegacyDefinitionLoader hand-rolled JSON parser for .NET 3.5
- [Sprint A] SelfUpdateService stage-and-flag, swap handled by InstallService.ps1
- [Sprint A] AlertDispatcher UDP retry queue, flat file fallback after 3 fails
### Breaking Changes
- SelfUpdateService: updates now require service restart to apply

## [3.3.0] - 2026-06-15
### Added
- Enterprise-grade hardware token derivation supporting Kingston and IronKey [Phase 9]
- Robust NVD pagination engine for offline CVE mapping [Phase 9]
- Deep discovery network parsers for DNP3, CIP, and BACnet [Phase 9]
- Fully operational Operator Console TUI for live daemon monitoring [Phase 11]

### Security
- Integrated `libyara.NET` memory scanning for advanced persistent OT threats [Phase 9]
- Air-gap enforced AlertDispatcher emitting CEF to Syslog, EventLog, and HMAC flat files [Phase 11]
- Enforced zeroization of hardware token material upon USB ejection [Phase 9]
- Enforced HMAC-SHA256 verification on all update payloads and artifacts [Phase 10]

### Breaking Changes
- Replaced the plaintext `device.token` fallback system; hardware tokens are now mandatory [Phase 9]
- Dropped generic console output in favor of the Operator Console TUI [Phase 11]

### Known Limitations
- YARA engine currently restricted to Windows x64 only [Phase 9]
- BACnet MS/TP (serial) not supported; IP-only [Phase 9]
- No cloud/internet update path by design [Phase 10]
- PDF reports not supported, HTML only [Phase 11]

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

## [2.0.0] - 2026-06-12
### Added
- `build_release.ps1` orchestrating the dual-target standalone `.NET` build, `SignTool.exe` code signing, and USB package staging.
- Formalized `.env.example` to harden the cryptographic pipeline and eliminate hardcoded keys.
- **Milestone Reached**: Project is Production Hardened and ready for physical deployment!

## [1.4.0] - 2026-06-12
### Added
- I2C OLED support (`Tiny4kOLED`) integrated into the ATtiny85 hardware payload for visual deployment status.
- `ONBOARDING.md` and `SETUP.md` added for developer documentation.

## [1.3.0] - 2026-06-12
### Added
- `ExportService.cs` generating robust CSV extracts directly from SQLite via backend logic to bypass UI abstraction.
- Refactored `DeviceRepository.cs` Cadence computation to a native `datetime` SQL query.

## [1.2.0] - 2026-06-12
### Added
- `VaxLauncher.ino` Digispark hardware script to deliver payload via emulated keyboard to bypass disabled AutoRun.
- `launcher.bat` and `.vaxdrive` marker file implementing a dynamic `cmd` loop to reliably execute payloads regardless of assigned drive letter.

## [1.1.0] - 2026-06-12
### Added
- `PassiveArpListener.cs` utilizing `SharpPcap` to identify active PLCs via ARP sweeps seamlessly across `.NET 8` boundaries.
- `S7Scanner.cs` (Port 102) and `ModbusScanner.cs` (Port 502) enabling handcrafted OT interrogations without utilizing disruptive write PDUs.
- Integrated `AuditLogger.cs` enforcing strict TCP footprint tracking within `/logs`.

## [1.0.0] - 2026-06-12
### Added
- First Shippable milestone achieved! Windows-only Core completely stable.

## [0.9.0] - 2026-06-12
### Added
- `DefinitionsPackVersion` to `Findings` schema to firmly anchor findings to specific packs.
- `DefinitionLoader.cs` with strict validation to prevent orphaned Remediation IDs.
- `FindingsStateManager.cs` for robust SQLite state transitions (`MarkResolved`, `Escalate`).
- `RemediationCardView.xaml` providing operator-friendly vulnerability guidance.

## [0.8.0] - 2026-06-12
### Added
- `Dashboard.xaml` integrating severity summaries (`CriticalCount`, `HighCount`) computed directly in SQL.
- `DeviceDetail.xaml` outlining specific device findings.
- `App.xaml` service layer bootstrapping.
- `Dispatcher.InvokeAsync` implemented to safely synchronize background ingest tasks to the UI thread.

## [0.7.0] - 2026-06-12
### Added
- Embedded `SchemaV1.sql` for VaxDock with WAL journal mode enforcement.
- `ScanRepository.cs` enforcing `BEGIN/COMMIT` transactional ingest and conflict resolution on `ScanId`.
- `IngestPipeline.cs` strict state machine (`Detected → HMACVerified → Decrypted → Parsed → Inserted`).
- `DriveDetector.cs` background polling for the `VAXDRIVE` volume.

## [0.6.0] - 2026-06-12
### Added
- `Program.cs` entry point to sequentially run all checks with a hard 90s budget.
- Binary self-hash verification on launch (aborts on mismatch).
- Stable hardware-id hash for device fingerprint generation.
- ScanOrchestrator collects results, builds `ScanResult`, and writes the final `.vax` payload.
- Single-file self-contained publish configuration for `net8.0`.

## [0.5.0] - 2026-06-12
### Added
- `VaxFileWriter.cs` safely writes the `SCAN_[HASH]_[TIMESTAMP].vax` file to `/results`.
- `DefinitionLoader.cs` securely loads and validates the HMAC signature on the definitions pack.
- `CveMatchCheck.cs` cross-references OS and installed software against the definitions pack.
- Definitions pack mapping (`CVE` → `REM-XXX-NNN`) enforced.

## [0.4.0] - 2026-06-12
### Added
- `HardwareTokenKeyProvider` stub for development key material derivation.
- `KeyDerivation.cs` using HKDF-SHA256 to derive distinct AES and HMAC keys.
- `VaxEncryptor.cs` applying AES-256-GCM encryption (12-byte nonce, 16-byte tag).
- Canonical `.vax` layout enforcing a 32-byte HMAC-SHA256 signature over all preceding bytes.

## [0.3.0] - 2026-06-11
### Added
- `UsbHistoryCheck.cs` enumerates `USBSTOR` registry keys and flags unknown VIDs.
- `ScheduledTasksCheck.cs` parses `schtasks` execution output.
- `FirmwareCheck.cs` extracts baseline WMI `Win32_BIOS` strings.
- `RogueProcessCheck.cs` flags known-bad process IOCs.

## [0.2.0] - 2026-06-11
### Added
- `OsCheck.cs` utilizing WMI `Win32_OperatingSystem`.
- `InstalledSoftwareCheck.cs` enumerating `HKLM Uninstall` keys with normalized matching.
- `ServicesCheck.cs` inspecting running services via WMI.
- `OpenPortsCheck.cs` capturing `netstat -ano` output via `Process.Start`.
- Strict try/catch/finally wrappers around all checks to guarantee partial output on failure.

## [0.1.0] - 2026-06-10
### Added
- Monorepo initialized (`VaxDrive.sln`).
- Dual-target project files for `VaxAgent` (`net8.0` and `net35`).
- Shared Data `Models` established (`ScanResult`, `Finding`, `Device`, `PlcNeighbor`).
- `ICheck` interface defined for all scanning modules.
- CI pipeline skeleton and build targets matrix.
