# SmartPay Feature Tracking System

> **Live feature inventory with automated regression testing and test coverage mapping**

## Overview

This directory contains the **authoritative source of truth** for SmartPay platform features, their implementation status, test coverage, and regression testing protocols. Every feature MUST be tracked here with corresponding tests before it can be committed to GitHub.

## 📁 Directory Structure

```
WPS/FEATURES/
├── SmartPay_Feature_Inventory.csv     # Master feature inventory
├── Smoke_Features.md                  # Feature testing guide
├── README.md                          # This file
├── automation/                        # Automation scripts
│   ├── feature-validator.ps1          # Main validation script
│   ├── pre-commit-hook.ps1           # Pre-commit regression check
│   ├── test-coverage-mapper.ps1      # Coverage analysis
│   └── report-archiver.ps1           # Report management
├── reports/                           # Test reports (auto-generated)
│   ├── validation-report-*.json      # Validation results
│   ├── coverage-report-*.json        # Coverage analysis
│   └── archive/                       # Older reports (auto-cleaned)
└── reports/archive/                   # Historical reports
```

## 🏗️ Feature Hierarchy

Features are organized using a hierarchical identifier system:

- **Epic (E#)**: Major feature groups (e.g., `E4` Advanced Features)
- **Feature (E#.F#)**: Specific features (e.g., `E4.F5` FX Quote Created Trigger)
- **Nugget (E#.F#.N#)**: Implementation details (e.g., `E4.F2.N1` Supabase Realtime Provider)
- **Requirement (E#.F#.R#)**: Specific requirements at any level

### Current Epics

- **E1**: Platform Foundation (FastEndpoints, Health Checks, CI/CD)
- **E2**: Foreign Exchange Core (Quotes, Persistence, Pricing)
- **E3**: Payment Provider Orchestration (Planned)
- **E4**: Advanced Features (Notifications, Multi-tenancy, Analytics)
- **E5**: Security & Secret Hygiene
- **E6**: Developer Experience & Observability
- **E7**: Blazor SSR Admin UI (Planned)
- **E8**: Low-Cost Azure Hosting (Planned)

## 📊 CSV Schema

The feature inventory CSV contains these columns:

| Column | Description | Example |
|--------|-------------|---------|
| `ID` | Hierarchical feature identifier | `E4.F5` |
| `Name` | Human-readable feature name | `FX Quote Created Trigger` |
| `Category` | User-facing or Under-the-Hood | `User-facing` |
| `Component(s)` | Technical components involved | `FxQuoteTriggerMiddleware` |
| `Status` | Implementation status | `Implemented (WP4.1)` |
| `Invocation` | How to trigger/use | `On POST /api/fx/quote` |
| `Inputs` | Required inputs | `Response JSON` |
| `Outcome` | Expected result | `Emit fx.quote.created event` |
| `Work Package` | Which WP delivered it | `WP4.1` |
| `Value` | Business value proposition | `Instant UI refresh` |
| `Test_Coverage_%` | Current test coverage | `92%` |
| `Test_IDs` | Mapped test identifiers | `T4.5.1,T4.5.2,T4.5.3` |
| `Requirements` | Detailed requirements | `E4.F5.R1: Event emitted on successful quote` |
| `Last_Tested` | Last validation date | `2025-09-14` |
| `Test_Status` | Test result status | `PASS` |
| `Regression_Risk` | Risk level if failing | `HIGH` |

## 🔄 Pre-Commit Protocol

### MANDATORY: Run Before Every Commit

```bash
# 1. Ensure API is running
cd ZEN
dotnet run --project SOURCE/Api

# 2. Run pre-commit validation (in separate terminal)
cd WPS/FEATURES/automation
./pre-commit-hook.sh
```

### What It Checks

✅ **Feature Regression Tests**: All `HIGH` risk features must pass
✅ **API Health**: API must be responding
✅ **Test Coverage**: Minimum 60% coverage maintained
✅ **Critical Endpoints**: Core functionality validated

### Failure Handling

- **HIGH regression risk failures** → **COMMIT BLOCKED** 🛑
- **Non-critical failures** → **Commit allowed with warnings** ⚠️
- **Coverage below 60%** → **COMMIT BLOCKED** 🛑

## 🧪 Testing Commands

### Quick Feature Validation
```bash
./automation/feature-validator.sh precommit
```

### Full Feature Analysis
```bash
./automation/feature-validator.sh full
```

### Update Coverage Data
```bash
./automation/test-coverage-mapper.sh --update-csv
```

### Archive Old Reports
```bash
./automation/report-archiver.sh
```

### Test System
```bash
./automation/simple-test.sh
```

## 📈 Test Coverage Integration

### Current Test Mapping

Features are automatically mapped to test coverage based on:

- **Test Project**: Which test assembly contains the tests
- **Test Classes**: Specific test class patterns
- **Code Paths**: Which code files/components are covered

Example mapping for `E4.F5` (FX Quote Trigger):
```powershell
'E4.F5' = @{
    TestProjects = @('Api.Tests')
    TestClasses = @('*FxQuoteTriggerTest*', '*TriggerTest*')
    CodePaths = @('*FxQuoteTrigger*', '*Trigger*')
}
```

### Coverage Thresholds

- **90-100%**: ✅ Excellent
- **60-89%**: ⚠️ Acceptable
- **Below 60%**: ❌ Requires improvement

## 🚨 Regression Risk Levels

### HIGH Risk Features
Features that **MUST NOT** regress (blocks commits):
- `E2.F1` (Create FX Quote) - Core business functionality
- `E4.F1` (SignalR Hub) - Real-time infrastructure
- `E4.F5` (FX Quote Trigger) - Event-driven notifications
- `E5` (Security features) - Critical for compliance

### MEDIUM Risk Features
Important features that generate warnings:
- Multi-tenancy components
- Performance monitoring
- Database health checks

### LOW Risk Features
Non-critical features that can temporarily fail:
- Documentation updates
- Logging enhancements
- Development tools

## 🔧 Maintenance

### Weekly Tasks (Automated)
- Archive reports older than 7 days
- Delete archived reports older than 30 days
- Update test coverage percentages
- Generate trend analysis

### Manual Updates Required
- New feature additions to CSV
- Test mapping updates when tests change
- Requirements updates when specifications change
- Risk level adjustments based on operational experience

## 🔗 Integration Points

### Git Hooks
Install the pre-commit hook:
```bash
cp WPS/FEATURES/automation/pre-commit-hook.sh .git/hooks/pre-commit
chmod +x .git/hooks/pre-commit
```

### CI/CD Pipeline
The validation scripts integrate with GitHub Actions:
- PR checks run `feature-validator.sh precommit`
- Nightly builds run `feature-validator.sh full`
- Coverage reports auto-update the CSV

### Development Workflow
1. Implement feature
2. Add tests with coverage ≥60%
3. Update feature inventory CSV
4. Run `./automation/feature-validator.sh`
5. Fix any failures
6. Commit (pre-commit hook validates automatically)

## 🎯 Quality Gates

### Before WP Release
- All Epic features have ≥80% coverage
- No HIGH risk regressions
- All requirements validated
- Documentation current

### Before Production Deploy
- All MEDIUM+ risk features passing
- Coverage reports generated
- Regression test evidence archived
- Performance thresholds validated

## 📞 Support

If the pre-commit hook blocks your commit:

1. **Check API**: Is the API running at `http://localhost:5001`?
2. **Run Tests**: `cd ZEN && dotnet test`
3. **Check Coverage**: Look for coverage reports in `reports/`
4. **Fix Issues**: Address failing features or update CSV if needed
5. **Re-attempt**: The hook will re-validate on next commit attempt

For questions or issues with the feature tracking system, refer to the test output and generated reports in the `reports/` directory.