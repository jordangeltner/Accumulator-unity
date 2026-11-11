# Incremental strategy game — Speedrun roadmap to 1.0

**Status:** Initial 0.1–0.4 experiments accepted in their revised form; the initial 0.5 progression prototype is playable. Speedrunning is the approved primary direction. The 0.6 local run loop and revised UI are accepted by the user, with automated checks passing. Next implementation milestone: 0.7. Sustained repeat-run engagement remains unverified.

**Last planning update:** November 1, 2026.
**Document type:** Prototype evidence and approved release plan. Future milestones describe planned work, not implemented features. Approval of this roadmap does not establish that engagement or competitive integrity has been verified.

## 1. Approved product direction and foundation

- **Primary appeal:** Reach defined XP targets faster through production bottlenecks, allocation, upgrade sequencing and manual timing. Repeatable runs, execution mastery and route optimization replace continuing active/idle management as the primary release hypothesis.
- **Active play:** Timing production competes for attention with economic decisions. Expand this carefully so both routing and execution matter; do not impose artificial attempt scarcity.
- **Progression:** Each comparable run starts from the same rules-defined economy, XP, unlocks and upgrade state. XP is cumulative within a run and is never spent. Existing `LifetimeXp` terminology describes the prototype session; it must not silently become inherited account power. PBs, history and preferences persist separately from run state. Restarting a run must not reset those records.
- **Target:** Windows-first Unity 6/C# game, potentially released on Steam; preserve a path toward macOS, iOS, and Android.
- **Architecture/authority:** Keep deterministic C# simulation separate from Unity, portable to a server and free of unnecessary dependencies. Progression, economy and elapsed time for public competitive results must be authoritative, with durable cross-platform identity. Early local records are untrusted prototype records, not leaderboard-eligible results. Prove timing/latency feasibility before the UI overhaul and complete the authoritative path in 0.10.
- **Business direction:** Validate speedrunner engagement first. Monetization is optional, and a free 1.0 is valid. A validated commercial offer is no longer a mandatory release gate. Monetization implementation still requires explicit approval.
- **Presentation/identity:** Skip concept-art and character-model design. Use clear abstract/process-based presentation, typography, charts, timing feedback and restrained audio. A humanoid avatar, cosmetic identity system and separate tactical scene are outside the required 1.0 scope.
- **Theme:** Uncommitted. Mining labels such as “Ore” and “Alloy” are placeholders; economic IDs and game rules should not depend on a mining setting.

## 2. Prototype 0.1 — baseline and design history

### Original, mathematically tested simulation

- Three processes in series: produce A, convert A into B, use B for Development.
- Initially **6 allocatable capacity units**; nonnegative integer allocations; reallocations are free. Unassigned capacity allowed.
- Three repeatable efficiency upgrades. Costs increase geometrically; effects increase linearly.
- At upgrade levels `L1, L2, L3`, each assigned unit has these **maximum** rates per second:
  - Process 1 (A production): **2 + L1 units A produced**.
  - Process 2 (A→B): **2 A consumed**, producing **1 + 0.5 × L2 B**.
  - Process 3 (Development): **1 B consumed**, producing **1 + 0.5 × L3 development output**.
- If input is insufficient, throughput is limited and resource balances never become negative. A fixed 0.1-second reference tick evaluates stages in order, allowing same-tick output to feed downstream stages. Exact fixed-point integer subunits are preferred in code.
- Observed initial allocation patterns: **3/2/1** can build inventory while progressing; **2/2/2** can use A and B as fast as they are produced; after efficiency upgrades, **2/3/1** and **2/2/2** may become more favorable. These are examples, not proof of optimal strategies.

### Implemented 0.1 economy

Repository inspection and fresh Unity test runs confirmed these mechanics on October 29, 2026:

