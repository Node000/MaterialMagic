using System;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using UnityEngine;

/// <summary>
/// Portable rule DTO JSON. Unity vectors serialize as values, never computed normalized/magnitude
/// properties. Other DTO contracts, including readonly properties with JsonConstructor, are retained.
/// This serializer does not read or change JsonConvert.DefaultSettings or host profile settings.
/// </summary>
public static class RuleJson
{
    private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
    {
        ContractResolver = new DefaultContractResolver(),
        Culture = CultureInfo.InvariantCulture,
        TypeNameHandling = TypeNameHandling.None,
        MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
        ObjectCreationHandling = ObjectCreationHandling.Replace,
        ReferenceLoopHandling = ReferenceLoopHandling.Error,
        Converters = { new Vector2ValueConverter() }
    };

    public static string Serialize(object value, bool pretty = false)
    {
        using (var text = new StringWriter(CultureInfo.InvariantCulture))
        using (var writer = new JsonTextWriter(text))
        {
            writer.Formatting = pretty ? Formatting.Indented : Formatting.None;
            JsonSerializer.Create(Settings).Serialize(writer, value);
            writer.Flush();
            return text.ToString();
        }
    }

    public static T Deserialize<T>(string json)
    {
        if (json == null) throw new ArgumentNullException(nameof(json));
        using (var text = new StringReader(json))
        using (var reader = new JsonTextReader(text))
            return JsonSerializer.Create(Settings).Deserialize<T>(reader);
    }

    public static T Clone<T>(T value)
        => ReferenceEquals(value, null) ? default(T) : Deserialize<T>(Serialize(value));

    private sealed class Vector2ValueConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(Vector2);

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            Vector2 vector = (Vector2)value;
            writer.WriteStartObject();
            writer.WritePropertyName("x");
            writer.WriteValue(vector.x);
            writer.WritePropertyName("y");
            writer.WriteValue(vector.y);
            writer.WriteEndObject();
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JObject value = JObject.Load(reader);
            return new Vector2(
                (float?)value.GetValue("x", StringComparison.OrdinalIgnoreCase) ?? 0f,
                (float?)value.GetValue("y", StringComparison.OrdinalIgnoreCase) ?? 0f);
        }
    }
}
