using System;
using System.Collections.Generic;
using Accumulator.Core;
using UnityEngine;

namespace Accumulator.Presentation.Persistence
{
    [Serializable]
    public sealed class GameOptions
    {
        public float masterVolume = 1f;
        public bool fullScreen;
        public int width = 1280;
        public int height = 720;
        public int qualityLevel = -1;
        public bool showDetails;

        internal void Validate()
        {
            masterVolume = float.IsNaN(masterVolume) || float.IsInfinity(masterVolume)
                ? 1f : Mathf.Clamp01(masterVolume);
            width = Mathf.Clamp(width, 640, 16384);
            height = Mathf.Clamp(height, 360, 16384);
            qualityLevel = Mathf.Clamp(qualityLevel, -1, QualitySettings.names.Length - 1);
        }

        internal GameOptions Copy() => (GameOptions)MemberwiseClone();
    }

    /// <summary>Untrusted, local-only comparison data. Never proof of server progression.</summary>
    [Serializable]
    public sealed class PersonalBest
    {
        public string categoryKey;
        public string rulesetId;
        public long targetXp;
        public long elapsedMilliseconds;

        internal PersonalBest Copy() => (PersonalBest)MemberwiseClone();
        internal bool IsValid => ValidKey(categoryKey) && ValidKey(rulesetId)
            && targetXp > 0 && elapsedMilliseconds >= 0;
        internal static bool ValidKey(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 512;
    }

    public sealed class LocalProfile
    {
        internal const int MaximumRecords = 1000;
        private readonly List<PersonalBest> bests = new List<PersonalBest>();
        public GameOptions Options { get; private set; } = new GameOptions();

        internal LocalProfile Copy()
        {
            var copy = new LocalProfile { Options = Options.Copy() };
            foreach (var best in bests) copy.Import(best);
            return copy;
        }

        // Copies prevent a UI caller from mutating stored records through a returned DTO.
        public IReadOnlyList<PersonalBest> PersonalBests
        {
            get
            {
                var copies = new List<PersonalBest>(bests.Count);
                foreach (var best in bests) copies.Add(best.Copy());
                return copies.AsReadOnly();
            }
        }

        public PersonalBest FindBest(string categoryKey)
        {
            var best = bests.Find(value => string.Equals(value.categoryKey, categoryKey, StringComparison.Ordinal));
            return best?.Copy();
        }

        public bool TryRecord(RunResult result)
        {
            if (result == null || !result.IsPersonalBestEligible || result.FinalXp < result.TargetXp) return false;
            return Import(new PersonalBest
            {
                categoryKey = result.CategoryKey,
                rulesetId = result.RulesetId,
                targetXp = result.TargetXp,
                elapsedMilliseconds = result.ElapsedMilliseconds
            });
        }

        internal bool Import(PersonalBest candidate)
        {
            if (candidate == null || !candidate.IsValid) return false;
            int index = bests.FindIndex(value => string.Equals(value.categoryKey, candidate.categoryKey, StringComparison.Ordinal));
            if (index >= 0)
            {
                if (candidate.elapsedMilliseconds >= bests[index].elapsedMilliseconds) return false;
                bests[index] = candidate.Copy();
                return true;
            }
            if (bests.Count >= MaximumRecords) return false;
            bests.Add(candidate.Copy());
            return true;
        }
    }
}
