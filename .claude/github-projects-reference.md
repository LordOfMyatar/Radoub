# GitHub Projects CLI Reference

Quick reference for using `gh project` commands with Radoub repositories.

## What Goes on Project Boards

**Only Sprints and Epics** - individual issues (bugs, features, enhancements) stay off project boards unless explicitly working solo on them.

| Issue Type | Add to Project? |
|------------|-----------------|
| Epic | ✅ Yes |
| Sprint | ✅ Yes |
| Bug | ❌ No |
| Enhancement | ❌ No |
| Feature | ❌ No |

## Prerequisites

### Required Scope

GitHub CLI needs the `project` scope for project operations:

```bash
# Check current scopes
gh auth status

# Add project scope if missing
gh auth refresh -s project
```

## Project Information

### Radoub Project (#3)

| Field | Value |
|-------|-------|
| Number | 3 |
| Project ID | `PVT_kwHOAotjYs4BHbMq` |
| Status Field ID | `PVTSSF_lAHOAotjYs4BHbMqzg4Lxyk` |

**Status Options:**
| Status | Option ID |
|--------|-----------|
| Todo | `f75ad846` |
| In Progress | `47fc9ee4` |
| Done | `98236657` |

### Parley Project (#2)

| Field | Value |
|-------|-------|
| Number | 2 |
| Project ID | `PVT_kwHOAotjYs4BHFCR` |
| Status Field ID | `PVTSSF_lAHOAotjYs4BHFCRzg37-KA` |

**Status Options:**
| Status | Option ID |
|--------|-----------|
| Todo | `f75ad846` |
| In Progress | `47fc9ee4` |
| Done | `98236657` |

## Common Operations

### Add Issue to Project

```bash
# Add to Radoub project
gh project item-add 3 --owner LordOfMyatar --url https://github.com/LordOfMyatar/Radoub/issues/[NUMBER]

# Add to Parley project
gh project item-add 2 --owner LordOfMyatar --url https://github.com/LordOfMyatar/Radoub/issues/[NUMBER]
```

Returns JSON with item ID:
```json
{"id":"PVTI_..."}
```

### Set Status to "In Progress"

```bash
# For Radoub project
gh project item-edit \
  --id [ITEM_ID] \
  --project-id PVT_kwHOAotjYs4BHbMq \
  --field-id PVTSSF_lAHOAotjYs4BHbMqzg4Lxyk \
  --single-select-option-id 47fc9ee4

# For Parley project
gh project item-edit \
  --id [ITEM_ID] \
  --project-id PVT_kwHOAotjYs4BHFCR \
  --field-id PVTSSF_lAHOAotjYs4BHFCRzg37-KA \
  --single-select-option-id 47fc9ee4
```

### Set Status to "Done"

```bash
# For Radoub project
gh project item-edit \
  --id [ITEM_ID] \
  --project-id PVT_kwHOAotjYs4BHbMq \
  --field-id PVTSSF_lAHOAotjYs4BHbMqzg4Lxyk \
  --single-select-option-id 98236657

# For Parley project
gh project item-edit \
  --id [ITEM_ID] \
  --project-id PVT_kwHOAotjYs4BHFCR \
  --field-id PVTSSF_lAHOAotjYs4BHFCRzg37-KA \
  --single-select-option-id 98236657
```

## Project Selection Logic

Determine which project to use based on issue labels:

| Label | Project |
|-------|---------|
| `parley` | Parley (#2) |
| `radoub` | Radoub (#3) |
| `quartermaster` | Radoub (#3) |
| `manifest` | Radoub (#3) |
| `[Parley]` in title | Parley (#2) |
| `[Radoub]` in title | Radoub (#3) |
| `[Quartermaster]` in title | Radoub (#3) |
| `[Manifest]` in title | Radoub (#3) |

## Listing Commands

```bash
# List all projects
gh project list --owner LordOfMyatar

# List items in a project
gh project item-list 3 --owner LordOfMyatar --format json

# List project fields
gh project field-list 3 --owner LordOfMyatar --format json
```

## Notes

- Item IDs are returned when adding items to projects
- Status field is a single-select field requiring option IDs
- Both projects use the same status option IDs (Todo, In Progress, Done)
- Project operations require the `project` scope on your GitHub CLI token
