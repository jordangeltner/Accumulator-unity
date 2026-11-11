namespace Accumulator.Core
{
    /// <summary>A live read-only view. It deliberately exposes no reference to the mutable economy.</summary>
    public sealed class RunSnapshot
    {
        private readonly Economy _economy;
        internal RunSnapshot(Economy economy) { _economy = economy; }
        public long A => _economy.A;
        public long B => _economy.B;
        public long Credits => _economy.Credits;
        public long LifetimeXp => _economy.LifetimeXp;
        public long Ticks => _economy.Ticks;
        public int Capacity => _economy.Capacity;
        public int Allocation(int index) => _economy.Allocation(index);
        public int Level(int index) => _economy.Level(index);
        public bool AlternateConversionEnabled => _economy.AlternateConversionEnabled;
        public bool TimingProductionEnabled => _economy.TimingProductionEnabled;
        public bool FastConversionUnlocked => _economy.FastConversionUnlocked;
        public bool TimingProductionUnlocked => _economy.TimingProductionUnlocked;
        public ProductionMode SelectedProductionMode => _economy.SelectedProductionMode;
        public ConversionMode SelectedConversionMode => _economy.SelectedConversionMode;
        public long TimingElapsedMilliseconds => _economy.TimingElapsedMilliseconds;
        public int TimingPosition => _economy.TimingPosition;
        public bool TimingAttemptAvailable => _economy.TimingAttemptAvailable;
        public long TimingHits => _economy.TimingHits;
        public long TimingAttempts => _economy.TimingAttempts;
        public TimingResult LastTimingResult => _economy.LastTimingResult;
        public Throughput Preview() => _economy.Preview();
        public bool TryGetCost(int index, out long credits) => _economy.TryGetCost(index, out credits);
        public bool TryGetExpansionCost(out long credits) => _economy.TryGetExpansionCost(out credits);
        public bool CanPurchase(int index) => _economy.CanPurchase(index);
    }
}
