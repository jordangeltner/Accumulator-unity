using Accumulator.Core;
using NUnit.Framework;

namespace Accumulator.Tests.EditMode
{
    public sealed class ModeProgressionTests
    {
        [TestCase(false, 1000, "100")]
        [TestCase(true, 5000, "500")]
        public void EarnedXpUnlocksAtThresholdWithoutSpendingOrAutomaticSelection(bool timing, int ticks, string thresholdLabel)
        {
            var economy = new Economy(true, true);
            Assert.That(Economy.FastConversionUnlockXp, Is.EqualTo(100));
            Assert.That(Economy.TimingProductionUnlockXp, Is.EqualTo(500));
            economy.AdvanceTicks(ticks - 1);
            Assert.That(IsUnlocked(economy, timing), Is.False);
            var before = State(economy);
            Assert.That(TrySelect(economy, timing, out var reason), Is.False);
            Assert.That(reason, Does.Contain(thresholdLabel));
            Assert.That(State(economy), Is.EqualTo(before));
            Assert.That(economy.TrySetConversionMode(ConversionMode.Standard, out _), Is.True);
            Assert.That(economy.TrySetProductionMode(ProductionMode.Passive, out _), Is.True);
            economy.AdvanceTicks(1);
            Assert.That(economy.LifetimeXp, Is.EqualTo((timing ? 500 : 100) * Economy.Scale));
            Assert.That(IsUnlocked(economy, timing), Is.True);
            Assert.That(economy.SelectedConversionMode, Is.EqualTo(ConversionMode.Standard));
            Assert.That(economy.SelectedProductionMode, Is.EqualTo(ProductionMode.Passive));
            long xp = economy.LifetimeXp, credits = economy.Credits, elapsedTicks = economy.Ticks;
            Assert.That(TrySelect(economy, timing, out reason), Is.True);
            Assert.That(reason, Is.Null);
            Assert.That(economy.LifetimeXp, Is.EqualTo(xp));
            Assert.That(economy.Credits, Is.EqualTo(credits));
            Assert.That(economy.Ticks, Is.EqualTo(elapsedTicks));
            if (!timing) Assert.That(economy.TimingProductionUnlocked, Is.False);
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        public void ExperimentFlagsStillGateModesAfterBothXpThresholds(bool enableFast, bool enableTiming)
        {
            var economy = new Economy(enableFast, enableTiming);
            economy.AdvanceTicks(5000);
            Assert.That(economy.FastConversionUnlocked, Is.EqualTo(enableFast));
            Assert.That(economy.TimingProductionUnlocked, Is.EqualTo(enableTiming));
            var before = State(economy);
            if (!enableFast)
            {
                Assert.That(economy.TrySetConversionMode(ConversionMode.Fast, out var reason), Is.False);
                Assert.That(reason, Is.Not.Null.And.Not.Empty);
                Assert.That(State(economy), Is.EqualTo(before));
            }
            if (!enableTiming)
            {
                Assert.That(economy.TrySetProductionMode(ProductionMode.Timing, out var reason), Is.False);
                Assert.That(reason, Is.Not.Null.And.Not.Empty);
                Assert.That(State(economy), Is.EqualTo(before));
            }
        }

        [Test]
        public void CreditSpendingDoesNotRelockModesOrSpendLifetimeXp()
        {
            var economy = new Economy(true, true);
            economy.AdvanceTicks(5000);
            Assert.That(economy.TryPurchase(Economy.AProducer, out _), Is.True);
            Assert.That(economy.TryExpandCapacity(out _), Is.True);
            Assert.That(economy.Credits, Is.EqualTo(360 * Economy.Scale));
            Assert.That(economy.LifetimeXp, Is.EqualTo(500 * Economy.Scale));
            Assert.That(economy.FastConversionUnlocked, Is.True);
            Assert.That(economy.TimingProductionUnlocked, Is.True);
            Assert.That(economy.TrySetConversionMode(ConversionMode.Fast, out _), Is.True);
            Assert.That(economy.TrySetProductionMode(ProductionMode.Timing, out _), Is.True);
            Assert.That(economy.SelectedConversionMode, Is.EqualTo(ConversionMode.Fast));
            Assert.That(economy.SelectedProductionMode, Is.EqualTo(ProductionMode.Timing));
            Assert.That(economy.Credits, Is.EqualTo(360 * Economy.Scale));
            Assert.That(economy.LifetimeXp, Is.EqualTo(500 * Economy.Scale));
            Assert.That(economy.TrySetConversionMode(ConversionMode.Standard, out _), Is.True);
            Assert.That(economy.TrySetProductionMode(ProductionMode.Passive, out _), Is.True);
            Assert.That(economy.FastConversionUnlocked, Is.True);
            Assert.That(economy.TimingProductionUnlocked, Is.True);
        }

        [TestCase(false, 100)]
        [TestCase(true, 500)]
        public void OneRawXpSubunitBelowThresholdRemainsLocked(bool timing, long displayedThreshold)
        {
            // Supplemental storage-boundary fixture: normal progression tests above earn XP.
            var economy = new Economy(true, true);
            var setter = typeof(Economy).GetProperty(nameof(Economy.LifetimeXp)).GetSetMethod(true);
            Assert.That(setter, Is.Not.Null);
            setter.Invoke(economy, new object[] { displayedThreshold * Economy.Scale - 1 });
            Assert.That(IsUnlocked(economy, timing), Is.False);
            var before = State(economy);
            Assert.That(TrySelect(economy, timing, out _), Is.False);
            Assert.That(State(economy), Is.EqualTo(before));
            setter.Invoke(economy, new object[] { displayedThreshold * Economy.Scale });
            Assert.That(IsUnlocked(economy, timing), Is.True);
            Assert.That(TrySelect(economy, timing, out _), Is.True);
            Assert.That(economy.LifetimeXp, Is.EqualTo(displayedThreshold * Economy.Scale));
        }

        [Test]
        public void InvalidModeActionsLeaveLockedAndUnlockedSessionsUnchanged()
        {
            var economy = new Economy(true, true);
            AssertInvalidModesUnchanged(economy);
            economy.AdvanceTicks(5000);
            Assert.That(economy.TrySetConversionMode(ConversionMode.Fast, out _), Is.True);
            Assert.That(economy.TrySetProductionMode(ProductionMode.Timing, out _), Is.True);
            AssertInvalidModesUnchanged(economy);
        }

        [Test]
        public void BatchedAndSingleTickProgressionReachIdenticalUnlocks()
        {
            var batched = new Economy(true, true);
            var repeated = new Economy(true, true);
            foreach (var count in new[] { 999, 1, 3999, 1 })
            {
                batched.AdvanceTicks(count);
                for (var tick = 0; tick < count; tick++) repeated.AdvanceTicks(1);
                Assert.That(State(batched), Is.EqualTo(State(repeated)));
                Assert.That(batched.SelectedConversionMode, Is.EqualTo(ConversionMode.Standard));
                Assert.That(batched.SelectedProductionMode, Is.EqualTo(ProductionMode.Passive));
            }
            Assert.That(batched.FastConversionUnlocked, Is.True);
            Assert.That(batched.TimingProductionUnlocked, Is.True);
        }

        private static bool IsUnlocked(Economy economy, bool timing) =>
            timing ? economy.TimingProductionUnlocked : economy.FastConversionUnlocked;

        private static bool TrySelect(Economy economy, bool timing, out string reason) => timing
            ? economy.TrySetProductionMode(ProductionMode.Timing, out reason)
            : economy.TrySetConversionMode(ConversionMode.Fast, out reason);

        private static void AssertInvalidModesUnchanged(Economy economy)
        {
            var before = State(economy);
            Assert.That(economy.TrySetConversionMode((ConversionMode)99, out var reason), Is.False);
            Assert.That(reason, Is.Not.Null.And.Not.Empty);
            Assert.That(economy.TrySetProductionMode((ProductionMode)(-1), out reason), Is.False);
            Assert.That(reason, Is.Not.Null.And.Not.Empty);
            Assert.That(State(economy), Is.EqualTo(before));
        }

        private static long[] State(Economy economy) => new[]
        {
            economy.A, economy.B, economy.Credits, economy.LifetimeXp, economy.Ticks,
            economy.Capacity, economy.Allocation(0), economy.Allocation(1), economy.Allocation(2),
            economy.Level(0), economy.Level(1), economy.Level(2),
            (long)economy.SelectedConversionMode, (long)economy.SelectedProductionMode,
            economy.TimingElapsedMilliseconds, economy.TimingAttempts, economy.TimingHits,
            (long)economy.LastTimingResult, economy.TimingAttemptAvailable ? 1L : 0L,
            economy.FastConversionUnlocked ? 1L : 0L, economy.TimingProductionUnlocked ? 1L : 0L
        };
    }
}
