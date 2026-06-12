# Version-Based Drawbacks & Limitations

This document tracks known architectural drawbacks, compatibility limitations, or missing features tied to specific versions of the VaxDrive system. 
As these drawbacks are mitigated or resolved in future releases, they must be removed from this list and documented as fixed in the `CHANGELOG.md`.

## Active Drawbacks

### Target Version: v0.1.0 (Phase 1-3)

- **.NET 3.5 Fallback Cryptography:** The `.NET 3.5` build of `VaxAgent` (required for Windows XP targets) lacks `AesGcm` in its base class library. It must rely on a fallback encryption scheme (e.g., AES-CBC with manual HMAC) which complicates the VaxDock decryption logic.
- **Hardware Key Derivation Stub:** Production SDKs (IronKey/Kingston) for hardware-bound key derivation are not yet integrated. The system currently relies on a `device.token` fallback file stored in the `/boot` partition.
- **SharpPcap on Locked HMIs:** `PlcNeighborCheck` relies on SharpPcap for passive ARP sweeping. This requires Npcap/WinPcap installed on the target host. Locked HMIs without these drivers will fail the check unless the raw socket fallback is fully robust.
- **Orchestrator Synchronous Blocking:** The `ScanOrchestrator` uses a synchronous loop to maintain `.NET 3.5` compatibility. If a check module becomes infinitely blocked within a native OS call, the 90-second budget enforcement cannot preemptively kill it.
- **WMI Missing Strings:** Virtualized environments (like Parallels or VMware) may omit `Manufacturer` or `SMBIOSBIOSVersion` strings in `Win32_BIOS`. The `FirmwareCheck` currently gracefully degrades to "Unknown", which could reduce fingerprint accuracy on VMs.
- **Registry Hives Hardcoded:** `InstalledSoftwareCheck` hardcodes `WOW6432Node` for 32-bit software detection. This could miss software on non-standard emulation layers like Windows on ARM.
- **schtasks.exe CSV Parsing:** `ScheduledTasksCheck` dynamically parses CSV output, but cannot handle complex escaped quotes inside task names.
- **netstat Localization:** `OpenPortsCheck` relies on the exact string `LISTENING`. If deployed on non-English OS variants, this string check will fail and open ports will be missed.
- **USB Timestamps Omitted:** `UsbHistoryCheck` flags unauthorized device names but omits connection timestamps because extracting binary `FILETIME` properties safely across WinXP to Win10 is highly complex without P/Invoke.
- **DefinitionLoader on .NET 3.5:** `.NET 3.5` lacks `System.Text.Json`. Currently, the legacy build intercepts the load request and returns a blank pack, meaning XP machines currently bypass CVE logic entirely.
- **Naive CVE Version Matching:** `CveMatchCheck` currently uses simple string `Contains` matching for software names. A full Semantic Versioning (`SemVer`) parser is required to properly honor `min_version` and `max_version` constraints.
- **.NET 3.5 Crypto Fallbacks (Critical):** `AesGcm` and `HKDF` are not available in `.NET 3.5`. Currently, `VaxEncryptor` and `KeyDerivation` use non-functional stubs for the legacy target, meaning Windows XP scans are currently written in plaintext without HMAC signatures until a manual AES-CBC/HMAC-SHA256 fallback is implemented.
- **Silent Crash Logging:** `Program.cs` catches global exceptions and prints them to `Console`. If the agent is run silently via an automated script without a console window attached, critical startup crashes will leave no trace.
- **Drive Path Resolution:** If `Program.cs` is executed without arguments from a strange context (like a `Win+R` shortcut), `AppDomain.CurrentDomain.BaseDirectory` might resolve incorrectly to `C:\Windows\System32`, causing the agent to fail to find `/boot/definitions.json`.
