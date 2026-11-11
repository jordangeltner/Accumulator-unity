using System;
using System.IO;
using Accumulator.Core;
using Accumulator.Presentation.Persistence;
using NUnit.Framework;

namespace Accumulator.Tests.PlayMode
{
    public sealed class LocalProfileTests
    {
        private string directory;
        private string path;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "AccumulatorProfileTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            path = Path.Combine(directory, "profile.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        private static RunResult Complete(RunDefinition definition = null, bool practice = false, int delay = 0)
        {
            var run = new RunSession(definition ?? new RunDefinition("test-v1", Economy.Scale, 0), practice);
            if (delay > 0)
            {
                Assert.That(run.TrySetAllocation(Economy.Developer, 0, out _), Is.True);
                run.AdvanceMilliseconds(delay);
                Assert.That(run.TrySetAllocation(Economy.Developer, 1, out _), Is.True);
            }
            run.AdvanceMilliseconds(10000);
            Assert.That(run.Result, Is.Not.Null);
            return run.Result;
        }

        [Test]
        public void RoundTripPreservesOptionsAndPersonalBestWithoutAliases()
        {
            var profile = new LocalProfile();
            profile.Options.masterVolume = 0.35f;
            profile.Options.fullScreen = true;
            profile.Options.width = 1920;
            profile.Options.height = 1080;
            profile.Options.showDetails = true;
            var result = Complete();
            Assert.That(profile.TryRecord(result), Is.True);
            var returned = profile.FindBest(result.CategoryKey);
            returned.elapsedMilliseconds = -100;
            profile.PersonalBests[0].categoryKey = "mutated";
            var store = new LocalProfileStore(path);
            Assert.That(store.Save(profile, out string saved), Is.True, saved);
            var loaded = store.Load(out string status);
            Assert.That(loaded.Options.masterVolume, Is.EqualTo(0.35f));
            Assert.That(loaded.Options.fullScreen, Is.True);
            Assert.That(loaded.Options.width, Is.EqualTo(1920));
            Assert.That(loaded.Options.height, Is.EqualTo(1080));
            Assert.That(loaded.Options.showDetails, Is.True);
            Assert.That(loaded.FindBest(result.CategoryKey).elapsedMilliseconds, Is.EqualTo(result.ElapsedMilliseconds), status);
        }

        [Test]
        public void OnlyStrictImprovementRecordsAndPracticeIsExcluded()
        {
            var profile = new LocalProfile();
            var slow = Complete(delay: 1000);
            var fast = Complete();
            Assert.That(profile.TryRecord(null), Is.False);
            Assert.That(profile.TryRecord(Complete(practice: true)), Is.False);
            Assert.That(profile.TryRecord(slow), Is.True);
            Assert.That(profile.TryRecord(fast), Is.True);
            Assert.That(profile.TryRecord(fast), Is.False);
            Assert.That(profile.TryRecord(slow), Is.False);
            Assert.That(profile.FindBest(fast.CategoryKey).elapsedMilliseconds, Is.EqualTo(fast.ElapsedMilliseconds));
        }

        [Test]
        public void FullCategoryIncludesRuleVersionTargetAndRuleFlags()
        {
            var profile = new LocalProfile();
            var definitions = new[]
            {
                new RunDefinition("test-v1", Economy.Scale, 0),
                new RunDefinition("test-v2", Economy.Scale, 0),
                new RunDefinition("test-v1", Economy.Scale * 2, 0),
                new RunDefinition("test-v1", Economy.Scale, 0, false)
            };
            foreach (var definition in definitions) Assert.That(profile.TryRecord(Complete(definition)), Is.True);
            Assert.That(profile.PersonalBests.Count, Is.EqualTo(4));
            Assert.That(profile.FindBest("unknown"), Is.Null);
        }

        [Test]
        public void MissingAndCorruptProfilesReturnSafeDefaults()
        {
            var store = new LocalProfileStore(path);
            Assert.That(store.Load(out string missing).PersonalBests, Is.Empty);
            Assert.That(missing, Does.Contain("No local profile"));
            File.WriteAllText(path, "{ broken json");
            Assert.That(store.Load(out string corrupt).PersonalBests, Is.Empty);
            Assert.That(corrupt, Does.Contain("Could not load"));
            Assert.That(store.CanSave, Is.True);
        }

