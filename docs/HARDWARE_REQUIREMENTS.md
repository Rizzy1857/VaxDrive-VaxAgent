# Hardware Requirements

This document outlines the physical hardware requirements for deploying the VaxDrive ecosystem.

## Development Requirements

What you need to build, test, and run VaxDrive today:

- **USB Flash Drive**: Any standard 16GB+ USB drive.
- **Security Laptop**: Windows 10/11 machine running VaxDock (4GB+ RAM, 10GB storage).
- **Npcap**: Version 1.60+ installed on the Security Laptop (required for SharpPcap if network discovery is enabled).

## Target Scanned Machine (HMI / Engineering Station)

The endpoints where VaxAgent.exe will execute:

- **Target Support**: Windows 7 SP1+, Windows 10, Windows 11.
- **Experimental Support**: Windows XP SP3 (`.NET 3.5` build).

**Execution Profile**:
- Network required: **NO**
- Elevation required: **NO** (graceful fallback expected if UAC triggers)
- Writes to host: **NO** (any artifacts are limited to normal OS execution telemetry)
- Runtime install required: **NO** (self-contained binaries)
- Launch Method: **Manual Execution** (Double-click `VaxAgent.exe` from the drive)

## Production Recommendations

For enterprise deployments on actual factory floors, the following are highly recommended:

- **Hardware Encrypted USB**: Devices like IronKey S1000 or Kingston Vault Privacy 80 providing AES-256 XTS onboard encryption.
- **Read-Only Deployment Mode**: Hardware or policy-level write protection on the `/engine` and `/definitions` partitions to prevent malware infection.
- **Digitally Signed Binaries**: Code signing certificates for all executables.
- **Asset Classification Configured**: Operator-defined criticality for accurate risk scoring.

## Future Hardware Enhancements

Concepts for future iterations of the platform:

- Physical scan button (custom enclosure)
- OLED status display for scan progress
