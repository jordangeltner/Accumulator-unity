# Prototype 0.2 — Conversion mode experiment

Progression update: Fast now requires 100 lifetime XP in addition to its experiment
toggle. See [Prototype05.md](Prototype05.md); the rules below record the original
mode experiment.

Status: initial experiment complete. The user's five-minute manual playtest found
both modes useful and allocation strategies more complex, dynamic and flexible.
Long-term balance remains open, especially as Production upgrades favor Fast.
The user has already manually explored investment strategies in the 0.1 economy.

Open `Assets/Scenes/Prototype01.unity` and enter Play Mode. Conversion starts in
Standard. Click **Mode: Standard** in the Conversion row to switch to Fast;
click again to return. Switching is free and affects subsequent simulation ticks.
Inventories, Credits, XP, allocations and upgrade levels are preserved.

| Mode | A consumed per capacity /s | B produced per capacity /s |
|---|---:|---:|
| Standard | 2 | 1 + 0.5 × Conversion level |
| Fast | 4 | 1.5 + 0.75 × Conversion level |

These are maximum rates with sufficient input. Fast trades resource efficiency
for more output per converter. It can use surplus A or a stockpile to feed more
Development capacity, but shortages can reduce actual output below Standard.
Actual/max output, input consumption, net rates and starvation indicators update
with the selected mode. The displayed mode recipe includes current upgrades.

The **Enable Alternate Conversion** checkbox on `PrototypeController` is the
experiment toggle. Set it before entering Play Mode; when disabled, the selector
is omitted and only Standard is accepted. Existing scene assets need no edits:
the new field defaults to enabled. Core sessions created with `new Economy()`
still use the disabled experiment baseline; `new Economy(true)` enables it.

Simulation remains Unity-independent and uses explicit 0.1-second ticks with
10,000 integer subunits per resource unit. Fast can produce fractional subunits
under starvation; output rounds down at each production stage. Preview uses the
same calculations as advancement. Mode selection is a validated simulation action.
No persistence or backend authority is added by this experiment.

For comparison, start fresh sessions in Standard-only and selectable-mode
conditions. Keep starting state and investment approach consistent, then try
different allocations and switching decisions. At 5/10/15 simulated minutes,
record XP, Credits, capacity, upgrade mix, milestone times and meaningful
reallocations/mode switches. Existing automated strategy policies describe the
Standard baseline; they do not optimize Fast mode.

Success means at least two materially different approaches feel worth trying,
with observable trade-offs and understandable UI. Increased output alone does
not complete 0.2. Sessions remain temporary; use the existing pause/speed controls
for inspection and restart Play Mode for identical starting conditions.

Verification on October 29, 2026: Unity compilation completed without errors
or warnings; 32/32 EditMode and 6/6 PlayMode tests passed. The lead inspected
Standard and Fast UI at 1280x720 and observed a fresh Fast session advance from
tick 1 to tick 78 with no new Console errors. A gameplay agent implemented Core;
a test agent supplied independent EditMode cases and reviewed the integration.
The lead implemented UI/PlayMode coverage and executed all Unity checks.

Editing scripts during Play Mode can reset the prototype's unsaved simulation
on domain reload; restart Play Mode after edits. Broader window-size readability
and the experiment's strategic value still require human testing.
