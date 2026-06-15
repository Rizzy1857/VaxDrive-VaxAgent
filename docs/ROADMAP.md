# VaxDrive Development Roadmap
> **Revision:** 1.0 · **Status:** Pre-implementation · **Owner:** Engineering

---

## Version Progress

| Version | Name | Phase | Milestone | Status |
|---|---|---|---|---|
| **0.1.0** | Repo Bootstrap | 1 | | ✅ Complete |
| **0.2.0** | Windows Checks Core | 1 | | ✅ Complete |
| **0.3.0** | Windows Checks Extended | 1 | | ✅ Complete |
| **0.4.0** | Crypto Layer | 2 | | ✅ Complete |
| **0.5.0** | .vax Writer + CVE Matching | 2 | | ✅ Complete |
| **0.6.0** | VaxAgent Runner | 2 | Agent feature-complete | ✅ Complete |
| **0.7.0** | VaxDock Ingest Pipeline | 3 | | ✅ Complete |
| **0.8.0** | VaxDock Dashboard + DeviceDetail | 3 | End-to-end scan→triage | ✅ Complete |
| **0.9.0** | Remediation Cards | 4 | | ✅ Complete |
| **1.0.0** | FIRST SHIPPABLE | 1-4 | Windows-only shippable | ✅ Complete |
| **1.1.0** | PLC Banner Grab | 5 | | ✅ Complete |
| **1.2.0** | ATtiny85 HID Hardware Path | 6 | | ✅ Complete |
| **1.3.0** | Cadence, Trends, Export | 7 | | ✅ Complete |
| **1.4.0** | OLED + UX Polish | 7 | | ✅ Complete |
| **2.0.0** | Production Hardened Release | all | Full hardware, pen-tested | ✅ Complete |
| **3.0.0** | Enterprise Edition | 9 | Advanced Threat & Intel | 🚧 Planned |

---

## Pre-Phase: Repo Bootstrap

> Complete before any phase begins. Fast — 1–2 days.

### Tasks

- [ ] Create solution file `VaxDrive.sln` at repo root
- [ ] Scaffold all project directories (see `ARCHITECTURE.md §3`)
- [ ] Create `VaxAgent/VaxAgent.csproj` — dual target `net8.0;net35`
- [ ] Create `VaxDock/VaxDock.csproj` — `net8.0-windows`, WPF enabled
- [ ] Create `Tests/VaxAgent.Tests/VaxAgent.Tests.csproj` — xUnit
- [ ] Create `Tests/VaxDock.Tests/VaxDock.Tests.csproj` — xUnit
- [ ] Add shared `Models/` as a project reference in both VaxAgent and VaxDock `.csproj`
- [ ] Add `VaxAgent/Crypto/` reference from VaxDock's `VaxDock/Crypto/` (shared key derivation logic)
- [ ] Create `.env.example` — document all required env var names, no values
- [ ] Create `.gitignore` — exclude `*.vax`, `*.env`, `bin/`, `obj/`, `*.user`
- [ ] Set up GitHub Actions CI skeleton (build-only) targeting `net8.0`
- [ ] Install gitleaks in CI for secret scanning

### Deliverable Checklist

```
✅ dotnet build VaxDrive.sln → succeeds (empty projects)
✅ dotnet test → 0 tests, 0 failures (scaffolded)
✅ .env.example committed, no real values
✅ CI build passes on main branch
```

---

## Phase 1 — VaxAgent Windows Checks

> **Risk:** .NET 3.5 / XP compatibility for WMI paths.
> **Dependency:** None — start here.

### Goal

A working `VaxAgent.exe` that completes all 9 Windows checks, builds a `ScanResult` in memory, and prints it to console (crypto/file-write comes in Phase 2).

### Architecture Decisions to Lock In First

- [ ] Define `ICheck` interface in `VaxAgent/Checks/ICheck.cs`
- [ ] Define `ScanResult`, `Finding`, `Device`, `PlcNeighbor` models in `Models/`
- [ ] Define `CheckResult` (wrapper with error field) in `VaxAgent/Checks/CheckResult.cs`
- [ ] Define `ScanContext` (shared mutable state passed to checks) in `VaxAgent/ScanContext.cs`

```csharp
// ICheck.cs
public interface ICheck
{
    string Name { get; }
    CheckResult Run(ScanContext context);
}

// CheckResult.cs
public sealed class CheckResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }     // null = clean
}

// ScanContext.cs — mutable bag passed down the chain
public sealed class ScanContext
{
    public ScanResult Result { get; } = new();
    public string DefinitionsPath { get; init; } = string.Empty;
    public string ResultsPath { get; init; } = string.Empty;
}
```

### Check Implementation Order (recommended — easiest first)

