@echo off
echo ╔════════════════════════════════════════════════════════╗
echo ║       SpinMonitor Backend API - Startup Script        ║
echo ╚════════════════════════════════════════════════════════╝
echo.

REM Check if .env exists
if not exist .env (
    echo ⚠️  .env file not found!
    echo Creating .env from .env.example...
    copy .env.example .env
    echo ✓ .env created
    echo.
    echo Please edit the .env file with your MySQL credentials and run this script again.
    pause
    exit /b 1
)

REM Check if node_modules exists
if not exist node_modules (
    echo 📦 Installing dependencies...
    call npm install
    echo ✓ Dependencies installed
    echo.
)

REM Start the server
echo 🚀 Starting SpinMonitor Backend API...
echo.
call npm start
pause
