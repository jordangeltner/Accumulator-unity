using System;
using System.Numerics;
using NUnit.Framework;
using Accumulator.Core;

namespace Accumulator.Tests.EditMode
{
    /// <summary>Test-only deterministic policies used to lock long-run economy projections.</summary>
    public sealed class EconomyStrategyRegressionTests
    {
        public enum Policy { FixedSix, EfficiencyFirst, CapacityOpening, Mixed }

        [TestCase(Policy.FixedSix, 3000, 23922000L, 7522000L, 6, 3, 4, 4)]
        [TestCase(Policy.FixedSix, 6000, 70260750L, 17060750L, 6, 4, 5, 6)]
        [TestCase(Policy.FixedSix, 9000, 126972500L, 19372500L, 6, 5, 6, 7)]
        [TestCase(Policy.EfficiencyFirst, 3000, 24848625L, 3148625L, 9, 4, 3, 3)]
        [TestCase(Policy.EfficiencyFirst, 6000, 87360875L, 510875L, 10, 6, 6, 5)]
        [TestCase(Policy.EfficiencyFirst, 9000, 177079875L, 32629875L, 10, 7, 7, 6)]
        [TestCase(Policy.CapacityOpening, 3000, 23449500L, 2849500L, 8, 3, 4, 4)]
        [TestCase(Policy.CapacityOpening, 6000, 91207500L, 7557500L, 10, 4, 5, 6)]
        [TestCase(Policy.CapacityOpening, 9000, 187344500L, 49294500L, 10, 5, 6, 7)]
        [TestCase(Policy.Mixed, 3000, 28112000L, 7512000L, 8, 3, 4, 4)]
        [TestCase(Policy.Mixed, 6000, 99464000L, 15814000L, 10, 4, 5, 6)]
        [TestCase(Policy.Mixed, 9000, 197043000L, 58993000L, 10, 5, 6, 7)]
        public void PolicyProjectionMatchesExactRegression(Policy policy, int tick,
            long lifetimeXp, long credits, int capacity, int producerLevel, int converterLevel, int developerLevel)
        {
            var economy = new Economy();
            Run(economy, policy, tick);
            Assert.That(economy.Ticks, Is.EqualTo(tick));
            Assert.That(economy.LifetimeXp, Is.EqualTo(lifetimeXp));
            Assert.That(economy.Credits, Is.EqualTo(credits));
            Assert.That(economy.Capacity, Is.EqualTo(capacity));
            Assert.That(economy.Level(0), Is.EqualTo(producerLevel));
            Assert.That(economy.Level(1), Is.EqualTo(converterLevel));
            Assert.That(economy.Level(2), Is.EqualTo(developerLevel));
        }

        private static void Run(Economy economy, Policy policy, int untilTick)
        {
            var dirty = true;
            long[] debt = { 0, 0, 0 };
            for (var t = 1; t <= untilTick; t++)
            {
                if (dirty || (t - 1) % 10 == 0)
                {
                    Allocate(economy, debt, dirty);
                    dirty = false;
                }

                economy.AdvanceTicks(1);
                var action = Choose(economy, policy);
                if (action == 3)
                {
                    Assert.That(economy.TryExpandCapacity(out _), Is.True);
                    dirty = true;
                }
                else if (action >= 0)
                {
                    Assert.That(economy.TryPurchase(action, out _), Is.True);
                    dirty = true;
                }
                Assert.That(economy.LifetimeXp - economy.Credits,
                    Is.EqualTo(SpentCost(economy)), "XP must equal current credits plus lifetime spending at tick " + t);
            }
        }

        private static void Allocate(Economy economy, long[] debt, bool resetDebt)
        {
            if (resetDebt) Array.Clear(debt, 0, debt.Length);
            var aWeight = 2L + economy.Level(0);
            var bWeight = 2L + economy.Level(1);
            var weights = new[] { 4L, 2L * aWeight, aWeight * bWeight };
            var sum = weights[0] + weights[1] + weights[2];
            var output = new[] { 0, 0, 0 };
            for (var i = 0; i < 3; i++) debt[i] += economy.Capacity * weights[i];
            for (var unit = 0; unit < economy.Capacity; unit++)
            {
                var selected = 0;
                if (debt[1] > debt[selected]) selected = 1;
                if (debt[2] > debt[selected]) selected = 2;
                debt[selected] -= sum;
                output[selected]++;
            }
            SetAllocations(economy, output[0], output[1], output[2]);
        }

