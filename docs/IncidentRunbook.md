# Incident Response Runbook

This runbook outlines the required immediate actions when the VaxDrive agent detects anomalous or malicious activity on the factory floor.

---

## Scenario 1: YARA Hit Detected

**Trigger:** The Operator Console displays a YARA hit count greater than 0, or an alert log contains `CEF:0|...|YaraMatch`.
**Symptoms:** The agent has found a known malware pattern (e.g., Industroyer2, Triton) in the active memory of a running process on the factory computer.
**Immediate Actions:**
- [ ] Do NOT reboot the computer or unplug the network cable immediately, as memory forensics may be lost.
- [ ] Take a photo of the Operator Console showing the "last rule name".
- [ ] Retrieve the latest HTML report from the `reports\` folder to identify the specific Process ID (PID) that was flagged.
**Escalation:** Call the Level 3 Security Operations Center (SOC) immediately. Read them the rule name and PID. Stand by to physically isolate the machine if directed.

---

## Scenario 2: Hardware Token Failure

**Trigger:** The agent service repeatedly stops, or the Operator Console shows `[FATAL ERROR] Hardware token unavailable`.
**Symptoms:** The cryptographic hardware token (Kingston/IronKey) was removed, damaged, or its PIN was locked due to too many failed attempts.
**Immediate Actions:**
- [ ] Verify the hardware token is firmly plugged into a working USB port.
- [ ] Restart the `VaxDriveAgent` service in `services.msc`.
- [ ] If the service immediately stops again, check the `logs\` folder for an HMAC audit entry citing token failure.
**Escalation:** Do not attempt to format or guess the PIN of the hardware token. Return the token and the VaxDrive USB to the engineering team for a physical replacement.

---

## Scenario 3: Update Tamper Detected

**Trigger:** The `logs\` folder contains an entry reading: `[HMAC_AUDIT] | TamperAlert | Hash mismatch on file... Aborting update.`
**Symptoms:** Someone or something altered the update files in the `updates\` folder. The file hashes no longer match the digitally signed manifest.
**Immediate Actions:**
- [ ] Delete all contents inside the `updates\` folder immediately.
- [ ] Ensure the physical write-protect switch on the VaxDrive USB is currently set to ON (Read-Only).
- [ ] Generate a fresh HTML report using the Operator Console `[R]` key to ensure the system is clean.
**Escalation:** Provide the tamper audit log to the factory IT manager. A tampered update implies either a corrupted USB transfer or an active insider threat attempting to poison the agent.

---

## Scenario 4: NVD Sync Anomaly

**Trigger:** The HTML report highlights a CVE delta, or an alert is generated for an "Anomalous Shift".
**Symptoms:** A vulnerability severity score for a discovered asset suddenly increased by more than 3.0 points overnight.
**Immediate Actions:**
- [ ] Open the HTML report and locate the "CVE Severity Deltas" table.
- [ ] Identify the `CVE ID` and the specific factory asset (MAC/IP address) it impacts.
- [ ] Check if the asset is currently in active production or undergoing maintenance.
**Escalation:** Contact the plant engineering lead. Provide the CVE ID and the IP address. A jump of > 3.0 usually indicates a theoretical vulnerability was just weaponized in the wild, requiring emergency patching.

---

## Scenario 5: Agent Fails Self-Integrity Check

**Trigger:** The agent refuses to start entirely, logging `IntegrityCheckFailed` to the console or flat file.
**Symptoms:** The core `VaxAgent.exe` binary no longer matches its own digital signature. The file has been corrupted or maliciously modified by a virus on the factory computer.
**Immediate Actions:**
- [ ] Unplug the VaxDrive USB immediately.
- [ ] Assume the factory computer is compromised by a self-replicating virus (like Stuxnet).
- [ ] Do not plug the VaxDrive USB into any other computer on the network.
**Escalation:** Call the SOC and declare a potential host compromise. The engineering team must physically wipe and re-image the VaxDrive USB from a secure, clean terminal.
