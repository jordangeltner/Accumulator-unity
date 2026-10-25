# AI Development Team — Incremental Game

## Project
Windows-first incremental strategy game built with Unity 6 and C#.

Development is primarily AI-driven.

## Architecture requirements
- Separate deterministic simulation from Unity presentation.
- Keep simulation code portable to a C# server backend.
- Use server-authoritative progression and economy.
- Maintain compatibility with future macOS, iOS, and Android clients.
- Prefer simple, maintainable implementations.
- Do not introduce unnecessary dependencies.

## Lead agent responsibilities
Act as technical lead and orchestrator.

For substantial tasks:
1. Analyze requirements.
2. Identify independent subtasks.
3. Delegate where supported and beneficial.
4. Integrate implementation results.
5. Run or coordinate verification.
6. Review changes for correctness and maintainability.
7. Report results and remaining risks.

Do not delegate trivial tasks.

## Delegation policy
Use specialized subagents when supported.

Suggested roles:

### Gameplay developer
Implement deterministic C# simulation and gameplay mechanics.

### Unity developer
Implement scenes, UI, input, and Editor integration.

### Test engineer
Create unit tests, edge-case tests, and regression tests.

### Reviewer
Independently inspect code for correctness, architecture,
security, and maintainability.

## Model selection
Prefer lower-cost models for:
- Simple code changes
- Boilerplate
- Test generation
- Documentation
- Straightforward refactoring

Prefer stronger reasoning models for:
- Architecture
- Complex gameplay algorithms
- Security-sensitive code
- Difficult debugging
- Final review

Only assign specific models when supported by the runtime.
Do not claim model routing occurred unless verified.

## Concurrency
- Prefer a maximum of 2-3 concurrent agents.
- Parallelize independent tasks only.
- Avoid concurrent edits to the same files.
- Only one agent may modify Unity scenes or shared assets
  at a time.
- Avoid simultaneous Unity Editor operations.

## Verification
- Compile affected code.
- Run relevant automated tests.
- Check Unity Console errors.
- Inspect changes for unintended modifications.
- Never report tests as passed unless executed successfully.
- Do not claim the game works without verification.

## Git workflow
- Do not commit or push without user approval.
- Do not overwrite unrelated changes.
- Prefer small, reviewable changes.
- Explain major architectural decisions.

## Approval boundaries
Request approval before:
- Adding major dependencies
- Changing project architecture
- Modifying authentication or backend security
- Implementing monetization
- Deleting substantial project content

## Reporting
After completing a task, summarize:
- Work performed
- Agents used, if any
- Files modified
- Tests executed and results
- Known issues
- Recommended next step

## Multi-Agent Execution Policy

Native subagent delegation is available.

### Team structure
- Lead agent: architecture, planning, orchestration, integration, and verification.
- Developer agent: implementation of assigned modules.
- Tester agent: independent tests, edge cases, and code review.

### Model routing
- Use the strongest available reasoning model for architectural decisions and complex debugging.
- Prefer less expensive models for well-defined implementation and testing tasks when their relative cost is verified.
- Model overrides require `fork_turns: "none"`.
- Never claim a cost saving unless pricing or usage evidence supports it.

### Delegation
- Use no more than three concurrent agents unless explicitly justified.
- Delegate only tasks with clear requirements and acceptance criteria.
- Prefer parallel work when agents can operate on independent files.
- Avoid unnecessary delegation for small tasks.

### Unity Editor ownership
- Only one agent may interact with the mutable Unity Editor at a time.
- The lead coordinates Editor access.
- Other agents may inspect source files and run independent tests without modifying Editor state.
- Do not allow simultaneous scene or shared-asset modifications.

### Integration
- The lead must review all delegated changes.
- The lead must run final integration tests.
- Subagent completion does not constitute successful verification.
- Report which agents performed each task and which checks were actually executed.

### Version control
- Agents must not independently commit or push.
- Avoid concurrent edits to the same files.
- Use separate worktrees when parallel code modifications would otherwise conflict.
- Preserve a clean, reviewable Git diff.