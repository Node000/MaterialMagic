using System;
using System.Collections.Generic;
using UnityEngine;

public enum SecondFloorPart { Lower, Upper }
public enum MinimapTileKind { Path, Connector, Room, BossRoom, Portal }

public readonly struct MinimapTile
{
    public readonly Vector2Int Position;
    public readonly string Area;
    public readonly MinimapTileKind Kind;
    public readonly LevelType? RoomType;

    public MinimapTile(Vector2Int position, string area, MinimapTileKind kind = MinimapTileKind.Path, LevelType? roomType = null)
    {
        Position = position;
        Area = area;
        Kind = kind;
        RoomType = roomType;
    }
}

public readonly struct MinimapStrip
{
    // Area id for a local strip, or the area pair (e.g. A-B) for a connector.
    public readonly string Owner;
    public readonly IReadOnlyList<Vector2Int> Cells;
    public MinimapStrip(string owner, List<Vector2Int> cells)
    {
        Owner = owner;
        Cells = cells.AsReadOnly();
    }
}

public sealed class SecondFloorMinimapBoard
{
    private readonly Dictionary<Vector2Int, MinimapTile> tiles = new Dictionary<Vector2Int, MinimapTile>();
    private readonly Dictionary<Vector2Int, HashSet<Vector2Int>> neighbors = new Dictionary<Vector2Int, HashSet<Vector2Int>>();
    private readonly Dictionary<Vector2Int, Vector2Int> portalDestinations = new Dictionary<Vector2Int, Vector2Int>();

    private readonly List<MinimapStrip> strips = new List<MinimapStrip>();
    public IEnumerable<MinimapStrip> Strips => strips.AsReadOnly();
    public IEnumerable<MinimapTile> Tiles => tiles.Values;
    public MinimapTile GetTile(Vector2Int position) => tiles[position];
    public bool Contains(Vector2Int position) => tiles.ContainsKey(position);
    public bool CanStep(Vector2Int from, Vector2Int to) => neighbors.TryGetValue(from, out var exits) && exits.Contains(to);
    public bool TryGetPortal(Vector2Int from, out Vector2Int destination) => portalDestinations.TryGetValue(from, out destination);

    internal void Add(MinimapTile tile)
    {
        tiles.Add(tile.Position, tile);
        neighbors.Add(tile.Position, new HashSet<Vector2Int>());
    }

    internal void Link(Vector2Int from, Vector2Int to)
    {
        Vector2Int delta = to - from;
        if (Math.Abs(delta.x) + Math.Abs(delta.y) != 1)
            throw new ArgumentException("A walking connection must link cardinal neighbors.");
        neighbors[from].Add(to);
        neighbors[to].Add(from);
    }

    internal void SetPortal(Vector2Int from, Vector2Int destination) => portalDestinations.Add(from, destination);
    internal void AddStrip(string owner, List<Vector2Int> cells) => strips.Add(new MinimapStrip(owner, cells));
}

public readonly struct MinimapRegion
{
    public readonly string Id;
    public readonly SecondFloorPart Part;
    public readonly Vector3 WorldCenter;

    public MinimapRegion(string id, SecondFloorPart part, Vector3 worldCenter)
    {
        Id = id;
        Part = part;
        WorldCenter = worldCenter;
    }
}

public sealed class SecondFloorMinimapMap
{
    public int Seed { get; }
    public Vector2Int Start { get; internal set; }
    public float LowerYScale { get; }
    public IReadOnlyList<MinimapRegion> Regions { get; }
    private readonly SecondFloorMinimapBoard lower = new SecondFloorMinimapBoard();
    private readonly SecondFloorMinimapBoard upper = new SecondFloorMinimapBoard();

    internal SecondFloorMinimapMap(int seed, int side, int rows)
    {
        Seed = seed;
        float rise = side * Mathf.Sqrt(3f) / 2f;
        LowerYScale = rise / rows;
        Regions = Array.AsReadOnly(new[]
        {
            new MinimapRegion("A", SecondFloorPart.Lower, Vector3.zero),
            new MinimapRegion("B", SecondFloorPart.Lower, new Vector3(side, 0, 0)),
            new MinimapRegion("C", SecondFloorPart.Lower, new Vector3(side / 2f, 0, rise)),
            new MinimapRegion("U", SecondFloorPart.Upper, new Vector3(side / 2f, side * Mathf.Sqrt(2f / 3f), rise / 3f))
        });
    }

    public SecondFloorMinimapBoard Board(SecondFloorPart part) => part == SecondFloorPart.Lower ? lower : upper;
}

public enum MinimapAction { Forward, Backward, TurnLeft, TurnRight }
public enum MinimapOutcome { Turned, Blocked, Moved, Teleported }

public readonly struct MinimapStep
{
    public readonly MinimapOutcome Outcome;
    // For a teleport, the entered tile is the source room, not the landing tile.
    public readonly MinimapTile EnteredTile;
    public MinimapStep(MinimapOutcome outcome, MinimapTile enteredTile = default)
    {
        Outcome = outcome;
        EnteredTile = enteredTile;
    }
}

/// <summary>Rules only: no input, rendering, run state or room effects.</summary>
public sealed class SecondFloorMinimapSession
{
    private static readonly Vector2Int[] Directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
    public SecondFloorMinimapMap Map { get; }
    public SecondFloorPart Part { get; private set; }
    public Vector2Int Position { get; private set; }
    public SecondFloorFacing Facing { get; private set; }
    public SecondFloorMinimapBoard CurrentBoard => Map.Board(Part);
    public Vector2Int Direction => Directions[(int)Facing];

    public SecondFloorMinimapSession(SecondFloorMinimapMap map)
    {
        Map = map ?? throw new ArgumentNullException(nameof(map));
        Part = SecondFloorPart.Lower;
        Position = map.Start;
        for (int i = 0; i < Directions.Length; i++)
        {
            if (!CurrentBoard.CanStep(Position, Position + Directions[i])) continue;
            Facing = (SecondFloorFacing)i;
            break;
        }
    }

    public MinimapStep Execute(MinimapAction action)
    {
        switch (action)
        {
            case MinimapAction.TurnLeft:
            case MinimapAction.TurnRight:
                Facing = (SecondFloorFacing)(((int)Facing + (action == MinimapAction.TurnLeft ? 3 : 1)) % 4);
                return new MinimapStep(MinimapOutcome.Turned);
            case MinimapAction.Forward:
                return Step(Direction);
            case MinimapAction.Backward:
                return Step(-Direction);
            default:
                throw new ArgumentOutOfRangeException(nameof(action));
        }
    }

    private MinimapStep Step(Vector2Int delta)
    {
        Vector2Int next = Position + delta;
        var board = CurrentBoard;
        if (!board.CanStep(Position, next)) return new MinimapStep(MinimapOutcome.Blocked);
        var entered = board.GetTile(next);
        if (!board.TryGetPortal(next, out var landing))
        {
            Position = next;
            return new MinimapStep(MinimapOutcome.Moved, entered);
        }

        Part = Part == SecondFloorPart.Lower ? SecondFloorPart.Upper : SecondFloorPart.Lower;
        Position = landing;
        return new MinimapStep(MinimapOutcome.Teleported, entered);
    }
}
