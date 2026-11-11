using Accumulator.Core;
using NUnit.Framework;

namespace Accumulator.Tests.EditMode
{
    public sealed class ConversionModeTests
    {
        [Test]
        public void BaselineRejectsFastAndUnknownModesWithoutChangingState()
        {
            var economy = new Economy();
            economy.AdvanceTicks(1200);
            var before = State(economy);
            Assert.That(economy.AlternateConversionEnabled, Is.False);
            Assert.That(economy.SelectedConversionMode, Is.EqualTo(ConversionMode.Standard));
            Assert.That(economy.TrySetConversionMode(ConversionMode.Fast, out var reason), Is.False);
            Assert.That(reason, Is.Not.Null.And.Not.Empty);
            Assert.That(State(economy), Is.EqualTo(before));
            Assert.That(economy.TrySetConversionMode((ConversionMode)99, out reason), Is.False);
            Assert.That(reason, Is.Not.Null.And.Not.Empty);
            Assert.That(economy.TrySetConversionMode(ConversionMode.Standard, out reason), Is.True);
            Assert.That(reason, Is.Null);
            Assert.That(State(economy), Is.EqualTo(before));
        }

        [Test]
        public void SwitchingModesIsFreeAndDoesNotAdvanceOrReallocate()
        {
            var economy = UnlockedEconomy();
            economy.AdvanceTicks(1200);
            Assert.That(economy.TryPurchase(Economy.Converter, out _), Is.True);
            var before = State(economy);
            Assert.That(economy.AlternateConversionEnabled, Is.True);
            Assert.That(economy.SelectedConversionMode, Is.EqualTo(ConversionMode.Standard));
            Assert.That(economy.TrySetConversionMode(ConversionMode.Fast, out _), Is.True);
            Assert.That(State(economy), Is.EqualTo(before));
            Assert.That(economy.SelectedConversionMode, Is.EqualTo(ConversionMode.Fast));
            Assert.That(economy.TrySetConversionMode((ConversionMode)(-1), out _), Is.False);
            Assert.That(economy.SelectedConversionMode, Is.EqualTo(ConversionMode.Fast));
            Assert.That(State(economy), Is.EqualTo(before));
            Assert.That(economy.TrySetConversionMode(ConversionMode.Fast, out _), Is.True);
            Assert.That(economy.TrySetConversionMode(ConversionMode.Standard, out _), Is.True);
            Assert.That(State(economy), Is.EqualTo(before));
        }

        [Test]
        public void FastTradesTwiceTheInputForFiftyPercentMoreConversionOutput()
        {
            var economy = UnlockedEconomy();
            SetAllocations(economy, 3, 1, 2);
            var standard = economy.Preview();
            Assert.That(standard.AConsumed, Is.EqualTo(2000));
            Assert.That(standard.BProduced, Is.EqualTo(1000));
            Assert.That(economy.TrySetConversionMode(ConversionMode.Fast, out _), Is.True);
            var fast = economy.Preview();
            Assert.That(fast.AConsumed, Is.EqualTo(4000));
            Assert.That(fast.BProduced, Is.EqualTo(1500));
            Assert.That(fast.MaxBProduced, Is.EqualTo(1500));
            Assert.That(fast.ConversionStarved, Is.False);
            Assert.That(fast.DevelopmentStarved, Is.True);
            Assert.That(fast.CreditsProduced, Is.EqualTo(1500));
            Assert.That(fast.XpProduced, Is.EqualTo(1500));
            economy.AdvanceTicks(10);
            Assert.That(economy.A, Is.EqualTo(2 * Economy.Scale));
            Assert.That(economy.B, Is.Zero);
            Assert.That(economy.Credits, Is.EqualTo(15000));
            Assert.That(economy.LifetimeXp, Is.EqualTo(100 * Economy.Scale + 15000));
        }

        [Test]
        public void FastUsesSameTickInputAndClampsStarvedStages()
        {
            var economy = UnlockedEconomy();
            SetAllocations(economy, 1, 3, 2);
            Assert.That(economy.TrySetConversionMode(ConversionMode.Fast, out _), Is.True);
            var preview = economy.Preview();
            Assert.That(preview.AProduced, Is.EqualTo(2000));
            Assert.That(preview.AConsumed, Is.EqualTo(2000));
            Assert.That(preview.BProduced, Is.EqualTo(750));
            Assert.That(preview.MaxBProduced, Is.EqualTo(4500));
            Assert.That(preview.BConsumed, Is.EqualTo(750));
            Assert.That(preview.CreditsProduced, Is.EqualTo(750));
            Assert.That(preview.ConversionStarved, Is.True);
            Assert.That(preview.DevelopmentStarved, Is.True);
            Assert.That(economy.Ticks, Is.Zero);
            Assert.That(economy.Credits, Is.Zero);
            economy.AdvanceTicks(1);
            Assert.That(economy.A, Is.Zero);
            Assert.That(economy.B, Is.Zero);
            Assert.That(economy.Credits, Is.EqualTo(preview.CreditsProduced));
            Assert.That(economy.LifetimeXp, Is.EqualTo(100 * Economy.Scale + preview.XpProduced));
            economy.AdvanceTicks(999);
            Assert.That(economy.A, Is.Zero);
            Assert.That(economy.B, Is.Zero);
            Assert.That(economy.Credits, Is.EqualTo(750000));
            SetAllocations(economy, 0, 3, 3);
            economy.AdvanceTicks(100);
            Assert.That(economy.A, Is.Zero);
            Assert.That(economy.B, Is.Zero);
            Assert.That(economy.Credits, Is.EqualTo(750000));
        }