- Distinguish **Credits** (spendable primary currency) from **Lifetime XP** (cumulative achievement, never spent).
- Development generates **equal quantities of Credits and Lifetime XP**, using the existing output formula. Earlier `Progress` terminology should be updated where applicable.
- Both efficiency upgrades **and** capacity expansions cost Credits; A and B remain production-flow inputs rather than upgrade currencies.
- **Experimental efficiency costs:** first level 20 / 40 / 60 Credits for Processes 1 / 2 / 3, doubling for each subsequent level.
- **Experimental capacity growth:** start at 6; buy +1 unit, capped at 10 for the experiment; suggested Credit prices 120, 300, 750, 1,875.
- These prices are **test parameters, not accepted long-term balance**. Do not confuse test projections from older A/B-priced upgrades with the revised Credits-only economy.
- Stable, theme-independent resource/process/upgrade IDs; player-facing names reside in presentation data.
- Validated simulation actions are methods that safely reject unaffordable or invalid purchases and capacity assignments. They are a gameplay architecture feature; unit tests and Unity playtesting verify their behavior.

### What the first human playtest found

- The economic math was initially hard to interpret: some resource counters stayed flat because production matched consumption, not because a process was inactive.
- UI felt too text-heavy; upgrade costs and buttons were not immediately intuitive.
- Reduce mouse travel: position `− [allocation] +` together and keep upgrade buttons on the same side of process rows.
- Use **consistent resource-specific colors** in balances, rates and costs, reinforced by labels (color should not be the only distinction).
- Show **net rates**, clearly distinguish zero-net flow from blocked production, and use short explanations of bottlenecks such as “Need more A.” Preserve deeper formulas as optional detail rather than permanent clutter.
- Upgrade choices changed effective allocation strategies in encouraging ways. **Long-term playability remains unproven**, and a minute of play was not enough to measure sustained engagement.

### 0.1 exit/handoff checklist — complete

- [x] Confirm which of the latest Credits/XP and capacity features were actually implemented.
- [x] Check Unity Play Mode loads, production continues, and allocation / purchase controls work.
- [x] Run tests for deterministic production, purchases, capacity constraints and invalid actions.
- [x] Confirm readability of costs, net production and bottlenecks.
- [x] Save working project state and commit/push to GitHub when ready.

Completion evidence: fresh runs passed 25/25 EditMode and 4/4 PlayMode tests;
Unity reported no compilation failure or Console errors, with Prototype01 active.
The user confirmed the checklist was complete, including human readability and
the prior save/commit/push handoff. Commit/push was not repeated in this session.
Unity logged a warning about the previously repaired Visual Studio package cache
file; package operations may replace that local repair. Long-term balance and
engagement remain questions for subsequent experiments.

## 3. Prototype progress and approved release milestones

### 0.2 — Economic depth experiment — complete

**Implementation update (October 29):** The user has manually explored 0.1
investment strategies. Standard/Fast Conversion is now selectable behind an
Inspector experiment toggle; Standard is the starting mode. Fast uses twice the A
for 50% more B per converter, with the existing upgrade multiplier. See
[Prototype02.md](Prototype02.md) for rules and comparison instructions. The
user's five-minute playtest found both modes useful and the strategies more
complex, dynamic and flexible. This satisfies the initial experiment; long-term
mode balance remains open. With divisible allocations, Fast gains a sustainable
efficiency advantage as A production is upgraded; integer allocation and inventory
buffers can still favor Standard situationally.

**Question:** Can players discover multiple viable strategies, or does the economy settle into repetitive arithmetic?

- Playtest Credits-only efficiency vs. capacity reinvestment using identical starting conditions.
- Run short, repeatable tests (e.g. 5/10/15 minutes); compare lifetime XP, capacity purchased, upgrade mix, time-to-milestone and frequency of meaningful reallocation.
- Introduce **one** selectable alternate process mode in **one** slot (e.g. steady vs. burst Development, or efficient vs. fast Conversion) behind a test toggle. Avoid building a large combinatorial tech tree.
- Preserve a baseline scenario to tell whether the change improves decisions or merely increases output.
- **Exit criterion:** At least two materially different approaches feel worth trying and have observable trade-offs; UI remains understandable.

### 0.3 — Skill production experiment

**Scope revised October 29:** At the user's request, first test a timing game as
an alternate resource-production mode within the economy. A separate tactical
scene and humanoid/avatar are deferred. This revision intentionally brings a
small, validated production reward forward from 0.4.

