@echo off
pushd "%~dp0"
echo Bootstrapping VaxDrive Solution...

dotnet new sln -n VaxDrive
dotnet sln add VaxAgent\VaxAgent.csproj
dotnet sln add VaxDock\VaxDock.csproj
dotnet sln add Tests\VaxAgent.Tests\VaxAgent.Tests.csproj
dotnet sln add Tests\VaxDock.Tests\VaxDock.Tests.csproj

echo Done! Open VaxDrive.sln in Visual Studio or Rider.
pause
