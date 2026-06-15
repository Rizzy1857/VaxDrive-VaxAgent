# VaxDrive OT Agent - Hardening Checklist

This checklist must be completed by the designated security operator prior to any production deployment in a Level 2/Level 3 OT environment. Check each item to confirm compliance and sign the owner column.

## Section 1: SecureString Audit
Confirm that all hardware token bytes and cryptographic keys touching the managed heap are pinned and securely zeroized.

| Status | Item | Owner |
|:---:|:---|:---|
| [ ] | Verify `KingstonTokenProvider.GetCryptographicToken()` uses `GCHandle.Alloc(..., GCHandleType.Pinned)`. | |
| [ ] | Verify `KingstonTokenProvider` zeros the raw unmanaged byte array (`Array.Clear`) before releasing the GC pin. | |
| [ ] | Verify `IronKeyTokenProvider.GetCryptographicToken()` frees the COM BSTR (`Marshal.ZeroFreeBSTR`) in a `finally` block. | |
| [ ] | Verify `AgentBootstrap` clears `SecureString` references when the `WM_DEVICECHANGE` (USB eject) event fires. | |

## Section 2: P/Invoke Surface
Confirm all unmanaged interoperability calls are restricted and target x64 architectures exclusively.

| Status | Item | Owner |
|:---:|:---|:---|
| [ ] | Confirm `kernel32.dll` `OpenProcess` restricts access flags to exactly `PROCESS_QUERY_INFORMATION | PROCESS_VM_READ`. | |
| [ ] | Confirm `kernel32.dll` `ReadProcessMemory` is wrapped in `try/catch` for `ERROR_ACCESS_DENIED`. | |
| [ ] | Confirm `kernel32.dll` `VirtualQueryEx` correctly sizes the `MEMORY_BASIC_INFORMATION64` struct for x64 targets. | |
| [ ] | Confirm `yara.dll` exports are strictly bound to `yara64.dll` conventions. | |

## Section 3: SQL Parameterization
Confirm that all SQL queries use parameterized arguments and zero string concatenation exists in data access layers.

| Status | Item | Owner |
|:---:|:---|:---|
| [ ] | Verify `CveRepository.UpsertCve()` utilizes `SqliteParameter` for all bound fields. | |
| [ ] | Verify `CveRepository.SearchCves()` handles `LIKE` clauses securely (`$"{keyword}%"` bound as parameter, not interpolated into SQL string). | |
| [ ] | Verify `CveRepository.UpsertCheckpoint()` securely passes the numeric ID. | |
| [ ] | Run Roslyn `CA2100` analyzer. Confirm 0 warnings/errors for SQL injection vulnerabilities. | |

## Section 4: Crypto Primitives
Confirm that only approved Cryptography Next Generation (CNG) primitives are used. Legacy CAPI is strictly forbidden.

| Status | Item | Owner |
|:---:|:---|:---|
| [ ] | Verify `HardwareTokenProviders` use `HKDF` from `System.Security.Cryptography.CngKey`. | |
| [ ] | Verify all hash comparisons (e.g., manifest signatures) use `HMACSHA256`. | |
| [ ] | Verify no usage of `MD5`, `SHA1`, `DES`, or `RC4` exists anywhere in the codebase. | |
| [ ] | Verify `NvdPaginationEngine` pins TLS certificates (`api.nvd.nist.gov`) securely. | |

## Section 5: Attack Surface
Confirm that all environmental interactions (EnvVars, Disk IO, Sockets) are accounted for and restricted.

| Status | Item | Owner |
|:---:|:---|:---|
| [ ] | **Env Vars:** Verify `VAXDRIVE_HARDWARE_TOKEN_PROVIDER`, `VAXDRIVE_DB_KEY`, `NVD_API_KEY`, `VAXDRIVE_BUILD_KEY`, `VAXDRIVE_UPDATE_PATH`, `VAXDRIVE_SYSLOG_HOST` are the *only* external configuration vectors. | |
| [ ] | **Disk IO:** Verify agent only writes to the VaxDrive USB volume (`VAXDRIVE_ROOT`) and the ephemeral OS `%TEMP%` dir. No writes to `C:\Windows`. | |
| [ ] | **Sockets (Listening):** Verify `TopologyMap` relies solely on *passive* PCAP captures. Zero listening TCP/UDP ports are opened by the agent. | |
| [ ] | **Sockets (Egress):** Verify external outbound calls are restricted strictly to `api.nvd.nist.gov:443`. No telemetry, no cloud logging, no updates via internet. | |
