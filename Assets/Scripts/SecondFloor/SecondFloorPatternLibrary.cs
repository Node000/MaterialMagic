using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>Runtime reader for an exported PlotWeave v3 strip-template library.</summary>
public sealed class SecondFloorPatternLibrary
{
    public sealed class CellType
    {
        public string Id { get; }
        public string Name { get; }
        public double Resource { get; }
        public double Difficulty { get; }
        public bool SupportsOffset { get; }
        public int DefaultOffset { get; }

        internal CellType(string id, string name, double resource, double difficulty, bool supportsOffset, int defaultOffset)
        {
            Id = id;
            Name = name;
            Resource = resource;
            Difficulty = difficulty;
            SupportsOffset = supportsOffset;
            DefaultOffset = defaultOffset;
        }
    }

    public sealed class Template
    {
        public string Id { get; }
        public string Name { get; }
        public string Role { get; }
        public IReadOnlyList<string> RegionIds { get; }
        public bool SpawnEligible { get; }
        public bool NoLoop { get; }
        public bool AllowRotate { get; }
        public int Max { get; }
        public double Weight { get; }
        public bool Unique { get; }
        public int InstanceLimit => Unique ? Math.Min(Max, 1) : Max;
        public double AutoResource { get; }
        public double AutoDifficulty { get; }
        public double? ResourceOverride { get; }
        public double? DifficultyOverride { get; }
        public double Resource => ResourceOverride ?? AutoResource;
        public double Difficulty => DifficultyOverride ?? AutoDifficulty;
        public IReadOnlyList<Variant> Variants { get; }

        internal Template(string id, string name, string role, string[] regionIds, bool spawnEligible, bool noLoop,
            bool allowRotate, int max, double weight, bool unique, double autoResource, double autoDifficulty,
            double? resourceOverride, double? difficultyOverride, IReadOnlyList<Variant> variants)
        {
            Id = id;
            Name = name;
            Role = role;
            RegionIds = Array.AsReadOnly(regionIds);
            SpawnEligible = spawnEligible;
            NoLoop = noLoop;
            AllowRotate = allowRotate;
            Max = max;
            Weight = weight;
            Unique = unique;
            AutoResource = autoResource;
            AutoDifficulty = autoDifficulty;
            ResourceOverride = resourceOverride;
            DifficultyOverride = difficultyOverride;
            Variants = variants;
        }

        public bool MatchesRegion(string regionId)
        {
            if (RegionIds.Count == 0)
                return true;
            foreach (string allowed in RegionIds)
                if (string.Equals(allowed, regionId, StringComparison.Ordinal))
                    return true;
            return false;
        }
    }

    public sealed class Variant
    {
        /// <summary>Clockwise quarter turns in PlotWeave coordinates, before mapping (x, y) to Unity X/Z as (x, -y).</summary>
        public int QuarterTurns { get; }
        public IReadOnlyList<Cell> Cells { get; }
        /// <summary>Only authored internal passages. Adjacent cells with no matching edge are walled off.</summary>
        public IReadOnlyList<Edge> Edges { get; }

        internal Variant(int quarterTurns, Cell[] cells, Edge[] edges)
        {
            QuarterTurns = quarterTurns;
            Cells = Array.AsReadOnly(cells);
            Edges = Array.AsReadOnly(edges);
        }
    }

    public readonly struct Cell
    {
        /// <summary>X and Z offsets; the normalized PlotWeave position (rx, ry) maps to (rx, -ry).</summary>
        public Vector2Int Position { get; }
        public string Type { get; }
        public int DifficultyOffset { get; }
        public bool IsPortal { get; }
        public int Index { get; }
        public long PlacedOrder { get; }
        public IReadOnlyList<string> Tags { get; }

        internal Cell(Vector2Int position, string type, int difficultyOffset, bool isPortal, int index, long placedOrder,
            IReadOnlyList<string> tags)
        {
            Position = position;
            Type = type;
            DifficultyOffset = difficultyOffset;
            IsPortal = isPortal;
            Index = index;
            PlacedOrder = placedOrder;
            Tags = tags;
        }

        internal Cell At(Vector2Int position) => new Cell(position, Type, DifficultyOffset, IsPortal, Index, PlacedOrder, Tags);
    }

    public readonly struct Edge
    {
        public Vector2Int A { get; }
        public Vector2Int B { get; }
        public int AIndex { get; }
        public int BIndex { get; }

        internal Edge(Cell a, Cell b)
        {
            A = a.Position;
            B = b.Position;
            AIndex = a.Index;
            BIndex = b.Index;
        }
    }

    public IReadOnlyDictionary<string, CellType> CellTypes { get; }
    public IReadOnlyList<Template> Templates { get; }

