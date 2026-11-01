using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;

namespace Accumulator.Core
{
    /// <summary>Owns all local run mutation. No wall clock, Unity dependency, or offline progression.</summary>
    public sealed class RunSession
    {
        public const int MaxAdvanceMilliseconds = 3600000;
        public const int MaxEvents = 8192;
        public const int MaxTimelineSamples = 4096;
        private const int TickMilliseconds = 1000 / Economy.TicksPerSecond;
        private readonly Economy _economy;
        private readonly bool _practice;
        private readonly List<RunEvent> _events = new List<RunEvent>();
        private readonly List<RunTimelineSample> _timeline = new List<RunTimelineSample>();
        private RunState _resumeState;
        private long _sequence, _normalHits, _criticalHits, _misses, _skipped;
        public RunDefinition Definition { get; }
        public RunState State { get; private set; }
        public RunSnapshot Snapshot { get; }
        public int CountdownRemainingMilliseconds { get; private set; }
        public long ElapsedMilliseconds { get; private set; }
        public int Speed { get; private set; } = 1;
        public bool IsPractice => _practice;
        public bool AllowsAcceleration => _practice;
        public bool IsPersonalBestEligible { get; private set; }
        public string EligibilityReason { get; private set; }
        public RunResult Result { get; private set; }
        public ReadOnlyCollection<RunEvent> Events { get; }
        public ReadOnlyCollection<RunTimelineSample> Timeline { get; }
        public bool RecordingTruncated { get; private set; }

        public RunSession(RunDefinition definition, bool practice = false)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            _practice = practice;
            _economy = new Economy(definition.AlternateConversionEnabled, definition.TimingProductionEnabled);
            Snapshot = new RunSnapshot(_economy);
            Events = _events.AsReadOnly();
            Timeline = _timeline.AsReadOnly();
            IsPersonalBestEligible = !practice;
            EligibilityReason = practice ? "Practice run." : null;
            CountdownRemainingMilliseconds = definition.CountdownMilliseconds;
            State = CountdownRemainingMilliseconds == 0 ? RunState.Running : RunState.Countdown;
            Record("run.created");
            Sample();
            if (State == RunState.Running) Record("run.started");
        }

        /// <summary>
        /// Explicitly rejects oversized calls before mutation rather than silently dropping time.
        /// Marker advances precede each economy tick, so frame subdivision cannot alter chronology.
        /// Elapsed time measures simulated active milliseconds (practice speed scales it).
        /// </summary>
        public void AdvanceMilliseconds(int milliseconds)
        {
            if (milliseconds < 0 || milliseconds > MaxAdvanceMilliseconds)
                throw new ArgumentOutOfRangeException(nameof(milliseconds));
            if (State == RunState.Countdown)
            {
                int consumed = Math.Min(milliseconds, CountdownRemainingMilliseconds);
                CountdownRemainingMilliseconds -= consumed;
                milliseconds -= consumed;
                if (CountdownRemainingMilliseconds == 0)
                { State = RunState.Running; Record("run.started"); }
            }
            if (State != RunState.Running) return;
            long remaining = (long)milliseconds * Speed;
            while (remaining > 0 && State == RunState.Running)
            {
                int step = (int)Math.Min(remaining, TickMilliseconds - ElapsedMilliseconds % TickMilliseconds);
                long nextElapsed = checked(ElapsedMilliseconds + step);
                bool timing = _economy.SelectedProductionMode == ProductionMode.Timing;
                if (timing)
                {
                    int untilSweep = Economy.TimingSweepMilliseconds
                        - (int)(_economy.TimingElapsedMilliseconds % Economy.TimingSweepMilliseconds);
                    step = Math.Min(step, untilSweep);
                    nextElapsed = checked(ElapsedMilliseconds + step);
                    // A skipped opportunity is an unattempted completed sweep while production
                    // has workers. Rejected strikes never count as attempts or misses.
                    if (step == untilSweep && _economy.TimingAttemptAvailable && _economy.Allocation(Economy.AProducer) > 0)
                        _skipped = checked(_skipped + 1);
                    _economy.AdvanceTimingMilliseconds(step);
                }
                ElapsedMilliseconds = nextElapsed;
                remaining -= step;
                if (ElapsedMilliseconds % TickMilliseconds != 0) continue;
                bool fast = _economy.FastConversionUnlocked;
                bool challenge = _economy.TimingProductionUnlocked;
                _economy.AdvanceTicks(1);
                if (!fast && _economy.FastConversionUnlocked) Record("unlock.fast_conversion");
                if (!challenge && _economy.TimingProductionUnlocked) Record("unlock.timing_production");
                if (_economy.LifetimeXp >= Definition.TargetXp)
                {
                    State = RunState.Finished;
                    Result = new RunResult(this, _normalHits, _criticalHits, _misses, _skipped);
                    Record("run.finished");
                    Sample();
                }
                else if (ElapsedMilliseconds % 1000 == 0) Sample();
            }
        }

