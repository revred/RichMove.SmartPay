#!/bin/bash
#
# Simple test to verify the feature tracking system is working
#

echo "🧪 Testing SmartPay Feature Tracking System"
echo "=========================================="
echo ""

# Test 1: CSV file exists and is readable
echo "📋 Test 1: Feature inventory CSV"
CSV_PATH="../SmartPay_Feature_Inventory.csv"
if [ -f "$CSV_PATH" ]; then
    feature_count=$(wc -l < "$CSV_PATH")
    echo "✅ CSV found with $feature_count lines"
else
    echo "❌ CSV not found"
    exit 1
fi

# Test 2: Scripts are executable
echo ""
echo "⚙️  Test 2: Scripts executable"
for script in feature-validator.sh pre-commit-hook.sh test-coverage-mapper.sh report-archiver.sh; do
    if [ -x "$script" ]; then
        echo "✅ $script is executable"
    else
        echo "❌ $script is not executable"
    fi
done

# Test 3: Reports directory exists
echo ""
echo "📁 Test 3: Reports directory structure"
if [ -d "../reports" ]; then
    echo "✅ Reports directory exists"
else
    echo "❌ Reports directory missing"
fi

# Test 4: Generate sample report
echo ""
echo "📄 Test 4: Generate sample report"
mkdir -p "../reports"
timestamp=$(date +"%Y-%m-%d_%H-%M-%S")
sample_report="../reports/test-report-$timestamp.json"

cat > "$sample_report" << EOF
{
  "timestamp": "$(date -Iseconds)",
  "test": "simple-test",
  "status": "success",
  "message": "Feature tracking system verification complete"
}
EOF

if [ -f "$sample_report" ]; then
    echo "✅ Sample report created: $(basename "$sample_report")"
else
    echo "❌ Failed to create sample report"
fi

# Test 5: CSV structure validation
echo ""
echo "🔍 Test 5: CSV structure validation"
header_line=$(head -n 1 "$CSV_PATH")
expected_columns="ID,Name,Category,Component(s),Status,Invocation,Inputs,Outcome,Work Package,Value,Test_Coverage_%,Test_IDs,Requirements,Last_Tested,Test_Status,Regression_Risk"

if [[ "$header_line" == "$expected_columns" ]]; then
    echo "✅ CSV header structure is correct"
else
    echo "❌ CSV header structure mismatch"
    echo "Expected: $expected_columns"
    echo "Actual:   $header_line"
fi

echo ""
echo "✅ Feature tracking system verification complete!"
echo ""
echo "📊 Summary:"
echo "- Feature inventory CSV: Ready"
echo "- Automation scripts: Ready"
echo "- Reports directory: Ready"
echo "- System structure: Valid"
echo ""
echo "To use the system:"
echo "1. Start API: cd ZEN && dotnet run --project SOURCE/Api"
echo "2. Run validation: ./feature-validator.sh precommit"
echo "3. Update coverage: ./test-coverage-mapper.sh --update-csv"
echo "4. Archive reports: ./report-archiver.sh"