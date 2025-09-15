#!/bin/bash

echo "🔄 Killing all SmartPay services..."

# Kill any dotnet processes running the API or Admin
pkill -f "dotnet.*Api" 2>/dev/null || true
pkill -f "dotnet.*Admin" 2>/dev/null || true
pkill -f "RichMove.SmartPay.Api" 2>/dev/null || true
pkill -f "SmartPay.AdminBlazor" 2>/dev/null || true

# Kill processes on common ports
lsof -ti:5268 2>/dev/null | xargs kill -9 2>/dev/null || true  # API
lsof -ti:50985 2>/dev/null | xargs kill -9 2>/dev/null || true # Admin HTTPS
lsof -ti:50986 2>/dev/null | xargs kill -9 2>/dev/null || true # Admin HTTP
lsof -ti:7169 2>/dev/null | xargs kill -9 2>/dev/null || true  # Alternative API port

# Windows-specific fallback (if running on Windows with WSL or Git Bash)
if command -v taskkill &> /dev/null; then
    taskkill //F //IM dotnet.exe 2>/dev/null || true
fi

echo "✅ All SmartPay services stopped"
sleep 2