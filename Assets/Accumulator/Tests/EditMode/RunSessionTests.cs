using System;
using System.Linq;
using Accumulator.Core;
using NUnit.Framework;

namespace Accumulator.Tests.EditMode
{
    public sealed class RunSessionTests
    {
        private static RunSession Create(long target = 100000000, int countdown = 0, bool practice = false)
            => new RunSession(new RunDefinition("test", target, countdown), practice);

        [Test]
        public void DefinitionValidatesAndCategoriesIncludeAllStartingRules()
        {
            Assert.Throws<ArgumentException>(() => new RunDefinition("", 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RunDefinition("test", 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RunDefinition("test", 1, -1));
            Assert.That(RunDefinition.Default.TargetXp, Is.EqualTo(2000 * Economy.Scale));
            Assert.That(RunDefinition.Default.CountdownMilliseconds, Is.Zero);
            var baseline = new RunDefinition("test", 1).CategoryKey;
            Assert.That(new RunDefinition("test", 2).CategoryKey, Is.Not.EqualTo(baseline));
            Assert.That(new RunDefinition("test", 1, 3000).CategoryKey, Is.Not.EqualTo(baseline));
            Assert.That(new RunDefinition("test", 1, 0, false).CategoryKey, Is.Not.EqualTo(baseline));
            Assert.That(new RunDefinition("test", 1, 0, true, false).CategoryKey, Is.Not.EqualTo(baseline));
            Assert.That(new RunDefinition("test", 1).CategoryKey, Is.EqualTo(baseline));
        }

        [Test]
        public void CountdownProducesNothingAndConsumesOnlyItsPortionOfAFrame()
        {
            var run = Create(countdown: 3000);
            Assert.That(run.State, Is.EqualTo(RunState.Countdown));
            Assert.That(run.TrySetAllocation(0, 0, out _), Is.False);
            run.AdvanceMilliseconds(2999);
            Assert.That(run.CountdownRemainingMilliseconds, Is.EqualTo(1));
            Assert.That(run.ElapsedMilliseconds, Is.Zero);
            Assert.That(run.Snapshot.Ticks, Is.Zero);
            run.AdvanceMilliseconds(101);
            Assert.That(run.State, Is.EqualTo(RunState.Running));
            Assert.That(run.ElapsedMilliseconds, Is.EqualTo(100));
            Assert.That(run.Snapshot.Ticks, Is.EqualTo(1));
        }

        [Test]
        public void CountdownBoundaryStartsWithoutAnExtraTick()
        {
            var run = Create(countdown: 3000);
            run.AdvanceMilliseconds(3000);
            Assert.That(run.State, Is.EqualTo(RunState.Running));
            Assert.That(run.ElapsedMilliseconds, Is.Zero);
            Assert.That(run.Snapshot.Ticks, Is.Zero);
        }

        [Test]
        public void HugeFrameAndSubdivisionFinishAtSameFirstQualifyingTick()
        {
            var whole = Create(2500, 3000);
            var split = Create(2500, 3000);
            whole.AdvanceMilliseconds(1000000);
            for (int i = 0; i < 10000; i++) split.AdvanceMilliseconds(17);
            Assert.That(whole.Result.ElapsedMilliseconds, Is.EqualTo(300));
            Assert.That(whole.Result.FinalXp, Is.EqualTo(3000));
            Assert.That(split.Result.ElapsedMilliseconds, Is.EqualTo(whole.Result.ElapsedMilliseconds));
            Assert.That(split.Snapshot.A, Is.EqualTo(whole.Snapshot.A));
            Assert.That(split.Snapshot.B, Is.EqualTo(whole.Snapshot.B));
            Assert.That(split.Events.Select(e => e.Kind), Is.EqualTo(whole.Events.Select(e => e.Kind)));
        }

        [Test]
        public void ResultFreezesOnceAndFreshRetryHasNoPreviousProgress()
        {
            var run = Create(1);
            run.AdvanceMilliseconds(5000);
            var result = run.Result;
            int events = run.Events.Count;
            run.AdvanceMilliseconds(5000);
            run.Pause(); run.Resume(); run.Abandon();
            Assert.That(run.Result, Is.SameAs(result));
            Assert.That(run.Events.Count, Is.EqualTo(events));
            Assert.That(run.Events.Count(e => e.Kind == "run.finished"), Is.EqualTo(1));
            Assert.That(run.TryPurchase(0, out _), Is.False);
            Assert.That(run.TrySetAllocation(0, 0, out _), Is.False);
            Assert.That(run.TrySetSpeed(1, out _), Is.False);
            Assert.That(run.State, Is.EqualTo(RunState.Finished));
            var retry = new RunSession(run.Definition);
            Assert.That(retry.Snapshot.LifetimeXp, Is.Zero);
            Assert.That(retry.ElapsedMilliseconds, Is.Zero);
            Assert.That(retry.Result, Is.Null);
            Assert.That(retry.IsPersonalBestEligible, Is.True);
        }

        [Test]
        public void PauseFreezesCountdownRejectsCommandsAndPermanentlyRemovesEligibility()
        {
            var run = Create(countdown: 3000);
            run.AdvanceMilliseconds(100);
            run.Pause("Focus lost");
            run.AdvanceMilliseconds(5000);
            Assert.That(run.CountdownRemainingMilliseconds, Is.EqualTo(2900));
            Assert.That(run.TrySetAllocation(0, 0, out _), Is.False);
            Assert.That(run.TryPurchase(0, out _), Is.False);
            Assert.That(run.TryExpandCapacity(out _), Is.False);
            Assert.That(run.TrySetConversionMode(ConversionMode.Standard, out _), Is.False);
            Assert.That(run.TrySetProductionMode(ProductionMode.Passive, out _), Is.False);
            Assert.That(run.TryStrikeTiming(out _, out _, out _), Is.False);
            Assert.That(run.TrySetSpeed(1, out _), Is.False);
            run.Resume();
            Assert.That(run.State, Is.EqualTo(RunState.Countdown));
            run.AdvanceMilliseconds(3000);
            run.Pause("Second pause"); run.Resume();
            Assert.That(run.State, Is.EqualTo(RunState.Running));
            Assert.That(run.IsPersonalBestEligible, Is.False);
            Assert.That(run.EligibilityReason, Is.EqualTo("Focus lost"));
        }

        [Test]
        public void AbandonFreezesNoncompletedRun()
        {
            var run = Create();
            run.AdvanceMilliseconds(300);
            run.Abandon(); run.Resume(); run.AdvanceMilliseconds(5000);
            Assert.That(run.State, Is.EqualTo(RunState.Abandoned));
            Assert.That(run.ElapsedMilliseconds, Is.EqualTo(300));
            Assert.That(run.Result, Is.Null);
            Assert.That(run.IsPersonalBestEligible, Is.False);
        }

        [Test]
        public void PracticeAllowsKnownSpeedsWhileStandardRejectsAcceleration()
        {
            var standard = Create();
            Assert.That(standard.TrySetSpeed(5, out _), Is.False);
            Assert.That(standard.TrySetSpeed(1, out _), Is.True);
            var practice = Create(practice: true);
            Assert.That(practice.IsPersonalBestEligible, Is.False);
            Assert.That(practice.TrySetSpeed(2, out _), Is.False);
            Assert.That(practice.TrySetSpeed(5, out _), Is.True);
            practice.AdvanceMilliseconds(100);
            Assert.That(practice.Snapshot.Ticks, Is.EqualTo(5));
            Assert.That(practice.TrySetSpeed(20, out _), Is.True);
            practice.AdvanceMilliseconds(100);
            Assert.That(practice.Snapshot.Ticks, Is.EqualTo(25));
        }

        [Test]
        public void TimingChronologyMatchesSubdivisionAndForcesOneTimesSpeed()
        {
            var whole = Create(practice: true);
            var split = Create(practice: true);
            foreach (var run in new[] { whole, split })
            {
                run.AdvanceMilliseconds(500000);
                Assert.That(run.Snapshot.TimingProductionUnlocked, Is.True);
                Assert.That(run.TrySetSpeed(20, out _), Is.True);
                Assert.That(run.TrySetProductionMode(ProductionMode.Timing, out _), Is.True);
                Assert.That(run.Speed, Is.EqualTo(1));
                Assert.That(run.TrySetSpeed(5, out _), Is.False);
            }
            whole.AdvanceMilliseconds(2250);
            for (int i = 0; i < 225; i++) split.AdvanceMilliseconds(10);
            Assert.That(whole.Snapshot.TimingElapsedMilliseconds, Is.EqualTo(2250));
            Assert.That(whole.Snapshot.TimingPosition, Is.EqualTo(split.Snapshot.TimingPosition));
            foreach (var run in new[] { whole, split })
            {
                Assert.That(run.TryStrikeTiming(out bool hit, out long reward, out _), Is.True);
                Assert.That(hit, Is.True);
                Assert.That(reward, Is.EqualTo(18 * Economy.Scale));
                Assert.That(run.Snapshot.LastTimingResult, Is.EqualTo(TimingResult.Critical));
                Assert.That(run.TryStrikeTiming(out _, out _, out _), Is.False);
            }
            Assert.That(whole.Snapshot.A, Is.EqualTo(split.Snapshot.A));
            Assert.That(whole.Snapshot.Credits, Is.EqualTo(split.Snapshot.Credits));
        }

        [Test]
        public void AcceptedStrikesSeparateGradesAndSkippedSweepsFromRejectedAttempts()
        {
            // Large existing B reserve sustains development while testing marker inputs.
            var run = Create(503 * Economy.Scale);
            run.AdvanceMilliseconds(500000);
            Assert.That(run.TrySetProductionMode(ProductionMode.Timing, out _), Is.True);
            Assert.That(run.TryStrikeTiming(out bool hit, out _, out _), Is.True);
            Assert.That(hit, Is.False);
            Assert.That(run.TryStrikeTiming(out _, out _, out _), Is.False);
            run.AdvanceMilliseconds(2150);
            Assert.That(run.TryStrikeTiming(out hit, out _, out _), Is.True);
            Assert.That(hit, Is.True);
            Assert.That(run.Snapshot.LastTimingResult, Is.EqualTo(TimingResult.Success));
            run.AdvanceMilliseconds(850);
            Assert.That(run.Result.Attempts, Is.EqualTo(2));
            Assert.That(run.Result.NormalHits, Is.EqualTo(1));
            Assert.That(run.Result.CriticalHits, Is.Zero);
            Assert.That(run.Result.Misses, Is.EqualTo(1));
            Assert.That(run.Result.SkippedOpportunities, Is.Zero);

            var skipped = Create(503 * Economy.Scale);
            skipped.AdvanceMilliseconds(500000);
            skipped.TrySetProductionMode(ProductionMode.Timing, out _);
            skipped.AdvanceMilliseconds(3000);
            Assert.That(skipped.Result.SkippedOpportunities, Is.EqualTo(2));
            Assert.That(skipped.Result.Misses, Is.Zero);
        }

        [Test]
        public void EventsCaptureAcceptedCommandsAndUnlocksInStableOrder()
        {
            var run = Create();
            Assert.That(run.TrySetAllocation(0, 2, out _), Is.True);
            Assert.That(run.TrySetAllocation(0, 999, out _), Is.False);
            run.AdvanceMilliseconds(100000);
            Assert.That(run.TryPurchase(0, out _), Is.True);
            Assert.That(run.TrySetConversionMode(ConversionMode.Fast, out _), Is.True);
            Assert.That(run.Events.Count(e => e.Kind == "allocation.set"), Is.EqualTo(1));
            Assert.That(run.Events.Any(e => e.Kind == "upgrade.purchased"), Is.True);
            Assert.That(run.Events.Any(e => e.Kind == "unlock.fast_conversion"), Is.True);
            for (int i = 1; i < run.Events.Count; i++)
            {
                Assert.That(run.Events[i].Sequence, Is.GreaterThan(run.Events[i - 1].Sequence));
                Assert.That(run.Events[i].ElapsedMilliseconds, Is.GreaterThanOrEqualTo(run.Events[i - 1].ElapsedMilliseconds));
            }
        }

        [Test]
        public void RecordingsAreBoundedAndTruncationDoesNotLoseResult()
        {
            var run = Create(5000 * Economy.Scale);
            for (int i = 0; i < RunSession.MaxEvents + 20; i++) run.TrySetAllocation(0, 3, out _);
            Assert.That(run.Events.Count, Is.EqualTo(RunSession.MaxEvents));
            Assert.That(run.RecordingTruncated, Is.True);
            run.AdvanceMilliseconds(3000000);
            run.AdvanceMilliseconds(3000000);
            Assert.That(run.Timeline.Count, Is.LessThanOrEqualTo(RunSession.MaxTimelineSamples));
            Assert.That(run.Result, Is.Not.Null);
            Assert.That(run.Result.FinalXp, Is.GreaterThanOrEqualTo(run.Definition.TargetXp));
            Assert.That(run.Events[run.Events.Count - 1].Kind, Is.EqualTo("run.finished"));
            Assert.That(run.Timeline[run.Timeline.Count - 1].LifetimeXp, Is.EqualTo(run.Result.FinalXp));
        }

        [Test]
        public void InvalidAdvanceRejectsBeforeMutation()
        {
            var run = Create(countdown: 3000);
            Assert.Throws<ArgumentOutOfRangeException>(() => run.AdvanceMilliseconds(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => run.AdvanceMilliseconds(int.MaxValue));
            Assert.That(run.CountdownRemainingMilliseconds, Is.EqualTo(3000));
            Assert.That(run.Snapshot.Ticks, Is.Zero);
        }
    }
}
