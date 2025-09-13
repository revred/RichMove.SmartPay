# Secrets Folder

This directory is ignored by git (see `.gitignore`). Place **local-only** secret files here.

## Files
- `SMARTPAY-RED.secrets.sample.env` → template you can copy to `SMARTPAY-RED.secrets.env` (never commit).

## Usage (macOS/Linux)
```bash
set -a
source secrets/SMARTPAY-RED.secrets.env
set +a
```

## Usage (Windows PowerShell)
```powershell
Get-Content secrets/SMARTPAY-RED.secrets.env | ForEach-Object {
  if ($_ -match '^\s*#') { return }
  $kv = $_.Split('=',2)
  if ($kv.Length -eq 2) { [Environment]::SetEnvironmentVariable($kv[0], $kv[1]) }
}
```