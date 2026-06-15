# Air-Gapped Deployment Bundle Manifest

This manifest documents the exact contents, purposes, and integrity hashes of the `deploy_payload` package meant for air-gapped field deployment.

## Section 1: Agent Binaries

| Filename | SHA-256 (Example/Placeholder) | Purpose | Required |
|:---|:---|:---|:---:|
| `VaxAgent.exe` | *[Computed at Build]* | Primary .NET 8 Single-File Agent Daemon | Y |
| `VaxDock.dll` | *[Computed at Build]* | Shared orchestration and data access library | Y |
| `manifest.json` | *[Computed at Build]* | HMAC-signed integrity manifest | Y |

## Section 2: Native Dependencies

| Filename | SHA-256 (Example/Placeholder) | Purpose | Required |
|:---|:---|:---|:---:|
| `native/win-x64/yara.dll` | *[Computed at Build]* | Unmanaged YARA scanning engine | Y |

## Section 3: Config Templates

| Filename | SHA-256 (Example/Placeholder) | Purpose | Required |
|:---|:---|:---|:---:|
| `.env.template` | *[Computed at Build]* | Defines required environment variables. Prompted on install. | Y |

## Section 4: Update Bundle Structure

When delivering updates to an offline endpoint, the update payload must be dropped into the `updates\` folder on the VAXDRIVE.

| Filename | Purpose | Required |
|:---|:---|:---:|
| `updates/manifest.json` | The HMAC-signed manifest defining the update files. | Y |
| `updates/VaxAgent.exe` | The newer version of the agent daemon. | Y |
| `updates/native/win-x64/yara.dll` | Any updated unmanaged libraries. | N |

> [!WARNING]
> The `SelfUpdateService` will abort the update entirely if *any* file specified in the `updates/manifest.json` has a mismatched SHA-256 hash or is missing.

## Section 5: Operator Pre-Flight Checklist

Field operators must complete this checklist before inserting the VAXDRIVE into a Level 2/3 target endpoint.

- [ ] **1. Integrity Verification:** Run `build/PrepareUsb.ps1` and confirm it outputs `USB READY`.
- [ ] **2. Hardware Token Check:** Confirm the Kingston/IronKey HSM is plugged in and initialized.
- [ ] **3. Environment Config:** Review `.env.template` and gather required values (e.g., `VAXDRIVE_DB_KEY`, `NVD_API_KEY`).
- [ ] **4. Write Protection:** Ensure any physical write-protect switches on the USB drive are set to **OFF** during installation, then switched to **ON** during passive monitoring.
- [ ] **5. Authorization:** Verify active Change Board approval for executing `.exe` payloads on the target HMI/SCADA host.
