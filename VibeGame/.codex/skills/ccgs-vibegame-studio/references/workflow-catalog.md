# Workflow Catalog

Use these workflows as practical routes, not bureaucracy. Skip steps that are already satisfied by existing project artifacts.

## Concept Workflow

Use for a new game idea, feature concept, mode, or major creative direction.

1. Inspect existing `design/`, `docs/`, `production/`, `Assets/Scripts`, and relevant Unity assets.
2. Identify the starting state: no idea, vague idea, clear concept, or existing work.
3. Produce or update a concept brief with elevator pitch, target player fantasy, pillars, anti-pillars, core loop, MVP scope, stretch scope, risks, and validation plan.
4. If Unity work follows, route through `unity-project-execution.md`.

## Systems Design Workflow

Use for mechanics, economy, combat, AI, UI, progression, level, narrative, content systems, or networking flows.

1. Read the concept brief and any related GDDs.
2. Define the system goal, player-facing behavior, rules, data model, edge cases, feedback, balancing knobs, and testable acceptance criteria.
3. Check cross-system impacts: UI, save/load, analytics, localization, accessibility, performance, networking, art/audio dependencies.
4. Write or update a GDD in `design/gdd/` or the nearest existing design location.
5. Run a role review using `role-review-checks.md` when the system affects multiple domains.

## Architecture / ADR Workflow

Use when a decision changes technical direction, module boundaries, data ownership, Unity subsystem usage, package choice, asset pipeline, or runtime-vs-editor policy.

1. Read existing architecture docs and ADRs under `docs/` or `design/`.
2. State the problem, constraints, options, decision, consequences, rejected alternatives, and validation plan.
3. For Unity decisions, include package and asset implications: assembly boundaries, `.meta` handling, scene/prefab impact, play mode tests, build targets, performance diagnostics, and rollback.
4. Save the ADR under `docs/architecture/` unless the project already has another ADR path.
5. When implementation starts, reference the ADR in story notes or comments where appropriate.

## Epics, Stories, Sprint Workflow

Use to convert design into production work.

1. Create epics from player-visible systems or technical milestones.
2. Break each epic into small stories with acceptance criteria and QA notes.
3. Keep story scope implementable in one focused pass.
4. Mark dependencies explicitly: design, asset, code, UI, audio, QA, performance, build, package, network.
5. Store plans under `production/epics/`, `production/sprints/`, or an existing project convention.

## Dev Story Workflow

Use when implementing a story or feature.

1. Read the story, linked GDD, linked ADR, and relevant code/assets.
2. Confirm acceptance criteria and unresolved decisions.
3. Implement narrowly, following local C# and Unity patterns.
4. For Unity asset or scene changes, preserve `.meta` relationships and verify by opening/building/testing through Unity when possible.
5. Run relevant tests/builds or explain why they were not run.
6. Update the story with evidence if the project tracks story status.

## Review Workflow

Use for code, design, architecture, content, assets, balance, UX, QA, release, networking, or security reviews.

1. Read target files in full enough to understand behavior.
2. Apply the relevant role checks from `role-review-checks.md`.
3. Report findings first, ordered by severity.
4. Include file/line references for code issues and exact asset paths for Unity issues.
5. Include open questions and test gaps.
6. Avoid broad rewrites unless the user asked for fixes.

## Validation and Release Workflow

Use for QA planning, smoke checks, regression suites, playtest reports, changelogs, patch notes, release checklists, hotfixes, and launch readiness.

1. Define the build or feature under test.
2. Map acceptance criteria to observable checks.
3. Include manual Unity verification steps when scene, prefab, input, UI, render, or play mode behavior matters.
4. Capture evidence: screenshots, logs, test output, performance numbers, reproduction steps.
5. For release/hotfix work, include risk, rollback, known issues, and verification matrix.
