# Accumulator

A Windows-first incremental strategy game about reaching an XP target as fast as
possible. Allocate production capacity, invest in upgrades, and combine economic
routing with manual timing to improve your personal best.

Built with **Unity 6 and C#**. The current **0.6 prototype** is a local speedrun
game; online competition and the full run-analysis tools are planned.

## Play the prototype

1. Open **New Run** and choose **Standard** or **Practice**.
2. Select **Begin Run** to start immediately. The current target is **2,000 Run XP**.
3. Assign capacity across **Production → Conversion → Development**. Production
   makes Resource A; Conversion turns A into B; Development consumes B to earn
   Credits and XP.
4. Spend Credits on efficiency upgrades or additional capacity. XP is cumulative
   within a run and is never spent. Fast Conversion unlocks at **100 XP** and
   Timing Production at **500 XP**; select these modes when useful.
5. In Timing Production, strike the bar in the success zone for resources. The
   smaller critical zone gives a larger reward; a miss gives none.
6. Finish automatically at the XP target, inspect your time and timing counts,
   then retry with a different route. Each attempt starts fresh; no power carries
   over between runs.

Use the mouse for gameplay controls. Menus also support arrow-key navigation and
Enter. **Escape** opens Options during a run. **Details** explains production
rates and recipes; **How to Play** is available from the main menu.

**Current PB rule:** Only uninterrupted Standard runs can update a compatible
local personal best. Opening Options, losing application focus, or suspension
pauses the attempt and permanently removes that attempt's PB eligibility.
Returning to the run resumes play. Practice never updates PBs. This interruption
policy and the clarity of save feedback are tracked for review.

## Run on Windows

**[Download the complete Windows x64 package (.zip, 39 MB)](https://github.com/jordangeltner/Accumulator-unity/releases/download/v0.6.0-windows-preview.1/Accumulator-windows-x64.zip)**

This is the Prototype 0.6 Windows preview. The ZIP includes the executable and
all required runtime/data files; Unity does not need to be installed to play.

The Windows package contains `Accumulator.exe` and its supporting Unity files.
Extract the **entire ZIP** into a folder, then launch the executable. Keep the
executable, data directory, and runtime files together.

To build from source, open **File → Build Profiles**, select **Windows x64**, and
include `Assets/Scenes/Prototype01.unity` as the starting scene. Build to
`Accumulator.exe` in a new output folder and distribute the entire generated
folder. A Unity Editor installation is needed to build from source; it is not
needed to play the packaged application.

See [Windows build and packaging](Docs/WINDOWS_BUILD.md) for the repeatable ZIP
build command and current verification limits.

## Open from source

1. Clone this repository and open its root folder as a project in Unity Hub.
2. Use **Unity 6000.6.7f1**, the version pinned in
   [ProjectVersion.txt](ProjectSettings/ProjectVersion.txt). Install Windows build
   support for your chosen scripting backend when building a player.
3. Let Unity restore the packages listed in [manifest.json](Packages/manifest.json).
4. Open `Assets/Scenes/Prototype01.unity` and press **Play**. The scene name is
   historical; it hosts the current prototype. The Editor menu
   **Accumulator → Open Prototype 0.1** opens the same scene.

The main menu appears before the economy starts advancing. Use a 1280×720 Game
view as a starting point when inspecting the current interface.

## Current scope and saved data

Implemented: Standard/Practice runs, immediate start and retry, automatic finish,
basic results, local PBs by compatible ruleset, saved options, and an in-game guide.
The run engine also records bounded events and timeline samples in memory.

Preferences and eligible PB improvements are saved to
`Application.persistentDataPath/profile-06.json`, with backup/recovery handling.
Restarting an attempt preserves these records. Completed runs that do not improve
an eligible PB are **not saved as history** today, and timeline recordings do not
survive application restart.

Local records are unverified. There is currently no server-authoritative online
run service or global leaderboard. Extra XP targets, configurable critical
ranges, durable full run history, and detailed charts remain planned. Run Options
is currently a placeholder.

## Code layout

| Location | Responsibility |
| --- | --- |
| `Assets/Accumulator/Core/` | Portable deterministic economy, run rules, lifecycle, results, and recordings; no Unity engine references. |
| `Assets/Accumulator/Presentation/` | Unity UI, input, local time scheduling, and profile persistence. |
| `Assets/Accumulator/Editor/` | Editor scene and build tooling. |
| `Assets/Accumulator/Tests/EditMode/` | Economy and run-engine regression tests. |
| `Assets/Accumulator/Tests/PlayMode/` | Profile persistence and UI/run-flow integration tests. |
| `Docs/` | Prototype evidence, playtest instructions, and release planning. |

Keep gameplay rules in the simulation and Unity-specific behavior in presentation.
The intended online competitive path requires server-authoritative progression,
economy, and elapsed time; local frame timing and saved PBs do not provide that
authority. Preserve portability for future clients while shipping Windows first.

## Verify changes

In Unity, open **Window → General → Test Runner** and run both **EditMode** and
**PlayMode** suites. Check the Console for compilation and runtime errors, then
exercise the affected player flow in the actual Windows build when relevant.
Historical test results in prototype documents are evidence for those revisions,
not a substitute for checking a new change.

Start new work on a fresh branch from an updated `main`. Keep changes small and
reviewable. [AGENTS.md](AGENTS.md) describes architecture, agent coordination,
verification, and approval boundaries; commits and pushes require user approval.

## Direction toward 1.0

The goal is a game speedrunners want to replay: personal improvement, meaningful
routes, responsive execution, comparable run categories, useful post-run analysis,
and trustworthy global competition. A dedicated minor release will focus entirely
on the UI overhaul. Concept-art and character-model design are outside the scope;
monetization is optional, and engagement is the release gate.

- [Speedrun roadmap to 1.0](Docs/UNITY_GAME_PRE_1_0_ROADMAP.md) — milestones and open decisions.
- [Prototype 0.6](Docs/Prototype06.md) — exact current rules, persistence limitations, and prior verification.
- [Repair notes](Docs/REPAIR_NOTES.md) — project/tooling repair history.
