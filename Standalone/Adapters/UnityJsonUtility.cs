#if MATERIALMAGIC_STANDALONE
using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace UnityEngine
{
    /// <summary>
    /// Field-only JSON for the linked content DTOs. It does not serialize computed properties,
    /// resolve Unity asset references, or implement Unity's native serializer/lifecycle.
    /// </summary>
    public static class JsonUtility
    {
        public static T FromJson<T>(string json)
        {
            object result = FromJson(json, typeof(T));
            return result == null ? default : (T)result;
        }

        public static object FromJson(string json, Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (string.IsNullOrEmpty(json)) return null;
            if (typeof(Object).IsAssignableFrom(type))
                throw new NotSupportedException("Use ScriptableObject.CreateInstance and JsonUtility.FromJsonOverwrite for a standalone config value.");
            RequireObjectJson(json);
            return JsonConvert.DeserializeObject(json, type, Settings());
        }

        public static void FromJsonOverwrite(string json, object objectToOverwrite)
        {
            if (objectToOverwrite == null) throw new ArgumentNullException(nameof(objectToOverwrite));
            if (string.IsNullOrEmpty(json)) return;
            RequireObjectJson(json);
            // Populate preserves omitted fields while replacing explicitly supplied arrays/lists.
            JsonConvert.PopulateObject(json, objectToOverwrite, Settings());
        }

        public static string ToJson(object obj, bool prettyPrint = false)
        {
            if (obj == null) return string.Empty;
            if (obj is Object)
                throw new NotSupportedException("Standalone JsonUtility serializes content DTOs, not Unity object identities.");
            return JsonConvert.SerializeObject(obj, prettyPrint ? Formatting.Indented : Formatting.None, Settings());
        }

        private static void RequireObjectJson(string json)
        {
            if (!(JToken.Parse(json) is JObject))
                throw new ArgumentException("JsonUtility expects an object root, not a primitive or array.", nameof(json));
        }

        private static JsonSerializerSettings Settings()
        {
            return new JsonSerializerSettings
            {
                ContractResolver = FieldContractResolver.Instance,
                ObjectCreationHandling = ObjectCreationHandling.Replace,
                TypeNameHandling = TypeNameHandling.None,
                MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
                MissingMemberHandling = MissingMemberHandling.Ignore,
                MaxDepth = 128,
                Converters = { new UnityObjectReferenceConverter() }
            };
        }

        private sealed class FieldContractResolver : DefaultContractResolver
        {
            internal static readonly FieldContractResolver Instance = new FieldContractResolver();
            protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
            {
                var properties = new List<JsonProperty>();
                var names = new HashSet<string>(StringComparer.Ordinal);
                for (Type current = type; current != null && current != typeof(object); current = current.BaseType)
                {
                    foreach (FieldInfo field in current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    {
                        if (field.IsStatic || field.IsInitOnly || field.IsLiteral || field.IsDefined(typeof(NonSerializedAttribute), false)) continue;
                        if (!field.IsPublic && !field.IsDefined(typeof(SerializeField), true)) continue;
                        if (!names.Add(field.Name))
                            throw new NotSupportedException("Duplicate serialized field '" + field.Name + "' in " + type.FullName + ".");
                        JsonProperty property = base.CreateProperty(field, MemberSerialization.Fields);
                        property.PropertyName = field.Name;
                        property.Ignored = false;
                        property.Readable = true;
                        property.Writable = true;
                        properties.Add(property);
                    }
                }
                return properties;
            }
        }

        private sealed class UnityObjectReferenceConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType) => typeof(Object).IsAssignableFrom(objectType);
            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.Null) return null;
                throw new NotSupportedException("Unity asset references must be registered explicitly; JSON cannot resolve " + objectType.Name + ".");
            }
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                if (value == null) { writer.WriteNull(); return; }
                throw new NotSupportedException("Unity asset references are not serializable in standalone content DTOs.");
            }
        }
    }
}
#endif
