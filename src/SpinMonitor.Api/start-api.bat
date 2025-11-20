@echo off
echo ╔════════════════════════════════════════════════════════╗
echo ║    SpinMonitor API - C# ASP.NET Core Startup Script   ║
echo ╚════════════════════════════════════════════════════════╝
echo.

REM Check if appsettings.json exists
if not exist appsettings.json (
    echo ⚠️  appsettings.json not found!
    echo Please create appsettings.json with your MySQL configuration.
    pause
    exit /b 1
)

REM Restore dependencies
echo 📦 Restoring NuGet packages...
dotnet restore
echo ✓ Dependencies restored
echo.

REM Build the project
echo 🔨 Building project...
dotnet build
echo ✓ Build completed
echo.

REM Run the API
echo 🚀 Starting SpinMonitor API...
echo 📖 Swagger UI will be available at: http://localhost:5000
echo.
dotnet run
pause
