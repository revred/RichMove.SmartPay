#!/bin/bash

echo "🚀 Starting SmartPay services..."

# Get the script directory and navigate to project root
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
cd "$PROJECT_ROOT"

# Kill any existing services first
"$SCRIPT_DIR/kill-services.sh"

echo "🔧 Starting API service..."
cd ZEN/SOURCE/Api
dotnet run &
API_PID=$!

echo "🎨 Starting Admin service..."
cd ../../ADMIN/SmartPay.AdminBlazor
dotnet run &
ADMIN_PID=$!

cd "$PROJECT_ROOT"

echo "📊 Services starting..."
echo "  API PID: $API_PID (http://localhost:5268)"
echo "  Admin PID: $ADMIN_PID (https://localhost:50985)"
echo ""
echo "⏳ Waiting for services to initialize..."
sleep 5

echo "🔍 Testing service health..."
if curl -s http://localhost:5268/health/live > /dev/null 2>&1; then
    echo "  ✅ API is healthy"
else
    echo "  ⚠️  API may still be starting..."
fi

if curl -s https://localhost:50985 -k > /dev/null 2>&1; then
    echo "  ✅ Admin is responding"
else
    echo "  ⚠️  Admin may still be starting..."
fi

echo ""
echo "🌐 Access URLs:"
echo "  API: http://localhost:5268"
echo "  Admin: https://localhost:50985"
echo "  Swagger: http://localhost:5268/swagger"
echo ""
echo "🛑 To stop services, run: ZEN/TOOLS/kill-services.sh"