        [Test]
        public void ExistingConverterUpgradeMultipliesBothModes()
        {
            var economy = UnlockedEconomy();
            economy.AdvanceTicks(400);
            Assert.That(economy.TryPurchase(Economy.Converter, out _), Is.True);
            SetAllocations(economy, 3, 1, 2);
            Assert.That(economy.Preview().BProduced, Is.EqualTo(1500));
            Assert.That(economy.TrySetConversionMode(ConversionMode.Fast, out _), Is.True);
            var fast = economy.Preview();
            Assert.That(fast.AConsumed, Is.EqualTo(4000));
            Assert.That(fast.BProduced, Is.EqualTo(2250));
            Assert.That(fast.MaxBProduced, Is.EqualTo(2250));
        }

        [Test]
        public void FastFractionalOutputRoundsDownToIntegerSubunits()
        {
            var economy = UnlockedEconomy();
            economy.AdvanceTicks(1200);
            for (var index = 0; index < 3; index++)
                Assert.That(economy.TryPurchase(index, out _), Is.True);
            Assert.That(economy.TrySetConversionMode(ConversionMode.Fast, out _), Is.True);
            // Empty the funded run's buffers using public production actions only.
            SetAllocations(economy, 0, 6, 0);
            economy.AdvanceTicks(1000);
            SetAllocations(economy, 0, 0, 6);
            economy.AdvanceTicks(10000);
            Assert.That(economy.A, Is.Zero);
            Assert.That(economy.B, Is.Zero);
            SetAllocations(economy, 1, 3, 2);
            var before = State(economy);
            var preview = economy.Preview();
            Assert.That(State(economy), Is.EqualTo(before));
            // 0.3 A produces 0.16875 B; storage is in units of 0.0001.
            Assert.That(preview.AConsumed, Is.EqualTo(3000));
            Assert.That(preview.BProduced, Is.EqualTo(1687));
            Assert.That(preview.CreditsProduced, Is.EqualTo(2530));
            var creditsBefore = economy.Credits;
            economy.AdvanceTicks(1);
            Assert.That(economy.A, Is.Zero);
            Assert.That(economy.B, Is.Zero);
            Assert.That(economy.Credits - creditsBefore, Is.EqualTo(2530));
        }

        [Test]
        public void MixedModeBatchesMatchSingleTicksExactly()
        {
            var batched = UnlockedEconomy();
            var repeated = UnlockedEconomy();
            var modes = new[] { ConversionMode.Standard, ConversionMode.Fast, ConversionMode.Standard, ConversionMode.Fast };
            var counts = new[] { 1000, 137, 251, 997 };
            for (var stage = 0; stage < modes.Length; stage++)
            {
                Assert.That(batched.TrySetConversionMode(modes[stage], out _), Is.True);
                Assert.That(repeated.TrySetConversionMode(modes[stage], out _), Is.True);
                batched.AdvanceTicks(counts[stage]);
                for (var tick = 0; tick < counts[stage]; tick++) repeated.AdvanceTicks(1);
                Assert.That(State(batched), Is.EqualTo(State(repeated)));
                Assert.That(batched.A, Is.GreaterThanOrEqualTo(0));
                Assert.That(batched.B, Is.GreaterThanOrEqualTo(0));
                if (stage == 0)
                {
                    Assert.That(batched.TryPurchase(Economy.Converter, out _), Is.True);
                    Assert.That(repeated.TryPurchase(Economy.Converter, out _), Is.True);
                    SetAllocations(batched, 1, 3, 2);
                    SetAllocations(repeated, 1, 3, 2);
                }
            }
        }

        // Isolate flow mechanics from progression without pre-filling A/B buffers.
        // ModeProgressionTests earns its XP through actual simulation ticks.
        private static Economy UnlockedEconomy()
        {
            var economy = new Economy(true);
            var setter = typeof(Economy).GetProperty(nameof(Economy.LifetimeXp)).GetSetMethod(true);
            Assert.That(setter, Is.Not.Null);
            setter.Invoke(economy, new object[] { 100 * Economy.Scale });
            Assert.That(economy.FastConversionUnlocked, Is.True);
            return economy;
        }

        private static void SetAllocations(Economy economy, int a, int b, int d)
        {
            var targets = new[] { a, b, d };
            for (var i = 0; i < 3; i++)
                if (targets[i] < economy.Allocation(i)) Assert.That(economy.TrySetAllocation(i, targets[i], out _), Is.True);
            for (var i = 0; i < 3; i++)
                if (targets[i] > economy.Allocation(i)) Assert.That(economy.TrySetAllocation(i, targets[i], out _), Is.True);
        }

        private static long[] State(Economy economy) => new[]
        {
            economy.A, economy.B, economy.Credits, economy.LifetimeXp, economy.Ticks,
            economy.Capacity, economy.Allocation(0), economy.Allocation(1), economy.Allocation(2),
            economy.Level(0), economy.Level(1), economy.Level(2)
        };
    }
}
