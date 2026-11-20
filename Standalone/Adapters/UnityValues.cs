#if MATERIALMAGIC_STANDALONE
using System;
using System.Globalization;

// Intentionally bounded to values used by the linked production rules. No Unity runtime is emulated.
namespace UnityEngine
{
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class SerializeField : Attribute { }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class CreateAssetMenuAttribute : Attribute
    {
        public string fileName;
        public string menuName;
        public int order;
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class TooltipAttribute : Attribute
    {
        public string tooltip { get; }
        public TooltipAttribute(string tooltip) { this.tooltip = tooltip; }
    }

    // CLR identity/null semantics only: there is no native object lifetime or destroyed-object state.
    public class Object
    {
        public string name { get; set; } = string.Empty;
    }

    public class ScriptableObject : Object
    {
        public static T CreateInstance<T>() where T : ScriptableObject
        {
            return (T)CreateInstance(typeof(T));
        }

        public static ScriptableObject CreateInstance(Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (!typeof(ScriptableObject).IsAssignableFrom(type) || type.IsAbstract)
                throw new ArgumentException("A concrete ScriptableObject type is required.", nameof(type));
            return (ScriptableObject)Activator.CreateInstance(type, true);
        }
    }

    // Opaque visual reference required by MaterialModifierDefinition; no rendering API is available.
    public sealed class Material : Object { }

    public sealed class TextAsset : Object
    {
        public string text { get; }
        public TextAsset(string text) { this.text = text ?? string.Empty; }
    }

    public static class Mathf
    {
        public static int Max(int a, int b) => a > b ? a : b;
        public static float Max(float a, float b) => a > b ? a : b;
        public static int Min(int a, int b) => a < b ? a : b;
        public static float Min(float a, float b) => a < b ? a : b;
        public static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
        public static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
        public static float Clamp01(float value) => Clamp(value, 0f, 1f);
        // Unity rounds exact midpoint values to the nearest even integer, not away from zero.
        public static int RoundToInt(float value) => (int)Math.Round(value, MidpointRounding.ToEven);
        public static bool Approximately(float a, float b)
        {
            return Math.Abs(b - a) < Max(0.000001f * Max(Math.Abs(a), Math.Abs(b)), float.Epsilon * 8f);
        }
    }

    [Serializable]
    public struct Vector2
    {
        public float x;
        public float y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "({0}, {1})", x, y);
    }

    [Serializable]
    public struct Vector2Int : IEquatable<Vector2Int>
    {
        public int x;
        public int y;
        public Vector2Int(int x, int y) { this.x = x; this.y = y; }
        public static Vector2Int zero => new Vector2Int(0, 0);
        public static Vector2Int up => new Vector2Int(0, 1);
        public static Vector2Int down => new Vector2Int(0, -1);
        public static Vector2Int left => new Vector2Int(-1, 0);
        public static Vector2Int right => new Vector2Int(1, 0);
        public bool Equals(Vector2Int other) => x == other.x && y == other.y;
        public override bool Equals(object obj) => obj is Vector2Int other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(x, y);
        public static bool operator ==(Vector2Int a, Vector2Int b) => a.Equals(b);
        public static bool operator !=(Vector2Int a, Vector2Int b) => !a.Equals(b);
        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "({0}, {1})", x, y);
    }

    [Serializable]
    public struct Color
    {
        public float r;
        public float g;
        public float b;
        public float a;
        public Color(float r, float g, float b, float a = 1f)
        {
            this.r = r; this.g = g; this.b = b; this.a = a;
        }
        public static Color white => new Color(1f, 1f, 1f, 1f);
    }

    public static class ColorUtility
    {
        // The content tables use hexadecimal HTML colors. Named CSS colors are outside this adapter.
        public static bool TryParseHtmlString(string htmlString, out Color color)
        {
            color = default;
            if (string.IsNullOrEmpty(htmlString) || htmlString[0] != '#') return false;
            string hex = htmlString.Substring(1);
            if (hex.Length == 3 || hex.Length == 4)
            {
                string expanded = string.Empty;
                for (int i = 0; i < hex.Length; i++) expanded += new string(hex[i], 2);
                hex = expanded;
            }
            if (hex.Length != 6 && hex.Length != 8) return false;
            if (!uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint value)) return false;
            if (hex.Length == 6) value = (value << 8) | 255u;
            color = new Color(((value >> 24) & 255) / 255f, ((value >> 16) & 255) / 255f,
                ((value >> 8) & 255) / 255f, (value & 255) / 255f);
            return true;
        }

        public static string ToHtmlStringRGB(Color color)
        {
            return Channel(color.r).ToString("X2", CultureInfo.InvariantCulture)
                + Channel(color.g).ToString("X2", CultureInfo.InvariantCulture)
                + Channel(color.b).ToString("X2", CultureInfo.InvariantCulture);
        }
        private static int Channel(float value) => Mathf.RoundToInt(Mathf.Clamp01(value) * 255f);
    }

    public static class Random
    {
        // A missing run RNG is a host integration error, not permission to introduce nondeterminism.
        public static int Range(int minInclusive, int maxExclusive)
        {
            throw new NotSupportedException("UnityEngine.Random is unavailable in standalone rules. Supply the production run RNG or an explicit NextRandomInt service.");
        }
    }
}
#endif
