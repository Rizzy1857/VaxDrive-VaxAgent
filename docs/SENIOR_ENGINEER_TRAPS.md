# Senior Engineer Traps & Hardening Advice

This document aggregates known architectural traps and defensive coding strategies for the VaxDrive ecosystem.

## Top 10 Traps to Avoid

1. **WMI Timeout**: WMI calls frequently hang on degraded XP machines. A missing per-check deadline will blow the 90-second scan budget on a single check.
2. **AES-GCM Nonce Reuse**: Deriving nonces from timestamps or hashes is catastrophic. Two scans in the same second will cause nonce collision and break GCM. Always use `RandomNumberGenerator.GetBytes(12)`.
3. **HMAC Byte Range Mismatch**: VaxDock must compute HMAC over the exact same canonical byte range (nonce + tag + ciphertext) as VaxAgent. Any deviation causes 100% verification failure.
4. **HID Enumeration Race**: Keystrokes firing before the OS assigns a drive letter. Ensure a 2000ms delay in the ATtiny85 firmware.
5. **SQLite Lock Contention**: Failing to enable WAL mode (`PRAGMA journal_mode=WAL`) or failing to wrap scan ingestion in a `BEGIN/COMMIT` transaction will cause massive I/O delays.
6. **SharpPcap Missing Dependency**: `Npcap` cannot be bundled. If it is not pre-installed on the HMI, the PLC phase must silently log failure without crashing the agent.
7. **.NET 3.5 XP Drift**: Using C# 9+ syntax in shared code will silently break the `net35` build if CI doesn't strictly verify both targets on every PR.
8. **Leaked Key Providers**: The `FileBasedKeyProvider` (used for dev) must be excluded from signed production builds via preprocessor directives to prevent reverse engineering.
9. **S7comm Half-Open Connections**: Failing to close the TCP socket in a `finally` block during a PLC banner grab can block the SCADA system from reconnecting to the PLC.
10. **Cadence Timezone Skew**: Cadence alerts must compare UTC timestamps from SQLite against `datetime('now')`. Converting to local time during the query misfires alerts across timezone-shifted OT sites.

## Phase-Specific Hardening

### Phase 1: Agent Checks
- **Elevation**: Do not crash if WMI `Win32_OperatingSystem` demands elevation on locked-down hosts. Use a graceful `access_denied` status.
- **Normalization**: HKLM Uninstall keys have inconsistent naming by vendor. Apply `Trim().ToLowerInvariant()` before matching.

### Phase 2: Crypto
- **TagSize**: Explicitly pass `AesGcm.TagByteSizes.MaxSize` (16 bytes). Never rely on default crypto parameters.
- **Disposal**: Wrap `AesGcm` in a `using` block to purge key material from memory immediately.

### Phase 3: VaxDock Core
- **Thread Safety**: Never block the WPF UI thread with SQLite or Crypto I/O. Use `Task.Run` and `Dispatcher.InvokeAsync`.
- **Drive Removal**: If the USB is yanked mid-ingest, catch `DriveNotFoundException`, rollback the SQLite transaction, and quarantine the scan ID.

### Phase 4: Remediation
- **Orphaned IDs**: Validate all `REM-XXX-NNN` mapping on pack load. Missing remediation IDs must fail the load process, not silently pass.

### Phase 5: PLC Banner Grab
- **ARP Throttle**: Limit passive ARP sweeps to 1 packet/second. Rapid burst ARP can crash 1990s-era OT managed switches.

### Phase 7: Export
- **CSV Escaping**: RFC 4180 must be strictly followed. Un-escaped commas inside "Severity" or "Status" columns will corrupt audit logs.