        private static int Choose(Economy economy, Policy policy)
        {
            if (policy == Policy.EfficiencyFirst)
            {
                var min = Math.Min(economy.Level(0), Math.Min(economy.Level(1), economy.Level(2)));
                if (economy.Capacity < Economy.MaxCapacity && min >= economy.Capacity - 5)
                    return CanExpand(economy) ? 3 : -1;
                for (var i = 0; i < 3; i++)
                    if (economy.Level(i) == min) return economy.CanPurchase(i) ? i : -1;
                return -1;
            }
            if (policy == Policy.CapacityOpening && economy.Capacity == Economy.InitialCapacity)
                return CanExpand(economy) ? 3 : -1;

            var allowExpansion = policy != Policy.FixedSix;
            var best = -1;
            long bestNumerator = 0, bestDenominator = 1, bestCost = 1;
            var before = RateNumerator(economy, economy.Capacity, economy.Level(0), economy.Level(1), economy.Level(2));
            var beforeDen = RateDenominator(economy.Level(0), economy.Level(1));
            for (var i = 0; i < (allowExpansion ? 4 : 3); i++)
            {
                if (i == 3 && economy.Capacity >= Economy.MaxCapacity) continue;
                var l0 = economy.Level(0) + (i == 0 ? 1 : 0);
                var l1 = economy.Level(1) + (i == 1 ? 1 : 0);
                var l2 = economy.Level(2) + (i == 2 ? 1 : 0);
                var cap = economy.Capacity + (i == 3 ? 1 : 0);
                var afterNum = RateNumerator(economy, cap, l0, l1, l2);
                var afterDen = RateDenominator(l0, l1);
                var gainNum = afterNum * beforeDen - before * afterDen;
                var gainDen = afterDen * beforeDen;
                long cost;
                if (i == 3) economy.TryGetExpansionCost(out cost);
                else economy.TryGetCost(i, out cost);
                // Rank every candidate, even when unaffordable; ties preserve lower action index.
                var candidateCrossProduct = new BigInteger(gainNum) * bestDenominator * bestCost;
                var bestCrossProduct = new BigInteger(bestNumerator) * gainDen * cost;
                if (best < 0 || candidateCrossProduct > bestCrossProduct)
                {
                    best = i;
                    bestNumerator = gainNum;
                    bestDenominator = gainDen;
                    bestCost = cost;
                }
            }
            return best >= 0 && BestCost(economy, best) <= economy.Credits ? best : -1;
        }

        private static long BestCost(Economy economy, int action)
        {
            if (action == 3) { economy.TryGetExpansionCost(out var expansion); return expansion; }
            economy.TryGetCost(action, out var cost); return cost;
        }

        private static long RateNumerator(Economy economy, int capacity, int l0, int l1, int l2)
        {
            var a = 2L + l0;
            var b = 2L + l1;
            return capacity * a * b * (2L + l2);
        }

        private static long RateDenominator(int l0, int l1)
        {
            var a = 2L + l0;
            var b = 2L + l1;
            return 2 * (4 + 2 * a + a * b);
        }

        private static bool CanExpand(Economy economy) =>
            economy.TryGetExpansionCost(out var cost) && economy.Credits >= cost;

        private static long SpentCost(Economy economy)
        {
            long spent = 0;
            long[] baseCosts = { 20, 40, 60 };
            for (var i = 0; i < 3; i++)
                spent += baseCosts[i] * Economy.Scale * ((1L << economy.Level(i)) - 1);
            long[] expansions = { 120, 300, 750, 1875 };
            for (var i = Economy.InitialCapacity; i < economy.Capacity; i++)
                spent += expansions[i - Economy.InitialCapacity] * Economy.Scale;
            return spent;
        }

        private static void SetAllocations(Economy economy, int x, int y, int z)
        {
            var target = new[] { x, y, z };
            for (var i = 0; i < 3; i++)
                if (target[i] < economy.Allocation(i)) Assert.That(economy.TrySetAllocation(i, target[i], out _), Is.True);
            for (var i = 0; i < 3; i++)
                if (target[i] > economy.Allocation(i)) Assert.That(economy.TrySetAllocation(i, target[i], out _), Is.True);
        }
    }
}
