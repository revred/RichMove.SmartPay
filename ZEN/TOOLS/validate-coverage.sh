#!/bin/bash
# Script: validate-coverage.sh
# Purpose: Validate test coverage meets minimum threshold
# Usage: ./validate-coverage.sh [coverage-file] [threshold]

set -e  # Exit on any error

# Default values
COVERAGE_FILE=""
THRESHOLD="0.60"

# Parse arguments
if [ $# -ge 1 ]; then
    COVERAGE_FILE="$1"
fi

if [ $# -ge 2 ]; then
    THRESHOLD="$2"
fi

# Find coverage file if not specified
if [ -z "$COVERAGE_FILE" ]; then
    COVERAGE_FILE=$(find .. -name "coverage.cobertura.xml" 2>/dev/null | head -n 1)
fi

# Check if coverage file exists
if [ -z "$COVERAGE_FILE" ] || [ ! -f "$COVERAGE_FILE" ]; then
    echo "::warning::No coverage file found. Skipping coverage validation."
    echo "To generate coverage: dotnet test --collect:\"XPlat Code Coverage\""
    exit 0
fi

echo "Validating coverage from: $COVERAGE_FILE"

# Convert threshold to percentage for display (multiply by 100)
THRESHOLD_PERCENT=$(printf "%.0f" $(echo "$THRESHOLD * 100" | awk '{print $1 * $3}'))
echo "Minimum threshold: ${THRESHOLD_PERCENT}%"

# Extract line-rate from XML using grep and cut
LINE_RATE=$(grep -o 'line-rate="[0-9.]*"' "$COVERAGE_FILE" | head -1 | cut -d'"' -f2)

if [ -z "$LINE_RATE" ]; then
    echo "::error::Could not extract line-rate from coverage file"
    exit 1
fi

# Convert coverage to percentage for display
COVERAGE_PERCENT=$(printf "%.1f" $(awk "BEGIN {print $LINE_RATE * 100}"))
echo "Current coverage: ${COVERAGE_PERCENT}%"

# Compare using awk for floating point arithmetic
COMPARISON=$(awk "BEGIN {print ($LINE_RATE >= $THRESHOLD) ? 1 : 0}")

if [ "$COMPARISON" -eq 1 ]; then
    THRESHOLD_DISPLAY=$(printf "%.0f" $(awk "BEGIN {print $THRESHOLD * 100}"))
    echo "✅ Coverage OK: ${COVERAGE_PERCENT}% >= ${THRESHOLD_DISPLAY}%"
    exit 0
else
    THRESHOLD_DISPLAY=$(printf "%.0f" $(awk "BEGIN {print $THRESHOLD * 100}"))
    echo "❌ Coverage below threshold: ${COVERAGE_PERCENT}% < ${THRESHOLD_DISPLAY}%"
    exit 2
fi