#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Showdown.Game
{
    /// <summary>
    /// Reads the old app's AsyncStorage rows straight out of its SQLite file over JNI:
    /// <c>Context.getDatabasePath("RKStorage")</c>, opened read-only, one <c>rawQuery</c>, every cursor and database
    /// handle closed. The decision of what to do with the rows lives in <see cref="LegacyImporter"/>.
    ///
    /// Needs on-device verification: it cannot run in the Editor or the dotnet harness.
    /// </summary>
    public sealed class AndroidLegacyRowSource : ILegacyRowSource
    {
        private const string DatabaseName = "RKStorage";
        private const string Query = "SELECT key, value FROM catalystLocalStorage WHERE key LIKE '" + StorageKeys.Namespace + ".%'";
        private const string DatabaseClass = "android/database/sqlite/SQLiteDatabase";
        private const string OpenDatabaseSignature =
            "(Ljava/lang/String;Landroid/database/sqlite/SQLiteDatabase$CursorFactory;I)Landroid/database/sqlite/SQLiteDatabase;";
        private const int OpenReadOnly = 1;
        private const int KeyColumn = 0;
        private const int ValueColumn = 1;

        public IReadOnlyList<KeyValuePair<string, string>> ReadRows()
        {
            var path = FindDatabasePath();
            if (path == null) return null;

            using (var database = OpenReadOnlyDatabase(path))
            {
                try
                {
                    return QueryRows(database);
                }
                finally
                {
                    database.Call("close");
                }
            }
        }

        private static string FindDatabasePath()
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var file = activity.Call<AndroidJavaObject>("getDatabasePath", DatabaseName))
            {
                return file.Call<bool>("exists") ? file.Call<string>("getAbsolutePath") : null;
            }
        }

        /// <summary>
        /// <c>SQLiteDatabase.openDatabase(path, factory, flags)</c> takes a null <c>CursorFactory</c>, and
        /// <c>AndroidJavaClass.CallStatic</c> cannot resolve an overload from a null argument, so this one call is
        /// made through raw JNI with the signature spelled out.
        /// </summary>
        private static AndroidJavaObject OpenReadOnlyDatabase(string path)
        {
            var databaseClass = AndroidJNI.FindClass(DatabaseClass);
            ThrowIfJavaExceptionPending("FindClass");
            var javaPath = AndroidJNI.NewStringUTF(path);
            var opened = IntPtr.Zero;
            try
            {
                var method = AndroidJNI.GetStaticMethodID(databaseClass, "openDatabase", OpenDatabaseSignature);
                ThrowIfJavaExceptionPending("GetStaticMethodID");
                var arguments = new jvalue[3];
                arguments[0].l = javaPath;
                arguments[1].l = IntPtr.Zero;
                arguments[2].i = OpenReadOnly;
                opened = AndroidJNI.CallStaticObjectMethod(databaseClass, method, arguments);
                ThrowIfJavaExceptionPending("openDatabase");
                if (opened == IntPtr.Zero) throw new InvalidOperationException("openDatabase returned null");
                return new AndroidJavaObject(opened);
            }
            finally
            {
                if (opened != IntPtr.Zero) AndroidJNI.DeleteLocalRef(opened);
                AndroidJNI.DeleteLocalRef(javaPath);
                AndroidJNI.DeleteLocalRef(databaseClass);
            }
        }

        /// <summary>
        /// Raw JNI calls do not clear a thrown Java exception the way Unity's wrappers do. Leaving one pending makes the
        /// next JNI call (haptics setup, a moment later) undefined — and under CheckJNI, in debug builds, fatal. So clear it
        /// here and report it as an ordinary failure the importer already knows how to swallow.
        /// </summary>
        private static void ThrowIfJavaExceptionPending(string step)
        {
            var pending = AndroidJNI.ExceptionOccurred();
            if (pending == IntPtr.Zero) return;
            AndroidJNI.ExceptionClear();
            AndroidJNI.DeleteLocalRef(pending);
            throw new InvalidOperationException(step + " raised a Java exception");
        }

        private static IReadOnlyList<KeyValuePair<string, string>> QueryRows(AndroidJavaObject database)
        {
            var rows = new List<KeyValuePair<string, string>>();
            // An empty String[] stands in for "no bind arguments": a null array has no resolvable type over JNI.
            using (var cursor = database.Call<AndroidJavaObject>("rawQuery", Query, new string[0]))
            {
                try
                {
                    while (cursor.Call<bool>("moveToNext"))
                    {
                        var key = cursor.Call<string>("getString", KeyColumn);
                        var value = cursor.Call<string>("getString", ValueColumn);
                        if (key != null && value != null) rows.Add(new KeyValuePair<string, string>(key, value));
                    }
                }
                finally
                {
                    cursor.Call("close");
                }
            }

            return rows;
        }
    }
}
#endif
