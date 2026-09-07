# GameEngine — Engineering Docs

Working documentation for the GameEngine project: a full code review, a tracked
findings register, a phased action plan, and the reproduction harness that backs
the behavioural claims.

## Contents

| Document | What it's for |
|---|---|
| [`01-review.md`](01-review.md) | Full narrative review, organised by subsystem. Read this first. |
| [`02-findings.md`](02-findings.md) | Every finding with a stable ID (`GE-01`…`GE-100`), severity, location, and status. The tracking surface. |
| [`03-action-plan.md`](03-action-plan.md) | Phased, checkboxed work plan. This is what we execute against. |
| [`04-evidence.md`](04-evidence.md) | The probe harness source and its measured output. Re-runnable. |
| [`05-vision-review.md`](05-vision-review.md) | 2026-08-30 review against the product vision (editor, publish-everywhere, embeddability, LLM-friendliness, licensing). Findings `GE-67`…`GE-79`. |
| [`06-reliability-plan.md`](06-reliability-plan.md) | Draft implementation plan from the September 2026 review: content safety, mutation consistency, host lifecycle, rendering, physics, and a complete editing workflow. |

| [`07-emberbrook-engine-review.md`](07-emberbrook-engine-review.md) | Engine ergonomics after building Emberbrook, prioritized improvements, and browser deployment findings. |

## How these fit together

- **`01-review.md`** explains *why* something is a problem, with context.
- **`02-findings.md`** is the register — one row per finding, referenced by ID.
- **`03-action-plan.md`** sequences the fixes into phases with dependencies.
- **`04-evidence.md`** is proof: every finding marked **confirmed** was reproduced
  by the harness in this file, not inferred from reading.

Reference finding IDs in commit messages so the register stays live:

```
fix(core): monotonic entity IDs and dictionary lookup (GE-01)
```

Update the **Status** column in `02-findings.md` as items land.

## Review baseline

| | |
|---|---|
| Date | 2026-07-31 |
| Commit | `df48b9f` — *Merge branch 'core-op': allocation-free render snapshot* |
| Scope | `GameEngine.Core`, all four runners, `GameEngine.Editor`, the level system, demos, tests, build and CI |
| Size | ~10,463 lines of C# across 13 projects in the solution |
| Build | Clean (exit 0), ~30 nullable warnings |
| Tests | 17/17 passing, 2s |

## Pre-existing documents

These live in the repo root and predate this review. They are **not** superseded
wholesale — `IMPROVEMENTS.md` independently identifies several real issues — but
two of its proposed fixes are incorrect. See `GE-57` and `GE-58`.

- `IMPROVEMENTS.md`
- `GameEngine.Core_Review.md`
- `GameEngine.Editor_Review.md`

Consider moving them under `docs/archive/` so the root stays clean; left in place
for now since they're yours to relocate.
