# Role Review Checks

Use these as lenses during planning or review. Do not pretend separate Claude subagents exist. Apply the relevant checks yourself, or use Codex subagents only when available and useful.

## Directors

Creative Director:
- Does the work reinforce the core fantasy, pillars, and anti-pillars?
- Does scope creep dilute the MVP?
- Are design tradeoffs visible to the user?

Technical Director:
- Does the architecture fit Unity, C#, package, and project constraints?
- Are subsystem boundaries clear?
- Are build, testing, rollback, and performance risks addressed?

Producer:
- Is the work small enough to execute and verify?
- Are dependencies, blockers, and acceptance criteria explicit?
- Is the next action clear?

## Department Leads

Game Designer:
- Are mechanics clear, testable, and player-facing?
- Are edge cases, tuning knobs, failure states, and rewards documented?

Lead Programmer:
- Are dependencies, ownership, and data flow sane?
- Does implementation follow local patterns and avoid unnecessary abstractions?
- Are Unity lifecycle methods, async flows, events, and scene teardown handled safely?

Art Director / Technical Artist:
- Are asset needs concrete enough to produce?
- Are materials, VFX, lighting, style, memory, and URP constraints addressed?

UX Designer / UI Programmer:
- Is the interaction readable, accessible, localizable, and responsive?
- Does UI observe state rather than own gameplay state?
- Are input actions, focus, touch/mouse/gamepad paths, and scaling considered?

Audio Director / Sound Designer:
- Are audio states, triggers, mix priority, and feedback moments identified?

Narrative Director / Writer:
- Does text, tone, world logic, and environmental storytelling support the design?

QA Lead / QA Tester:
- Are acceptance criteria observable?
- Are automated and manual checks specified?
- Are screenshots/logs/play mode steps required?

Release Manager / DevOps:
- Are build target, package, hotfix, rollback, changelog, and known issue paths clear?

Accessibility / Localization / Security / Analytics:
- Are colorblind cues, remapping, readability, translations, privacy, telemetry, and abuse cases considered where relevant?

## Review Severity

- Blocking: breaks requested behavior, risks data loss, prevents compile/build, violates project asset safety, or contradicts accepted architecture.
- High: likely runtime bug, serious UX/QA gap, performance risk, event/async lifecycle leak, or missing acceptance criterion.
- Medium: maintainability, testability, edge case, or documentation gap.
- Low: polish, naming, minor consistency, optional improvement.
