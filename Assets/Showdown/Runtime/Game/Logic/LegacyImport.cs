#nullable enable
using System;
using System.Collections.Generic;

namespace Showdown.Game
{
    /// <summary>
    /// Where the previous (React Native) app's stored rows come from. On Android that is the AsyncStorage SQLite
    /// database; the platform reader implements this so the decision of what to copy stays testable.
    /// </summary>
    public interface ILegacyRowSource
    {
        /// <summary>
        /// Every <c>key, value</c> row of the old storage under the app's namespace, or null when the old storage
        /// does not exist (a fresh install). Throws if it exists but cannot be read.
        /// </summary>
        IReadOnlyList<KeyValuePair<string, string>>? ReadRows();
    }

    /// <summary>
    /// Decides which legacy rows to carry over. The rule is precedence, not merging: a legacy row is copied only
    /// when the new store has no value for that key yet, so progress made in this build is never overwritten.
    /// </summary>
    public static class LegacyImportPlanner
    {
        public static IReadOnlyList<KeyValuePair<string, string>> Plan(
            IEnumerable<KeyValuePair<string, string>> rows,
            IKeyValueStore target)
        {
            var plan = new List<KeyValuePair<string, string>>();
            var planned = new HashSet<string>();
            foreach (var row in rows)
            {
                if (!IsCandidate(row) || planned.Contains(row.Key) || target.Get(row.Key) != null) continue;
                planned.Add(row.Key);
                plan.Add(row);
            }

            return plan;
        }

        /// <summary>A row we know the name of, whose value is JSON we can read (anything else would only be read as defaults).</summary>
        private static bool IsCandidate(KeyValuePair<string, string> row) =>
            Array.IndexOf(StorageKeys.Legacy, row.Key) >= 0
            && row.Value != null
            && MiniJson.TryParse(row.Value, out var tree)
            && tree != null;
    }

    /// <summary>The one-time import of the old app's data into the new store.</summary>
    public sealed class LegacyImporter
    {
        private const string DoneMarker = "1";

        private readonly ILegacyRowSource source;
        private readonly Action<string> warn;

        public LegacyImporter(ILegacyRowSource source, Action<string> warn)
        {
            this.source = source;
            this.warn = warn;
        }

        /// <summary>
        /// Copies what the plan allows, then marks the import done. Returns how many keys were copied.
        ///
        /// A missing old database is a finished job (there will never be one), so it sets the marker. A failure to
        /// read an existing one does not: it is logged and retried next launch, which is safe because the planner
        /// never overwrites anything the player has since saved. Nothing here throws.
        /// </summary>
        public int RunOnce(IKeyValueStore target)
        {
            if (target.Get(StorageKeys.LegacyImportDone) != null) return 0;

            IReadOnlyList<KeyValuePair<string, string>>? rows;
            try
            {
                rows = source.ReadRows();
            }
            catch (Exception error)
            {
                warn("Showdown: legacy storage import failed, will retry next launch: " + error.Message);
                return 0;
            }

            var copied = rows == null ? 0 : Copy(rows, target);
            target.Set(StorageKeys.LegacyImportDone, DoneMarker);
            return copied;
        }

        private static int Copy(IReadOnlyList<KeyValuePair<string, string>> rows, IKeyValueStore target)
        {
            var plan = LegacyImportPlanner.Plan(rows, target);
            foreach (var row in plan)
            {
                target.Set(row.Key, row.Value);
            }

            return plan.Count;
        }
    }
}
