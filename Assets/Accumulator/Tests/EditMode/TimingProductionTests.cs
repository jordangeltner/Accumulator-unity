using System;
using Accumulator.Core;
using NUnit.Framework;

namespace Accumulator.Tests.EditMode
{
    public sealed class TimingProductionTests
    {
        [Test]
        public void BaselineGatesTimingAndRejectsUndefinedModesWithoutMutation()
        {
            var economy = new Economy();
            var before = State(economy);
            Assert.That(economy.TimingProductionEnabled, Is.False);
            Assert.That(economy.SelectedProductionMode, Is.EqualTo(ProductionMode.Passive));
            Assert.That(economy.LastTimingResult, Is.EqualTo(TimingResult.None));
            Assert.That(economy.TrySetProductionMode(ProductionMode.Timing, out var reason), Is.False);
            Assert.That(reason, Is.Not.Null.And.Not.Empty);
            Assert.That(economy.TrySetProductionMode((ProductionMode)99, out reason), Is.False);
            Assert.That(reason, Is.Not.Null.And.Not.Empty);
            Assert.That(economy.TrySetProductionMode(ProductionMode.Passive, out reason), Is.True);
            Assert.That(reason, Is.Null);
            Assert.That(economy.TryStrikeTiming(out var hit, out var reward, out reason), Is.False);
            Assert.That(hit, Is.False);
            Assert.That(reward, Is.Zero);
            economy.AdvanceTimingMilliseconds(3000);
            Assert.That(State(economy), Is.EqualTo(before));
        }

        [Test]
        public void EnablingExperimentPreservesPassiveEconomyAndSwitchIsFree()
        {
            var baseline = new Economy();
            var experiment = new Economy(timingProductionEnabled: true);
            baseline.AdvanceTicks(5000);
            experiment.AdvanceTicks(5000);
            Assert.That(State(experiment), Is.EqualTo(State(baseline)));
            var before = State(experiment);
            Assert.That(experiment.TrySetProductionMode(ProductionMode.Timing, out _), Is.True);
            Assert.That(State(experiment), Is.EqualTo(before));
            Assert.That(experiment.SelectedProductionMode, Is.EqualTo(ProductionMode.Timing));
            Assert.That(experiment.TrySetProductionMode((ProductionMode)(-1), out _), Is.False);
            Assert.That(experiment.SelectedProductionMode, Is.EqualTo(ProductionMode.Timing));
            Assert.That(State(experiment), Is.EqualTo(before));
        }

        [Test]
        public void TimingStopsPassiveAWhileDownstreamProductionContinues()
        {
            var economy = UnlockedEconomy();
            economy.AdvanceTicks(100);
            Assert.That(economy.TrySetProductionMode(ProductionMode.Timing, out _), Is.True);
            var preview = economy.Preview();
            Assert.That(preview.AProduced, Is.Zero);
            Assert.That(preview.MaxAProduced, Is.Zero);
            Assert.That(preview.AConsumed, Is.EqualTo(4000));
            Assert.That(preview.BProduced, Is.EqualTo(2000));
            Assert.That(preview.BConsumed, Is.EqualTo(1000));
            Assert.That(preview.CreditsProduced, Is.EqualTo(1000));
            economy.AdvanceTicks(1);
            Assert.That(economy.A, Is.EqualTo(196000));
            Assert.That(economy.B, Is.EqualTo(101000));
            Assert.That(economy.Credits, Is.EqualTo(101000));
            Assert.That(economy.TimingElapsedMilliseconds, Is.Zero);
            Assert.That(economy.TrySetProductionMode(ProductionMode.Passive, out _), Is.True);
            Assert.That(economy.Preview().AProduced, Is.EqualTo(6000));
        }

        [Test]
        public void ExplicitTimingClockDoesNotAdvanceEconomyAndRejectsNegativeTime()
        {
            var economy = UnlockedEconomy();
            Assert.Throws<ArgumentOutOfRangeException>(() => economy.AdvanceTimingMilliseconds(-1));
            economy.AdvanceTimingMilliseconds(750);
            Assert.That(economy.TimingElapsedMilliseconds, Is.Zero);
            Assert.That(economy.TrySetProductionMode(ProductionMode.Timing, out _), Is.True);
            economy.AdvanceTimingMilliseconds(750);
            var before = State(economy);
            Assert.That(economy.TimingPosition, Is.EqualTo(5000));
            Assert.That(economy.Ticks, Is.Zero);
            Assert.That(economy.A, Is.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() => economy.AdvanceTimingMilliseconds(-1));
            Assert.That(State(economy), Is.EqualTo(before));
        }

