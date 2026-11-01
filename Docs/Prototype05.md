# Prototype 0.5 — XP-gated mode progression

Status: initial progression and compact UI received positive playtest feedback;
long-term return motivation is not established.

The user accepted the timing-production prototype as also covering the initial
intent of 0.4. Progression now gives lifetime XP a concrete role beyond an
informational counter. The starting economy remains Passive Production and
Standard Conversion, with all original purchases available.

| Lifetime XP | Unlock |
|---|---|
| 100 | Fast Conversion |
| 500 | Timing Production |

These are experimental thresholds. Gates use lifetime XP, not Credits. Reaching
a threshold enables the mode selector automatically, without spending anything
or changing the current mode. Purchasing upgrades and capacity cannot remove an
unlock. Inspector experiment toggles still control whether each mode exists in
the session; XP does not override a disabled experiment.

Locked mode controls and their descriptions stay hidden until their XP threshold
is reached. One short next-unlock line names the upcoming feature and required
XP; the balance header already shows current XP. After both unlocks this line
disappears. The duplicate XP milestone list was removed; 2,000 XP grants no reward.

The default interface shows net resource rates, compact process output, upgrade
levels/costs/benefits and short bottleneck notices. **Details** reveals actual/max
output, input consumption and mode recipes. Successful routine actions no longer
produce extra confirmation lines; timing results appear beside the bar.

Run `Assets/Scenes/Prototype01.unity` to play. You can still use 5x/20x while
running passive production to inspect progression faster; Timing runs at 1x.
Sessions are unsaved, so restarting Play Mode starts progression from zero.
This is a progression experiment, not persistent account progress.

Core owns the thresholds and validates mode-selection commands. Unity displays
the derived unlock state. There is no bypass toggle, new dependency, save format,
cloud service or authentication change.

## Next playtest

Start from zero and optimize normally. Evaluate whether Fast unlocks after enough
time to understand Standard, whether Timing feels like a meaningful new option,
and whether the progression display helps you decide what to aim for. Report
whether either threshold feels too early or late. The current unlocks are early
goals; they do not prove interest beyond fifteen minutes.

October 29 feedback: the user reports that the simplified UI works well and that
this was their longest playtest so far. No duration was supplied. This supports
continued iteration but does not establish the milestone's long-term exit criterion.

## Possible later slices

- Earned allocation presets could help manage the growing set of useful operating
  patterns without increasing raw production power.
- A small milestone choice between an idle-focused benefit and a timing-focused
  benefit could create a new objective, after evidence supports its balance.
- Combo tracking and stacking bonuses remain recorded ideas from 0.3, not part
  of this change. Decide caps and mode-switching behavior before implementing them.
- More customized upgrades: separate critical and success reward multipliers,
  and upgrades affecting Fast Conversion's input/output ratio.
- Additional manual timing triggers, potentially mapped to individual keys or
  separate clickable timing windows. Explore combinations of skill timing,
  resource allocation and upgrade sequencing.

## Speedrunning direction update

The [release roadmap](UNITY_GAME_PRE_1_0_ROADMAP.md) now defines the approved
speedrun path through 1.0. This document records the 0.5 progression prototype
and its original candidate mechanics.

The user originally suggested a game focused on speedrunners, rewarding intricate
timing combinations and upgrade decisions. On November 1, 2026, the user selected
this direction and approved the release roadmap. That decision does not establish
long-term engagement; repeat-run appeal still needs playtesting.

Such play would need a clear primary objective or defined maximum/target score,
with the time taken to reach it displayed alongside the result. Define the finish
condition and elapsed-time rules before comparing runs. Keyboard-triggered skills
and multiple clickable timing windows could support the execution layer.

These notes are future candidates; objective selection, customized upgrades and
additional timing inputs have not been implemented.

## Verification

October 29, 2026: compilation completed without errors or warnings. The lead ran
72/72 EditMode and 10/10 PlayMode cases successfully. Earned-progression tests
reach both thresholds through production, check immediately below and at the
thresholds, and verify no XP spending or automatic selection. Separate mechanics
fixtures seed XP only to preserve empty resource buffers for exact rate tests.

Locked and unlocked UI states were inspected at 1280x720. In a fresh session,
advancing explicit economy ticks unlocked both modes while keeping Passive and
Standard selected; no new Console errors were reported. Play Mode was stopped
after verification. Broader window sizes and measured progression pacing remain
untested; subsequent qualitative playtest feedback is recorded above.

A gameplay agent implemented the Core gates. A test agent updated independent
EditMode coverage and reviewed integration. The lead implemented UI/PlayMode
coverage, documentation and final Unity verification.

The subsequent UI simplification also passed 72/72 EditMode and 10/10 PlayMode
cases. Tests now check that locked controls are absent from the active hierarchy,
appear at the correct XP threshold, and remain usable after layout changes.
Details toggling preserves input/max-rate information and recipes; bottleneck
notices remain visible in the compact view. The lead inspected fresh, unlocked
Timing and expanded Details views at 1280x720, with no new Console errors. One
test agent adapted the interface checks and independently reviewed the changes.
