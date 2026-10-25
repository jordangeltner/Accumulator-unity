using System;
using NUnit.Framework;
using Accumulator.Core;

namespace Accumulator.Tests.EditMode
{
    public sealed class EconomyTests
    {
        [Test]
        public void InitializesWithExpectedCapacityAllocationAndBalances()
        {
            var economy = new Economy();
            Assert.That(Economy.Scale, Is.EqualTo(10000));
            Assert.That(Economy.InitialCapacity, Is.EqualTo(6));
            Assert.That(Economy.MaxCapacity, Is.EqualTo(10));
            Assert.That(Economy.TicksPerSecond, Is.EqualTo(10));
            Assert.That(economy.Capacity, Is.EqualTo(6));
            Assert.That(economy.A, Is.Zero);
            Assert.That(economy.B, Is.Zero);
            Assert.That(economy.Credits, Is.Zero);
            Assert.That(economy.LifetimeXp, Is.Zero);
            Assert.That(economy.Ticks, Is.Zero);
            Assert.That(economy.Allocation(0), Is.EqualTo(3));
            Assert.That(economy.Allocation(1), Is.EqualTo(2));
            Assert.That(economy.Allocation(2), Is.EqualTo(1));
            for (var i = 0; i < 3; i++) Assert.That(economy.Level(i), Is.Zero);
        }

        [Test]
        public void PreviewReportsPerTickFlowsAndDoesNotMutate()
        {
            var economy = new Economy();
            var preview = economy.Preview();
            Assert.That(preview.AProduced, Is.EqualTo(6000));
            Assert.That(preview.AConsumed, Is.EqualTo(4000));
            Assert.That(preview.BProduced, Is.EqualTo(2000));
            Assert.That(preview.BConsumed, Is.EqualTo(1000));
            Assert.That(preview.CreditsProduced, Is.EqualTo(1000));
            Assert.That(preview.XpProduced, Is.EqualTo(1000));
            Assert.That(preview.MaxAProduced, Is.EqualTo(6000));
            Assert.That(preview.MaxBProduced, Is.EqualTo(2000));
            Assert.That(preview.MaxCreditsProduced, Is.EqualTo(1000));
            Assert.That(preview.MaxXpProduced, Is.EqualTo(1000));
            Assert.That(preview.ConversionStarved, Is.False);
            Assert.That(preview.DevelopmentStarved, Is.False);
            AssertUnchanged(economy, 0, 0, 0, 0, 0, 6, 3, 2, 1, 0, 0, 0);

            economy.AdvanceTicks(1);
            Assert.That(economy.A, Is.EqualTo(2000));
            Assert.That(economy.B, Is.EqualTo(1000));
            Assert.That(economy.Credits, Is.EqualTo(1000));
            Assert.That(economy.LifetimeXp, Is.EqualTo(1000));
            Assert.That(economy.Ticks, Is.EqualTo(1));
        }

