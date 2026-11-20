#if MATERIALMAGIC_STANDALONE
using System;
using System.Collections.Generic;

namespace UnityEngine
{
    /// <summary>Host-observable diagnostics only; no Editor Console or native logging backend.</summary>
    public static class Debug
    {
        public static Action<string> LogSink { get; set; } = message => Console.Error.WriteLine(message);
        public static void Log(object message) => LogSink?.Invoke(message?.ToString() ?? "null");
        public static void LogWarning(object message) => LogSink?.Invoke("[Warning] " + (message?.ToString() ?? "null"));
        public static void LogError(object message) => LogSink?.Invoke("[Error] " + (message?.ToString() ?? "null"));
    }

    /// <summary>Read-only startup preferences. No host/user preference file is ever opened.</summary>
    public static class PlayerPrefs
    {
        private static readonly Dictionary<string, string> strings = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, int> integers = new Dictionary<string, int>(StringComparer.Ordinal);

        public static void SetReadOnlySnapshot(IReadOnlyDictionary<string, string> stringValues = null,
            IReadOnlyDictionary<string, int> intValues = null)
        {
            strings.Clear();
            integers.Clear();
            if (stringValues != null)
                foreach (KeyValuePair<string, string> pair in stringValues) strings.Add(pair.Key, pair.Value);
            if (intValues != null)
                foreach (KeyValuePair<string, int> pair in intValues) integers.Add(pair.Key, pair.Value);
        }

        public static string GetString(string key, string defaultValue = "") => strings.TryGetValue(key, out string value) ? value : defaultValue;
        public static int GetInt(string key, int defaultValue = 0) => integers.TryGetValue(key, out int value) ? value : defaultValue;
        public static void SetString(string key, string value) => throw PersistenceUnsupported();
        public static void SetInt(string key, int value) => throw PersistenceUnsupported();
        public static void Save() => throw PersistenceUnsupported();
        private static NotSupportedException PersistenceUnsupported() => new NotSupportedException("PlayerPrefs persistence is unavailable in standalone rules. Supply a read-only host snapshot instead.");
    }

    public static class Application
    {
        public static string version => "standalone";
        public static string persistentDataPath => throw new NotSupportedException("Standalone rules have no Unity profile/save directory. Use an explicit host persistence service.");
    }
}
#endif
