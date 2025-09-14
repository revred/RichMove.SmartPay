# Patch Notes — 0037 Core API Perf Suite (Release + ANALYSIS + YAML)

**What changed vs. 0033:**
1. **Release mode** reinforced in docs and CI (already used; now documented explicitly).
2. **Folder move**: perf artifacts live under **`ANALYSIS/PERF`** (not `DOCS/PERF`).
3. **YAML-based tuning**: gates and baselines are now configured via **YAML** (`PerfGate.yaml`, `BestKnown.yaml`). The perf runner reads YAML using **YamlDotNet**.

**Updated files:**
- `ANALYSIS/PERF/ServiceGuarantees.md` (SLOs, gates, workflow).
- `ANALYSIS/PERF/PerfGate.yaml` (absolute + regression budgets).
- `ANALYSIS/PERF/Baselines/BestKnown.sample.yaml` (template baseline).
- `ZEN/PERF/PerfRunner/SmartPay.PerfRunner.csproj` (+YamlDotNet).
- `ZEN/PERF/PerfRunner/Program.cs` (YAML loader + new paths).
- `.github/workflows/perf.yml` (kept **Release** build/run).

**Migration (if you applied 0033)**
- You can delete `DOCS/PERF/*` after adopting this patch. The runner now reads only `ANALYSIS/PERF/*`.

**Usage**
```bash
ASPNETCORE_URLS=http://localhost:5000 dotnet run -c Release --project ZEN/SOURCE/Api &
SMARTPAY_BASE_URL=http://localhost:5000 \
SMARTPAY_RPS=75 SMARTPAY_DURATION_SECONDS=30 SMARTPAY_WARMUP_SECONDS=3 \
dotnet run -c Release --project ZEN/PERF/PerfRunner
```

**Start regression tracking**
- Copy `ANALYSIS/PERF/Baselines/BestKnown.sample.yaml` → `BestKnown.yaml` with your best p95/p99 values.

— ChatGPT