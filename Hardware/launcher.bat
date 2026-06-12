@echo off
:: VaxDrive Target Payload Launcher
:: This script is invoked by the ATtiny85 HID payload once the .vaxdrive marker is found.

:: Seamlessly execute the VaxAgent binary in the background, relative to this script's path
start "" "%~dp0VaxAgent.exe"

exit
