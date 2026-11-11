# Prototype 0.6 — Local speedrun loop

**Status (November 1, 2026):** Implementation and revised UI accepted by the user
for commit/push after playtest feedback. Automated checks passed. Measured
target-duration feedback and sustained repeat-run engagement remain unverified.
This is a local prototype, not a trusted online competitive service.

**Record-storage follow-up (November 1):** A user-completed 2,000 XP run took
306,100 ms (**5:06.1**) but produced no saved PB. Read-only inspection confirmed
the retained result was excluded with `Options opened; practice only.` and the
profile's PB list was empty. Only eligible improvements are persisted today;
other completed runs have no durable history. The roadmap now explicitly tracks
local-PB eligibility/save feedback as a 0.6 follow-up, full durable personal run
history/reports in 0.8, and verified server records in 0.10. No rule or storage
implementation was changed by recording this report.

## Play

Open `Assets/Scenes/Prototype01.unity` and enter Play Mode. The game now starts
on a main menu; no economy advances before a run begins.

- **New Run** opens a short dialog with a Standard/Practice dropdown. **Run Options** opens a placeholder configuration dialog; Begin Run starts immediately.
- **How to Play**, immediately below New Run, uses short Goal, Economy, Upgrades and Timing sections with bullet points.
- **Options** saves master volume, windowed/fullscreen mode, resolution, graphics quality and the default economy-details preference. Display size applies to standalone clients, not the Editor Game view. Input/output devices follow system settings; remapping and device selection are deferred.
- **Statistics** shows local PBs, including historical ruleset entries. Full history/charts and live leaderboards remain later work.

Use the mouse or the existing UI keyboard navigation (arrows and Enter).
Escape opens Options during a run. Options, Details, Restart and Main Menu stay
in a fixed bottom-right dock outside the scrolling gameplay panel. Pause and
speed buttons are removed. The New Run and Run Options modals block interaction
and keyboard navigation into the screens beneath them.

## Initial ruleset

- Ruleset ID: `speedrun-06-v2`; provisional objective: **2,000 Run XP**. Earlier `v1` records are retained as a separate historical category.
- Start from the existing six-capacity economy, allocations **3 / 2 / 1**, zero balances/upgrades, Passive Production and Standard Conversion. No power carries between attempts.
- Fast Conversion unlocks at 100 XP and Timing Production at 500 XP. XP is never spent. Existing economic and timing reward rules are unchanged.
- **Begin Run and Retry start immediately**, with no countdown. The official local finish time is the first **0.1-second economy tick** reaching or exceeding the target. The timing marker retains millisecond precision between ticks. The portable engine still supports explicit countdown fixtures for testing/history, but the current playable ruleset uses zero delay.
- Standard runs use **1x**. The result freezes exactly once at completion, including when a long frame contains time beyond the finish.
- Opening Options during an attempt, losing application focus or application suspension freezes the run and permanently removes its local PB eligibility. Focus/suspension interruptions open Options; **Back to Run** explicitly resumes. Gameplay commands are rejected while interrupted. No offline catch-up occurs.
- Practice is selectable before starting and never updates PBs. Both playable modes now run at 1x with no speed controls. The engine retains acceleration methods for automated checks, not player-facing controls.
- Restart abandons the old attempt and starts fresh immediately with the same rules and initial Standard/Practice choice. Main Menu abandons the unfinished run. Neither action clears PBs or preferences.
- Only strictly faster eligible completed results replace a compatible PB. Ties retain the existing record. Identity includes ruleset ID, target, countdown and experiment flags, so incompatible configurations are not compared.

With no purchases or allocation changes, the baseline reaches 2,000 XP in
**33:20.0** of active simulation. This is a reproducible baseline, not an expected
optimized duration or proof that the target is well paced. Playtest routing and
investment before accepting the target as final balance.

## Implementation and records

`RunSession` owns a private `Economy`, accepted gameplay commands, countdown,
chronological timing/tick advancement and completion. Presentation reads a live
`RunSnapshot` facade and cannot directly purchase, allocate, advance or mutate
the underlying economy. All new simulation types remain independent of Unity.
Unity schedules explicit local elapsed inputs; those inputs are not trusted time.

