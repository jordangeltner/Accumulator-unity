# Incremental strategy game — Prototype 0.1 handoff and pre-1.0 roadmap

**Status:** Prototypes 0.1 and 0.2 complete; current stage is 0.3 — Skill production experiment.

**Last planning update:** October 29, 2026.
**Document type:** Verified 0.1 handoff and provisional pre-1.0 roadmap.

## 1. Vision and non-negotiable direction

- **Core play:** A deterministic economic optimization loop that rewards understanding of production bottlenecks, allocation, upgrade sequencing, and investment timing. Players alternate between active optimization and periods of idle/AFK accumulation.
- **Distinct secondary play:** Optional, separate timing/precision/reflex-based tactical challenges. Challenges should be enjoyable on their own, repeatable, and grant rewards that benefit economic progression. No attempt limits in the initial tactical prototype.
- **Progression:** Persistent growth with no prestige/reset system at first. Explore seasons, resets, or run XP only if they demonstrably improve the experience.
- **Target:** Windows-first Unity 6/C# game, potentially released on Steam; preserve a path toward macOS, iOS, and Android.
- **Authority/identity:** Eventually server-authoritative progression, elapsed time and reward validation; durable account identity across platforms. These are long-term requirements, not reasons to build cloud infrastructure before testing gameplay.
- **Business/identity direction (tentative):** Free-to-play/freemium possibilities, a customizable humanoid player avatar for tactical play, and customizable economic environments. Cosmetics and convenience may be monetized later; do not introduce monetization or artificial tactical attempt scarcity while validating the core loop.
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

## 3. Suggested pre-1.0 milestones — hypotheses, not commitments

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

### 0.3 — Standalone tactical proof of fun

**Question:** Is a short timing/precision skill challenge satisfying to repeat without economy rewards?

- One placeholder humanoid/avatar control target; one simple challenge with readable feedback, timing and score.
- Repeatable without diminishing rewards/attempt limits for the initial prototype.
- No art pipeline, customization, inventory, premium currency, or elaborate level design.
- **Exit criterion:** Inputs are responsive; players understand success/failure; repeated attempts reveal skill improvement.

### 0.4 — Combined active/idle loop

**Question:** Does optional tactical play complement the economy instead of becoming a compulsory chore?

- Award bounded, validated economic rewards for tactical success (e.g. Credits or a temporary economic benefit). Decide rewards through simulation commands, not direct UI state mutation.
- Compare progress from economy-only play, tactical-heavy play, and mixed play. Aim for different but viable patterns.
- Add basic session metrics and revisit intervals; simulate elapsed-time accumulation only through explicit simulation inputs. Do not trust the user's wall clock.
- **Exit criterion:** Switching between optimization, tactical play and downtime feels rewarding; tactical success is not mandatory.

### 0.5 — Progression and breadth test

**Question:** Is there a reason to return beyond the first 15 minutes?

- Test a limited next tier of processes, unlocks or alternative operating modes only after evidence from 0.2–0.4.
- Introduce a small number of long-term goals powered by lifetime XP; distinguish milestone tracking from currency spending.
- Prototype lightweight progression visualization and optional placeholder cosmetic identity, if it supports player attachment.
- **Exit criterion:** A concrete next-session objective exists without becoming a repetitive mandatory grind.

### 0.6 — Architecture and persistence proof

**Question:** Can the economy and tactical rewards be made trustworthy and persistent without rewriting the game?

- Prove portable simulation logic runs independently of Unity; authoritative backend processes actions, validated rewards and elapsed time.
- Test identity, reconnect, rehydration, idempotent purchases, offline accumulation caps/policies and save migration. Avoid assuming the client clock is trustworthy.
- Keep the implementation small: one end-to-end account/save/reconnect path before scaling services.
- **Exit criterion:** No known easy route to create resources by changing device time; robust save/reconnect flow.

### 0.7 — World, avatar and usability vertical slice

**Question:** Does a coherent fantasy and visual identity improve comprehension, attachment and desire to customize?

- Choose a thematic direction based on gameplay and audience, not inherited prototype mining terminology.
- Build one small coherent visual slice: economic environment, a customizable humanoid avatar, and a tactical scene.
- Establish UI/accessibility rules, visual language, audio feedback, control remapping and performance budgets.
- **Exit criterion:** First-time players can orient themselves quickly; customization has an identifiable subject and appeal.

### 0.8 — External alpha and economy balancing

**Question:** Do people besides the developer understand and enjoy the loop, return, and progress fairly?

- Test with a small external cohort across a range of familiarity with incremental and skill games.
- Measure comprehension, retention intent, choice variety, time-to-goal, performance, crashes and progression exploit attempts.
- Balance economy and reward cadence using observed behavior rather than only fixed-policy simulations.
- **Exit criterion:** Evidence supports both the active loop and the reasons to return; high-priority usability/exploit issues addressed.

### 0.9 — Release-candidate scope definition

**Question:** What is the smallest reliable, appealing Windows release and its viable business model?

- Lock MVP content and features, onboarding, accessibility, save reliability, telemetry/privacy and deployment pipeline.
- Decide whether the Windows/Steam release should be premium, free-to-play, or another model using evidence. Cosmetics, convenience and optional special-event attempt refills remain **candidates**, not promises. Do not add arbitrary scarcity to core tactical play merely to monetize it.
- Define account portability and future cross-platform compatibility expectations clearly.
- **Exit criterion:** A scoped, testable 1.0 checklist with documented known limitations.

## 4. Cross-cutting engineering workflow

- Unity 6 + C#, VS Code/Codex through Unity CLI/Pipeline; Git/GitHub source control.
- The Codex integration has successfully inspected the running Unity project. Separate command-line `codex` on Windows PATH is not required for the VS Code workflow.
- **Lead orchestrator:** GPT-6.1 Sol for architecture, orchestration, integration and final verification; only lead interacts with the mutable Unity Editor.
- **Workers:** GPT-6 Luna for bounded implementation/tests with `fork_turns: "none"` and explicit file ownership; at most two worker agents concurrently unless justified.
- Never equate subagent completion with verification. Run actual Unity/tests, inspect errors and Git diff. Avoid parallel edits of scenes/shared assets. Don't commit/push without approval.
- Keep simulation theme-independent and separate from Unity view. Introduce networking and monetization only when needed for the associated experiment.

## 5. Open questions for the next session

1. Which investment and allocation strategies are worth comparing in the 0.2 experiment, now that the Credits/XP and capacity baseline is implemented and tested?
2. Are playtesters optimizing **lifetime XP**, near-term spendable Credits, or something else—and what makes that objective compelling?
3. Do alternate operating modes create fundamentally different strategies or merely extra optimization bookkeeping?
4. What is the simplest repeatable tactical challenge that feels skillful independent of rewards?
5. What should the economic layer represent visually, and how does the customizable humanoid relate to it?
6. How should server-validated idle growth and tactical rewards be bounded without undermining skill or trust?
7. What monetization, if any, fits a Windows-first audience without compromising the main loop?

## 6. Suggested first task when resuming

> Playtest the selectable Standard/Fast Conversion experiment described in `Docs/Prototype02.md`. Compare identical starts at 5/10/15 minutes; evaluate strategic trade-offs and human comprehension, not output increases alone. The 0.1 repository inspection and baseline verification are complete.