**Question:** Is a short timing challenge responsive, understandable and enjoyable
as an optional alternative to passive production?

- Add Passive/Timing selection to the Resource A Production slot. Passive remains the default.
- Click a bar when its marker enters a labeled success zone, with a smaller critical zone inside. Success gives 1x reward, critical 1.5x, and outside gives zero. Show graded feedback and hit/attempt counts.
- Timing replaces passive A output. Validated hits produce A using assigned Production capacity and efficiency; other processes continue.
- Repeatable without diminishing rewards or a session attempt limit. One click per sweep is part of the timing challenge.
- Preserve a toggle to disable the experiment; keep simulation and reward rules independent of Unity.
- No art pipeline, customization, inventory, premium currency, or elaborate level design.
- **Exit criterion:** Inputs are responsive; players understand success/failure; repeated attempts reveal skill improvement.

Implementation and playtest instructions: [Prototype03.md](Prototype03.md).
**Initial playtest accepted (October 29):** The user approved the current
prototype for commit. Active play competes for attention with idle management,
encouraging mode switching, and makes upgrade decisions more interesting.
Long-term reward balance and repeated-attempt skill improvement remain open.
Future ideas, deferred for now: a combo tracker for consecutive successes and/or
criticals, with stacking bonus factors. These are not implemented.

### 0.4 — Combined active/idle loop

**Covered by the revised 0.3 prototype (user accepted October 29):** Timing
production awards simulation-validated A and runs alongside the economy. Initial
playtesting found that attention shifts between active timing and idle management,
and that this makes upgrades more interesting. The user considers this sufficient
to move to 0.5. Formal policy comparisons, revisit metrics, long-term balance and
server-trusted elapsed time remain unverified or deferred; they are not claimed
complete. The bullets below preserve the original milestone's intent.

**Question:** Does optional tactical play complement the economy instead of becoming a compulsory chore?

- Award bounded, validated economic rewards for tactical success (e.g. Credits or a temporary economic benefit). Decide rewards through simulation commands, not direct UI state mutation.
- Compare progress from economy-only play, tactical-heavy play, and mixed play. Aim for different but viable patterns.
- Add basic session metrics and revisit intervals; simulate elapsed-time accumulation only through explicit simulation inputs. Do not trust the user's wall clock.
- **Exit criterion:** Switching between optimization, tactical play and downtime feels rewarding; tactical success is not mandatory.

### 0.5 — Progression and breadth test

**Current slice:** Gate Fast Conversion at 100 lifetime XP and Timing Production
at 500 lifetime XP. XP is never spent; unlocks enable optional selection without
automatically changing modes. Hide locked controls until earned; keep one short
next-unlock cue as a near-term objective. Detailed rates and recipes are optional
through the Details control. These thresholds are experimental. See
[Prototype05.md](Prototype05.md). Longer-term goals beyond the initial unlocks
remain future work; this slice alone does not complete the milestone.

**Latest playtest notes (October 29):** The compact UI worked well during the
user's longest playtest so far (duration not recorded). Future progression ideas:
custom success/critical multipliers, upgrades to Fast Conversion's ratio, and more
manual timing triggers using keys or separate clickable skill windows.

**Direction update (November 1):** The user selected speedrunning, with intricate
timing combinations and upgrade sequencing. The initial 0.5 slice supplies the
economy for 0.6; sustained repeat-run engagement remains to be established. See
[Prototype05.md](Prototype05.md) for the original playtest notes.

**Original experiment question:** Which progression makes players want another run or another management session? The primary direction is now selected; repeat-run engagement still needs evidence.

- Test a limited next tier of processes, unlocks or alternative operating modes only after evidence from 0.2–0.4.
- Introduce a small number of long-term goals powered by lifetime XP; distinguish milestone tracking from currency spending.
- Prototype lightweight progression visualization where it helps players understand routing. Cosmetic identity is excluded from the approved release scope.
- Keep additions small enough to measure their effect on repeat-run appeal. The 100/500 XP gates are early goals; they do not establish long-term engagement.
- **Exit criterion:** Record a primary direction, its player objective, and evidence from comparable playtests. The original goal of a compelling next session remains relevant; for speedrunning that can mean improving a personal best rather than accumulating permanent power.