| Order | Check | Notes |
|---|---|---|
| 1 | `FirmwareCheck` | WMI only — simplest, good baseline test |
| 2 | `OsCheck` | WMI — verify dual-target |
| 3 | `InstalledSoftwareCheck` | Registry read — lots of entries, test pagination |
| 4 | `ServicesCheck` | WMI — test on clean and service-heavy machines |
| 5 | `ScheduledTasksCheck` | Process execution — test schtasks parsing edge cases |
| 6 | `OpenPortsCheck` | Process execution — netstat parsing is the fiddly bit |
| 7 | `UsbHistoryCheck` | Registry — test with known and unknown VIDs |
| 8 | `RogueProcessCheck` | WMI + IOC list — requires definitions pack stub |
| 9 | `CveMatchCheck` | Last — depends on all prior outputs + definitions |

### .NET 3.5 Compatibility Rules

Any check method that has a `net35` incompatibility must use conditional compilation:

```csharp
#if NET35
    // .NET 3.5 compatible path — no LINQ, no async, no dynamic
    var mgmtObj = new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem");
#else
    // .NET 8 path
    using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem");
#endif
```

**Known incompatibilities to handle:**
- `string.IsNullOrWhiteSpace` → not in .NET 3.5 → use `str == null || str.Trim().Length == 0`
- `Enumerable.ToList()` → add `using System.Linq` guard or avoid
- `Task` / `async` / `await` → prohibited in net35 build target
- `AesGcm` → not in .NET 3.5 → Phase 2 uses `#if NET35` fallback to AES-CBC (document limitation)

### ScanOrchestrator

```csharp
// ScanOrchestrator.cs
public sealed class ScanOrchestrator
{
    private readonly IEnumerable<ICheck> _checks;
    private readonly TimeSpan _budget = TimeSpan.FromSeconds(90);

    public ScanResult Run(ScanContext context)
    {
        var sw = Stopwatch.StartNew();
        foreach (var check in _checks)
        {
            if (sw.Elapsed > _budget) break;   // Hard 90s cutoff
            try
            {
                var result = check.Run(context);
                if (!result.Success)
                    context.Result.CheckErrors[check.Name] = result.Error!;
            }
            catch (Exception ex)
            {
                // Never abort on single check failure
                context.Result.CheckErrors[check.Name] = ex.Message;
            }
        }
        return context.Result;
    }
}
```

### Phase 1 Tests

```
VaxAgent.Tests/Checks/
  OsCheckTests.cs               — verify WMI output parsed correctly (mock ManagementObject)
  InstalledSoftwareCheckTests.cs — verify registry enumeration (mock RegistryKey)
  ServicesCheckTests.cs
  OpenPortsCheckTests.cs        — test netstat parser with fixture output strings
  UsbHistoryCheckTests.cs       — test VID flagging logic with known/unknown VIDs
  ScheduledTasksCheckTests.cs   — test schtasks CSV parser with fixture strings
  FirmwareCheckTests.cs
  RogueProcessCheckTests.cs     — test IOC matching logic
  CveMatchCheckTests.cs         — test version range matching with fixture CVE pack
  ScanOrchestratorTests.cs      — test fault isolation (one check throws, others run)
```

### Phase 1 Exit Criteria

```
✅ dotnet build VaxAgent -- TargetFramework net8.0 → succeeds
✅ dotnet build VaxAgent -- TargetFramework net35 → succeeds
✅ All 9 check unit tests pass
✅ VaxAgent.exe run on a real Windows machine → ScanResult printed to console
✅ ScanResult JSON is valid against output schema
✅ No elevation prompt on launch
✅ No registry writes observed (use Process Monitor to verify)
✅ Total execution time < 90 seconds on reference HMI hardware
```

---

## Phase 2 — Crypto + .vax Output

> **Risk:** Hardware token key derivation (OPEN-1). Unblock with boot-partition fallback token.
> **Dependency:** Phase 1 complete — `ScanResult` model frozen.

### Goal

`VaxAgent.exe` writes an encrypted, HMAC-signed `.vax` file to `/results`. Key derivation works. VaxDock can verify the file (even before full VaxDock UI is built).

### Key Derivation — Development Stub

OPEN-1 blocks production key derivation. Use this stub for Phases 2–3:

```csharp
// Crypto/HardwareTokenProvider.cs
public static class HardwareTokenProvider
{
    /// <summary>
    /// STUB: Reads a deterministic token from /boot/device.token on the drive.
    /// Production: replace with IronKey/Kingston SDK call.
    /// </summary>
    public static byte[] GetTokenBytes(string drivePath)
    {
        var tokenPath = Path.Combine(drivePath, "boot", "device.token");
        // device.token is a 32-byte random file generated at drive manufacture time
        // and signed by the pack signing key — read-only, never changes
        return File.ReadAllBytes(tokenPath);
    }
}
```

