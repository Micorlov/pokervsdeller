using UnityEngine;

namespace Showdown.Game
{
    /// <summary>
    /// Carries the previous app's saved bankroll, stats and settings into this one, once. The React Native build
    /// kept them in AsyncStorage, which on Android is the SQLite database <c>RKStorage</c>; this build ships under
    /// the same package and signing key, so that file is still in the app's data directory after the update.
    /// </summary>
    public static class LegacyStorageImport
    {
        /// <summary>Android only; elsewhere there is no old storage and this does nothing. Never throws.</summary>
        public static void RunOnce(IKeyValueStore target)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            new LegacyImporter(new AndroidLegacyRowSource(), Debug.LogWarning).RunOnce(target);
#endif
        }
    }
}
