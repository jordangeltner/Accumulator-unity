using System;
using System.Globalization;

namespace Accumulator.Core
{
    public enum RunState { Countdown, Running, Paused, Finished, Abandoned }

    /// <summary>Immutable starting configuration. Category identity includes every configurable rule.</summary>
    public sealed class RunDefinition
    {
        public static RunDefinition Default { get; } = new RunDefinition("speedrun-06-v2", 2000 * Economy.Scale);
        public string RulesetId { get; }
        public long TargetXp { get; }
        public int CountdownMilliseconds { get; }
        public bool AlternateConversionEnabled { get; }
        public bool TimingProductionEnabled { get; }
        public string CategoryKey { get; }

        public RunDefinition(string rulesetId, long targetXp, int countdownMilliseconds = 0,
            bool alternateConversionEnabled = true, bool timingProductionEnabled = true)
        {
            if (string.IsNullOrWhiteSpace(rulesetId) || rulesetId.Length > 128)
                throw new ArgumentException("Ruleset ID must contain 1 to 128 characters.", nameof(rulesetId));
            if (targetXp <= 0) throw new ArgumentOutOfRangeException(nameof(targetXp));
            if (countdownMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(countdownMilliseconds));
            RulesetId = rulesetId;
            TargetXp = targetXp;
            CountdownMilliseconds = countdownMilliseconds;
            AlternateConversionEnabled = alternateConversionEnabled;
            TimingProductionEnabled = timingProductionEnabled;
            CategoryKey = rulesetId.Length.ToString(CultureInfo.InvariantCulture) + ":" + rulesetId + ":"
                + targetXp.ToString(CultureInfo.InvariantCulture) + ":" + countdownMilliseconds.ToString(CultureInfo.InvariantCulture)
                + ":" + (alternateConversionEnabled ? "1" : "0") + ":" + (timingProductionEnabled ? "1" : "0");
        }
    }

    /// <summary>Untrusted local result; eligibility means comparable locally, never server verified.</summary>
    public sealed class RunResult
    {
        public string RulesetId { get; }
        public string CategoryKey { get; }
        public long TargetXp { get; }
        public long ElapsedMilliseconds { get; }
        public bool IsPersonalBestEligible { get; }
        public string EligibilityReason { get; }
        public long Attempts { get; }
        public long NormalHits { get; }
        public long CriticalHits { get; }
        public long Misses { get; }
        public long SkippedOpportunities { get; }
        public long FinalXp { get; }

        internal RunResult(RunSession run, long normalHits, long criticalHits, long misses, long skipped)
        {
            RulesetId = run.Definition.RulesetId;
            CategoryKey = run.Definition.CategoryKey;
            TargetXp = run.Definition.TargetXp;
            ElapsedMilliseconds = run.ElapsedMilliseconds;
            IsPersonalBestEligible = run.IsPersonalBestEligible;
            EligibilityReason = run.EligibilityReason;
            Attempts = run.Snapshot.TimingAttempts;
            NormalHits = normalHits;
            CriticalHits = criticalHits;
            Misses = misses;
            SkippedOpportunities = skipped;
            FinalXp = run.Snapshot.LifetimeXp;
        }
    }

    /// <summary>Stable string kind IDs; sequence preserves order when records share a timestamp.</summary>
    public sealed class RunEvent
    {
        public long Sequence { get; }
        public long ElapsedMilliseconds { get; }
        public long Tick { get; }
        public string Kind { get; }
        public string Detail { get; }
        public long Value { get; }
        internal RunEvent(long sequence, long elapsed, long tick, string kind, string detail, long value)
        { Sequence = sequence; ElapsedMilliseconds = elapsed; Tick = tick; Kind = kind; Detail = detail; Value = value; }
    }

    public sealed class RunTimelineSample
    {
        public long ElapsedMilliseconds { get; }
        public long Tick { get; }
        public long A { get; }
        public long B { get; }
        public long Credits { get; }
        public long LifetimeXp { get; }
        internal RunTimelineSample(long elapsed, RunSnapshot snapshot)
        { ElapsedMilliseconds = elapsed; Tick = snapshot.Ticks; A = snapshot.A; B = snapshot.B;
            Credits = snapshot.Credits; LifetimeXp = snapshot.LifetimeXp; }
    }
}