        [TestCase(600, 4000, TimingResult.Success, 12)]
        [TestCase(900, 6000, TimingResult.Success, 12)]
        [TestCase(599, 3993, TimingResult.Miss, 0)]
        [TestCase(901, 6006, TimingResult.Miss, 0)]
        [TestCase(2100, 6000, TimingResult.Success, 12)]
        [TestCase(2400, 4000, TimingResult.Success, 12)]
        [TestCase(2099, 6007, TimingResult.Miss, 0)]
        [TestCase(2401, 3994, TimingResult.Miss, 0)]
        [TestCase(705, 4700, TimingResult.Critical, 18)]
        [TestCase(795, 5300, TimingResult.Critical, 18)]
        [TestCase(704, 4693, TimingResult.Success, 12)]
        [TestCase(796, 5306, TimingResult.Success, 12)]
        [TestCase(2205, 5300, TimingResult.Critical, 18)]
        [TestCase(2295, 4700, TimingResult.Critical, 18)]
        [TestCase(2204, 5307, TimingResult.Success, 12)]
        [TestCase(2296, 4694, TimingResult.Success, 12)]
        public void ZonesIncludeBothBoundariesInForwardAndReverseSweeps(int elapsed, int position, TimingResult expectedResult, int expectedReward)
        {
            var economy = UnlockedEconomy();
            Assert.That(economy.TrySetProductionMode(ProductionMode.Timing, out _), Is.True);
            economy.AdvanceTimingMilliseconds(elapsed);
            Assert.That(economy.TimingPosition, Is.EqualTo(position));
            Assert.That(economy.TryStrikeTiming(out var hit, out var reward, out var reason), Is.True);
            Assert.That(reason, Is.Null);
            bool expectedHit = expectedResult != TimingResult.Miss;
            Assert.That(hit, Is.EqualTo(expectedHit));
            Assert.That(economy.LastTimingResult, Is.EqualTo(expectedResult));
            Assert.That(reward, Is.EqualTo(expectedReward * Economy.Scale));
            Assert.That(economy.A, Is.EqualTo(reward));
            Assert.That(economy.TimingAttempts, Is.EqualTo(1));
            Assert.That(economy.TimingHits, Is.EqualTo(expectedHit ? 1 : 0));
            Assert.That(economy.Credits, Is.Zero);
            Assert.That(economy.LifetimeXp, Is.EqualTo(500 * Economy.Scale));
        }

        [TestCase(600, TimingResult.Success, 12)]
        [TestCase(750, TimingResult.Critical, 18)]
        public void HitRewardUsesCurrentWorkersAndProductionUpgrade(int elapsed, TimingResult expectedResult, int expectedReward)
        {
            var economy = UnlockedEconomy();
            economy.AdvanceTicks(200);
            Assert.That(economy.TryPurchase(Economy.AProducer, out _), Is.True);
            Assert.That(economy.TrySetAllocation(Economy.AProducer, 2, out _), Is.True);
            Assert.That(economy.TrySetProductionMode(ProductionMode.Timing, out _), Is.True);
            economy.AdvanceTimingMilliseconds(elapsed);
            var aBefore = economy.A;
            var creditsBefore = economy.Credits;
            var xpBefore = economy.LifetimeXp;
            Assert.That(economy.TryStrikeTiming(out var hit, out var reward, out _), Is.True);
            Assert.That(hit, Is.True);
            Assert.That(economy.LastTimingResult, Is.EqualTo(expectedResult));
            // Two workers at 3 A/second each, paid for two or three seconds.
            Assert.That(reward, Is.EqualTo(expectedReward * Economy.Scale));
            Assert.That(economy.A - aBefore, Is.EqualTo(reward));
            Assert.That(economy.Credits, Is.EqualTo(creditsBefore));
            Assert.That(economy.LifetimeXp, Is.EqualTo(xpBefore));
        }

