using System;

namespace Accumulator.Core
{
    /// <summary>Deterministic fixed-point economy simulation, independent of Unity.</summary>
    public sealed class Economy
    {
        /// <summary>
        /// Number of integer subunits in one displayed resource unit. Reachable tick rates,
        /// resource amounts, and upgrade costs remain divisible at every integer half-step;
        /// this scale avoids floating-point arithmetic without losing reachable precision.
        /// </summary>
        public const long Scale = 10000;
        public const int InitialCapacity = 6;
        public const int MaxCapacity = 10;
        public const int TicksPerSecond = 10;

        /// <summary>Stable allocation and upgrade identifiers.</summary>
        public const int AProducer = 0;
        public const int Converter = 1;
        public const int Developer = 2;

        /// <summary>Stable presentation identifiers for resources and processes.</summary>
        public const string ResourceAId = "resource.a";
        public const string ResourceBId = "resource.b";
        public const string CreditsId = "currency.credits";
        public const string LifetimeXpId = "progress.lifetime_xp";
        public const string AProducerId = "process.01";
        public const string ConverterId = "process.02";
        public const string DeveloperId = "process.03";
        public const string AProducerUpgradeId = "upgrade.01";
        public const string ConverterUpgradeId = "upgrade.02";
        public const string DeveloperUpgradeId = "upgrade.03";

        private readonly int[] _allocations = { 3, 2, 1 };
        private readonly int[] _levels = { 0, 0, 0 };
        private int _capacity = InitialCapacity;

        public long A { get; private set; }
        public long B { get; private set; }
        public long Credits { get; private set; }
        public long LifetimeXp { get; private set; }
        public long Ticks { get; private set; }
        public int Capacity => _capacity;

        /// <summary>Returns the worker allocation for stable index 0, 1, or 2.</summary>
        public int Allocation(int index) => _allocations[ValidateIndex(index)];

        /// <summary>Returns the upgrade level for stable index 0, 1, or 2.</summary>
        public int Level(int index) => _levels[ValidateIndex(index)];

        /// <summary>Changes one allocation if the resulting total remains within capacity.</summary>
        public bool TrySetAllocation(int index, int amount, out string reason)
        {
            if (!IsValidIndex(index))
            {
                reason = "Index must be between 0 and 2.";
                return false;
            }
            if (amount < 0)
            {
                reason = "Allocation cannot be negative.";
                return false;
            }
            long total = amount;
            for (var i = 0; i < _allocations.Length; i++)
                if (i != index) total += _allocations[i];
            if (total > _capacity)
            {
                reason = "Total allocation exceeds capacity.";
                return false;
            }
            _allocations[index] = amount;
            reason = null;
            return true;
        }

        /// <summary>Gets the next upgrade cost in fixed-point subunits.</summary>
        public bool TryGetCost(int index, out long credits)
        {
            credits = 0;
            if (!IsValidIndex(index) || _levels[index] >= 63) return false;
            try
            {
                checked
                {
                    var multiplier = 1L << _levels[index];
                    credits = BaseCost(index) * Scale * multiplier;
                }
                return true;
            }
            catch (OverflowException)
            {
                credits = 0;
                return false;
            }
        }

        /// <summary>Purchases the next upgrade when its cost is representable and affordable.</summary>
        public bool TryPurchase(int index, out string reason)
        {
            if (!IsValidIndex(index))
            {
                reason = "Index must be between 0 and 2.";
                return false;
            }
            if (_levels[index] == int.MaxValue || !TryGetCost(index, out var cost))
            {
                reason = "Upgrade cost is no longer representable.";
                return false;
            }
            if (Credits < cost)
            {
                reason = "Insufficient credits.";
                return false;
            }
            Credits -= cost;
            _levels[index]++;
            reason = null;
            return true;
        }

        /// <summary>Returns whether the next upgrade can currently be purchased.</summary>
        public bool CanPurchase(int index) => IsValidIndex(index) && TryGetCost(index, out var cost) && Credits >= cost;

        /// <summary>Gets the next capacity expansion cost in fixed-point subunits.</summary>
        public bool TryGetExpansionCost(out long credits)
        {
            credits = 0;
            if (_capacity >= MaxCapacity) return false;
            credits = ExpansionBaseCost(_capacity) * Scale;
            return true;
        }