### Approved direction decision — November 1, 2026

The user chose speedrunning and approved the milestones below, including the
main menu, options, statistics and staged tutorial. The former branching
speedrun/management plan and proposed direction-comparison experiment are
superseded. This is a product decision, not evidence that repeat-run retention
has already been demonstrated. Earlier prototype evidence remains above.

### 0.6 — Complete local speedrun loop

**Implementation update (November 1):** Main menu/dialog, short sectioned guide, basic
saved options/statistics, 2,000 XP Standard/Practice runs, immediate start, finish,
results/retry, compatible local PB storage and bounded event/timeline capture are
implemented. The lead ran 90/90 EditMode and 26/26 PlayMode tests successfully and
inspected the live UI at 1280x720. The target is still provisional; human repeat-run
engagement and a standalone release build have not been verified. Rules,
limitations and playtest instructions: [Prototype06.md](Prototype06.md).

**Acceptance (November 1):** After reviewing the initial flow and requesting the
UI revisions below, the user accepted 0.6 and authorized commit/push. This accepts
the implemented slice without claiming measured retention or final target balance.

**Open record-storage follow-up (November 1):** The user completed a 2,000 XP
run in **5:06.1** but found no PB saved. Read-only inspection of the retained
result confirmed `Options opened; practice only.` as its eligibility reason;
the on-disk profile contained no PBs. The current implementation only attempts
to save eligible PB improvements and has no durable history for other completed
runs. This explains this instance; it does not establish that every missing-PB
report has the same cause.

- Review the local-PB interruption policy separately from future ranked eligibility. Opening game settings must have a clear, understandable consequence; do not silently change the existing rules while this decision is open.
- Make result feedback distinguish **PB saved**, **run excluded from PB comparison (with reason)** and **storage failed**. An in-memory best must not be represented as durably saved when a write fails.
- Verify the full eligible completion → save → restart → Statistics path against the actual application profile, alongside missing/corrupt-file and failed-write cases. Keep this as an explicit 0.6 follow-up rather than treating all record concerns as future leaderboard work.
- Preserve non-PB results through the durable run history in 0.8 below. Competitive eligibility must not determine whether a run appears in personal history.

- Define one target-XP objective and a versioned ruleset with identical starts, fixed comparable speed, permitted inputs, XP/unlock state and no inherited account power. The earlier 2,000 XP suggestion is provisional until a short playtest establishes feasibility.
- Implement immediate start, elapsed time, automatic finish, a basic results screen and quick retry. Define timer precision, action ordering at the finish boundary, Options, background/focus loss, abort/restart and interrupted-run behavior before comparing times. Begin Run and retry have no delay; completion must freeze the result exactly once. The initial countdown proposal was removed after user playtesting feedback.
- Save local PBs by compatible run type/ruleset, retain historical records and preserve preferences across restarts. Separate practice results and altered rules from comparable PBs. These remain local untrusted records; ranked server submissions are unavailable until 0.10.
- Record timestamped upgrade purchases, unlocks, allocations, mode changes and timing outcomes, plus bounded timeline samples, so later analysis does not depend on reconstructing missing data. Define attempt, normal-hit, critical, miss and skipped-opportunity semantics. Do not build the full charts yet.
- Add a landing/main menu before simulation begins. **New Run** opens a concise dialog with a Standard/Practice dropdown and a **Run Options** button opening a placeholder configuration dialog. Only supported choices are selectable; critical presets arrive in 0.7 and additional targets in 0.8.
- Put **How to Play** directly beneath New Run. Keep the initial guide short, with category headings and bullets for the goal, economy, upgrades and timing; no guided tutorial is required yet. This supersedes the initial paragraph-only presentation after user feedback.
- Keep Options, Details, Restart and Main Menu in a fixed bottom-right gameplay dock. Remove the prototype Pause and 1x/5x/20x buttons. Back to Run from Options resumes an interrupted attempt without restoring PB eligibility.
- Provide top-level **Options** for game configuration and a **Statistics** entry with basic PB information. Save the basic supported audio/display/control preferences; fuller keyboard remapping comes in 0.7, aggregate history/reports in 0.8 and live leaderboards in 0.10. Ordinary presentation settings must not silently change competitive conditions.
- **Exit evidence:** Repeat attempts have comparable starts and finish rules; players can finish, view a result, retry and recover PBs/options after restarting. Deterministic and Unity integration tests cover boundaries and lifecycle behavior. No claim of trusted public results.

