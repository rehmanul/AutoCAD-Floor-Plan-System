@echo off
echo Deploying AutoCAD Plugin to Forge Design Automation...

echo.
echo Step 1: Building AutoCAD Plugin...
cd src\AutoCADPlugin
dotnet build -c Release
if %errorlevel% neq 0 (
    echo Failed to build plugin
    pause
    exit /b 1
)

echo.
echo Step 2: Creating plugin bundle...
rmdir /s /q bundle 2>nul
mkdir bundle
copy bin\Release\net8.0-windows\AutoCADPlugin.dll bundle\

echo.
echo Step 3: Creating PackageContents.xml...
echo ^<?xml version="1.0" encoding="utf-8"?^> > bundle\PackageContents.xml
echo ^<ApplicationPackage SchemaVersion="1.0" Version="1.0.0" ProductCode="{F11AD4E7-BC7C-4F59-B077-84C8BBFB533D}" Name="FloorPlanProcessor" Description="Floor Plan Processing Plugin" Author="FloorPlan System"^> >> bundle\PackageContents.xml
echo   ^<CompanyDetails Name="FloorPlan System" /^> >> bundle\PackageContents.xml
echo   ^<Components Description="Floor Plan Processing Components"^> >> bundle\PackageContents.xml
echo     ^<RuntimeRequirements Platform="AutoCAD" Version="24.0" /^> >> bundle\PackageContents.xml
echo     ^<ComponentEntry AppName="FloorPlanProcessor" Version="1.0.0" ModuleName="./AutoCADPlugin.dll" AppDescription="Floor Plan Processing Plugin" LoadOnCommandInvocation="True" LoadOnAutoCADStartup="True"^> >> bundle\PackageContents.xml
echo       ^<Commands GroupName="FloorPlanProcessor_Commands"^> >> bundle\PackageContents.xml
echo         ^<Command Global="PROCESS_FLOOR_PLAN" Local="PROCESS_FLOOR_PLAN" /^> >> bundle\PackageContents.xml
echo       ^</Commands^> >> bundle\PackageContents.xml
echo     ^</ComponentEntry^> >> bundle\PackageContents.xml
echo   ^</Components^> >> bundle\PackageContents.xml
echo ^</ApplicationPackage^> >> bundle\PackageContents.xml

echo.
echo Step 4: Creating ZIP bundle...
powershell -Command "Compress-Archive -Path 'bundle\*' -DestinationPath 'FloorPlanProcessor.zip' -Force"

echo.
echo Step 5: Uploading to Forge Design Automation...
cd ..\..
node deploy-to-forge.js

echo.
echo Plugin deployment complete!
pause