`device.token` for development: generated once with `RandomNumberGenerator.GetBytes(32)`, stored in `/boot/device.token`, committed to a dev-only branch. **Never commit production tokens.**

### VaxEncryptor.cs

```csharp
// Crypto/VaxEncryptor.cs
public static class VaxEncryptor
{
    private static readonly byte[] Magic = "VAX1"u8.ToArray();

    public static byte[] Encrypt(byte[] plaintext, byte[] encKey, byte[] hmacKey)
    {
        // 1. Generate nonce
        var nonce = new byte[12];
        RandomNumberGenerator.Fill(nonce);

        // 2. AES-256-GCM encrypt
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(encKey, AesGcm.TagByteSizes.MaxSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        // 3. HMAC over nonce || ciphertext || tag
        var hmacData = nonce.Concat(ciphertext).Concat(tag).ToArray();
        var hmac = HMACSHA256.HashData(hmacKey, hmacData);

        // 4. Assemble file bytes
        using var ms = new MemoryStream();
        ms.Write(Magic);                                          // 4 bytes
        ms.Write(nonce);                                          // 12 bytes
        ms.Write(tag);                                            // 16 bytes
        ms.Write(BitConverter.GetBytes((uint)ciphertext.Length)); // 4 bytes (BE)
        ms.Write(ciphertext);                                     // N bytes
        ms.Write(hmac);                                           // 32 bytes
        return ms.ToArray();
    }
}
```

### VaxSigner.cs / HmacVerifier.cs

Signer and verifier are symmetric. Both live in their respective project's `Crypto/` folder but implement the same algorithm. Shared test fixtures verify round-trips.

### VaxFileWriter.cs

```csharp
// Output/VaxFileWriter.cs
public sealed class VaxFileWriter
{
    public void Write(ScanResult result, string resultsDirPath, byte[] encKey, byte[] hmacKey)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(result);
        var encrypted = VaxEncryptor.Encrypt(json, encKey, hmacKey);

        var filename = $"SCAN_{result.DeviceFingerprint}_{result.Timestamp:yyyyMMddTHHmmssZ}.vax";
        var fullPath = Path.Combine(resultsDirPath, filename);

        // Validate output is on the drive, not the host
        if (!fullPath.StartsWith(resultsDirPath, StringComparison.OrdinalIgnoreCase))
            throw new SecurityException("Output path must be on drive — host write blocked.");

        File.WriteAllBytes(fullPath, encrypted);
    }
}
```

### Phase 2 Tests

```
VaxAgent.Tests/Crypto/
  EncryptDecryptRoundTripTests.cs   — encrypt → decrypt → JSON matches original
  HmacTamperTests.cs                — flip one byte → verify throws
  MagicBytesTests.cs                — wrong magic → reject
  KeyDerivationStubTests.cs         — stub token → deterministic keys
  VaxFileWriterTests.cs             — file written to correct path, rejected if host path
```

### Phase 2 Exit Criteria

```
✅ VaxAgent.exe writes a .vax file to /results on development drive
✅ .vax file validates: magic bytes correct, HMAC passes, AES decrypts cleanly
✅ Bit-flip in ciphertext → HMAC verify fails in test
✅ Attempt to write outside /results → SecurityException thrown in test
✅ Crypto round-trip test: serialize ScanResult → encrypt → decrypt → deserialize → equal
✅ net35 build: .vax written with AES-CBC fallback (documented limitation)
```

---

## Phase 3 — VaxDock Core

> **Risk:** HMAC pipeline must be airtight before any other VaxDock feature is built.
> **Dependency:** Phase 2 complete — .vax format frozen.

### Goal

VaxDock detects VaxDrive, ingests and decrypts `.vax` files, populates SQLite, and displays a working Dashboard. No remediation cards yet — data display only.

### Build Order Within Phase

1. `DatabaseBootstrap.cs` — schema creation, run on app start
2. `HmacVerifier.cs` — verify before any other ingest code exists
3. `VaxDecryptor.cs` — decrypt after verify
4. `IngestPipeline.cs` — orchestrate verify → decrypt → insert
5. `DriveDetector.cs` — background watcher triggering pipeline
6. `ScanRepository.cs` + `DeviceRepository.cs` + `FindingRepository.cs`
7. `Dashboard.xaml` — bind to repository query results
8. `DeviceDetail.xaml` — per-device findings view

### HMAC First Rule

**Do not write a single line of ingest code before `HmacVerifier` is tested green.**

