#if MATERIALMAGIC_STANDALONE
using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityEngine
{
    /// <summary>
    /// Explicit standalone resource registry. It never searches a checkout, Resources folder, or disk.
    /// Register content before first use; production caches retain assets until explicitly cleared.
    /// </summary>
    public static class Resources
    {
        private static readonly Dictionary<string, Object> registered = new Dictionary<string, Object>(StringComparer.Ordinal);
        private static Func<Type, string, Object> loadResolver;
        private static Func<Type, string, Object[]> loadAllResolver;

        /// <summary>Install optional host-owned resolvers. Null resolvers mean registered assets only.</summary>
        public static void Configure(Func<Type, string, Object> load = null, Func<Type, string, Object[]> loadAll = null)
        {
            loadResolver = load;
            loadAllResolver = loadAll;
        }

        public static void Register<T>(string path, T value) where T : Object
        {
            string key = Normalize(path, false);
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (registered.ContainsKey(key))
                throw new InvalidOperationException("A resource is already registered at '" + key + "'. Reset the registry before replacing content.");
            registered.Add(key, value);
        }

        public static T Load<T>(string path) where T : Object
        {
            string key = Normalize(path, false);
            if (registered.TryGetValue(key, out Object value)) return value as T;
            Object resolved = loadResolver?.Invoke(typeof(T), key);
            if (resolved != null && !(resolved is T))
                throw new InvalidOperationException("Resource resolver returned " + resolved.GetType().Name + " for " + typeof(T).Name + " at '" + key + "'.");
            return resolved as T;
        }

        public static T[] LoadAll<T>(string path) where T : Object
        {
            string key = Normalize(path, true);
            string prefix = key.Length == 0 ? string.Empty : key + "/";
            var result = new List<T>();
            // Stable ordinal order makes registration order irrelevant to resource enumeration.
            foreach (KeyValuePair<string, Object> entry in registered.OrderBy(entry => entry.Key, StringComparer.Ordinal))
            {
                if ((entry.Key == key || entry.Key.StartsWith(prefix, StringComparison.Ordinal)) && entry.Value is T value)
                    result.Add(value);
            }
            Object[] resolved = loadAllResolver?.Invoke(typeof(T), key);
            if (resolved != null)
            {
                foreach (Object value in resolved)
                {
                    if (value == null) continue;
                    if (!(value is T typed))
                        throw new InvalidOperationException("Resource collection resolver returned an incompatible asset for '" + key + "'.");
                    if (!result.Any(existing => ReferenceEquals(existing, typed))) result.Add(typed);
                }
            }
            return result.ToArray();
        }

        /// <summary>Clears the host registry and resolvers, not production static caches.</summary>
        public static void Reset()
        {
            registered.Clear();
            loadResolver = null;
            loadAllResolver = null;
        }

        private static string Normalize(string path, bool allowEmpty)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            string key = path.Replace('\\', '/').TrimEnd('/');
            if ((!allowEmpty && key.Length == 0) || key.StartsWith("/", StringComparison.Ordinal)
                || key.IndexOf(':') >= 0 || key.Split('/').Any(segment => segment == "." || segment == ".."))
                throw new ArgumentException("Use a Resources-relative key without a root or traversal segments.", nameof(path));
            return key;
        }
    }
}
#endif
