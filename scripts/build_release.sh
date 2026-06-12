#!/bin/bash
set -e

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DEPLOY_DIR="$REPO_ROOT/deploy_payload"

echo "[1] Cleaning deployment directory..."
rm -rf "$DEPLOY_DIR"
mkdir -p "$DEPLOY_DIR/logs"

echo "[2] Building VaxAgent (net8.0, Single File)..."
dotnet publish "$REPO_ROOT/VaxAgent/VaxAgent.csproj" -f net8.0 -c Release -r win-x64 --self-contained -o "$DEPLOY_DIR/Agent_Net8"

echo "[3] Building VaxAgent (net35, Legacy)..."
dotnet publish "$REPO_ROOT/VaxAgent/VaxAgent.csproj" -f net35 -c Release -o "$DEPLOY_DIR/Agent_Net35"

echo "[4] Building VaxDock (net8.0-windows)..."
dotnet publish "$REPO_ROOT/VaxDock/VaxDock.csproj" -c Release -o "$DEPLOY_DIR/VaxDock"

echo "[5] Staging VAXDRIVE USB Root Structure..."
# Copy primary net8.0 agent to the root
cp "$DEPLOY_DIR/Agent_Net8/VaxAgent.exe" "$DEPLOY_DIR/VaxAgent.exe" || echo "Warning: net8.0 compilation failed"

# Copy batch launcher
cp "$REPO_ROOT/Hardware/launcher.bat" "$DEPLOY_DIR/launcher.bat"

# Create marker file
touch "$DEPLOY_DIR/.vaxdrive"

echo "[6] (Skipped) Code Signing on Mac OS..."

echo "============================================="
echo "✅ Production Deployment Package Staged"
echo "Payload located at: $DEPLOY_DIR"
echo "Copy contents of '$DEPLOY_DIR' strictly to the root of the exFAT VAXDRIVE USB volume."
echo "============================================="