Each finished run has an immutable `RunResult`, a basic overview with time and
timing counts, and quick retry. Attempts count accepted strikes only. Normal
successes, criticals and misses are mutually exclusive; rejected strikes do not
change counts. A skipped opportunity is a completed unattempted timing sweep
with Production capacity assigned at the sweep boundary. It is not a miss.

Ordered events capture accepted allocations, purchases, capacity changes, modes,
speed changes, unlocks, timing outcomes and lifecycle transitions. Resource
samples are taken once per active simulation second, plus start/finish. Limits
are 8,192 events and 4,096 samples; overflow is reported and the final transition
and resource sample are preserved. These recordings remain in memory for the
current run; persistent run history and graph analysis are deferred to 0.8.

`LocalProfileStore` writes versioned PB/preferences JSON to
`Application.persistentDataPath/profile-06.json`, using a temporary write and
backup/replacement. Missing/corrupt profiles recover safely, backup data is
preserved, future schemas disable saves rather than being overwritten, and save
failures are shown in the interface. PBs are local/unverified even when eligible
for local comparison; they must not be promoted directly to future global boards.

## Verification performed

November 1, 2026, lead-owned integration checks:

- Unity compilation completed with **0 errors and 0 warnings**.
- **90/90 EditMode tests passed**, including 18 new run lifecycle/edge cases and the existing economy regressions.
- **26/26 PlayMode tests passed**, including 10 persistence cases and 16 menu/run-flow cases. Pointer checks use actual UI raycasts, including dropdown item selection; keyboard submit/navigation and nested modal blocking are exercised.
- Coverage includes immediate default starts, explicit countdown boundaries in the engine, frame subdivision/timing chronology, target overshoot, frozen results, practice eligibility, partial ticks across interruptions, command rejection, recording bounds, PB ties/configurations, corrupt/future profiles, recovery, saved preferences, nested modal interaction, fixed dock raycasts and preserved historical PBs.
- Live Play Mode frame movement was confirmed with a server-side frame-count wait. Separate observations showed the active run advancing from 115 to 119 economy ticks and its elapsed counter increasing.
- Main menu, New Run dialog, Options, How to Play, Statistics, gameplay, unlocked critical feedback and the completed practice-run overview were visually inspected at 1280x720. The live practice check created no artificial PB.
- The final current Unity Console sample had no compilation failure, errors or warnings. Pipeline's historical buffer retained an earlier unrelated Version Control plugin error; it was not a new gameplay failure.

The first PlayMode run exposed an uncaught profile-validation exception and
layout activation timing in the tests. Both were corrected before the successful
full rerun. No standalone Windows build, other-platform verification, measured
input latency or human engagement result is claimed.

**UI feedback revision (November 1):** The user accepted the landing page and
requested the short sectioned guide, simpler New Run dialog, native mode
dropdown, placeholder Run Options dialog, immediate starts and fixed gameplay
dock. These revisions compiled cleanly and passed the 90 EditMode / 26 PlayMode
checks above. The revised guide, dropdown, placeholder dialog and expanded
timing/dock layout were visually inspected at 1280x720. One worker updated the
interaction regressions and independently reviewed the source; the lead
implemented and verified the changes. Existing v1 PBs remain intact.

## Team and next playtest

Two medium-effort workers implemented the portable run engine and local
persistence respectively. They added independent tests and cross-reviewed the
modules; the persistence worker also reviewed presentation integration. The lead
defined contracts, implemented Unity menus/integration, migrated UI tests,
reviewed all changes and executed the final Unity checks. Only the lead operated
the Editor. No dependency, authentication, monetization or backend change was
introduced.

Try an uninterrupted Standard run, inspect the result and retry with a different
upgrade/allocation route. Check whether the target length, immediate start,
feedback, Options/focus eligibility policy and desire to improve your PB feel
right. Use Practice when experimenting or switching to other windows.
The user accepted 0.6 for handoff; use further playtests to inform 0.7 balance.
Critical customization, additional categories, charts, guided tutorials and
online records remain deferred to their planned milestones.
