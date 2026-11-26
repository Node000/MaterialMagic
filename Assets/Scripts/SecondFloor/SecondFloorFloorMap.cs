using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

public enum SecondFloorEdgeKind { Wall, Open, Door }

/// <summary>Physical grid shared by scene and minimap consumers. Coordinates are Unity X/Z grid coordinates.</summary>
public sealed class SecondFloorFloorMap
{
    public const string PreviewResourcePath = "Data/Maps/Patterns/Preview.floor";

    public sealed class Tile
    {
        public Vector2Int Position { get; }
        public string Type { get; }
        public int? EnemyLevel { get; }
        public IReadOnlyList<string> StripIds { get; }

        internal Tile(Vector2Int position, string type, int? enemyLevel, string[] stripIds)
        {
            Position = position;
            Type = type ?? "empty";
            EnemyLevel = enemyLevel;
            StripIds = Array.AsReadOnly(stripIds ?? Array.Empty<string>());
        }
    }

    private static readonly Vector2Int[] Directions = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
    private readonly Dictionary<Vector2Int, Tile> tiles = new Dictionary<Vector2Int, Tile>();
    private readonly Dictionary<string, string> stripVisualTypes = new Dictionary<string, string>(StringComparer.Ordinal);
    private readonly Dictionary<(Vector2Int, Vector2Int), SecondFloorEdgeKind> edges =
        new Dictionary<(Vector2Int, Vector2Int), SecondFloorEdgeKind>();

    private SecondFloorFloorMap() { }

    public string Id { get; private set; }
    public Vector2Int Spawn { get; private set; }
    public IEnumerable<Tile> Tiles => tiles.Values;
    public bool HasTile(Vector2Int position) => tiles.ContainsKey(position);
    public string GetStripVisualType(string stripId) => stripId != null && stripVisualTypes.TryGetValue(stripId, out var type) ? type : "default";

    public SecondFloorEdgeKind GetEdge(Vector2Int from, Vector2Int to) =>
        edges.TryGetValue(EdgeKey(from, to), out var kind) ? kind : SecondFloorEdgeKind.Wall;

    public bool CanStep(Vector2Int from, Vector2Int to) => GetEdge(from, to) != SecondFloorEdgeKind.Wall;

    public static SecondFloorFloorMap Read(TextAsset asset)
    {
        if (asset == null) throw new ArgumentNullException(nameof(asset));
        return Read(asset.text);
    }

    public static SecondFloorFloorMap Read(string json)
    {
        var document = RuleJson.Deserialize<Document>(json);
        if (document == null || document.Kind != "plotweave-floor-map" || document.SchemaVersion != 1)
            throw new FormatException("Unsupported PlotWeave floor-map schema.");
        if (string.IsNullOrWhiteSpace(document.Id) || document.Spawn == null || document.Cells == null || document.Edges == null)
            throw new FormatException("Floor map requires id, spawn, cells and edges.");

        var map = new SecondFloorFloorMap { Id = document.Id, Spawn = document.Spawn.ToGridPosition() };
        if (document.Strips != null)
            foreach (var strip in document.Strips)
                if (strip != null && !string.IsNullOrWhiteSpace(strip.Id) && !string.IsNullOrWhiteSpace(strip.VisualTypeId))
                    map.stripVisualTypes.Add(strip.Id, strip.VisualTypeId);
        foreach (var cell in document.Cells)
        {
            if (cell == null) throw new FormatException("Null floor tile.");
            var position = cell.ToGridPosition();
            if (map.HasTile(position)) throw new FormatException($"Duplicate floor tile {position}.");
            map.tiles.Add(position, new Tile(position, cell.Type, cell.EnemyLevel, cell.StripIds));
        }
        if (!map.HasTile(map.Spawn)) throw new FormatException("Spawn is outside the floor tiles.");

        foreach (var edge in document.Edges)
        {
            if (edge == null) throw new FormatException("Null floor edge.");
            var from = edge.ToGridPosition();
            Vector2Int to;
            switch (edge.Direction)
            {
                case "E": to = from + Vector2Int.right; break;
                case "S": to = from + Vector2Int.down; break;
                default: throw new FormatException($"Invalid edge direction at {from}.");
            }
            SecondFloorEdgeKind kind;
            switch (edge.Type)
            {
                case "open": kind = SecondFloorEdgeKind.Open; break;
                case "door": kind = SecondFloorEdgeKind.Door; break;
                default: throw new FormatException($"Invalid edge type at {from}.");
            }
            if (!map.HasTile(from) || !map.HasTile(to)) throw new FormatException($"Edge {from} to {to} requires two floor tiles.");
            var key = EdgeKey(from, to);
            if (map.edges.ContainsKey(key)) throw new FormatException($"Duplicate floor edge {from} to {to}.");
            map.edges.Add(key, kind);
        }
        map.ValidateReachability();
        return map;
    }

