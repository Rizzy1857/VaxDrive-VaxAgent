@echo off
:: VaxDrive Target Payload Launcher
:: This script is invoked by the ATtiny85 HID payload once the .vaxdrive marker is found.

:: Provide fallback hardware token provider for testing
set VAXDRIVE_HARDWARE_TOKEN_PROVIDER=MOCK

:: Seamlessly execute the real one-shot VaxAgent orchestrator in the background
start "" "%~dp0VaxAgent.exe" --scan

exit