        [Test]
        public void FutureSchemaIsPreservedEvenWhenSavePrecedesLoad()
        {
            const string future = "{\"schemaVersion\":999,\"future\":true}";
            File.WriteAllText(path, future);
            var store = new LocalProfileStore(path);
            Assert.That(store.Save(new LocalProfile(), out _), Is.False);
            Assert.That(store.Load(out string status).PersonalBests, Is.Empty);
            Assert.That(store.CanSave, Is.False);
            Assert.That(status, Does.Contain("unsupported schema"));
            Assert.That(store.Save(new LocalProfile(), out _), Is.False);
            Assert.That(File.ReadAllText(path), Is.EqualTo(future));
        }

        [Test]
        public void FutureBackupIsPreservedAndDisablesSaving()
        {
            const string future = "{\"schemaVersion\":999}";
            File.WriteAllText(path + ".bak", future);
            var store = new LocalProfileStore(path);
            Assert.That(store.Load(out string status).PersonalBests, Is.Empty);
            Assert.That(status, Does.Contain("unsupported schema"));
            Assert.That(store.CanSave, Is.False);
            Assert.That(new LocalProfileStore(path).Save(new LocalProfile(), out _), Is.False);
            Assert.That(File.ReadAllText(path + ".bak"), Is.EqualTo(future));
        }

        [Test]
        public void InvalidValuesAreClampedAndInvalidRecordsDiscarded()
        {
            File.WriteAllText(path, "{\"schemaVersion\":1,\"options\":{\"masterVolume\":5,\"width\":-5,\"height\":200000,\"qualityLevel\":999},"
                + "\"personalBests\":[null,{\"categoryKey\":\"bad\",\"rulesetId\":\"v1\",\"targetXp\":1,\"elapsedMilliseconds\":-1}]}" );
            var profile = new LocalProfileStore(path).Load(out string status);
            Assert.That(profile.Options.masterVolume, Is.EqualTo(1));
            Assert.That(profile.Options.width, Is.EqualTo(640));
            Assert.That(profile.Options.height, Is.EqualTo(16384));
            Assert.That(profile.Options.qualityLevel, Is.InRange(-1, UnityEngine.QualitySettings.names.Length - 1));
            Assert.That(profile.PersonalBests, Is.Empty);
            Assert.That(status, Does.Contain("discarded"));
        }

        [Test]
        public void OversizedProfileReturnsDefaults()
        {
            File.WriteAllText(path, new string(' ', 1024 * 1024 + 1));
            Assert.That(new LocalProfileStore(path).Load(out string status).PersonalBests, Is.Empty);
            Assert.That(status, Does.Contain("size limit"));
        }

        [Test]
        public void BackupRecoversCorruptOrMissingPrimaryAndSurvivesAnotherSave()
        {
            var store = new LocalProfileStore(path);
            var profile = new LocalProfile();
            profile.Options.masterVolume = 0.4f;
            Assert.That(store.Save(profile, out _), Is.True);
            profile.Options.masterVolume = 0.8f;
            Assert.That(store.Save(profile, out _), Is.True);
            File.WriteAllText(path, "broken");
            var recovered = store.Load(out string status);
            Assert.That(recovered.Options.masterVolume, Is.EqualTo(0.4f));
            Assert.That(status, Does.Contain("Recovered"));
            Assert.That(store.Save(recovered, out string saved), Is.True, saved);
            File.Delete(path);
            Assert.That(store.Load(out status).Options.masterVolume, Is.EqualTo(0.4f));
            Assert.That(status, Does.Contain("Recovered"));
        }

        [Test]
        public void WriteFailureReturnsStatusAndKeepsExistingProfile()
        {
            var store = new LocalProfileStore(path);
            var profile = new LocalProfile();
            Assert.That(store.Save(profile, out _), Is.True);
            string original = File.ReadAllText(path);
            Directory.CreateDirectory(path + ".tmp");
            Assert.That(store.Save(profile, out string status), Is.False);
            Assert.That(status, Does.Contain("Could not save"));
            Assert.That(File.ReadAllText(path), Is.EqualTo(original));
        }
    }
}
