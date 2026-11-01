using System;
using System.Collections.Generic;
using System.Linq;
using Accumulator.Core;
using NUnit.Framework;

namespace Accumulator.Tests.EditMode
{
    public sealed class RunSessionEdgeCaseTests
    {
        private static RunSession TimingRun(long targetXp)
        {
            var run = new RunSession(new RunDefinition("edges", targetXp, 0));
            run.AdvanceMilliseconds(500000);
            Assert.That(run.TrySetProductionMode(ProductionMode.Timing, out _), Is.True);
            return run;
        }

        private static void Advance(RunSession run, int milliseconds, bool subdivide)
        {
            if (!subdivide) { run.AdvanceMilliseconds(milliseconds); return; }
            while (milliseconds > 0)
            {
                int part = Math.Min(milliseconds, 7);
                run.AdvanceMilliseconds(part);
                milliseconds -= part;
            }
        }

        [Test]
        public void SubdivisionPreservesStrikesOnCriticalAndSweepBoundariesThroughFinish()
        {
            var whole = TimingRun(504 * Economy.Scale);
            var split = TimingRun(504 * Economy.Scale);
            foreach (var run in new[] { whole, split })
            {
                bool subdivide = ReferenceEquals(run, split);
                Advance(run, 705, subdivide); // Inclusive lower critical edge, position 4700.
                Assert.That(run.TryStrikeTiming(out bool hit, out _, out _), Is.True);
                Assert.That(hit, Is.True);
                Assert.That(run.Snapshot.LastTimingResult, Is.EqualTo(TimingResult.Critical));
                Advance(run, 795, subdivide); // New sweep, exact turnaround.
                Assert.That(run.TryStrikeTiming(out hit, out _, out _), Is.True);
                Assert.That(hit, Is.False);
                Advance(run, 750, subdivide);
                Assert.That(run.TryStrikeTiming(out _, out _, out _), Is.False);
                Advance(run, 1500, subdivide);
                Assert.That(run.TryStrikeTiming(out hit, out _, out _), Is.True);
                Assert.That(hit, Is.True);
                Advance(run, 10000, subdivide);
                Assert.That(run.Result.CriticalHits, Is.EqualTo(2));
                Assert.That(run.Result.Misses, Is.EqualTo(1));
                Assert.That(run.Result.SkippedOpportunities, Is.Zero);
            }
            Assert.That(split.Result.ElapsedMilliseconds, Is.EqualTo(whole.Result.ElapsedMilliseconds));
            Assert.That(split.Snapshot.A, Is.EqualTo(whole.Snapshot.A));
            Assert.That(split.Snapshot.B, Is.EqualTo(whole.Snapshot.B));
            Assert.That(split.Snapshot.Credits, Is.EqualTo(whole.Snapshot.Credits));
            Assert.That(split.Events.Select(value => (value.Kind, value.ElapsedMilliseconds, value.Value)),
                Is.EqualTo(whole.Events.Select(value => (value.Kind, value.ElapsedMilliseconds, value.Value))));
        }

        [Test]
        public void PausePreservesPartialTickAndPermanentlyInvalidatesCompletedResult()
        {
            var definition = new RunDefinition("edges", Economy.Scale, 0);
            var paused = new RunSession(definition);
            var baseline = new RunSession(definition);
            paused.AdvanceMilliseconds(73);
            paused.Pause("Focus lost");
            paused.AdvanceMilliseconds(100000);
            Assert.That(paused.ElapsedMilliseconds, Is.EqualTo(73));
            Assert.That(paused.Snapshot.Ticks, Is.Zero);
            paused.Resume();
            paused.AdvanceMilliseconds(26);
            Assert.That(paused.Snapshot.Ticks, Is.Zero);
            paused.AdvanceMilliseconds(1);
            Assert.That(paused.Snapshot.Ticks, Is.EqualTo(1));
            paused.AdvanceMilliseconds(900);
            baseline.AdvanceMilliseconds(1000);
            Assert.That(paused.Result.ElapsedMilliseconds, Is.EqualTo(baseline.Result.ElapsedMilliseconds));
            Assert.That(paused.Result.FinalXp, Is.EqualTo(baseline.Result.FinalXp));
            Assert.That(paused.Result.IsPersonalBestEligible, Is.False);
            Assert.That(paused.Result.EligibilityReason, Is.EqualTo("Focus lost"));
        }

        [Test]
        public void AcceleratedCatchupStopsAtFirstFinishTickWithoutConsumingRemainder()
        {
            var run = new RunSession(new RunDefinition("edges", 2500, 7), true);
            run.AdvanceMilliseconds(7);
            Assert.That(run.TrySetSpeed(20, out _), Is.True);
            run.AdvanceMilliseconds(RunSession.MaxAdvanceMilliseconds);
            Assert.That(run.Result.ElapsedMilliseconds, Is.EqualTo(300));
            Assert.That(run.Result.FinalXp, Is.EqualTo(3000));
            Assert.That(run.Snapshot.Ticks, Is.EqualTo(3));
            Assert.That(run.Timeline.Last().ElapsedMilliseconds, Is.EqualTo(300));
            Assert.That(run.Events.Count(value => value.Kind == "run.finished"), Is.EqualTo(1));
            Assert.That(run.Result.IsPersonalBestEligible, Is.False);
        }

        [Test]
        public void SkipsCountAtSweepBoundaryOnlyWhenWorkersAreAssigned()
        {
            var run = TimingRun(503 * Economy.Scale);
            run.AdvanceMilliseconds(1499);
            Assert.That(run.TrySetAllocation(Economy.AProducer, 0, out _), Is.True);
            Assert.That(run.TryStrikeTiming(out _, out _, out _), Is.False);
            run.AdvanceMilliseconds(1);
            Assert.That(run.TrySetAllocation(Economy.AProducer, 3, out _), Is.True);
            run.AdvanceMilliseconds(1500);
            Assert.That(run.Result.SkippedOpportunities, Is.EqualTo(1));
            Assert.That(run.Result.Attempts, Is.Zero);
            Assert.That(run.Result.Misses, Is.Zero);
            Assert.That(run.TryStrikeTiming(out _, out _, out _), Is.False);
        }

        [Test]
        public void RejectedCommandsDoNotWriteEventsOrMutateSnapshotAndCollectionsAreReadOnly()
        {
            var run = new RunSession(new RunDefinition("edges", Economy.Scale, 0));
            int events = run.Events.Count;
            Assert.That(run.TrySetProductionMode(ProductionMode.Timing, out _), Is.False);
            Assert.That(run.TrySetConversionMode((ConversionMode)999, out _), Is.False);
            Assert.That(run.TrySetAllocation(-1, 1, out _), Is.False);
            Assert.That(run.TryPurchase(0, out _), Is.False);
            Assert.That(run.TrySetSpeed(20, out _), Is.False);
            Assert.That(run.TryStrikeTiming(out bool hit, out long reward, out _), Is.False);
            Assert.That(hit, Is.False);
            Assert.That(reward, Is.Zero);
            Assert.That(run.Events.Count, Is.EqualTo(events));
            Assert.That(run.Snapshot.A, Is.Zero);
            Assert.That(run.Snapshot.Ticks, Is.Zero);
            Assert.Throws<NotSupportedException>(() => ((IList<RunEvent>)run.Events).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<RunTimelineSample>)run.Timeline).Clear());
            run.AdvanceMilliseconds(1000);
            foreach (var property in typeof(RunResult).GetProperties())
                Assert.That(property.GetSetMethod(), Is.Null, property.Name);
        }
    }
}
