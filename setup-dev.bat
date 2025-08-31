@echo off
echo Setting up development environment...

echo.
echo Checking PostgreSQL...
sc query postgresql-x64-17 | findstr RUNNING >nul
if %errorlevel% neq 0 (
    echo Starting PostgreSQL service...
    net start postgresql-x64-17
    if %errorlevel% neq 0 (
        echo Failed to start PostgreSQL. Please check installation.
        pause
        exit /b 1
    )
)

echo.
echo Starting Redis container...
docker run -d -p 6379:6379 --name redis-dev redis:alpine >nul 2>&1
echo Redis container started (or already running)

echo.
echo Checking Azure Storage Emulator...
AzureStorageEmulator.exe status >nul 2>&1
if %errorlevel% neq 0 (
    echo Starting Azure Storage Emulator...
    AzureStorageEmulator.exe start
    if %errorlevel% neq 0 (
        echo Azure Storage Emulator not found. Using Azurite instead...
        docker run -d -p 10000:10000 -p 10001:10001 -p 10002:10002 --name azurite-dev mcr.microsoft.com/azure-storage/azurite
    )
)

echo.
echo Setting up database...
cd src\Api
dotnet ef database update
if %errorlevel% neq 0 (
    echo Database setup failed. Creating database manually...
    createdb -h localhost -U postgres floorplan_dev
    dotnet ef database update
)

echo.
echo Development environment ready!
echo Starting API...
dotnet run --urls "http://localhost:5000"