    private SecondFloorPatternLibrary(Dictionary<string, CellType> cellTypes, List<Template> templates)
    {
        CellTypes = new ReadOnlyDictionary<string, CellType>(cellTypes);
        Templates = templates.AsReadOnly();
    }

    /// <exception cref="FormatException">The asset is missing or does not satisfy the v3 export contract.</exception>
    public static SecondFloorPatternLibrary Read(TextAsset asset)
    {
        if (!asset)
            throw new FormatException("Assign a PlotWeave strip-template export (schemaVersion 3).");

        try
        {
            using (var text = new StringReader(asset.text))
            using (var reader = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None })
            {
                JObject root = JObject.Load(reader, new JsonLoadSettings
                {
                    DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error
                });
                if (reader.Read())
                    throw new FormatException("Unexpected content after the JSON object.");
                return ReadLibrary(root);
            }
        }
        catch (JsonException ex)
        {
            throw new FormatException($"PlotWeave export '{asset.name}': {ex.Message}", ex);
        }
        catch (FormatException ex)
        {
            throw new FormatException($"PlotWeave export '{asset.name}': {ex.Message}", ex);
        }
    }

    private static SecondFloorPatternLibrary ReadLibrary(JObject root)
    {
        if (String(root, "kind") != "plotweave-strip-templates")
            throw Invalid(root, "kind must be 'plotweave-strip-templates'.");
        Integer(root, "schemaVersion", 3, 3);
        if (root["app"] != null && String(root, "app") != "plotweave")
            throw Invalid(root, "app must be 'plotweave'.");

        JArray rawTypes = Field<JArray>(root, "cellTypes", JTokenType.Array);
        JArray rawTemplates = Field<JArray>(root, "stripTemplates", JTokenType.Array);
        if (rawTypes.Count == 0 || rawTemplates.Count == 0)
            throw Invalid(root, "cellTypes and stripTemplates must both be non-empty arrays.");

        var types = new Dictionary<string, CellType>(StringComparer.Ordinal);
        foreach (JToken entry in rawTypes)
        {
            JObject type = Object(entry);
            string id = Identifier(type, "id");
            if (types.ContainsKey(id))
                throw Invalid(type, $"duplicate cell type id '{id}'.");
            types.Add(id, new CellType(id, type["name"] == null ? id : Identifier(type, "name"),
                Number(type, "resource", 0), Number(type, "difficulty", 0), Boolean(type, "supportsOffset"),
                (int)Integer(type, "defaultOffset", -10, 10)));
        }

        var templates = new List<Template>(rawTemplates.Count);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (JToken entry in rawTemplates)
        {
            JObject template = Object(entry);
            string id = Identifier(template, "id");
            if (!ids.Add(id))
                throw Invalid(template, $"duplicate strip template id '{id}'.");
            templates.Add(ReadTemplate(template, id, types));
        }
        return new SecondFloorPatternLibrary(types, templates);
    }

    private static Template ReadTemplate(JObject template, string id, Dictionary<string, CellType> types)
    {
        string role = String(template, "role");
        if (role != "normal" && role != "bridge")
            throw Invalid(template, "role must be 'normal' or 'bridge'.");
        bool spawnEligible = Boolean(template, "spawnEligible");
        bool noLoop = Boolean(template, "noLoop");
        if ((role == "bridge" && spawnEligible) || (noLoop && (role == "bridge" || spawnEligible)))
            throw Invalid(template, "bridge and spawn eligibility are mutually exclusive; noLoop requires a normal, non-spawn template.");

        string name = Identifier(template, "name");
        string[] regions = Identifiers(template, "regionIds");
        bool allowRotate = Boolean(template, "allowRotate");
        JObject metrics = Field<JObject>(template, "metrics", JTokenType.Object);
        double? resourceOverride = OptionalNumber(metrics, "resourceOverride");
        double? difficultyOverride = OptionalNumber(metrics, "difficultyOverride");
        JObject generation = Field<JObject>(template, "generation", JTokenType.Object);
        int max = (int)Integer(generation, "max", 0, 99);
        double weight = Number(generation, "weight", 0.01);
        bool unique = Boolean(generation, "unique");

        JArray rawCells = Field<JArray>(template, "cells", JTokenType.Array);
        if (rawCells.Count < 1 || rawCells.Count > 16)
            throw Invalid(rawCells, "the strip must contain 1–16 cells.");
        var cells = new Cell[rawCells.Count];
        var byPosition = new Dictionary<Vector2Int, int>(cells.Length);
        double autoResource = 0;
        double autoDifficulty = 0;
        for (int i = 0; i < cells.Length; i++)
        {
            JObject rawCell = Object(rawCells[i]);
            Vector2Int position = ReadPosition(rawCell, "rx", "ry");
            if (byPosition.ContainsKey(position))
                throw Invalid(rawCell, $"duplicate cell coordinate {position}.");
            byPosition.Add(position, i);
            string typeId = Identifier(rawCell, "type");
            if (!types.TryGetValue(typeId, out CellType type))
                throw Invalid(rawCell, $"unknown cell type '{typeId}'.");
            int offset = (int)Integer(rawCell, "difficultyOffset", -10, 10);
            if (!type.SupportsOffset && offset != 0)
                throw Invalid(rawCell, $"cell type '{typeId}' does not support difficulty offsets.");

            const long maxSafeInteger = 9007199254740991L;
            long order = rawCell["placedOrder"] == null ? i : Integer(rawCell, "placedOrder", -maxSafeInteger, maxSafeInteger);
            string[] tags = rawCell["tags"] == null ? Array.Empty<string>() : Identifiers(rawCell, "tags");
            cells[i] = new Cell(position, typeId, offset, Boolean(rawCell, "isPortal"), i, order, Array.AsReadOnly(tags));
            autoResource += type.Resource;
            autoDifficulty += type.Difficulty;
        }
        if (double.IsInfinity(autoResource) || double.IsInfinity(autoDifficulty))
            throw Invalid(template, "cell resource or difficulty sum is too large.");

        JArray rawEdges = Field<JArray>(template, "openEdges", JTokenType.Array);
        Edge[] edges = ReadOpenEdges(rawEdges, cells, byPosition, role == "bridge");
        return new Template(id, name, role, regions, spawnEligible, noLoop, allowRotate, max, weight, unique,
            Math.Round(autoResource, 2, MidpointRounding.AwayFromZero), Math.Round(autoDifficulty, 2, MidpointRounding.AwayFromZero),
            resourceOverride, difficultyOverride, CreateVariants(cells, edges, allowRotate));
    }

    private static Edge[] ReadOpenEdges(JArray rawEdges, Cell[] cells, Dictionary<Vector2Int, int> byPosition, bool bridge)
    {
        var edges = new List<Edge>();
        var neighbors = new List<int>[cells.Length];
        for (int i = 0; i < neighbors.Length; i++)
            neighbors[i] = new List<int>(4);
        foreach (JToken entry in rawEdges)
        {
            JObject rawEdge = Object(entry);
            Vector2Int position = ReadPosition(rawEdge, "x", "y");
            string dir = String(rawEdge, "dir");
            Vector2Int next;
            if (dir == "E" && position.x < int.MaxValue)
                next = position + Vector2Int.right;
            else if (dir == "S" && position.y < int.MaxValue)
                next = position + Vector2Int.up;
            else
                throw Invalid(rawEdge, "openEdges must use canonical E or S directions within the coordinate range.");
            if (!byPosition.TryGetValue(position, out int a) || !byPosition.TryGetValue(next, out int b))
                throw Invalid(rawEdge, "open edge must join two occupied cells.");
            if (neighbors[a].Contains(b))
                throw Invalid(rawEdge, "duplicate open edge.");
            neighbors[a].Add(b);
            neighbors[b].Add(a);
            edges.Add(new Edge(cells[a], cells[b]));
        }

        var reachable = new List<int>(cells.Length) { 0 };
        for (int i = 0; i < reachable.Count; i++)
            foreach (int other in neighbors[reachable[i]])
                if (!reachable.Contains(other))
                    reachable.Add(other);
        if (reachable.Count != cells.Length)
            throw Invalid(rawEdges, "the explicit open-edge graph must reach every strip cell.");
        if (bridge)
        {
            if (edges.Count != cells.Length - 1)
                throw Invalid(rawEdges, "a bridge's open passages must form a single chain.");
            foreach (List<int> links in neighbors)
                if (links.Count > 2)
                    throw Invalid(rawEdges, "a bridge's open passages must form a single chain.");
        }
        return edges.ToArray();
    }

    private static IReadOnlyList<Variant> CreateVariants(Cell[] source, Edge[] sourceEdges, bool allowRotate)
    {
        Vector2Int sourceMin = source[0].Position;
        foreach (Cell cell in source)
            sourceMin = Vector2Int.Min(sourceMin, cell.Position);

        var variants = new List<Variant>(allowRotate ? 4 : 1);
        for (int turns = 0; turns < (allowRotate ? 4 : 1); turns++)
        {
            // Connectivity and the cell-count limit make these translated offsets safe to rotate.
            var rotated = new Vector2Int[source.Length];
            Vector2Int minimum = new Vector2Int(int.MaxValue, int.MaxValue);
            for (int i = 0; i < source.Length; i++)
            {
                Vector2Int point = source[i].Position - sourceMin;
                rotated[i] = turns == 1 ? new Vector2Int(-point.y, point.x) :
                    turns == 2 ? -point : turns == 3 ? new Vector2Int(point.y, -point.x) : point;
                minimum = Vector2Int.Min(minimum, rotated[i]);
            }
            var cells = new Cell[source.Length];
            for (int i = 0; i < cells.Length; i++)
            {
                Vector2Int normalized = rotated[i] - minimum;
                cells[i] = source[i].At(new Vector2Int(normalized.x, -normalized.y));
            }
            var edges = new Edge[sourceEdges.Length];
            for (int i = 0; i < edges.Length; i++)
                edges[i] = new Edge(cells[sourceEdges[i].AIndex], cells[sourceEdges[i].BIndex]);

            if (!variants.Exists(existing => SameLayout(existing, cells, edges)))
                variants.Add(new Variant(turns, cells, edges));
        }
        return variants.AsReadOnly();
    }

    private static bool SameLayout(Variant existing, Cell[] cells, Edge[] edges)
    {
        // Identity/order are carried through to the selected variant, but do not change a symmetric layout.
        foreach (Cell cell in cells)
        {
            bool found = false;
            foreach (Cell other in existing.Cells)
            {
                if (cell.Position != other.Position)
                    continue;
                if (cell.Type != other.Type || cell.DifficultyOffset != other.DifficultyOffset || cell.IsPortal != other.IsPortal
                    || !SameTags(cell.Tags, other.Tags))
                    return false;
                found = true;
                break;
            }
            if (!found)
                return false;
        }
        foreach (Edge edge in edges)
        {
            bool found = false;
            foreach (Edge other in existing.Edges)
                if ((edge.A == other.A && edge.B == other.B) || (edge.A == other.B && edge.B == other.A))
                {
                    found = true;
                    break;
                }
            if (!found)
                return false;
        }
        return true;
    }

    private static bool SameTags(IReadOnlyList<string> first, IReadOnlyList<string> second)
    {
        if (first.Count != second.Count)
            return false;
        foreach (string tag in first)
        {
            bool found = false;
            foreach (string other in second)
                if (tag == other)
                {
                    found = true;
                    break;
                }
            if (!found)
                return false;
        }
        return true;
    }

    private static Vector2Int ReadPosition(JObject obj, string x, string y) =>
        new Vector2Int((int)Integer(obj, x, int.MinValue, int.MaxValue), (int)Integer(obj, y, int.MinValue, int.MaxValue));

    private static JObject Object(JToken token) => token as JObject ?? throw Invalid(token, "expected a JSON object.");

    private static FormatException Invalid(JToken token, string message) =>
        new FormatException($"{(string.IsNullOrEmpty(token.Path) ? "$" : token.Path)}: {message}");

    private static T Field<T>(JObject obj, string name, JTokenType type) where T : JToken
    {
        JToken token = obj[name];
        if (token?.Type != type)
            throw Invalid(obj, $"'{name}' must be a JSON {type}.");
        return (T)token;
    }

    private static string String(JObject obj, string name) => (string)Field<JValue>(obj, name, JTokenType.String);
    private static bool Boolean(JObject obj, string name) => (bool)Field<JValue>(obj, name, JTokenType.Boolean);

    private static string Identifier(JObject obj, string name) => Identifier(Field<JValue>(obj, name, JTokenType.String));

    private static string Identifier(JToken token)
    {
        if (token.Type != JTokenType.String || string.IsNullOrWhiteSpace((string)token) || ((string)token).Trim() != (string)token)
            throw Invalid(token, "expected a non-empty, trimmed string.");
        return (string)token;
    }

    private static string[] Identifiers(JObject obj, string name)
    {
        JArray tokens = Field<JArray>(obj, name, JTokenType.Array);
        var result = new string[tokens.Count];
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = Identifier(tokens[i]);
            if (!seen.Add(result[i]))
                throw Invalid(tokens[i], $"duplicate '{name}' value '{result[i]}'.");
        }
        return result;
    }

    private static long Integer(JObject obj, string name, long min, long max)
    {
        double value = (double)Field<JValue>(obj, name, JTokenType.Integer);
        if (value < min || value > max)
            throw Invalid(obj[name], $"must be an integer from {min} to {max}.");
        return (long)value;
    }

    private static double Number(JObject obj, string name, double min)
    {
        JToken token = obj[name];
        if (token?.Type != JTokenType.Integer && token?.Type != JTokenType.Float)
            throw Invalid(obj, $"'{name}' must be a JSON number.");
        double value = (double)token;
        if (double.IsNaN(value) || double.IsInfinity(value) || value < min)
            throw Invalid(token, $"must be a finite number greater than or equal to {min}.");
        return value;
    }

    private static double? OptionalNumber(JObject obj, string name) =>
        obj[name]?.Type == JTokenType.Null ? (double?)null : Number(obj, name, 0);
}
