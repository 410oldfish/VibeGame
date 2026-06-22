---
name: unity-review
description: Review workflow for Unity/VibeGame code, assets, scenes, prefabs, ScriptableObjects, design docs, implementation plans, and QA evidence. Use when the user asks for review, validation, risk assessment, architecture review, code review, design review, or readiness check.
---

# Unity Review

Use the shared studio workflow at `../ccgs-vibegame-studio`.

1. Read `../ccgs-vibegame-studio/SKILL.md`.
2. Read `../ccgs-vibegame-studio/references/role-review-checks.md`.
3. Read `../ccgs-vibegame-studio/references/unity-project-execution.md` for any Unity-facing review.
4. Read target files in full enough to understand the behavior.
5. Lead with findings, ordered by severity: Blocking, High, Medium, Low.
6. Include file/line references for code issues and exact asset paths for Unity issues.
7. Call out missing tests, missing evidence, unsafe asset operations, lifecycle leaks, performance risks, and unverified editor changes.

If no issues are found, say so clearly and mention residual risk or test gaps.