        /// <summary>Buys one additional unassigned worker capacity.</summary>
        public bool TryExpandCapacity(out string reason)
        {
            if (!TryGetExpansionCost(out var cost))
            {
                reason = "Capacity is already at maximum.";
                return false;
            }
            if (Credits < cost)
            {
                reason = "Insufficient credits.";
                return false;
            }
            Credits -= cost;
            _capacity++;
            reason = null;
            return true;
        }

        /// <summary>Advances deterministic simulation ticks; negative counts are invalid.</summary>
        public void AdvanceTicks(int count)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            for (var i = 0; i < count; i++)
            {
                var flow = Calculate(A, B, _allocations, _levels);
                var nextTicks = checked(Ticks + 1);
                // Calculate both balances first so overflow cannot partially commit a tick.
                var nextCredits = checked(Credits + flow.Throughput.CreditsProduced);
                var nextLifetimeXp = checked(LifetimeXp + flow.Throughput.XpProduced);
                A = flow.NextA;
                B = flow.NextB;
                Credits = nextCredits;
                LifetimeXp = nextLifetimeXp;
                Ticks = nextTicks;
            }
        }

        /// <summary>Calculates next-tick production and starvation without mutating state.</summary>
        public Throughput Preview()
        {
            var flow = Calculate(A, B, _allocations, _levels);
            return flow.Throughput;
        }

        private static Flow Calculate(long a, long b, int[] allocation, int[] levels)
        {
            checked
            {
                var aProduced = (long)allocation[AProducer] * (2L + levels[AProducer]) * Scale / TicksPerSecond;
                var aDemand = (long)allocation[Converter] * Scale / TicksPerSecond;
                var availableA = a + aProduced;
                var converted = Math.Min(aDemand, availableA / 2);
                var aConsumed = converted * 2;
                var nextA = availableA - aConsumed;

                var bProduced = converted * (2L + levels[Converter]) / 2;
                var bDemand = (long)allocation[Developer] * Scale / TicksPerSecond;
                var availableB = b + bProduced;
                var developed = Math.Min(bDemand, availableB);
                var nextB = availableB - developed;
                var creditsProduced = developed * (2L + levels[Developer]) / 2;
                var maxBProduced = aDemand * (2L + levels[Converter]) / 2;
                var maxXpProduced = bDemand * (2L + levels[Developer]) / 2;

                var throughput = new Throughput(aProduced, aConsumed, bProduced, developed,
                    creditsProduced, maxBProduced, maxXpProduced,
                    aDemand > converted, bDemand > developed);
                return new Flow(nextA, nextB, throughput);
            }
        }

        private static int ValidateIndex(int index)
        {
            if (!IsValidIndex(index)) throw new ArgumentOutOfRangeException(nameof(index));
            return index;
        }

        private static bool IsValidIndex(int index) => index >= 0 && index <= 2;

        private static int BaseCost(int index) => index == AProducer ? 20 : index == Converter ? 40 : 60;
        private static int ExpansionBaseCost(int capacity) => capacity == 6 ? 120 : capacity == 7 ? 300 : capacity == 8 ? 750 : 1875;

        private readonly struct Flow
        {
            public readonly long NextA, NextB;
            public readonly Throughput Throughput;
            public Flow(long nextA, long nextB, Throughput throughput)
            {
                NextA = nextA;
                NextB = nextB;
                Throughput = throughput;
            }
        }
    }

    /// <summary>Immutable per-tick production and consumption snapshot, in fixed-point subunits.</summary>
    public readonly struct Throughput
    {
        public long AProduced { get; }
        public long BProduced { get; }
        public long AConsumed { get; }
        public long BConsumed { get; }
        public long CreditsProduced { get; }
        public long XpProduced { get; }
        public long MaxAProduced { get; }
        public long MaxBProduced { get; }
        public long MaxCreditsProduced { get; }
        public long MaxXpProduced { get; }
        public bool ConversionStarved { get; }
        public bool DevelopmentStarved { get; }

        internal Throughput(long aProduced, long aConsumed, long bProduced, long bConsumed,
            long creditsProduced, long maxBProduced, long maxXpProduced,
            bool conversionStarved, bool developmentStarved)
        {
            AProduced = aProduced;
            MaxAProduced = aProduced;
            AConsumed = aConsumed;
            BProduced = bProduced;
            MaxBProduced = maxBProduced;
            BConsumed = bConsumed;
            CreditsProduced = creditsProduced;
            XpProduced = creditsProduced;
            MaxCreditsProduced = maxXpProduced;
            MaxXpProduced = maxXpProduced;
            ConversionStarved = conversionStarved;
            DevelopmentStarved = developmentStarved;
        }
    }
}