```csharp
// VaxDock/Crypto/HmacVerifier.cs
public static class HmacVerifier
{
    /// <exception cref="HmacVerificationException">Thrown on any mismatch.</exception>
    public static void Verify(ReadOnlySpan<byte> fileBytes, byte[] hmacKey)
    {
        // Extract components
        if (fileBytes.Length < 4 + 12 + 16 + 4 + 32)
            throw new HmacVerificationException("File too short to be valid .vax");

        var magic = fileBytes[..4];
        if (!magic.SequenceEqual("VAX1"u8))
            throw new HmacVerificationException("Invalid magic bytes");

        var nonce = fileBytes[4..16];
        var tag = fileBytes[16..32];
        var ctLen = BitConverter.ToUInt32(fileBytes[32..36]);
        var ciphertext = fileBytes[36..(int)(36 + ctLen)];
        var storedHmac = fileBytes[(int)(36 + ctLen)..];

        // Recompute
        var hmacData = new byte[nonce.Length + ciphertext.Length + tag.Length];
        nonce.CopyTo(hmacData);
        ciphertext.CopyTo(hmacData.AsSpan(nonce.Length));
        tag.CopyTo(hmacData.AsSpan(nonce.Length + ciphertext.Length));

        var computed = HMACSHA256.HashData(hmacKey, hmacData);

        // Constant-time compare
        if (!CryptographicOperations.FixedTimeEquals(computed, storedHmac))
            throw new HmacVerificationException("HMAC mismatch — file tampered or corrupt");
    }
}
```

### QuarantineService

```csharp
// VaxDock/Services/QuarantineService.cs
public sealed class QuarantineService
{
    private readonly IDbConnection _db;

    public void Quarantine(string filename, string reason)
    {
        const string sql = @"
            INSERT INTO QuarantinedFiles (Filename, FailureReason, DetectedAt)
            VALUES (@filename, @reason, @now)";
        _db.Execute(sql, new { filename, reason, now = DateTime.UtcNow.ToString("O") });
        // Never rethrow — quarantine is terminal for this file
    }
}
```

### Repository Pattern (Raw SQL)

Every repository follows this pattern — no ORM, no micro-ORM:

```csharp
// VaxDock/Data/ScanRepository.cs
public sealed class ScanRepository
{
    private readonly string _connectionString;

    public ScanRepository(string dbPath)
        => _connectionString = $"Data Source={dbPath}";

    public void Upsert(ScanResult scan)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        const string sql = @"
            INSERT INTO Scans (ScanId, DeviceId, Timestamp, PatchLevel, RawJson, IngestedAt)
            VALUES (@scanId, @deviceId, @timestamp, @patchLevel, @rawJson, @ingestedAt)
            ON CONFLICT(ScanId) DO NOTHING";
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@scanId", scan.ScanId);
        cmd.Parameters.AddWithValue("@deviceId", scan.DeviceFingerprint);
        cmd.Parameters.AddWithValue("@timestamp", scan.Timestamp.ToString("O"));
        cmd.Parameters.AddWithValue("@patchLevel", scan.PatchLevel ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@rawJson", JsonSerializer.Serialize(scan));
        cmd.Parameters.AddWithValue("@ingestedAt", DateTime.UtcNow.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    public IReadOnlyList<ScanSummary> GetDeviceSummaries()
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        const string sql = @"
            SELECT d.Id, d.LastSeen,
                   COUNT(CASE WHEN f.Severity = 'CRITICAL' AND f.ResolvedAt IS NULL THEN 1 END) AS CriticalCount,
                   COUNT(CASE WHEN f.Severity = 'HIGH'     AND f.ResolvedAt IS NULL THEN 1 END) AS HighCount
            FROM Devices d
            LEFT JOIN Findings f ON f.DeviceId = d.Id
            GROUP BY d.Id
            ORDER BY CriticalCount DESC, HighCount DESC";
        // ... read + map
    }
}
```

### Phase 3 Tests

```
VaxDock.Tests/
  IngestPipeline/
    HmacVerifyPassTests.cs        — valid .vax → ingested
    HmacVerifyFailTests.cs        — tampered .vax → quarantined, not ingested
    DriveDetectorTests.cs         — volume label matching logic
    DuplicateScanTests.cs         — same ScanId ingested twice → no duplicate
  Data/
    ScanRepositoryTests.cs        — upsert + query (in-memory SQLite)
    DeviceRepositoryTests.cs
    FindingRepositoryTests.cs
    QuarantineServiceTests.cs     — quarantine insert + cannot-ingest verification
```

### Phase 3 Exit Criteria

```
✅ VaxDrive plugged into laptop → Dashboard auto-populates within 5 seconds
✅ Tampered .vax → quarantine table entry + UI alert — NOT ingested
✅ Valid .vax from Phase 2 → correct data visible in Dashboard
✅ Dashboard device list sorted correctly (CRITICAL first)
✅ Status badges reflect correct severity state
✅ DeviceDetail shows all findings, USB anomalies, open ports
✅ SQLite DB file created at configured path, no ORM references in code
✅ All Phase 3 unit tests pass
```

