#!/bin/bash
# Run all Radoub test projects
# Usage: ./run-tests.sh [--ui-only] [--unit-only]

set -e

UI_ONLY=false
UNIT_ONLY=false

# Parse arguments
for arg in "$@"; do
    case $arg in
        --ui-only)
            UI_ONLY=true
            shift
            ;;
        --unit-only)
            UNIT_ONLY=true
            shift
            ;;
    esac
done

TIMESTAMP=$(date +"%Y%m%d%H%M%S")
OUTPUT_DIR="Radoub.IntegrationTests/TestOutput"
mkdir -p "$OUTPUT_DIR"

echo "========================================"
echo "Radoub Test Suite"
echo "Started: $(date '+%Y-%m-%d %H:%M:%S')"
echo "========================================"

TOTAL_PASSED=0
TOTAL_FAILED=0
declare -a RESULTS

# Unit/Headless Tests (fast, no UI required)
UNIT_TESTS=(
    "Radoub.Formats.Tests:Radoub.Formats/Radoub.Formats.Tests"
    "Radoub.UI.Tests:Radoub.UI/Radoub.UI.Tests"
    "Radoub.Dictionary.Tests:Radoub.Dictionary/Radoub.Dictionary.Tests"
    "Parley.Tests:Parley/Parley.Tests"
    "Manifest.Tests:Manifest/Manifest.Tests"
)

# UI Tests (Windows-only, FlaUI requires Windows)
# Note: Radoub.IntegrationTests uses FlaUI which is Windows-only
# On Linux/macOS, only unit/headless tests are available
UI_TESTS=()

invoke_test_project() {
    local name="${1%%:*}"
    local path="${1##*:}"
    local output_file="$OUTPUT_DIR/${name}_$TIMESTAMP.output"

    echo ""
    echo "--- $name ---"

    # Run tests and capture output
    dotnet test "$path" --logger "console;verbosity=normal" 2>&1 | tee "$output_file"

    # Parse results
    local total=$(grep -oP 'Total tests:\s*\K\d+' "$output_file" | tail -1)
    local passed=$(grep -oP '^\s+Passed:\s*\K\d+' "$output_file" | tail -1)
    local failed=$(grep -oP '^\s+Failed:\s*\K\d+' "$output_file" | tail -1)

    total=${total:-0}
    passed=${passed:-0}
    failed=${failed:-0}

    TOTAL_PASSED=$((TOTAL_PASSED + passed))
    TOTAL_FAILED=$((TOTAL_FAILED + failed))

    if [ "$failed" -eq 0 ]; then
        echo -e "  \033[32mPASS - Passed: $passed, Failed: $failed, Total: $total\033[0m"
        RESULTS+=("$name:PASS")
    else
        echo -e "  \033[31mFAIL - Passed: $passed, Failed: $failed, Total: $total\033[0m"
        RESULTS+=("$name:FAIL")

        # Show failed tests
        echo -e "  \033[31mFailed tests:\033[0m"
        grep "\[FAIL\]" "$output_file" | while read -r line; do
            echo -e "    \033[31m$line\033[0m"
        done
    fi
}

# Run unit tests unless UI_ONLY specified
if [ "$UI_ONLY" = false ]; then
    echo ""
    echo "=== Unit/Headless Tests ==="
    for test in "${UNIT_TESTS[@]}"; do
        invoke_test_project "$test"
    done
fi

# Run UI tests unless UNIT_ONLY specified (Windows-only)
if [ "$UNIT_ONLY" = false ] && [ ${#UI_TESTS[@]} -gt 0 ]; then
    echo ""
    echo "=== UI Integration Tests ==="
    for test in "${UI_TESTS[@]}"; do
        invoke_test_project "$test"
    done
elif [ "$UI_ONLY" = true ]; then
    echo ""
    echo -e "\033[33mNote: UI tests (FlaUI) are Windows-only. Use run-tests.ps1 on Windows.\033[0m"
fi

# Summary
echo ""
echo "========================================"
echo "Test Summary"
echo "========================================"
echo "Completed: $(date '+%Y-%m-%d %H:%M:%S')"
echo ""

for result in "${RESULTS[@]}"; do
    name="${result%%:*}"
    status="${result##*:}"
    if [ "$status" = "PASS" ]; then
        printf "  \033[32m%-30s %s\033[0m\n" "$name" "$status"
    elif [ "$status" = "FAIL" ]; then
        printf "  \033[31m%-30s %s\033[0m\n" "$name" "$status"
    else
        printf "  \033[90m%-30s %s\033[0m\n" "$name" "$status"
    fi
done

echo ""
if [ "$TOTAL_FAILED" -eq 0 ]; then
    echo -e "\033[32mTotal: Passed $TOTAL_PASSED, Failed $TOTAL_FAILED\033[0m"
else
    echo -e "\033[31mTotal: Passed $TOTAL_PASSED, Failed $TOTAL_FAILED\033[0m"
fi
echo "Output files saved to: $OUTPUT_DIR"

# Exit with error code if any tests failed
exit $TOTAL_FAILED