        [Test]
        public void OneAttemptPerSweepSurvivesModeSwitchAndMiss()
        {
            var economy = UnlockedEconomy();
            Assert.That(economy.TrySetProductionMode(ProductionMode.Timing, out _), Is.True);
            Assert.That(economy.TryStrikeTiming(out var hit, out var reward, out _), Is.True);
            Assert.That(hit, Is.False);
            Assert.That(reward, Is.Zero);
            Assert.That(economy.LastTimingResult, Is.EqualTo(TimingResult.Miss));
            economy.AdvanceTimingMilliseconds(750);
            var before = State(economy);
            Assert.That(economy.TimingAttemptAvailable, Is.False);
            Assert.That(economy.TryStrikeTiming(out hit, out reward, out var reason), Is.False);
            Assert.That(reason, Is.Not.Null.And.Not.Empty);
            Assert.That(State(economy), Is.EqualTo(before));
            Assert.That(economy.TrySetProductionMode(ProductionMode.Passive, out _), Is.True);
            economy.AdvanceTimingMilliseconds(1500);
            Assert.That(economy.TrySetProductionMode(ProductionMode.Timing, out _), Is.True);
            Assert.That(State(economy), Is.EqualTo(before));
            Assert.That(economy.TryStrikeTiming(out _, out _, out _), Is.False);
            economy.AdvanceTimingMilliseconds(1500);
            Assert.That(economy.TimingPosition, Is.EqualTo(5000));
            Assert.That(economy.TimingAttemptAvailable, Is.True);
            Assert.That(economy.TryStrikeTiming(out hit, out reward, out _), Is.True);
            Assert.That(hit, Is.True);
            Assert.That(reward, Is.EqualTo(18 * Economy.Scale));
            Assert.That(economy.LastTimingResult, Is.EqualTo(TimingResult.Critical));
            Assert.That(economy.TimingAttempts, Is.EqualTo(2));
            Assert.That(economy.TimingHits, Is.EqualTo(1));
        }

        [Test]
        public void RejectedStrikesPreserveCriticalResultAndCannotDoubleAward()
        {
            var economy = UnlockedEconomy();
            Assert.That(economy.TrySetProductionMode(ProductionMode.Timing, out _), Is.True);
            economy.AdvanceTimingMilliseconds(750);
            Assert.That(economy.TryStrikeTiming(out _, out var reward, out _), Is.True);
            Assert.That(reward, Is.EqualTo(18 * Economy.Scale));
            Assert.That(economy.LastTimingResult, Is.EqualTo(TimingResult.Critical));
            var before = State(economy);
            Assert.That(economy.TryStrikeTiming(out var hit, out reward, out _), Is.False);
            Assert.That(hit, Is.False);
            Assert.That(reward, Is.Zero);
            Assert.That(State(economy), Is.EqualTo(before));
            Assert.That(economy.TrySetProductionMode(ProductionMode.Passive, out _), Is.True);
            Assert.That(economy.TryStrikeTiming(out _, out _, out _), Is.False);
            Assert.That(State(economy), Is.EqualTo(before));
            Assert.That(economy.TrySetProductionMode(ProductionMode.Timing, out _), Is.True);
            economy.AdvanceTimingMilliseconds(1500);
            Assert.That(economy.TrySetAllocation(Economy.AProducer, 0, out _), Is.True);
            before = State(economy);
            Assert.That(economy.TryStrikeTiming(out _, out _, out _), Is.False);
            Assert.That(State(economy), Is.EqualTo(before));
            Assert.That(economy.LastTimingResult, Is.EqualTo(TimingResult.Critical));
        }

        [Test]
        public void ZeroWorkersRejectStrikeWithoutConsumingSweep()
        {
            var economy = UnlockedEconomy();
            Assert.That(economy.TrySetProductionMode(ProductionMode.Timing, out _), Is.True);
            Assert.That(economy.TrySetAllocation(Economy.AProducer, 0, out _), Is.True);
            economy.AdvanceTimingMilliseconds(750);
            Assert.That(economy.TryStrikeTiming(out var hit, out var reward, out var reason), Is.False);
            Assert.That(reason, Is.Not.Null.And.Not.Empty);
            Assert.That(hit, Is.False);
            Assert.That(reward, Is.Zero);
            Assert.That(economy.TimingAttempts, Is.Zero);
            Assert.That(economy.TimingAttemptAvailable, Is.True);
            Assert.That(economy.TrySetAllocation(Economy.AProducer, 3, out _), Is.True);
            Assert.That(economy.TryStrikeTiming(out hit, out reward, out _), Is.True);
            Assert.That(hit, Is.True);
        }

