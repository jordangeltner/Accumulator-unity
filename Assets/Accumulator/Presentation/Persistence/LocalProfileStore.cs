using System;
using System.IO;
using System.Security;
using System.Text;
using UnityEngine;

namespace Accumulator.Presentation.Persistence
{
    /// <summary>Versioned local preferences and PBs; no account or server authority.</summary>
    public sealed class LocalProfileStore
    {
        private const int SchemaVersion = 1;
        private const long MaximumBytes = 1024 * 1024;
        private readonly string filePath;
        public bool CanSave { get; private set; } = true;

        [Serializable]
        private sealed class Document
        {
            public int schemaVersion;
            public GameOptions options;
            public PersonalBest[] personalBests;
        }

        public LocalProfileStore(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("A profile path is required.", nameof(filePath));
            this.filePath = Path.GetFullPath(filePath);
        }

        public LocalProfile Load(out string status)
        {
            CanSave = true;
            var primary = Read(filePath, out string reason, out bool unsupported);
            if (unsupported)
            {
                CanSave = false;
                status = "Local profile uses an unsupported schema. Saving is disabled to preserve it.";
                return new LocalProfile();
            }
            if (primary != null)
            {
                status = reason;
                return primary;
            }
            var backup = Read(filePath + ".bak", out _, out bool backupUnsupported);
            if (backupUnsupported)
            {
                CanSave = false;
                status = "Local profile backup uses an unsupported schema. Saving is disabled to preserve it.";
                return new LocalProfile();
            }
            if (backup != null)
            {
                status = "Recovered local profile from backup. " + reason;
                return backup;
            }
            status = reason;
            return new LocalProfile();
        }

        public bool Save(LocalProfile profile, out string status)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (!CanSave)
            {
                status = "Saving is disabled because the local profile has an unsupported schema.";
                return false;
            }
            string temporary = filePath + ".tmp";
            try
            {
                // Also protect a newer profile if Save is called before Load.
                Read(filePath, out _, out bool unsupported);
                Read(filePath + ".bak", out _, out bool backupUnsupported);
                if (unsupported || backupUnsupported)
                {
                    CanSave = false;
                    status = "Saving is disabled because the local profile has an unsupported schema.";
                    return false;
                }
                var options = profile.Options.Copy();
                options.Validate();
                var records = profile.PersonalBests;
                var bests = new PersonalBest[records.Count];
                for (int i = 0; i < records.Count; i++) bests[i] = records[i];
                string json = JsonUtility.ToJson(new Document { schemaVersion = SchemaVersion, options = options, personalBests = bests }, true);
                byte[] bytes = new UTF8Encoding(false).GetBytes(json);
                if (bytes.LongLength > MaximumBytes) throw new InvalidDataException("Profile exceeds the size limit.");
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (File.Exists(filePath))
                {
                    // Never replace a good recovery backup with an invalid primary.
                    var current = Read(filePath, out _, out _);
                    File.Replace(temporary, filePath, current != null ? filePath + ".bak" : null);
                }
                else File.Move(temporary, filePath);
                status = "Local profile saved.";
                return true;
            }
            catch (Exception exception) when (IsRecoverable(exception))
            {
                status = "Could not save local profile: " + exception.Message;
                return false;
            }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (Exception exception) when (IsRecoverable(exception)) { }
            }
        }

        private static LocalProfile Read(string path, out string status, out bool unsupported)
        {
            unsupported = false;
            status = "No local profile yet; using defaults.";
            try
            {
                if (!File.Exists(path)) return null;
                if (new FileInfo(path).Length > MaximumBytes) throw new InvalidDataException("Profile exceeds the size limit.");
                var document = JsonUtility.FromJson<Document>(File.ReadAllText(path));
                if (document == null) throw new InvalidDataException("Empty profile.");
                if (document.schemaVersion != SchemaVersion)
                {
                    unsupported = document.schemaVersion > SchemaVersion;
                    throw new InvalidDataException("Unsupported profile schema.");
                }
                if (document.personalBests != null && document.personalBests.Length > LocalProfile.MaximumRecords)
                    throw new InvalidDataException("Too many personal bests.");
                var profile = new LocalProfile();
                var options = document.options ?? new GameOptions();
                options.Validate();
                profile.Options.masterVolume = options.masterVolume;
                profile.Options.fullScreen = options.fullScreen;
                profile.Options.width = options.width;
                profile.Options.height = options.height;
                profile.Options.qualityLevel = options.qualityLevel;
                profile.Options.showDetails = options.showDetails;
                bool discarded = false;
                if (document.personalBests != null)
                    foreach (var best in document.personalBests)
                    {
                        if (best == null || !best.IsValid) discarded = true;
                        else profile.Import(best);
                    }
                status = discarded ? "Local profile loaded; invalid personal bests were discarded." : "Local profile loaded.";
                return profile;
            }
            catch (Exception exception) when (IsRecoverable(exception))
            {
                status = "Could not load local profile; using defaults. " + exception.Message;
                return null;
            }
        }

        private static bool IsRecoverable(Exception exception) => exception is IOException
            || exception is InvalidDataException
            || exception is UnauthorizedAccessException || exception is SecurityException
            || exception is ArgumentException || exception is NotSupportedException;
    }
}
