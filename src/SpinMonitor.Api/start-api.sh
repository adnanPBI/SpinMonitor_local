#!/bin/bash

echo "╔════════════════════════════════════════════════════════╗"
echo "║    SpinMonitor API - C# ASP.NET Core Startup Script   ║"
echo "╚════════════════════════════════════════════════════════╝"
echo ""

# Check if appsettings.json exists
if [ ! -f appsettings.json ]; then
    echo "⚠️  appsettings.json not found!"
    echo "Please create appsettings.json with your MySQL configuration."
    exit 1
fi

# Restore dependencies
echo "📦 Restoring NuGet packages..."
dotnet restore
echo "✓ Dependencies restored"
echo ""

# Build the project
echo "🔨 Building project..."
dotnet build
echo "✓ Build completed"
echo ""

# Run the API
echo "🚀 Starting SpinMonitor API..."
echo "📖 Swagger UI will be available at: http://localhost:5000"
echo ""
dotnet run