        [Test]
        public void TimingClockBatchesMatchSingleMillisecondsAcrossSweepBoundaries()
        {
            var batched = UnlockedEconomy();
            var repeated = UnlockedEconomy();
            Assert.That(batched.TrySetProductionMode(ProductionMode.Timing, out _), Is.True);
            Assert.That(repeated.TrySetProductionMode(ProductionMode.Timing, out _), Is.True);
            foreach (var elapsed in new[] { 750, 1500, 2250, 3000 })
            {
                batched.AdvanceTimingMilliseconds(elapsed);
                for (var ms = 0; ms < elapsed; ms++) repeated.AdvanceTimingMilliseconds(1);
                Assert.That(batched.TimingPosition, Is.EqualTo(repeated.TimingPosition));
                Assert.That(batched.TryStrikeTiming(out var batchHit, out var batchReward, out _), Is.True);
                Assert.That(repeated.TryStrikeTiming(out var repeatedHit, out var repeatedReward, out _), Is.True);
                Assert.That(batchHit, Is.EqualTo(repeatedHit));
                Assert.That(batchReward, Is.EqualTo(repeatedReward));
                Assert.That(State(batched), Is.EqualTo(State(repeated)));
            }
        }

        [TestCase(nameof(Economy.A))]
        [TestCase(nameof(Economy.TimingAttempts))]
        [TestCase(nameof(Economy.TimingHits))]
        public void StrikeOverflowDoesNotPartiallyCommitRewardOrConsumeSweep(string property)
        {
            var economy = UnlockedEconomy();
            Assert.That(economy.TrySetProductionMode(ProductionMode.Timing, out _), Is.True);
            Assert.That(economy.TryStrikeTiming(out _, out _, out _), Is.True);
            Assert.That(economy.LastTimingResult, Is.EqualTo(TimingResult.Miss));
            economy.AdvanceTimingMilliseconds(2250);
            SetLongProperty(economy, property, long.MaxValue);
            var before = State(economy);
            Assert.Throws<OverflowException>(() => economy.TryStrikeTiming(out _, out _, out _));
            Assert.That(State(economy), Is.EqualTo(before));
            Assert.That(economy.TimingAttemptAvailable, Is.True);
        }

        [Test]
        public void TimingClockOverflowDoesNotAdvancePartially()
        {
            var economy = UnlockedEconomy();
            Assert.That(economy.TrySetProductionMode(ProductionMode.Timing, out _), Is.True);
            SetLongProperty(economy, nameof(Economy.TimingElapsedMilliseconds), long.MaxValue);
            Assert.Throws<OverflowException>(() => economy.AdvanceTimingMilliseconds(1));
            Assert.That(economy.TimingElapsedMilliseconds, Is.EqualTo(long.MaxValue));
        }

        // Keep timing/flow fixtures empty; progression is exercised using earned XP
        // independently in ModeProgressionTests.
        private static Economy UnlockedEconomy()
        {
            var economy = new Economy(timingProductionEnabled: true);
            SetLongProperty(economy, nameof(Economy.LifetimeXp), 500 * Economy.Scale);
            Assert.That(economy.TimingProductionUnlocked, Is.True);
            return economy;
        }

        private static void SetLongProperty(Economy economy, string property, long value)
        {
            var setter = typeof(Economy).GetProperty(property).GetSetMethod(true);
            Assert.That(setter, Is.Not.Null);
            setter.Invoke(economy, new object[] { value });
        }

        private static long[] State(Economy economy) => new[]
        {
            economy.A, economy.B, economy.Credits, economy.LifetimeXp, economy.Ticks,
            economy.Capacity, economy.Allocation(0), economy.Allocation(1), economy.Allocation(2),
            economy.Level(0), economy.Level(1), economy.Level(2),
            economy.TimingElapsedMilliseconds, economy.TimingAttempts, economy.TimingHits,
            economy.TimingAttemptAvailable ? 1L : 0L, (long)economy.LastTimingResult
        };
    }
}
