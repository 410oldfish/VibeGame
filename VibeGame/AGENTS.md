# Project Notes

- Before starting any project task, first check whether the Unity MCP connection is available. Report clearly when it is disconnected before continuing with other project work.
- This is a Unity project. Before code or asset work, inspect local patterns under `Assets/Scripts`, `Packages/manifest.json`, and `ProjectSettings/ProjectVersion.txt`.
- Claude Code Game Studios has been adapted into project skills under `.codex/skills/ccgs-vibegame-studio`; use those skills for game design, ADR, story, review, QA, release, and performance workflows instead of copying Claude runtime files.
- Avoid editing generated Unity cache or IDE files (`Library`, `Temp`, `obj`, generated `.csproj` files) for product changes unless the user explicitly asks.

## Adaptive Multi-Agent Workflow

The primary task remains the root orchestrator. It owns intent, decomposition, file ownership, integration, final verification, and the final response. Do not create a second orchestrator.

Available project roles:

- `environment_agent`: one bounded, read-only Unity MCP connectivity check.
- `game_designer`: player-facing behavior, mechanics, UX intent, scope, GDDs, and acceptance criteria.
- `unity_developer`: detailed Unity/C# design and the single writer for an assigned implementation slice.
- `technical_reviewer`: independent read-only architecture, code, asset, and readiness review.
- `test_engineer`: independent risk-based compile, automated, integration, regression, and manual verification.

Choose participation dynamically. Small, tightly coupled fixes can stay with the root or use only `unity_developer`; ambiguous player-facing work can use `game_designer` first; risky or cross-cutting work can add `technical_reviewer`; independent testing should be proportional to regression risk. Do not spawn agents merely to demonstrate multi-agent work.

Delegate only bounded, self-contained packets with explicit file ownership, acceptance criteria, stop conditions, and verification. Parallelize only genuinely independent work with non-overlapping write paths. Read-only exploration may run in parallel. Children must not spawn descendants or direct other roles.

Before implementation, the root must account for the current dirty worktree, Unity MCP state, relevant project skills, package and Unity versions, serialization risk, and available verification. Unity scenes, prefabs, ScriptableObjects, UI assets, and serialized references have one writer at a time. Compile scripts before serialized binding.

Developers verify their own slice; reviewers and testers independently check only the highest-value risks. The root inspects every handoff and remains responsible for final acceptance. Agent configuration makes roles available after a new Codex task starts; it does not prove a model has run.
