# Prototype 0.1 — Credits / lifetime XP experiment

## Run

Open `Assets/Scenes/Prototype01.unity` and enter Play Mode. The management UI is
built from uGUI at runtime. Move capacity with `- allocation +`. Efficiency and
capacity price buttons are manual purchases: grey and disabled until affordable,
colored when available, highlighted on hover/focus. Expanded capacity starts
unassigned. Pause and 1x/5x/20x are test controls. Sessions are not saved.

A/B are production inputs. Credits (mint) are the sole purchase currency.
Lifetime XP (purple) is cumulative and never spent. Milestones at 100, 500,
and 2,000 XP are informational. All labels and colors live in PresentationLabels.

## Exact rules

Core is a Unity-independent assembly. Each unit is 10,000 integer subunits;
one tick is 0.1 seconds. Initial state: zero A/B/Credits/XP, zero efficiency
levels, six capacity, allocations 3/2/1. For allocations x/y/z and levels L1/L2/L3:

1. Produce `x * (2 + L1) / 10` A.
2. Convert `min(y / 10, available A / 2)`: consume twice that A, produce
   `conversion * (1 + L2 / 2)` B.
3. Develop `min(z / 10, available B)`: consume that B, produce
   `development * (1 + L3 / 2)` Credits **and** the same quantity of lifetime XP.

This notation uses rational resource units; Core uses integer subunits only.
Same-tick output feeds the next stage. All reachable divisions are exact at this
scale. Checked arithmetic makes each tick atomic, including both output ledgers.
Earlier successful ticks in a batch remain committed if a later tick overflows.

| Purchase | Credit price |
|---|---:|
| A efficiency | 20 × 2^current level |
| B efficiency | 40 × 2^current level |
| Development efficiency | 60 × 2^current level |
| Capacity 6 → 7 | 120 |
| Capacity 7 → 8 | 300 |
| Capacity 8 → 9 | 750 |
| Capacity 9 → 10 | 1,875 |

Each efficiency purchase adds one level. Purchases never consume A/B or XP.
Invalid/unaffordable actions leave state unchanged. Capacity stops at ten;
efficiency costs that exceed numeric storage are unavailable. Prices are
experimental, not final balance.

Stable IDs: `resource.a`, `resource.b`, `currency.credits`,
`progress.lifetime_xp`, `process.01–03`, `upgrade.01–03`.
Preview shares production calculations with advancement; UI shows actual/max
output, input consumption, net rates, and starvation per simulated second.
Frame time schedules explicit local ticks; it is not trusted backend time.

## Reproducible comparisons

Policies and automatic allocations/purchases live **only in EditMode tests**.
They run from the same zero state, reallocate before tick 1, every simulated
second, and on the tick following each purchase. Production precedes purchases;
at most one purchase per tick, with effects starting on the next tick.

Let a=2+L1 and b2=2+L2. Allocation weights are [4, 2a, a×b2].
At each allocation event add capacity×weight to three integer deficits, then
assign each capacity unit to the largest deficit and subtract the weight sum.
Ties use Production, Conversion, Development order; purchases reset deficits.

- **Fixed six:** never expand; select efficiency by sustainable output gain/Credit.
- **Efficiency first:** one level of each efficiency, then one expansion; repeat.
- **Capacity opening:** buy capacity seven before any efficiency, then use Mixed.
- **Mixed:** choose efficiency or expansion by sustainable output gain/Credit.

Sustainable output is the exact fraction
`N*a*b2*(2+L3) / [2*(4+2*a+a*b2)]`. Compare purchase gains by BigInteger
cross-products; ties use A efficiency, B efficiency, Development, expansion.
Reserve funds for the chosen action even when another action is affordable.

| Policy | XP at 5 min | XP at 10 min | XP at 15 min | Credits at 15 min | Capacity at 15 min |
|---|---:|---:|---:|---:|---:|
| Fixed six | 2392.2000 | 7026.0750 | 12697.2500 | 1937.2500 | 6 |
| Efficiency first | 2484.8625 | 8736.0875 | 17707.9875 | 3262.9875 | 10 |
| Capacity opening | 2344.9500 | 9120.7500 | 18734.4500 | 4929.4500 | 10 |
| Mixed | 2811.2000 | 9946.4000 | 19704.3000 | 5899.3000 | 10 |

These are exact regression checkpoints, not optimal-play claims. One-second
automated rebalancing overstates convenient manual execution. Early efficiency
prices outperform a pure capacity rush; later doubled prices make expansion
more attractive. A/B stockpiles are buffers, not purchasing power. Previous
A/B-priced upgrade projections belong to the superseded economy.

## Scope and future work

No leveling, skill trees, tactics, characters, monetization, premium currencies,
prestige, resets, persistence, device-clock offline progression, or cloud services.
Hybrid Credits-plus-resource pricing is a possible later experiment, not a mode
implemented here. Configurable process slots and tooltips are not implemented.

Alternatives within the same three slots and A/B chain can fit the Core /
Presentation boundary with a localized extension to immutable process
definitions and selected IDs. Current three-element arrays, ordered calculations,
and specific Throughput fields remain intentionally specialized. Arbitrary
resource graphs would need broader changes.

## Verification and handoff

The lead owns Editor operations, UI, integration, review, and final checks.
Luna workers implemented Core and independent EditMode tests. Tests cover
equal output, Credit-only spending, unchanged XP, expansion prices/limits,
allocation constraints, invalid actions, exact long-run comparisons, batching,
preview, and atomic overflow. PlayMode checks exercise actual UI raycasts,
manual purchases, capacity allocation, XP milestones, starvation, pause, and
simulation advancement.

Latest executed results: 25/25 EditMode cases and 4/4 PlayMode cases passed.
A later compiler error in the installed Visual Studio integration package was
deferred at the user's request; see [REPAIR_NOTES.md](REPAIR_NOTES.md) for the
exact state and unverified cache-restoration attempt. Final clean compilation
has not been verified after that error. Human playtesting is still needed for
investment balance, enjoyable reallocation frequency, physical input comfort,
and window sizes beyond the verified desktop view.