---

## Phase 4 — Remediation Cards

> **Risk:** Content quality — plain language for non-technical operators. Technical review is not enough.
> **Dependency:** Phase 3 complete — `RemediationId` FK working in DB.

### Goal

Authored remediation cards for top 20 OT CVEs, displayed in `RemediationCard.xaml`. Mark Resolved / Escalate actions working. Cards sourced from definitions pack.

### Top 20 OT CVE Targets (initial list — expand per field intel)

| CVE | Component | Why It Matters |
|---|---|---|
| CVE-2017-0144 | SMBv1 (EternalBlue) | Ubiquitous on unpatched HMIs |
| CVE-2019-0708 | Remote Desktop Services (BlueKeep) | RDP on HMIs |
| CVE-2021-34527 | Windows Print Spooler (PrintNightmare) | Often enabled on plant PCs |
| CVE-2020-0601 | Windows CryptoAPI | Certificate trust |
| CVE-2017-0147 | SMBv1 (EternalRomance) | Related to EternalBlue family |
| CVE-2019-1040 | NTLM (NTLM Relay) | Common in flat OT networks |
| CVE-2020-1472 | Netlogon (Zerologon) | Domain controller risks in hybrid OT |
| CVE-2021-44228 | Log4Shell | SCADA software using Java |
| CVE-2018-7600 | Drupal (SA-CORE-2018-002) | HMI web interfaces |
| CVE-2022-30190 | MSDT (Follina) | Document-based, relevant to engineering stations |
| CVE-2017-0290 | Windows Defender MsMpEng | AV engine on managed HMIs |
| CVE-2019-0803 | Win32k elevation of privilege | Used in OT-targeted campaigns |
| CVE-2021-26855 | Exchange Server (ProxyLogon) | Engineering network mail servers |
| CVE-2022-21907 | HTTP Protocol Stack | IIS on HMI web servers |
| CVE-2023-23397 | Microsoft Outlook | Phishing vector on OT-adjacent PCs |
| CVE-2021-1675 | Windows Print Spooler | Pre-PrintNightmare variant |
| CVE-2016-0099 | Secondary Logon Service | Older Windows versions |
| CVE-2020-16898 | Windows TCP/IP (Bad Neighbor) | XP/7 network stacks |
| CVE-2017-8464 | LNK file (used in Industroyer) | ICS-specific malware vector |
| CVE-2019-0211 | Apache httpd | Web-based HMI dashboards |

### Remediation Card Content Rules

1. **No jargon.** "Click Programs" not "navigate to Add/Remove Programs in control panel applet."
2. **Numbered steps.** Max 6 steps per card.
3. **Verification step always last.** "To confirm this worked, run: `sc query mrxsmb10`"
4. **Escalate condition always stated.** "If this step requires a password you don't have, click Escalate."
5. **Language reviewed by a non-technical person before shipping.**

### RemediationCard View — WPF Binding

```xml
<!-- RemediationCard.xaml — simplified -->
<Grid>
    <StackPanel>
        <TextBlock Text="{Binding CveId}" Style="{StaticResource HeadingStyle}"/>
        <TextBlock Text="{Binding Component}" />
        <Border Background="{Binding SeverityBrush}">
            <TextBlock Text="{Binding Severity}" />
        </Border>
        <ItemsControl ItemsSource="{Binding Steps}">
            <ItemsControl.ItemTemplate>
                <DataTemplate>
                    <StackPanel Orientation="Horizontal">
                        <TextBlock Text="{Binding StepNumber}" />
                        <TextBlock Text="{Binding Text}" TextWrapping="Wrap"/>
                    </StackPanel>
                </DataTemplate>
            </ItemsControl.ItemTemplate>
        </ItemsControl>
        <StackPanel Orientation="Horizontal">
            <Button Content="Mark Resolved" Command="{Binding ResolveCommand}"/>
            <Button Content="Escalate" Command="{Binding EscalateCommand}"/>
            <Button Content="Export" Command="{Binding ExportCommand}"/>
        </StackPanel>
    </StackPanel>
</Grid>
```

### Phase 4 Exit Criteria

```
✅ All 20 CVE remediation cards authored in definitions pack JSON
✅ Pack schema validated against cve-pack.schema.json
✅ RemediationCard view displays correct steps for each CVE
✅ "Mark Resolved" updates Findings.ResolvedAt and Dashboard badge refreshes
✅ "Escalate" sets EscalatedAt and triggers single-finding PDF export
✅ Non-technical reviewer has read and approved all card language
✅ VaxDock renders cards with no CVE-not-found errors for all 20 CVEs
```

---

## Phase 5 — PLC Banner Grab

> **Risk:** SharpPcap on locked HMIs (WinPcap/Npcap may not be installable). Raw socket fallback needed.
> **Dependency:** Phase 1 complete (check framework in place).

