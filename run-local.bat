@echo off
echo Starting AutoCAD Floor Plan Analysis System...
echo.

echo Building the application...
dotnet build src\Api\FloorPlanAPI.csproj
if %ERRORLEVEL% neq 0 (
    echo Build failed!
    pause
    exit /b 1
)

echo.
echo Starting the API server...
echo The API will be available at: http://localhost:5000
echo Swagger UI will be available at: http://localhost:5000/swagger
echo.
echo Press Ctrl+C to stop the server
echo.

cd src\Api
dotnet run --urls "http://localhost:5000"