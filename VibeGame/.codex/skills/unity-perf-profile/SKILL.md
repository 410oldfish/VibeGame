---
name: unity-perf-profile
description: Unity performance triage workflow for this VibeGame project. Use when the user asks to profile, diagnose FPS, hitches, CPU/GPU status, Unity Profiler traces, optimization priorities, Update cost, allocations, UI rebuilds, asset loading, rendering, networking, or frame-time problems.
---

# Unity Perf Profile

Use the shared studio workflow at `../ccgs-vibegame-studio`.

1. Read `../ccgs-vibegame-studio/SKILL.md`.
2. Read `../ccgs-vibegame-studio/references/unity-project-execution.md`, Performance Procedure.
3. Start by classifying the symptom: CPU, GPU, memory, loading, GC allocation, physics, UI rebuild, asset streaming, or network latency.
4. Do not recommend rendering-only fixes unless evidence points at GPU or render-thread cost.
5. Inspect hot paths for allocations, repeated lookups, heavy `Update`, synchronous loads, async teardown races, UI layout rebuilds, and network message churn.
6. Report bound status, worst costs, likely causes, and next verification step.
7. Tie optimizations back to project constraints and acceptance criteria.

For visible or gameplay changes made during optimization, capture evidence according to the Unity execution rules.
