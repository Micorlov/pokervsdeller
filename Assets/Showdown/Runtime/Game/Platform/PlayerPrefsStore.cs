using System;
using UnityEngine;

namespace Showdown.Game
{
    /// <summary>
    /// <see cref="IKeyValueStore"/> over <c>PlayerPrefs</c> (Android SharedPreferences). Each write is flushed
    /// immediately: a phone can kill the app without any shutdown callback, and these values are tiny.
    /// A failure is logged as a warning and swallowed — the game keeps running on what it has in memory.
    /// </summary>
    public sealed class PlayerPrefsStore : IKeyValueStore
    {
        public string Get(string key)
        {
            try
            {
                return PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key, null) : null;
            }
            catch (Exception error)
            {
                Debug.LogWarning("Showdown: could not read '" + key + "': " + error.Message);
                return null;
            }
        }

        public void Set(string key, string value)
        {
            try
            {
                PlayerPrefs.SetString(key, value);
                PlayerPrefs.Save();
            }
            catch (Exception error)
            {
                Debug.LogWarning("Showdown: could not save '" + key + "': " + error.Message);
            }
        }

        public void Remove(string key)
        {
            try
            {
                PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
            catch (Exception error)
            {
                Debug.LogWarning("Showdown: could not remove '" + key + "': " + error.Message);
            }
        }
    }
}