    internal static SecondFloorFloorMap FromLayout(string id, Vector2Int spawn, IEnumerable<Tile> layoutTiles,
        IEnumerable<(Vector2Int From, Vector2Int To, SecondFloorEdgeKind Kind)> layoutEdges,
        IEnumerable<(string Id, string VisualTypeId)> visualTypes)
    {
        var map = new SecondFloorFloorMap { Id = id, Spawn = spawn };
        foreach (var (stripId, visualTypeId) in visualTypes) map.stripVisualTypes.Add(stripId, visualTypeId);
        foreach (var tile in layoutTiles) map.tiles.Add(tile.Position, tile);
        if (!map.HasTile(spawn)) throw new InvalidOperationException("Generated floor has no spawn tile.");
        foreach (var (from, to, kind) in layoutEdges)
        {
            var delta = to - from;
            if (kind == SecondFloorEdgeKind.Wall || !map.HasTile(from) || !map.HasTile(to) ||
                Mathf.Abs(delta.x) + Mathf.Abs(delta.y) != 1)
                throw new InvalidOperationException($"Invalid generated floor edge: {from} to {to} ({kind}).");
            map.edges.Add(EdgeKey(from, to), kind);
        }
        map.ValidateReachability();
        return map;
    }

    private void ValidateReachability()
    {
        var reachable = new HashSet<Vector2Int> { Spawn };
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(Spawn);
        while (queue.Count > 0)
        {
            var from = queue.Dequeue();
            foreach (var direction in Directions)
            {
                var to = from + direction;
                if (CanStep(from, to) && reachable.Add(to)) queue.Enqueue(to);
            }
        }
        if (reachable.Count != tiles.Count) throw new FormatException("Floor map contains tiles unreachable from spawn.");
    }

    private static (Vector2Int, Vector2Int) EdgeKey(Vector2Int a, Vector2Int b) =>
        a.x < b.x || (a.x == b.x && a.y <= b.y) ? (a, b) : (b, a);

    private sealed class Document
    {
        [JsonProperty("kind")] public string Kind { get; set; }
        [JsonProperty("schemaVersion")] public int SchemaVersion { get; set; }
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("spawn")] public Position Spawn { get; set; }
        [JsonProperty("cells")] public Cell[] Cells { get; set; }
        [JsonProperty("edges")] public Edge[] Edges { get; set; }
        [JsonProperty("strips")] public StripMetadata[] Strips { get; set; }
    }

    private sealed class StripMetadata
    {
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("visualTypeId")] public string VisualTypeId { get; set; }
    }

    private class Position
    {
        [JsonProperty("x", Required = Required.Always)] public int X { get; set; }
        [JsonProperty("y", Required = Required.Always)] public int Y { get; set; }

        public Vector2Int ToGridPosition()
        {
            if (X == int.MinValue || X == int.MaxValue || Y == int.MinValue || Y == int.MaxValue)
                throw new FormatException("Floor coordinate is outside the supported range.");
            return new Vector2Int(X, -Y);
        }
    }

    private sealed class Cell : Position
    {
        [JsonProperty("type")] public string Type { get; set; }
        [JsonProperty("enemyLevel")] public int? EnemyLevel { get; set; }
        [JsonProperty("stripIds")] public string[] StripIds { get; set; }
    }

    private sealed class Edge : Position
    {
        [JsonProperty("dir")] public string Direction { get; set; }
        [JsonProperty("type")] public string Type { get; set; }
    }
}
