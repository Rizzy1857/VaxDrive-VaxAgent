# VaxDrive v1.0.0 Release Checklist

This checklist must be fully signed off by the engineering and security leads before distributing the VaxDrive agent bundle.

## Pre-Release
- [ ] Build completes cleanly with zero warnings (`<TreatWarningsAsErrors>true`)
- [ ] All xUnit test suites pass successfully (100% pass rate)
- [ ] Fuzz harness tests pass successfully (zero unhandled exceptions)
- [ ] Roslyn analyzer checks (`CA2100`, `CA5350`, `CA5359`, `CA1031`, `CA2213`, `CA5397`) yield zero errors
- [ ] Final `manifest.json` is signed successfully with the production build key
- [ ] `HardeningChecklist.md` is fully completed and signed off by the security operator
- [ ] USB Prep script (`PrepareUsb.ps1`) tested successfully on a physical drive
- [ ] Service Installer (`InstallService.ps1`) installs cleanly on a fresh Windows system
- [ ] STRIDE threat model has been reviewed and accepted by stakeholders
- [ ] All field documentation (`FieldOpsGuide.md`, `IncidentRunbook.md`, `Architecture.md`) is complete and up to date

## Release
- [ ] Tag the `main` branch with `v1.0.0`
- [ ] Sign the `v1.0.0` git tag using the release team's GPG key
- [ ] Copy the finalized `deploy_payload` to the secure air-gap USB master bundle
- [ ] Verify the SHA-256 hash of the copied master bundle
- [ ] Notify all stakeholders (SOC, Plant Engineering) that the release is ready for deployment

## Post-Release
- [ ] Monitor the first 48 hours of HMAC audit logs from field deployments for anomalies
- [ ] Confirm the first NVD pagination sync completes successfully on a live field unit
- [ ] Confirm the `TopologyMap` successfully populates network assets in a production environment
- [ ] Archive all build artifacts securely for forensic reproducibility
- [ ] Close all related Phase 9-14 tracking tickets in the project board
