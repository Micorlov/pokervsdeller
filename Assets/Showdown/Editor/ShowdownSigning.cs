using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Showdown.EditorTools
{
    /// <summary>
    /// Android signing. Development builds use Unity's debug key. A release bundle is signed with the app's
    /// upload key, found through <c>SHOWDOWN_KEYSTORE_PROPERTIES</c> — a Gradle-style properties file
    /// (<c>storeFile</c>, <c>storePassword</c>, <c>keyAlias</c>, <c>keyPassword</c>) kept outside the repo.
    /// Secrets are read at build time and never logged.
    /// </summary>
    public static class ShowdownSigning
    {
        public const string PropertiesVariable = "SHOWDOWN_KEYSTORE_PROPERTIES";

        public static void Apply(bool release = false)
        {
            PlayerSettings.Android.useCustomKeystore = false;
            if (!release) return;

            string propertiesPath = Environment.GetEnvironmentVariable(PropertiesVariable);
            if (string.IsNullOrEmpty(propertiesPath) || !File.Exists(propertiesPath))
            {
                throw new InvalidOperationException(
                    "A release build needs " + PropertiesVariable + " to point at the upload keystore's keystore.properties.");
            }

            var properties = ReadProperties(propertiesPath);
            string keystore = ResolveKeystore(propertiesPath, Require(properties, "storeFile"));

            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = keystore;
            PlayerSettings.Android.keystorePass = Require(properties, "storePassword");
            PlayerSettings.Android.keyaliasName = Require(properties, "keyAlias");
            PlayerSettings.Android.keyaliasPass = Require(properties, "keyPassword");
            Debug.Log("Showdown: signing the release with the upload keystore " + Path.GetFileName(keystore) + ".");
        }

        private static Dictionary<string, string> ReadProperties(string path)
        {
            var values = new Dictionary<string, string>();
            foreach (var raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
                int split = line.IndexOf('=');
                if (split <= 0) continue;
                values[line.Substring(0, split).Trim()] = line.Substring(split + 1).Trim();
            }
            return values;
        }

        private static string Require(Dictionary<string, string> values, string key)
        {
            if (!values.TryGetValue(key, out var value) || string.IsNullOrEmpty(value))
            {
                throw new InvalidOperationException("keystore.properties is missing '" + key + "'.");
            }
            return value;
        }

        /// <summary>
        /// The properties file was written for Gradle, where <c>storeFile</c> is relative to android/app. Accept an
        /// absolute path, a path relative to the properties file, or the jks sitting beside it.
        /// </summary>
        private static string ResolveKeystore(string propertiesPath, string storeFile)
        {
            string directory = Path.GetDirectoryName(propertiesPath) ?? ".";
            var candidates = new[]
            {
                storeFile,
                Path.Combine(directory, storeFile),
                Path.Combine(directory, Path.GetFileName(storeFile)),
            };
            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate)) return Path.GetFullPath(candidate);
            }
            throw new FileNotFoundException("The upload keystore named in keystore.properties was not found next to it.");
        }
    }
}