        public bool TrySetAllocation(int index, int amount, out string reason)
        {
            if (!CanCommand(out reason) || !_economy.TrySetAllocation(index, amount, out reason)) return false;
            Record("allocation.set", index.ToString(CultureInfo.InvariantCulture), amount); return true;
        }
        public bool TryPurchase(int index, out string reason)
        {
            if (!CanCommand(out reason) || !_economy.TryPurchase(index, out reason)) return false;
            Record("upgrade.purchased", index.ToString(CultureInfo.InvariantCulture), _economy.Level(index)); return true;
        }
        public bool TryExpandCapacity(out string reason)
        {
            if (!CanCommand(out reason) || !_economy.TryExpandCapacity(out reason)) return false;
            Record("capacity.expanded", null, _economy.Capacity); return true;
        }
        public bool TrySetConversionMode(ConversionMode mode, out string reason)
        {
            if (!CanCommand(out reason) || !_economy.TrySetConversionMode(mode, out reason)) return false;
            Record("mode.conversion", mode.ToString()); return true;
        }
        public bool TrySetProductionMode(ProductionMode mode, out string reason)
        {
            if (!CanCommand(out reason) || !_economy.TrySetProductionMode(mode, out reason)) return false;
            if (mode == ProductionMode.Timing && Speed != 1)
            { Speed = 1; Record("speed.set", null, 1); }
            Record("mode.production", mode.ToString()); return true;
        }
        public bool TryStrikeTiming(out bool hit, out long reward, out string reason)
        {
            hit = false; reward = 0;
            if (!CanCommand(out reason) || !_economy.TryStrikeTiming(out hit, out reward, out reason)) return false;
            if (_economy.LastTimingResult == TimingResult.Critical) _criticalHits++;
            else if (hit) _normalHits++;
            else _misses++;
            Record("timing.strike", _economy.LastTimingResult.ToString(), reward); return true;
        }
        public bool TrySetSpeed(int speed, out string reason)
        {
            if (!CanCommand(out reason)) return false;
            if (speed != 1 && speed != 5 && speed != 20)
            { reason = "Speed must be 1, 5, or 20."; return false; }
            if (speed != 1 && (!_practice || _economy.SelectedProductionMode == ProductionMode.Timing))
            { reason = "Acceleration requires practice and passive production."; return false; }
            Speed = speed; reason = null; Record("speed.set", null, speed); return true;
        }

        public void Pause(string reason = "Paused")
        {
            if (State != RunState.Countdown && State != RunState.Running) return;
            _resumeState = State;
            State = RunState.Paused;
            if (IsPersonalBestEligible)
            { IsPersonalBestEligible = false; EligibilityReason = string.IsNullOrWhiteSpace(reason) ? "Paused" : Bounded(reason); }
            Record("run.paused", Bounded(reason));
        }
        public void Resume()
        { if (State == RunState.Paused) { State = _resumeState; Record("run.resumed"); } }
        public void Abandon()
        {
            if (State == RunState.Finished || State == RunState.Abandoned) return;
            State = RunState.Abandoned;
            IsPersonalBestEligible = false;
            if (EligibilityReason == null) EligibilityReason = "Abandoned";
            Record("run.abandoned");
        }
        private bool CanCommand(out string reason)
        { reason = State == RunState.Running ? null : "Commands require a running session."; return reason == null; }
        private static string Bounded(string value) => value != null && value.Length > 256 ? value.Substring(0, 256) : value;
        private void Record(string kind, string detail = null, long value = 0)
        {
            if (_events.Count == MaxEvents)
            {
                RecordingTruncated = true;
                // Preserve the final lifecycle transition even after command recording fills.
                if (kind == "run.finished" || kind == "run.abandoned")
                    _events[_events.Count - 1] = new RunEvent(++_sequence, ElapsedMilliseconds, _economy.Ticks, kind, detail, value);
                return;
            }
            _events.Add(new RunEvent(++_sequence, ElapsedMilliseconds, _economy.Ticks, kind, detail, value));
        }
        private void Sample()
        {
            if (_timeline.Count == MaxTimelineSamples)
            {
                RecordingTruncated = true;
                if (State == RunState.Finished)
                    _timeline[_timeline.Count - 1] = new RunTimelineSample(ElapsedMilliseconds, Snapshot);
                return;
            }
            _timeline.Add(new RunTimelineSample(ElapsedMilliseconds, Snapshot));
        }
    }
}
