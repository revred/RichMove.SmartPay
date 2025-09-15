# SmartPay Development Tools

This directory contains utility scripts for SmartPay development workflow.

## 🛠️ Available Scripts

### 🛑 `kill-services.sh`
**Purpose**: Stops all running SmartPay services
- Kills all dotnet processes related to SmartPay
- Frees up occupied ports (5268, 50985, 50986, 7169)
- Safe to run multiple times
- Cross-platform (Linux/Mac/Windows with Git Bash/WSL)

**Usage**:
```bash
ZEN/TOOLS/kill-services.sh
```

### 🚀 `start-services.sh`
**Purpose**: Starts both API and Admin services for development
- Automatically kills existing services first (clean startup)
- Starts API on `http://localhost:5268`
- Starts Admin on `https://localhost:50985`
- Performs health checks
- Shows service URLs and status

**Usage**:
```bash
ZEN/TOOLS/start-services.sh
```

**Service URLs**:
- API: http://localhost:5268
- Admin: https://localhost:50985
- Swagger: http://localhost:5268/swagger
- API Health: http://localhost:5268/health/live

## 🔄 Development Workflow

**Recommended workflow for clean development sessions**:

1. **Start Development Session**:
   ```bash
   ZEN/TOOLS/start-services.sh
   ```

2. **Stop Services** (when done):
   ```bash
   ZEN/TOOLS/kill-services.sh
   ```

3. **Restart Services** (after code changes):
   ```bash
   ZEN/TOOLS/start-services.sh  # Kills existing and starts fresh
   ```

## 📁 Script Locations

All scripts are located in `ZEN/TOOLS/` to maintain organized project structure:
- `ZEN/TOOLS/kill-services.sh`
- `ZEN/TOOLS/start-services.sh`

Scripts are designed to work when called from the project root directory.

## ⚡ Quick Reference

| Task | Command |
|------|---------|
| Start both services | `ZEN/TOOLS/start-services.sh` |
| Stop all services | `ZEN/TOOLS/kill-services.sh` |
| Clean restart | `ZEN/TOOLS/start-services.sh` |

## 🐛 Troubleshooting

**Port conflicts**: Run `ZEN/TOOLS/kill-services.sh` to free up ports
**Services not responding**: Check console output for errors, restart with `ZEN/TOOLS/start-services.sh`
**Permission denied**: Ensure scripts are executable with `chmod +x ZEN/TOOLS/*.sh`