### Goal

`PlcNeighborCheck` implemented. On networked HMIs, passive ARP identifies live hosts. TCP banner grab fingerprints PLC devices. Results flow into `.vax` and appear in `DeviceDetail` PLC Neighbors panel.

### SharpPcap Dependency Risk

| Scenario | Approach |
|---|---|
| Npcap installable on HMI | Full SharpPcap passive capture — preferred |
| HMI locked, no driver install | Raw socket ARP fallback (see below) |
| No network adapter | Check skipped silently — `PlcNeighbors = []` |

```csharp
// PlcNeighborCheck.cs — adapter selection
#if NET8_0
    // Try SharpPcap first; fall back to raw socket
    try { return SweepWithSharpPcap(context); }
    catch (PcapException) { return SweepWithRawSocket(context); }
#else
    // net35: raw socket only
    return SweepWithRawSocket(context);
#endif
```

### Raw Socket ARP Fallback

```csharp
private static List<string> ArpSweepRawSocket(string subnet)
{
    // Send ARP requests via UDP broadcast trick on Windows
    // Parse ARP cache via: arp -a (process execution)
    // Filter by subnet — no packet capture needed
}
```

### Banner Grab Protocol Table

| Protocol | Port | Function | Read-Only Guarantee |
|---|---|---|---|
| Siemens S7comm | TCP 102 | ISO-TSAP CONNECT → S7 Read SZL (system info) | SZL read = info only, no write function |
| Modbus TCP | TCP 502 | FC 43 subfunction 14 (Read Device ID) | FC 43 = read-only by spec |
| Mitsubishi MELSEC | TCP 5007 | SLMP version read | Response parsing only |
| Toyopuc | TCP 1024 | Raw banner read (first 256 bytes) | No command sent |

### Phase 5 Exit Criteria

```
✅ PlcNeighborCheck runs and returns PlcNeighbors[] on networked HMI (real hardware test)
✅ PlcNeighborCheck skips gracefully when no network adapter present
✅ SharpPcap unavailable → raw socket path activates without error
✅ No write Modbus function codes used (verify with Wireshark on test network)
✅ PLC Neighbors visible in VaxDock DeviceDetail panel
✅ Banner grab timeout respected (3s per host per port — test on unreachable host)
```

---

## Phase 6 — HID Launcher

> **Risk:** HID keyboard policy block on target — fallback must be ready.
> **Dependency:** None — can develop in parallel with Phase 4/5.

### Goal

VaxDrive auto-launches VaxAgent on plug-in with no operator action. AHK stub compiled and signed. ATtiny85 firmware path documented.

### AutoHotkey Stub

```autohotkey
; VaxLauncher.ahk
#NoTrayIcon
#Persistent

; Wait for drive to settle
Sleep, 1000

; Find drive letter where this script lives
SplitPath, A_ScriptDir, , , , , driveLetter
driveLetter := SubStr(A_ScriptDir, 1, 2)  ; e.g. "E:"

; Launch VaxAgent from engine partition
Run, %driveLetter%\engine\VaxAgent.exe, %driveLetter%\engine, Hide
ExitApp
```

Compiled with `Ahk2Exe.exe /in VaxLauncher.ahk /out VaxLauncher.exe`. Code-signed before committing.

### HID Policy Block Mitigations

| Blocker | Mitigation |
|---|---|
| Group Policy disables HID keyboards | Physical SCAN button (ATtiny85) — no policy hooks HID BUTTON |
| SmartScreen blocks unsigned exe | VaxLauncher.exe + VaxAgent.exe must be code-signed |
| AppLocker blocks unknown executables | Code signing + allowlist the drive publisher OID — document for IT |
| Windows Defender quarantines stub | Submit to MSDN for whitelist; document exception request |

### ATtiny85 Path (OPEN-2)

```
/Launcher/attiny85/vax_hid.ino
  - Arduino HID keyboard library
  - On button press: send Win+R, wait 500ms, type drive path, send Enter
  - Flashed with AVRDUDE via USB programmer
  - No PC driver needed — presents as standard HID keyboard to host
```

Document: `Launcher/attiny85/README.md` with wiring diagram and flash instructions.

### Phase 6 Exit Criteria

```
✅ VaxLauncher.exe compiled from source (reproducible build)
✅ VaxLauncher.exe code-signed
✅ Plug VaxDrive into vanilla Windows 10 → VaxAgent launches within 5 seconds
✅ Plug VaxDrive into Windows with HID policy → physical SCAN button path documented
✅ ATtiny85 firmware builds without errors (Arduino IDE)
✅ ATtiny85 README explains wiring and flash procedure
```

---

## Phase 7 — Polish + Export

