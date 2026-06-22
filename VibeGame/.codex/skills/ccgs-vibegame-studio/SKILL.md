---
name: ccgs-vibegame-studio
description: Game-studio workflow system adapted from Claude Code Game Studios for this Unity/VibeGame project. Use when the user asks to brainstorm a game feature, create or review GDDs, make ADRs, plan epics/stories/sprints, implement a story, run QA/release/performance checks, or coordinate design/programming/art/audio/narrative/production review for this Unity 6 C# project.
---

# CCGS VibeGame Studio

Use this skill to apply a structured indie game studio workflow inside the VibeGame Unity project. It adapts the useful parts of Claude Code Game Studios (roles, workflow phases, review gates, document templates) while replacing Claude-specific runtime features with Codex behavior and Unity/C# project rules.

## First Rule

For any project work, inspect the local Unity project before acting:

- Treat this as a Unity 6 project (`ProjectSettings/ProjectVersion.txt` currently reports Unity 6000.4.7f1).
- Prefer existing project patterns under `Assets/Scripts`, especially `HexDemo`, `Battle`, `Network`, and `TEngineIntegration`.
- Respect package choices visible in `Packages/manifest.json`, including URP, Input System, UGUI, Shader Graph, UniTask, YooAsset, TEngine, and Unity Test Framework.
- Do not hand-edit generated Unity cache files under `Library`, `Temp`, `obj`, or `UserSettings` unless the user explicitly asks for environment cleanup.
- Preserve Unity YAML/meta relationships when adding assets; create `.meta` files only through Unity or by following an existing local pattern when unavoidable.
- Avoid sweeping asset or project setting rewrites unless the story requires them.

Read `references/unity-project-execution.md` before any task that touches Unity scenes, prefabs, ScriptableObjects, materials, UI, render settings, build settings, package configuration, performance, tests, or editor automation.

## Workflow Picker

Choose the smallest workflow that matches the request:

- New idea or vague feature: use `references/workflow-catalog.md`, Concept Workflow.
- System design or mechanics documentation: use `references/workflow-catalog.md`, Systems Design Workflow.
- Technical architecture or a major implementation choice: use ADR workflow plus `references/document-templates.md`.
- Backlog planning: use Epics, Stories, Sprint Workflow.
- Implementation request: use Dev Story Workflow, then Unity execution rules.
- Review request: use `references/role-review-checks.md` and report findings first.
- QA, smoke, regression, playtest, release: use Validation and Release workflows.
- Performance request: use Unity performance procedure in `references/unity-project-execution.md`.

If the task spans multiple phases, state the phase sequence briefly and start with the first concrete deliverable.

## Collaboration Pattern

Keep the user in control without adding ceremony:

1. Gather project context from existing docs, source, assets, package manifest, and project settings.
2. Ask only for decisions that cannot be inferred safely.
3. Present 2-4 options when a design or architecture choice matters.
4. Draft the artifact or implementation plan.
5. Write files or perform project work when the user asked for action or clearly approved the direction.
6. Verify with evidence: tests, compile result, diff, scene/prefab inspection, screenshot, logs, profiler numbers, or build output.

Do not import Claude-only mechanics literally. Replace `AskUserQuestion` with concise user questions when needed. Replace Claude `Task`/subagent calls with role-based review checklists or Codex subagents only when they are available and genuinely useful.

## Output Expectations

- For documents, write concise production-ready Markdown under the existing project structure.
- For reviews, list blocking issues first with file/line references when available.
- For implementation, keep changes scoped to the story and verify before final response.
- For Unity work, report exact paths such as `Assets/Scripts/...`, scene paths, prefab paths, and verification evidence.
- For process work, update relevant `design/`, `docs/`, `production/`, or `tests/` artifacts instead of leaving plans only in chat when files exist.

## References

- `references/workflow-catalog.md`: phase map and task procedures.
- `references/unity-project-execution.md`: Unity/C# execution rules and performance workflow.
- `references/role-review-checks.md`: adapted studio roles and review gates.
- `references/document-templates.md`: lean templates for concept docs, GDDs, ADRs, stories, QA, bugs, and release notes.

Source inspiration: Donchitos/Claude-Code-Game-Studios, adapted for Codex and this Unity project rather than copied as Claude Code runtime configuration.
