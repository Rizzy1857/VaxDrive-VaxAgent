# Changelog

All notable changes to the VaxDrive project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]
- Maintenance and vulnerability tracking ongoing.

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