        [Test]
        public void StarvationClampsConsumptionAndNeverMakesBalancesNegative()
        {
            var economy = new Economy();
            SetAllocations(economy, 1, 3, 2);
            var preview = economy.Preview();
            Assert.That(preview.AProduced, Is.EqualTo(2000));
            Assert.That(preview.BProduced, Is.EqualTo(1000));
            Assert.That(preview.AConsumed, Is.EqualTo(2000));
            Assert.That(preview.BConsumed, Is.EqualTo(1000));
            Assert.That(preview.CreditsProduced, Is.EqualTo(1000));
            Assert.That(preview.ConversionStarved, Is.True);
            Assert.That(preview.DevelopmentStarved, Is.True);
            economy.AdvanceTicks(25);
            Assert.That(economy.A, Is.GreaterThanOrEqualTo(0));
            Assert.That(economy.B, Is.GreaterThanOrEqualTo(0));
            Assert.That(economy.Credits, Is.GreaterThanOrEqualTo(0));
            Assert.That(economy.LifetimeXp, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void LifetimeXpTracksAllCreditsProducedEvenAfterSpending()
        {
            var economy = new Economy();
            economy.AdvanceTicks(1000);
            Assert.That(economy.LifetimeXp, Is.EqualTo(economy.Credits));
            Assert.That(economy.TryPurchase(0, out _), Is.True);
            Assert.That(economy.LifetimeXp, Is.GreaterThan(economy.Credits));
            var xp = economy.LifetimeXp;
            economy.AdvanceTicks(100);
            Assert.That(economy.LifetimeXp, Is.GreaterThanOrEqualTo(xp));
            Assert.That(economy.LifetimeXp - economy.Credits, Is.EqualTo(20 * Economy.Scale));
        }

        [Test]
        public void AllocationChangesAndInvalidInputsAreRejectedAtomically()
        {
            var economy = new Economy();
            AssertRejectedAllocation(economy, -1, 0);
            AssertRejectedAllocation(economy, 3, 0);
            AssertRejectedAllocation(economy, 0, -1);
            AssertRejectedAllocation(economy, 0, 4);
            Assert.That(economy.TrySetAllocation(1, 1, out _), Is.True);
            Assert.That(economy.TrySetAllocation(0, 4, out _), Is.True);
            Assert.That(economy.Allocation(0), Is.EqualTo(4));
        }

        [Test]
        public void InvalidIdsAreRejectedByCostPurchaseAndAllocationMethods()
        {
            var economy = new Economy();
            Assert.Throws<ArgumentOutOfRangeException>(() => economy.Allocation(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => economy.Allocation(3));
            Assert.Throws<ArgumentOutOfRangeException>(() => economy.Level(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => economy.Level(3));
            Assert.That(economy.TrySetAllocation(-1, 0, out _), Is.False);
            Assert.That(economy.TrySetAllocation(3, 0, out _), Is.False);
            Assert.That(economy.TryGetCost(-1, out _), Is.False);
            Assert.That(economy.TryGetCost(3, out _), Is.False);
            Assert.That(economy.CanPurchase(-1), Is.False);
            Assert.That(economy.CanPurchase(3), Is.False);
            Assert.That(economy.TryPurchase(-1, out _), Is.False);
            Assert.That(economy.TryPurchase(3, out _), Is.False);
        }

        [Test]
        public void UpgradeCostsAreCreditOnlyAndDoubleAtEveryLevel()
        {
            var economy = new Economy();
            long[] initialCosts = { 20, 40, 60 };
            for (var i = 0; i < 3; i++)
            {
                Assert.That(economy.TryGetCost(i, out var cost), Is.True);
                Assert.That(cost, Is.EqualTo(initialCosts[i] * Economy.Scale));
            }

            economy.AdvanceTicks(1000);
            long aBefore = economy.A, bBefore = economy.B, xpBefore = economy.LifetimeXp;
            Assert.That(economy.TryGetCost(0, out var firstCost), Is.True);
            Assert.That(economy.CanPurchase(0), Is.True);
            Assert.That(economy.TryPurchase(0, out _), Is.True);
            Assert.That(economy.Credits, Is.EqualTo(xpBefore - firstCost));
            Assert.That(economy.LifetimeXp, Is.EqualTo(xpBefore));
            Assert.That(economy.A, Is.EqualTo(aBefore));
            Assert.That(economy.B, Is.EqualTo(bBefore));
            Assert.That(economy.Level(0), Is.EqualTo(1));
            Assert.That(economy.TryGetCost(0, out var secondCost), Is.True);
            Assert.That(secondCost, Is.EqualTo(firstCost * 2));
            Assert.That(economy.Preview().AProduced, Is.EqualTo(9000));
        }

        [Test]
        public void EachUpgradeImprovesItsOwnProductionEffect()
        {
            for (var index = 0; index < 3; index++)
            {
                var economy = new Economy();
                economy.AdvanceTicks(1000);
                var before = economy.Preview();
                Assert.That(economy.TryPurchase(index, out _), Is.True);
                var after = economy.Preview();
                if (index == 0) Assert.That(after.AProduced, Is.GreaterThan(before.AProduced));
                if (index == 1) Assert.That(after.BProduced, Is.GreaterThan(before.BProduced));
                if (index == 2) Assert.That(after.CreditsProduced, Is.GreaterThan(before.CreditsProduced));
            }
        }

        [Test]
        public void PurchasesAndExpansionsFailWithoutPartialStateChanges()
        {
            var economy = new Economy();
            Assert.That(economy.TryPurchase(0, out var purchaseReason), Is.False);
            Assert.That(string.IsNullOrEmpty(purchaseReason), Is.False);
            Assert.That(economy.Level(0), Is.Zero);
            Assert.That(economy.TryExpandCapacity(out var expansionReason), Is.False);
            Assert.That(string.IsNullOrEmpty(expansionReason), Is.False);
            AssertUnchanged(economy, 0, 0, 0, 0, 0, 6, 3, 2, 1, 0, 0, 0);
        }

        [Test]
        public void ExpansionPricesAndCapacityLimitAreExact()
        {
            var economy = new Economy();
            long[] prices = { 120, 300, 750, 1875 };
            for (var i = 0; i < prices.Length; i++)
            {
                Assert.That(economy.Capacity, Is.EqualTo(6 + i));
                Assert.That(economy.TryGetExpansionCost(out var cost), Is.True);
                Assert.That(cost, Is.EqualTo(prices[i] * Economy.Scale));
                economy.AdvanceTicks(20000);
                Assert.That(economy.Credits, Is.GreaterThanOrEqualTo(cost));
                var xpBefore = economy.LifetimeXp;
                var creditsBefore = economy.Credits;
                var aBefore = economy.A;
                var bBefore = economy.B;
                var workerAllocation = economy.Allocation(0);
                Assert.That(economy.TryExpandCapacity(out _), Is.True);
                Assert.That(economy.Capacity, Is.EqualTo(7 + i));
                Assert.That(economy.Credits, Is.EqualTo(creditsBefore - cost));
                Assert.That(economy.LifetimeXp, Is.EqualTo(xpBefore));
                Assert.That(economy.A, Is.EqualTo(aBefore));
                Assert.That(economy.B, Is.EqualTo(bBefore));
                Assert.That(economy.Allocation(0) + economy.Allocation(1) + economy.Allocation(2), Is.EqualTo(6));
                int available = economy.Capacity - economy.Allocation(1) - economy.Allocation(2);
                Assert.That(economy.TrySetAllocation(0, available, out _), Is.True);
                AssertRejectedAllocation(economy, 0, available + 1);
                Assert.That(economy.TrySetAllocation(0, workerAllocation, out _), Is.True);
            }
            Assert.That(economy.TryGetExpansionCost(out _), Is.False);
            Assert.That(economy.TryExpandCapacity(out _), Is.False);
            Assert.That(economy.Capacity, Is.EqualTo(Economy.MaxCapacity));
        }

        [Test]
        public void BatchedTicksMatchRepeatedTicksExactly()
        {
            var batched = new Economy();
            var repeated = new Economy();
            batched.AdvanceTicks(321);
            for (var i = 0; i < 321; i++) repeated.AdvanceTicks(1);
            AssertSameState(batched, repeated);
        }

        [Test]
        public void NegativeTickCountThrowsWithoutAdvancing()
        {
            var economy = new Economy();
            Assert.Throws<ArgumentOutOfRangeException>(() => economy.AdvanceTicks(-1));
            Assert.That(economy.Ticks, Is.Zero);
        }

        [Test]
        public void TickLedgerOverflowDoesNotPartiallyCommitBalances()
        {
            var economy = new Economy();
            var xpSetter = typeof(Economy).GetProperty(nameof(Economy.LifetimeXp)).GetSetMethod(true);
            Assert.That(xpSetter, Is.Not.Null);
            xpSetter.Invoke(economy, new object[] { long.MaxValue });
            var aBefore = economy.A;
            var bBefore = economy.B;
            var creditsBefore = economy.Credits;
            var ticksBefore = economy.Ticks;

            Assert.Throws<OverflowException>(() => economy.AdvanceTicks(1));

            Assert.That(economy.A, Is.EqualTo(aBefore));
            Assert.That(economy.B, Is.EqualTo(bBefore));
            Assert.That(economy.Credits, Is.EqualTo(creditsBefore));
            Assert.That(economy.LifetimeXp, Is.EqualTo(long.MaxValue));
            Assert.That(economy.Ticks, Is.EqualTo(ticksBefore));
        }

        private static void SetAllocations(Economy economy, int a, int b, int d)
        {
            var targets = new[] { a, b, d };
            for (var i = 0; i < 3; i++)
                if (targets[i] < economy.Allocation(i)) Assert.That(economy.TrySetAllocation(i, targets[i], out _), Is.True);
            for (var i = 0; i < 3; i++)
                if (targets[i] > economy.Allocation(i)) Assert.That(economy.TrySetAllocation(i, targets[i], out _), Is.True);
        }

        private static void AssertRejectedAllocation(Economy economy, int index, int amount)
        {
            var before = new[] { economy.Allocation(0), economy.Allocation(1), economy.Allocation(2) };
            Assert.That(economy.TrySetAllocation(index, amount, out var reason), Is.False);
            Assert.That(string.IsNullOrEmpty(reason), Is.False);
            for (var i = 0; i < 3; i++) Assert.That(economy.Allocation(i), Is.EqualTo(before[i]));
        }

        private static void AssertUnchanged(Economy e, long a, long b, long credits, long xp, long ticks,
            int capacity, int x, int y, int z, int l0, int l1, int l2)
        {
            Assert.That(e.A, Is.EqualTo(a)); Assert.That(e.B, Is.EqualTo(b));
            Assert.That(e.Credits, Is.EqualTo(credits)); Assert.That(e.LifetimeXp, Is.EqualTo(xp));
            Assert.That(e.Ticks, Is.EqualTo(ticks)); Assert.That(e.Capacity, Is.EqualTo(capacity));
            Assert.That(e.Allocation(0), Is.EqualTo(x)); Assert.That(e.Allocation(1), Is.EqualTo(y));
            Assert.That(e.Allocation(2), Is.EqualTo(z)); Assert.That(e.Level(0), Is.EqualTo(l0));
            Assert.That(e.Level(1), Is.EqualTo(l1)); Assert.That(e.Level(2), Is.EqualTo(l2));
        }

        private static void AssertSameState(Economy a, Economy b)
        {
            Assert.That(a.Ticks, Is.EqualTo(b.Ticks)); Assert.That(a.A, Is.EqualTo(b.A));
            Assert.That(a.B, Is.EqualTo(b.B)); Assert.That(a.Credits, Is.EqualTo(b.Credits));
            Assert.That(a.LifetimeXp, Is.EqualTo(b.LifetimeXp)); Assert.That(a.Capacity, Is.EqualTo(b.Capacity));
            for (var i = 0; i < 3; i++)
            {
                Assert.That(a.Allocation(i), Is.EqualTo(b.Allocation(i)));
                Assert.That(a.Level(i), Is.EqualTo(b.Level(i)));
            }
        }
    }
}
