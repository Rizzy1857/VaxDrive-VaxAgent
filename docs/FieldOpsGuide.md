# VaxDrive Field Operations Guide

This guide is for factory floor IT and operational staff who deploy and manage the VaxDrive OT Security Agent.

---

## Section 1: Prerequisites

Before touching the VaxDrive or any factory equipment, ensure you have the following:

- [ ] **Hardware:** A blank USB flash drive (8GB+ recommended) and a physical Kingston or IronKey hardware security token.
- [ ] **Target OS:** The factory computer must be running Windows (XP through Windows 11 are supported).
- [ ] **Privileges:** You must have Administrator access to the factory computer to install the background service.

> [!WARNING]
> Do NOT connect the factory computer to the internet to download updates or dependencies. VaxDrive is designed to be 100% offline. Connecting factory machines to the internet violates core security protocols.

---

## Section 2: USB Prep

The USB drive must be prepared on a secure engineering workstation before taking it to the factory floor.

1. Plug the blank USB drive into the engineering workstation.
2. Note the drive letter assigned to the USB (e.g., `D`, `E`, `F`).
3. Open PowerShell as an Administrator.
4. Run the prep script: `.\build\PrepareUsb.ps1 -DriveLetter D` (replace `D` with your letter).

**What to Verify:**
- [ ] The script should output exactly: `USB READY [Letter] sha256=[hash]`.
- [ ] Open the drive in File Explorer. You should see folders named `VaxAgent`, `VaxDock`, `logs`, `reports`, and `updates`.
- [ ] A `prep_log` file should exist in the `logs` folder.

> [!WARNING]
> Entering the wrong drive letter in the script will erase all data on that drive. Triple-check the letter in File Explorer before hitting Enter.

---

## Section 3: First Boot

Take the prepared VaxDrive USB and the hardware token to the factory computer.

1. Plug in the hardware token first. Wait 5 seconds for it to light up.
2. Plug in the VaxDrive USB.
3. Open PowerShell as an Administrator on the factory computer.
4. Run the install script: `D:\build\InstallService.ps1 -DriveLetter D` (replace `D` with your letter).
5. **Prompts:** The script will ask you for configuration values (like the `VAXDRIVE_DB_KEY`). Type them in carefully and press Enter.

**What to Verify:**
- [ ] The script should end with: `Service VaxDriveAgent installed and running successfully.`
- [ ] Open `services.msc` and verify `VaxDriveAgent` is "Running".

> [!WARNING]
> Ensure the service is running as `NT SERVICE\VaxDriveAgent`. Running the service as the powerful `SYSTEM` account is strictly prohibited as it increases the risk of a full system takeover if compromised.

---

## Section 4: Daily Ops

When the agent is running in the background (Daemon mode), you can open a live dashboard to monitor the factory network.

1. Open a Command Prompt.
2. Type `D:\VaxAgent\VaxAgent.exe --daemon` and press Enter.
3. You will see the **Operator Console**.

**TUI Keys:**
- `[Q]`: Press Q to safely shut down the console and wipe the hardware token from memory.
- `[R]`: Press R to force an immediate scan for malware.
- `[S]`: Press S to force an update of the vulnerability database.

**Reading Reports:**
Check the `reports\` folder on the USB drive daily. You will find `.html` files containing plain-English tables of everything the agent found. You can open these safely in any web browser.

---

## Section 5: Updating

When engineering releases a new version of the agent, you will be given an "Update Bundle".

1. Take the VaxDrive USB back to your secure engineering workstation.
2. Copy the new files into the `updates\` folder on the USB drive.
3. Walk the USB back to the factory floor and plug it in.
4. The agent checks the `updates\` folder automatically every 24 hours.

**What to Verify:**
- [ ] Check the `logs\` folder for an audit entry saying `UPDATE READY - restart required`.
- [ ] Restart the factory computer (or restart the `VaxDriveAgent` service) to apply the update.
