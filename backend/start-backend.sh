#!/bin/bash

echo "╔════════════════════════════════════════════════════════╗"
echo "║       SpinMonitor Backend API - Startup Script        ║"
echo "╚════════════════════════════════════════════════════════╝"
echo ""

# Check if .env exists
if [ ! -f .env ]; then
    echo "⚠️  .env file not found!"
    echo "Creating .env from .env.example..."
    cp .env.example .env
    echo "✓ .env created"
    echo ""
    echo "Please edit the .env file with your MySQL credentials and run this script again."
    exit 1
fi

# Check if node_modules exists
if [ ! -d node_modules ]; then
    echo "📦 Installing dependencies..."
    npm install
    echo "✓ Dependencies installed"
    echo ""
fi

# Start the server
echo "🚀 Starting SpinMonitor Backend API..."
echo ""
npm start