> **Risk:** Non-technical UX — cadence alerts and trend view must be comprehensible to plant floor staff.
> **Dependency:** Phase 3 complete (data layer), Phase 4 complete (resolved state).

### Goal

Cadence alerts, trend view, PDF/CSV export. VaxDock ready for field deployment.

### Cadence Alert UI

```
Dashboard top banner:
┌────────────────────────────────────────────────────────┐
│ ⚠️  3 devices have not been scanned in over 7 days.   │
│     HMI-04 · HMI-09 · ENG-PC-3                        │
│                                           [Dismiss]    │
└────────────────────────────────────────────────────────┘
```

- Dismisses per session (not persisted — always re-evaluates on launch)
- Configurable threshold: `appsettings.json` → `"CadenceThresholdDays": 7`

### TrendView — WPF Canvas Chart

No third-party charting library. WPF `Canvas` + `Polyline` + `Path`:

```csharp
// TrendView.xaml.cs (code-behind)
private void DrawTrendLine(Canvas canvas, IList<ScanSummary> scans)
{
    var points = scans.Select((s, i) =>
        new Point(
            i * (canvas.ActualWidth / scans.Count),
            canvas.ActualHeight - (s.CriticalCount * scaleY)
        )).ToList();

    var polyline = new Polyline
    {
        Points = new PointCollection(points),
        Stroke = Brushes.OrangeRed,
        StrokeThickness = 2
    };
    canvas.Children.Add(polyline);
}
```

### Export

#### PDF Export (WPF built-in)

```csharp
// ExportService.cs
public void ExportDevicePdf(string deviceId, string outputPath)
{
    // Build FlowDocument with device summary + findings table
    var doc = BuildFlowDocument(deviceId);

    // Use XpsDocument → convert to PDF via Windows print
    var pd = new PrintDialog();
    pd.PrintDocument(doc.DocumentPaginator, $"VaxDrive Report {deviceId}");
}
```

> Alternative: Use `XpsDocument` → save as `.xps` → convert to PDF via built-in Windows PDF printer. Zero external dependencies.

#### CSV Export

```csharp
public void ExportDeviceCsv(string deviceId, string outputPath)
{
    var findings = _findingRepository.GetByDevice(deviceId);
    using var writer = new StreamWriter(outputPath, false, Encoding.UTF8);
    writer.WriteLine("CVE,Severity,Component,Status,RemediationId,ResolvedAt");
    foreach (var f in findings)
        writer.WriteLine($"{f.CveId},{f.Severity},{f.Component},{f.Status},{f.RemediationId},{f.ResolvedAt}");
}
```

RFC 4180 compliant — escape commas in field values.

### Phase 7 Exit Criteria

```
✅ Cadence alert correctly identifies and names overdue devices
✅ Configurable threshold respected (test with 1-day threshold on stale data)
✅ TrendView chart renders correctly for 1, 6, and 12 scan cycles
✅ Mark Resolved events visible as annotations on trend chart
✅ PDF export produces readable report with device name, date, findings table
✅ CSV export opens correctly in Excel without encoding issues
✅ Non-technical user can interpret all UI elements without documentation
✅ Full end-to-end test: scan on test HMI → .vax → ingest → triage → resolve → export PDF
```

---

## CI/CD Pipeline Specification

```yaml
# .github/workflows/build.yml (skeleton — expand per phase)

name: VaxDrive CI

on: [push, pull_request]

jobs:
  build-agent-net8:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: '8.0.x' }
      - run: dotnet build VaxAgent/VaxAgent.csproj -f net8.0 -c Release
      - run: dotnet publish VaxAgent/VaxAgent.csproj -f net8.0 -c Release -r win-x64 --self-contained

  build-agent-net35:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - run: dotnet build VaxAgent/VaxAgent.csproj -f net35 -c Release

  build-dock:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: '8.0.x' }
      - run: dotnet build VaxDock/VaxDock.csproj -c Release

  test:
    runs-on: windows-latest
    needs: [build-agent-net8, build-dock]
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: '8.0.x' }
      - run: dotnet test Tests/VaxAgent.Tests/VaxAgent.Tests.csproj --logger trx
      - run: dotnet test Tests/VaxDock.Tests/VaxDock.Tests.csproj --logger trx

  secret-scan:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with: { fetch-depth: 0 }
      - uses: gitleaks/gitleaks-action@v2
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
```

---

## Testing Strategy Summary

| Test Type | Where | Tools | Coverage Target |
|---|---|---|---|
| Check unit tests | `VaxAgent.Tests/Checks/` | xUnit + Moq (mock WMI/registry) | Each check independently |
| Crypto round-trip | `VaxAgent.Tests/Crypto/` | xUnit | Encrypt→decrypt, tamper detection |
| Ingest pipeline | `VaxDock.Tests/IngestPipeline/` | xUnit + temp file fixtures | HMAC pass + fail paths |
| Repository SQL | `VaxDock.Tests/Data/` | xUnit + in-memory SQLite | All repository methods |
| Integration | Manual, Phase exit criteria | Real hardware | Full end-to-end scan → ingest |

