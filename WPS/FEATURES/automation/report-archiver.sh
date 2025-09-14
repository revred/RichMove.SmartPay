#!/bin/bash
#
# Report Archiver - Manages regression test reports with weekly cleanup
#
# Archives older regression test reports and maintains a clean reports directory.
# Keeps the most recent reports and archives older ones for historical tracking.
# Automatically runs weekly cleanup to prevent disk space issues.
#
# Usage:
#   ./report-archiver.sh [--archive-days N] [--delete-days N] [--force]
#

set -e

# Default configuration
ARCHIVE_DAYS=7
DELETE_DAYS=30
FORCE=false

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --archive-days)
            ARCHIVE_DAYS="$2"
            shift 2
            ;;
        --delete-days)
            DELETE_DAYS="$2"
            shift 2
            ;;
        --force)
            FORCE=true
            shift
            ;;
        *)
            echo "Unknown option: $1"
            echo "Usage: $0 [--archive-days N] [--delete-days N] [--force]"
            exit 1
            ;;
    esac
done

# Configuration
REPORTS_PATH="$(dirname "$0")/../reports"
ARCHIVE_PATH="$REPORTS_PATH/archive"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
GRAY='\033[0;37m'
NC='\033[0m' # No Color

echo -e "${CYAN}📦 SmartPay Report Archiver${NC}"
echo -e "${GRAY}Reports Path: $REPORTS_PATH${NC}"
echo -e "${GRAY}Archive Days: $ARCHIVE_DAYS | Delete Days: $DELETE_DAYS${NC}"
echo ""

# Ensure directories exist
mkdir -p "$REPORTS_PATH"
mkdir -p "$ARCHIVE_PATH"

# Get current reports (exclude archive directory)
all_reports=()
if [ -d "$REPORTS_PATH" ]; then
    while IFS= read -r -d '' file; do
        if [[ "$(basename "$file")" != "archive" ]] && [[ -f "$file" ]]; then
            all_reports+=("$file")
        fi
    done < <(find "$REPORTS_PATH" -maxdepth 1 -type f -print0 2>/dev/null || true)
fi

archive_cutoff_date=$(date -d "$ARCHIVE_DAYS days ago" +%s 2>/dev/null || date -v-${ARCHIVE_DAYS}d +%s 2>/dev/null || echo "0")

echo -e "${CYAN}📋 Report Analysis:${NC}"
echo -e "${GRAY}  Total Reports: ${#all_reports[@]}${NC}"