### 0.7 — Execution depth and critical-range experiment

- Add one additional manual timing opportunity, keyboard controls/remapping and a small set of upgrades with meaningful routing trade-offs. Avoid an uncontrolled expansion of the upgrade tree or timing windows.
- Experiment with a few critical presets: wider/lower reward, standard, narrower/higher reward. Select before a run and lock for that attempt. Initially keep the ordinary success zone unchanged: failing to hit critical can still yield a normal success.
- Record the selected configuration in results. Initially offer every player the same choices within a category; split boards only if playtesting shows distinct competitive formats are needed. Mid-run customization, continuous sliders and combos remain optional later experiments rather than promised scope.
- Evaluate completion times, attainable output, hit accuracy, route changes, fatigue and whether one preset dominates. Equal theoretical reward does not prove equal practical difficulty or interesting choices; downstream bottlenecks also matter.
- Prove the portable simulation outside Unity and run a bounded timing/authority feasibility experiment before the 0.9 UI freeze. Define server timing, input ordering, latency treatment and integrity limits without treating client timestamps or deterministic replay as proof of honest human input. Concrete backend/security changes still require their applicable approval.
- **Exit evidence:** Both routing and execution affect results; controls are responsive; critical choices have useful trade-offs or are revised based on evidence. The future authoritative timing approach is feasible enough to avoid a late gameplay redesign.

### 0.8 — Multiple run types and post-run analysis

- Add a small set of short, medium and long target-XP categories; choose actual targets through playtesting. Record ruleset version, target, starting unlocks and legal settings with every result. Preserve historical PBs without comparing incompatible balance versions.
- **Durable local run records:** Persist completed run summaries whether they are PB improvements, slower attempts, Practice or PB-ineligible. Record abandoned attempts with their status as well. Keep elapsed time, category/ruleset/configuration, completion status, timing statistics and eligibility/exclusion reasons separately from PB selection. History must survive returning to the menu, retrying and restarting the application.
- Store the bounded timeline and upgrade/allocation/mode events needed to reopen a historical run overview. Define retention limits and pruning/migration/recovery behavior explicitly; surface write failures and never discard PBs when pruning history. Test reopening reports after application restart, not just viewing data still in memory.
- Build a Statistics menu containing personal statistics and a leaderboard destination. Personal views include PBs per category, run history, completed/abandoned runs, playtime and timing accuracy. Live global data is deferred to 0.10 and must not be presented as available beforehand.
- Add run-overview graphs for XP/output over time, milestone splits and comparisons with a compatible PB. Show a timestamped upgrade path and allocation/mode changes, distinguishing unlocks from purchases.
- Report attempts, normal successes, criticals, misses, accuracy and timing performance. Use bounded storage/sampling and clear metric definitions so long or abandoned attempts remain manageable.
- **Exit evidence:** Players can explain where they gained or lost time and identify a change for their next attempt. Recorded events, statistics and chart values agree.

### 0.9 — Dedicated UI overhaul

- This minor release is entirely for presentation, usability and onboarding. Gameplay rules, event recording and result calculations must already exist. Do not add gameplay or backend features to this milestone.
- Overhaul the full flow: landing page, New Run dialog, Options, How to Play, Statistics/leaderboard layouts, gameplay controls, timing feedback, results and graphs. Reduce pointer travel and clutter; make upgrades, bottlenecks and run status legible.
- Organize Options around audio levels/devices, graphics/display, keyboard bindings, supported input/output configuration and accessibility/feedback. Make settings accessible during play under the run's pause rules; persist supported preferences and handle unavailable devices/settings gracefully.
- Replace the initial text guide with a skippable, replayable tutorial using progressive disclosure. Introduce concepts when relevant, highlight/circle controls and evaluate short animations where useful. Refine the exact format through playtesting rather than requiring an animation for every step.
- Validate readable scaling, non-color-only feedback, clear keyboard focus, input responsiveness and supported Windows resolutions. Use process-based visuals and restrained sound; no concept-art or character-model design.
- **Exit evidence:** The entire run workflow is clear and comfortable, including first use, repeat runs and reports. Tutorial help supports discovery without obstructing experienced players.