**Not automated (requires real hardware):**
- WMI queries on real Windows machines
- USB insertion detection on real drives
- PLC banner grab on real S7/Modbus hardware
- HID launcher on real target hosts

---

## Known Risks Register

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| SharpPcap not installable on locked HMI | High | Medium | Raw socket ARP fallback (Phase 5) |
| IronKey SDK unavailable or costly (OPEN-1) | Medium | High | Boot-partition token stub — ships as interim solution |
| .NET 3.5 WMI returns different schemas on XP | Medium | Medium | XP test VM in CI — mock WMI in unit tests |
| AHK stub blocked by AppLocker (Phase 6) | Medium | Medium | Code signing + ATtiny85 fallback |
| Plant IT blocks unsigned executables | High | High | Code signing mandatory before any field deployment |
| Operator misunderstands remediation step | Medium | High | Non-technical language review in Phase 4 |
| OLED driver unavailable on off-shelf hardware (OPEN-3) | High | Low | Console fallback — `ProgressReporter` abstracts both |
| .vax file accumulation fills /results | Low | Medium | VaxDock prunes synced files — document archive policy |

---

## Glossary

| Term | Definition |
|---|---|
| **.vax file** | AES-256-GCM encrypted + HMAC-SHA256 signed scan output file |
| **VaxAgent** | C# binary that runs on the target device and performs checks |
| **VaxDock** | WPF companion laptop app that ingests and triages .vax files |
| **VaxDrive** | Physical hardened USB device carrying VaxAgent and results |
| **Device fingerprint** | Stable SHA-256 derived identifier from ComputerName + BIOS serial + install date |
| **CVE pack** | OT-scoped signed JSON file containing CVE rules and remediation cards |
| **Hardware token** | Per-drive unique secret used for AES key derivation — never leaves drive |
| **Quarantine** | State a .vax file enters when HMAC verification fails — never ingested |
| **HID spoof** | Drive presents as USB HID keyboard to type launch command automatically |
| **IOC** | Indicator of Compromise — known-bad process name or artifact |
| **OPEN-N** | Decision items requiring investigation before that feature can ship |

---

## Phase 9 — Enterprise Edition (v3.0.0)

> **Risk:** Hardware dependency (IronKey SDK). Performance overhead of memory scanning (YARA).
> **Dependency:** 2.0.0 deployment feedback.

### Goal

Transform VaxDrive from a foundational OT scanner into an enterprise-grade threat hunting platform with hardware-backed security, deep protocol discovery, and advanced intelligence.

### 1. Hardware Token Integration (Resolve OPEN-1)
- Replace the deterministic `/boot/device.token` stub.
- Integrate Kingston / IronKey APIs or equivalent PKCS#11 hardware security modules.
- Ensure cryptographic keys are derived natively from the USB drive's secure microcontroller, guaranteeing they never leave the drive.

### 2. Expanded Protocol Parsing (Deep Discovery)
- Build upon the passive ARP and Modbus/S7 banner grab.
- Add robust discovery parsers for **DNP3**, **EtherNet/IP (CIP)**, and **BACnet**.
- Create a unified `ProtocolAsset` model to map the entire factory floor network topology.

### 3. YARA Rule Integration
- Integrate a lightweight YARA engine (`libyara.NET`) into `VaxAgent.exe`.
- Distribute OT-specific YARA signatures via the Definitions Pack.
- Scan target memory space and critical file paths for advanced malware variants (e.g., Industroyer, Triton, BlackEnergy).

### 4. NVD Pagination Engine
- Expand the `NvdSyncService` in VaxDock to support a resilient background crawler.
- Implement API pagination to download and cache the entire NVD database incrementally.
- Automatically compute severity deltas and flag new zero-days against previously ingested device inventories.

---

*Roadmap maintained by the VaxDrive engineering team. Phase exit criteria are gates — do not advance to next phase without meeting all criteria.*

---

## Technical Debt Sprints (v3.4.0+)

### Sprint A
- [x] Fix: AES-CBC/HMAC-SHA256 fallback for .NET 3.5
- [x] Fix: Silent Crash Logging
- [x] Fix: LegacyDefinitionLoader hand-rolled JSON parser
- [x] Fix: Atomic Directory Renames via SelfUpdateService
- [x] Fix: UDP Alert retry queue

### Sprint B
- [ ] Fix 3: Orchestrator watchdog thread
- [ ] Fix 10: SemVer range parser CveMatchCheck
- [ ] Fix 15: YARA overlap buffer
- [ ] Fix 13: Drive path resolution
