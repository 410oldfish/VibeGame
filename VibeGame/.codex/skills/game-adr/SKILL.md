---
name: game-adr
description: Architecture decision record workflow for Unity/VibeGame development. Use when the user asks for an ADR, architecture decision, package/subsystem choice, MonoBehaviour-vs-plain-C# decision, data ownership choice, assembly/module boundary, build strategy, asset pipeline, or technical tradeoff.
---

# Game ADR

Use the shared studio workflow at `../ccgs-vibegame-studio`.

1. Read `../ccgs-vibegame-studio/SKILL.md`.
2. Read `../ccgs-vibegame-studio/references/workflow-catalog.md`, Architecture / ADR Workflow.
3. Read `../ccgs-vibegame-studio/references/document-templates.md`, ADR.
4. For Unity-specific choices, read `../ccgs-vibegame-studio/references/unity-project-execution.md`.
5. Inspect existing architecture docs, package manifest, assembly definitions, and ADRs under `docs/` or `design/`.
6. Present options and tradeoffs before writing when the decision is not already made.
7. Save the ADR under `docs/architecture/` unless the project already uses another ADR path.

Include validation and rollback implications, especially for Unity assets, packages, builds, tests, and performance.
