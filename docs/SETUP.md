# VaxDrive Advanced Setup & Deployment Guide

This guide details the complete, end-to-end process for configuring your development environment, compiling the VaxDrive binaries, formatting the physical USB payload, and flashing the ATtiny85 hardware. 

---

## 1. Prerequisites

### Software Dependencies
- **.NET 8.0 SDK**: Required to compile the modern `VaxAgent` and `VaxDock` applications.
- **.NET Framework 3.5 Developer Pack**: Required to compile the legacy `VaxAgent` fallback for Windows XP/7 targets.
- **Arduino IDE**: Version 1.8.x or 2.x for flashing the hardware payload.
- **PowerShell 7+ or Bash**: Required to run the automated build scripts.

### Cryptographic Configuration
Before compiling, you **must** configure the cryptographic keys:
1. Locate the `.env.example` file in the repository root.
2. Copy it to `.env` (`cp .env.example .env`).
3. If they are not already populated, generate three strong 32-byte Base64 encoded keys:
   - `VAXDRIVE_MASTER_KEY`: Secures the AES-256-GCM payload.
   - `VAXDRIVE_HMAC_KEY`: Guarantees integrity against tampering.
   - `VAXDRIVE_BUILD_KEY`: Secures crash logs and self-update manifests.
   *(These must remain highly secure and air-gapped.)*

---

## 2. Compiling the C# Binaries

While you can compile via Visual Studio, the recommended approach for staging a production release is utilizing our automated build scripts which automatically trim the executables and stage them.

**On Windows (PowerShell):**
```powershell
.\scripts\build_release.ps1
```

**On Mac/Linux (Bash):**
```bash
chmod +x ./scripts/build_release.sh
./scripts/build_release.sh
```

**What this does:**
1. Compiles the `net8.0` single-file, self-contained `VaxAgent.exe`.
2. Compiles the legacy `net35` `VaxAgent.exe`.
3. Compiles the `VaxDock` WPF analyzer.
4. Stages the final payload into the `deploy_payload/` directory at the root of the repository.

*(Note: Production deployments require EV Code Signing. Edit `build_release.ps1` and inject your `CODE_SIGNING_THUMBPRINT` to automatically sign the binaries.)*

---

## 3. Formatting the Physical USB Drive

The physical USB drive is highly sensitive to correct formatting, as it must seamlessly bypass HMI restrictions.

1. Insert your target USB Drive.
2. Format the drive with the **exFAT** filesystem (crucial for cross-platform and large file support on modern systems).
3. Set the Volume Label to **exactly**: `VAXDRIVE`. *(The system relies on this label.)*
4. Copy the entire *contents* of your newly generated `deploy_payload/` folder to the **root** of the USB drive.

**Your USB Root Directory should look exactly like this:**
```text
VAXDRIVE (E:\)
├── .vaxdrive               (Empty marker file used by the HID payload)
├── launcher.bat            (The batch script that launches the agent)
├── VaxAgent.exe            (The primary net8.0 execution payload)
├── Agent_Net35/            (Legacy fallback payloads)
│   └── VaxAgent.exe
├── VaxDock/                (The analyzer tool used back at the analyst station)
└── logs/                   (Empty directory where the AuditLogger writes)
```

---

## 4. Flashing the ATtiny85 Hardware

The physical Digispark ATtiny85 emulator requires specific driver handling to flash successfully.

### Board Manager Configuration
1. Open the Arduino IDE.
2. Go to **File > Preferences**.
3. In "Additional Boards Manager URLs", paste:
   `http://digistump.com/package_digistump_index.json`
4. Go to **Tools > Board > Boards Manager**, search for "Digistump AVR Boards", and install it.
5. Go to **Tools > Board** and select **Digispark (Default - 16.5mhz)**.

### Library Dependencies
1. Go to **Sketch > Include Library > Manage Libraries**.
2. Search for and install **TinyWireM**.
3. Search for and install **Tiny4kOLED** (Required for the I2C display).

### Upload Sequence (Critical)
The Digispark does *not* behave like a standard Arduino. It must be unplugged during compilation.
1. Ensure the Digispark is **UNPLUGGED** from your USB port.
2. Open `Hardware/ATtiny85/VaxLauncher.ino` in the IDE.
3. Click the **Upload** arrow.
4. Watch the console output. When the console says: 
   `Running Digispark Uploader... Plug in device now... (will timeout in 60 seconds)`
5. **Now**, plug the Digispark into your USB port.
6. The upload will complete automatically in ~3 seconds.

You are now ready for field deployment.
