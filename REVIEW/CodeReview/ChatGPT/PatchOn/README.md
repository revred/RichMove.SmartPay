# PatchOn — ChatGPT Code Review Patches

This folder contains **patch files only** organized by date. All patches are additive and safe to apply.

## Structure
```
PatchOn/
├── 20250913/          # Sep 13 patches (WP1-WP3)
├── 20250914/          # Sep 14 patches (AUD, Top 100 improvements)
└── README.md          # This file
```

## Apply a patch
From repo root:
```bash
git apply REVIEW/CodeReview/ChatGPT/PatchOn/20250914/0017_TOP100_Micro_Improvements.patch
```

## Patch naming convention
- `HHMM_Description.patch` - Time-stamped patches
- `0NNN_Description.patch` - Sequential numbered patches

## Documentation
- Patch notes and implementation details are in `../PatchNotes/`
- Top-N improvement lists are in `../TopN/`

> All patches are designed to be **low-risk and incremental**.