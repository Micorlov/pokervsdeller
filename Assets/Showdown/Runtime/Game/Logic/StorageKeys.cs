namespace Showdown.Game
{
    /// <summary>
    /// The storage keys, kept byte-identical to the TypeScript app's (<c>ukc.poker-vs-dealer.&lt;key&gt;</c>) so
    /// data it wrote can be carried over untouched.
    /// </summary>
    public static class StorageKeys
    {
        public const string Namespace = "ukc.poker-vs-dealer";

        public const string Bankroll = Namespace + ".bankroll";
        public const string Stats = Namespace + ".stats";
        public const string Settings = Namespace + ".settings";
        public const string DailyBonus = Namespace + ".dailyBonus";

        /// <summary>Set once the one-time import of the old app's storage has run, so it never runs twice.</summary>
        public const string LegacyImportDone = "showdown.legacyImportDone";

        /// <summary>The keys the old app wrote, and so the only ones the legacy import may carry over.</summary>
        public static readonly string[] Legacy = { Bankroll, Stats, Settings, DailyBonus };
    }
}
