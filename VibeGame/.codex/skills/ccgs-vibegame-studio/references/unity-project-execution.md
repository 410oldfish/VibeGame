# Unity Project Execution

This file binds the studio workflow to this Unity project. Prefer local evidence over generic engine advice.

## Project Facts

- Unity version: read `ProjectSettings/ProjectVersion.txt` before version-sensitive work.
- Primary code paths currently include `Assets/Scripts/HexDemo`, `Assets/Scripts/Battle`, `Assets/Scripts/Network`, and `Assets/Scripts/TEngineIntegration`.
- Packages include URP, Shader Graph, Input System, UGUI, Unity Test Framework, UniTask, YooAsset, and TEngine.
- Generated/cache-heavy directories include `Library`, `Temp`, `obj`, and most `UserSettings`; avoid editing them for product changes.

## Editing Rules

- Read existing C# patterns before adding abstractions.
- Keep runtime code under the nearest existing feature folder unless a new module is clearly justified.
- Put editor-only tools under an `Editor` folder or editor assembly.
- Preserve namespaces, assembly definitions, and package references used by nearby code.
- Use UniTask only where the project already uses async patterns or where async behavior is required.
- Use YooAsset/Addressables only where asset loading already follows that pipeline.
- Avoid modifying `.csproj` or `.slnx` directly for source changes; Unity regenerates project files.
- Treat `.meta` files as part of asset identity. If creating assets outside Unity, verify the `.meta` outcome before finalizing.

## Unity Asset Safety

Every asset or scene task should:

1. Identify exact asset paths before editing.
2. Prefer Unity Editor automation or local project conventions for creating scenes, prefabs, ScriptableObjects, materials, and settings.
3. Avoid bulk text replacement across `.unity`, `.prefab`, `.asset`, or `.meta` files unless the format and blast radius are well understood.
4. Keep GUIDs stable unless intentionally replacing an asset.
5. Re-read the changed asset or run Unity validation after edits.

## Verification Patterns

Use the strongest cheap evidence available:

- C# logic: compile check, unit tests, play mode tests, or focused static review.
- Editor tooling: editor test or script execution result.
- UI/input: play mode/manual steps, screenshot when visible, and event/path verification.
- Scene/prefab/material: inspect serialized changes, open in Unity when possible, and capture screenshots for visible changes.
- Package/build setting changes: Unity import/compile result plus a diff of manifest/settings.
- Networking: local session test or a documented simulated-path test when multi-client verification is not available.

Useful command patterns depend on the local Unity installation. If Unity CLI is available, prefer batchmode tests such as:

```powershell
Unity.exe -batchmode -quit -projectPath "F:\VibeGame\VibeGame" -runTests -testPlatform EditMode -testResults "Temp\editmode-results.xml"
```

If Unity is not on PATH and no editor path is configured, report that tests could not be run and provide the exact command shape.

## Performance Procedure

Start by classifying the symptom:

1. Reproduce the scene or flow and capture frame timing if Unity Profiler or build logs are available.
2. Determine whether the issue is CPU, GPU, memory, loading, GC allocation, physics, UI rebuild, asset streaming, or network latency.
3. Check common Unity hot spots: per-frame allocations, heavy `Update`, excessive `FindObjectOfType`, synchronous asset loads, UI layout rebuilds, shader variant spikes, overdraw, expensive physics queries, and large scene activation.
4. Do not recommend rendering-only fixes until evidence points at GPU or render-thread cost.
5. Tie optimization changes to before/after measurements.

For code-level performance reviews, look for:

- Allocations in `Update`, `LateUpdate`, `FixedUpdate`, UI refresh, and pathfinding loops.
- LINQ or closure allocations in hot paths.
- Repeated component lookup or scene-wide searches.
- Unbounded collections, event leaks, and missing unsubscribe paths.
- Async operations that can race scene teardown or object destruction.

## Unity Review Heuristics

- Keep gameplay state outside UI controllers; UI should observe or dispatch intent.
- Prefer data-driven tuning through ScriptableObjects, serialized fields, config, or tables instead of hardcoded gameplay constants.
- Separate pure battle/domain logic from MonoBehaviour glue when it makes tests cheaper.
- Avoid unnecessary `Update`; prefer events, timers, coroutines/UniTask, input actions, or state machines where appropriate.
- Use dependency boundaries that keep networking, battle state, presentation, and persistence understandable.
- For mobile or URP targets, consider batching, texture memory, shader variants, overdraw, input responsiveness, and loading stutter early.