if [ ${#all_reports[@]} -eq 0 ]; then
    echo -e "${GRAY}  No reports found to process${NC}"
    exit 0
fi

# Categorize reports
recent_reports=()
archivable_reports=()

for report in "${all_reports[@]}"; do
    if [ "$FORCE" = true ]; then
        archivable_reports+=("$report")
    else
        # Get file modification time
        file_time=$(stat -c %Y "$report" 2>/dev/null || stat -f %m "$report" 2>/dev/null || echo "0")

        if [ "$file_time" -lt "$archive_cutoff_date" ]; then
            archivable_reports+=("$report")
        else
            recent_reports+=("$report")
        fi
    fi
done

echo -e "${GREEN}  Recent Reports: ${#recent_reports[@]}${NC}"
echo -e "${YELLOW}  Archivable Reports: ${#archivable_reports[@]}${NC}"

# Archive old reports
if [ ${#archivable_reports[@]} -gt 0 ]; then
    echo ""
    echo -e "${CYAN}📦 Archiving old reports...${NC}"

    for report in "${archivable_reports[@]}"; do
        archive_file="$ARCHIVE_PATH/$(basename "$report")"
        if mv "$report" "$archive_file" 2>/dev/null; then
            echo -e "${GRAY}  ✅ Archived: $(basename "$report")${NC}"
        else
            echo -e "${RED}  ❌ Failed to archive: $(basename "$report")${NC}"
        fi
    done

    echo -e "${GREEN}✅ Archived ${#archivable_reports[@]} reports${NC}"
fi

# Clean up very old archived reports
delete_cutoff_date=$(date -d "$DELETE_DAYS days ago" +%s 2>/dev/null || date -v-${DELETE_DAYS}d +%s 2>/dev/null || echo "0")

archived_reports=()
if [ -d "$ARCHIVE_PATH" ]; then
    while IFS= read -r -d '' file; do
        archived_reports+=("$file")
    done < <(find "$ARCHIVE_PATH" -type f -print0 2>/dev/null || true)
fi

deletable_reports=()
for report in "${archived_reports[@]}"; do
    file_time=$(stat -c %Y "$report" 2>/dev/null || stat -f %m "$report" 2>/dev/null || echo "0")
    if [ "$file_time" -lt "$delete_cutoff_date" ]; then
        deletable_reports+=("$report")
    fi
done

if [ ${#deletable_reports[@]} -gt 0 ]; then
    echo ""
    echo -e "${CYAN}🗑️  Deleting very old archived reports...${NC}"

    for report in "${deletable_reports[@]}"; do
        if rm -f "$report" 2>/dev/null; then
            echo -e "${GRAY}  ✅ Deleted: $(basename "$report")${NC}"
        else
            echo -e "${RED}  ❌ Failed to delete: $(basename "$report")${NC}"
        fi
    done

    echo -e "${GREEN}✅ Deleted ${#deletable_reports[@]} old archived reports${NC}"
fi

# Generate cleanup summary
remaining_reports=()
while IFS= read -r -d '' file; do
    if [[ "$(basename "$file")" != "archive" ]] && [[ -f "$file" ]]; then
        remaining_reports+=("$file")
    fi
done < <(find "$REPORTS_PATH" -maxdepth 1 -type f -print0 2>/dev/null || true)

remaining_archived=()
while IFS= read -r -d '' file; do
    remaining_archived+=("$file")
done < <(find "$ARCHIVE_PATH" -type f -print0 2>/dev/null || true)

echo ""
echo -e "${CYAN}📊 Cleanup Summary:${NC}"
echo -e "${GRAY}  Current Reports: ${#remaining_reports[@]}${NC}"
echo -e "${GRAY}  Archived Reports: ${#remaining_archived[@]}${NC}"

# Calculate disk space (approximate)
current_size=0
for file in "${remaining_reports[@]}"; do
    size=$(stat -c %s "$file" 2>/dev/null || stat -f %z "$file" 2>/dev/null || echo "0")
    current_size=$((current_size + size))
done

archived_size=0
for file in "${remaining_archived[@]}"; do
    size=$(stat -c %s "$file" 2>/dev/null || stat -f %z "$file" 2>/dev/null || echo "0")
    archived_size=$((archived_size + size))
done

total_size=$((current_size + archived_size))

current_size_mb=$((current_size / 1024 / 1024))
archived_size_mb=$((archived_size / 1024 / 1024))
total_size_mb=$((total_size / 1024 / 1024))

echo -e "${GRAY}  Current Size: ${current_size_mb} MB${NC}"
echo -e "${GRAY}  Archived Size: ${archived_size_mb} MB${NC}"
echo -e "${GRAY}  Total Size: ${total_size_mb} MB${NC}"

# Create archiver status file
status_file="$REPORTS_PATH/.archiver-status"
cat > "$status_file" << EOF
{
  "lastRun": "$(date -Iseconds)",
  "archiveDays": $ARCHIVE_DAYS,
  "deleteDays": $DELETE_DAYS,
  "archivedCount": ${#archivable_reports[@]},
  "deletedCount": ${#deletable_reports[@]},
  "currentReports": ${#remaining_reports[@]},
  "archivedReports": ${#remaining_archived[@]},
  "totalSizeMB": $total_size_mb
}
EOF

echo ""
echo -e "${GREEN}✅ Report archival complete!${NC}"