### 0.10 — Global leaderboard and trusted records

- Complete durable identity, the authoritative economy/run/timing/result path, cloud records and global leaderboards per compatible run type/ruleset. Local prototype PBs must not be promoted into verified results without an acceptable validation path.
- Store verified competitive run records durably on the service and define their synchronization/recovery policy. Keep local personal history and verified leaderboard records distinguishable; practice or excluded history entries are not verified competitive submissions.
- Show top times, nearby ranks and the player's position within Statistics, with run-type/ruleset filtering and recorded critical configuration. Implement tie handling, invalid-result removal, display-name/privacy decisions and basic moderation.
- Validate rules/configuration and input order server-side. Handle repeated/failed submissions idempotently, reconnects, interrupted runs, incompatible client versions and service failures. Publish clear eligibility, pause/background and timing/latency policies.
- Preserve local practice with clear eligibility labeling. Deterministic replay can check simulation consistency but does not establish that inputs came from a human. Document exploit/automation limits instead of claiming perfect anti-cheat.
- **Exit evidence:** Eligible results persist across reconnects, duplicates cannot create inconsistent records, and a client-reported completion time alone cannot establish a ranked result. End-to-end integrity and failure-path checks pass.

### 0.11 — External beta and release candidate

- Test with speedrunners, measuring voluntary retries, PB improvement, route variety, target durations, fatigue and clarity. Agree practical engagement criteria before evaluating the beta; collecting more XP alone is not evidence of appeal.
- Tune targets, upgrade pacing and critical choices, then freeze the release ruleset. Preserve incompatible historical records when balance changes.
- Verify input/display/device differences, performance, crash handling, save recovery, backend recovery, clean installation, updates, onboarding, accessibility and the complete menu-to-result flow.
- **Exit evidence:** Repeat play and improvement have external support, records are reliable and no release-blocking defects remain. Known competitive and platform limitations are documented.

### 1.0 — Scoped Windows release

- Ship the validated run types, local PB/history, global leaderboards, post-run analysis, main menu/options and progressive tutorial with the finished UI.
- Complete distribution packaging, operating-cost planning, support/privacy information, release monitoring and a reproducible Windows build/deployment process.
- Require passing deterministic simulation checks, Unity integration checks and end-to-end competitive-flow verification on the release candidate. Verify actual builds and Console output; do not substitute prior prototype test counts for current release evidence.
- Preserve future macOS/iOS/Android portability and define which records/preferences can follow a durable identity. Shipping those clients is outside the required Windows 1.0 scope.
- **Engagement is the product gate; monetization is optional.** A free release is valid. Stores, premium currencies, subscriptions and paid gameplay advantages are not required. Any later monetization needs separate approval and must preserve comparable competitive conditions.

### Main menu and tutorial flow

1. **New Run:** A concise dialog with a mode dropdown and a separate Run Options configuration dialog. Start immediately when Begin Run is selected. Critical configuration arrives from 0.7 and additional targets from 0.8; ranked options only appear when supported.
2. **How to Play:** Immediately beneath New Run. Short sections and bullets in 0.6; a guided, progressive, skippable and replayable tutorial in 0.9.
3. **Options:** Audio, graphics/display, keyboard/input and supported input/output/accessibility settings. Preferences survive sessions; competitive restrictions are explicit.
4. **Statistics:** Personal PBs first, then history/aggregate statistics and detailed run reports in 0.8, plus live global leaderboards in 0.10.

No simulation progress occurs while the player is on the landing page or selecting
a new run. Unimplemented choices are omitted or clearly unavailable rather than
presented as functioning ranked, device or configuration features.

## 4. Cross-cutting engineering workflow

- Unity 6 + C#, VS Code/Codex through Unity CLI/Pipeline; Git/GitHub source control.
- The Codex integration has successfully inspected the running Unity project. Separate command-line `codex` on Windows PATH is not required for the VS Code workflow.
- **Lead orchestrator:** Own architecture, shared contracts, scheduling, integration and final verification; use stronger reasoning for these decisions. Only the lead interacts with the mutable Unity Editor by default.
- **Workers:** Use low or medium reasoning effort for bounded implementation/tests, with `fork_turns: "none"` when setting supported overrides and explicit file ownership. Use only runtime-supported models/settings; do not assume relative pricing or claim cost savings without evidence. At most two worker agents concurrently unless justified.
- Never equate subagent completion with verification. Run actual Unity/tests, inspect errors and Git diff. Avoid parallel edits of scenes/shared assets. Don't commit/push without approval.
- Keep simulation theme-independent and separate from Unity view. Introduce networking only for the timing feasibility experiment and authoritative competitive path; monetization is optional and separately approved.

## 5. Remaining design decisions

1. Which first XP target produces a feasible, interesting run on the existing economy? Treat 2,000 XP as a candidate, not accepted balance.
2. What precise start/finish, action-ordering, timer, pause, focus-loss and interrupted-run rules define a comparable local attempt?
3. Which basic settings can be supported honestly in 0.6, and which remain deferred to remapping or the UI overhaul?
4. Which additional timing opportunity and small upgrade set best expose routing/execution trade-offs in 0.7?
5. What bounded critical-width/reward presets remain interesting across player skill levels? Does testing justify separate categories?
6. Which authoritative timing/latency policy preserves responsiveness while supporting defensible public results?
7. Which chart comparisons and tutorial steps actually help players improve without creating clutter?
8. What engagement evidence, operational budget and distribution channel support the scoped Windows release? Monetization is not a prerequisite.

## 6. Suggested first task when resuming

> Address the open 0.6 record-storage/eligibility feedback first, including a real-profile save/restart check and clear result-save status. Then use accepted 0.6 as the baseline for the bounded 0.7 execution slice: one additional timing opportunity, controls/remapping, a small upgrade set and the critical-range experiment. Keep collecting target-duration and repeat-run feedback. Durable full history/report storage belongs to 0.8; authoritative global records belong to 0.10.

## 7. Post-1.0 ideas — not scheduled or approved for implementation

### Challenge / Prestige modifier with financial stakes

**Idea recorded November 1, 2026:** Explore an optional high-prestige challenge
modifier in which distractions create financial stakes. This is a monetization
hypothesis, not a commitment to ship or evidence that players would enjoy it.

- **Entry concept:** Require a valid linked payment method with explicit charge authorization, or a prepaid amount. Linking a payment method alone is not consent to unspecified future charges.
- **Challenge concept:** Enable pop-ups or other distractions during a run. Clicking a designated distraction or failing its challenge would trigger a disclosed monetary penalty. Possible formats include ad-like distractions, quick-response minigames or short puzzles; the exact interaction and failure conditions are undecided.
- **Recognition:** Aim for a prestigious optional category awarding accolades such as badges, leaderboard-visible profile-name customization or achievements. Rewards would be recognition only, with no gameplay power or cash prizes. Whether players value this prestige remains to be tested.
- **Competitive separation:** Record the modifier and its rules, and compare like-for-like challenge runs separately from ordinary speedruns. Standard play remains available without financial stakes.
- **Exploration first:** Prototype with simulated stakes before handling real money. Evaluate whether distractions add interesting execution decisions and whether players voluntarily want to repeat the mode.
- **Before any real-money implementation:** Resolve explicit opt-in, visible penalty amounts and failure rules, a player-approved maximum loss per run/session, no automatic replenishment, unintended-click/dispute handling, and payment/platform/legal feasibility. Avoid undisclosed or uncapped billing. Monetization, payment/security changes and concrete dependencies require separate approval under AGENTS.md.

This idea does not change the approved 0.6–1.0 scope or make monetization a
release requirement. No payment integration or paid-penalty behavior is authorized
by recording it here.
