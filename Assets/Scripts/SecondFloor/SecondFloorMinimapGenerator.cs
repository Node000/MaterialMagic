using System;
using System.Collections.Generic;
using UnityEngine;

public static class SecondFloorMinimapGenerator
{
    private static readonly Vector2Int[] Cardinal = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

    public static bool TryGenerate(StripDungeonMapConfig config, int seed, out SecondFloorMinimapMap map, out string error)
    {
        map = null;
        if (config == null)
        {
            error = "缺少条带地图配置。";
            return false;
        }

        // Even footprints keep mirrored corridors aligned. Allow one extra tile around each local map for rooms.
        int width = (config.MapWidth + 1) / 2 * 2;
        int height = (config.MapHeight + 1) / 2 * 2;
        int side = 2 * (width + 8);
        int rise = height + 18;
        var a = Vector2Int.zero;
        var b = new Vector2Int(side, 0);
        var c = new Vector2Int(side / 2, rise);
        var centers = new[] { a, b, c, Vector2Int.zero };
        var random = new System.Random(seed);
        var sources = new StripDungeonMap[centers.Length];
        for (int i = 0; i < sources.Length; i++)
        {
            if (StripDungeonMapGenerator.TryGenerate(config, random.Next(1, int.MaxValue), out sources[i], out error)) continue;
            error = $"区域 {(i == 3 ? "U" : ((char)('A' + i)).ToString())}：{error}";
            return false;
        }

        var result = new SecondFloorMinimapMap(seed, side, rise);
        var lower = result.Board(SecondFloorPart.Lower);
        var upper = result.Board(SecondFloorPart.Upper);
        for (int i = 0; i < sources.Length; i++)
        {
            Vector2Int offset = centers[i] - new Vector2Int(width / 2, height / 2);
            ImportRegion(i == 3 ? upper : lower, sources[i], result.Regions[i].Id, offset, i == 3);
            if (i == 0) result.Start = sources[i].startPosition + offset;
        }

        // Three disjoint routes. The sloped sides are drawn as cardinal staircases and each is one logical strip.
        LinkRegions(lower, a, Vector2Int.right, b, Vector2Int.left, "A-B", width, height);
        LinkRegions(lower, a, Vector2Int.up, c, Vector2Int.left, "A-C", width, height);
        LinkRegions(lower, b, Vector2Int.up, c, Vector2Int.right, "B-C", width, height);

        for (int i = 0; i < 3; i++)
        {
            var id = result.Regions[i].Id;
            var downstairs = AddPortalRoom(lower, id, result.Start);
            var upstairs = AddPortalRoom(upper, "U", null);
            lower.SetPortal(downstairs, upstairs);
            upper.SetPortal(upstairs, downstairs);
        }

        map = result;
        error = null;
        return true;
    }

    private static void ImportRegion(SecondFloorMinimapBoard board, StripDungeonMap source, string id, Vector2Int offset, bool includeBoss)
    {
        foreach (var cell in source.cells)
        {
            if (cell.kind == StripDungeonCellKind.Path)
                board.Add(new MinimapTile(cell.position + offset, id, cell.isContent ? MinimapTileKind.Room : MinimapTileKind.Path,
                    cell.isContent ? (LevelType?)cell.levelType : null));
        }
        // Link only consecutive strip cells. Shared intersections naturally share one tile;
        // two unrelated strips that merely touch do not open a shortcut.
        foreach (var strip in source.strips)
        {
            var cells = new List<Vector2Int>(strip.cells.Count);
            foreach (var cell in strip.cells) cells.Add(cell + offset);
            for (int i = 1; i < cells.Count; i++) board.Link(cells[i - 1], cells[i]);
            board.AddStrip(id, cells);
        }

        if (!includeBoss) return;
        var boss = source.bossPosition + offset;
        board.Add(new MinimapTile(boss, id, MinimapTileKind.BossRoom));
        board.Link(source.bossEntrancePosition + offset, boss);
    }

    private static void LinkRegions(SecondFloorMinimapBoard board, Vector2Int fromCenter, Vector2Int fromDirection,
        Vector2Int toCenter, Vector2Int toDirection, string id, int width, int height)
    {
        Vector2Int from = OuterPath(board, id.Substring(0, 1), fromDirection);
        Vector2Int to = OuterPath(board, id.Substring(2, 1), toDirection);
        Vector2Int fromGate = Gate(from, fromCenter, fromDirection, width, height);
        Vector2Int toGate = Gate(to, toCenter, toDirection, width, height);

        var previous = from;
        var cells = new List<Vector2Int> { from };
        void Advance(Vector2Int position)
        {
            if (position != to) board.Add(new MinimapTile(position, id, MinimapTileKind.Connector));
            board.Link(previous, position);
            cells.Add(position);
            previous = position;
        }
        while (previous != fromGate) Advance(previous + fromDirection);
        if (fromDirection == Vector2Int.up)
        {
            // Keep the diagonal route outside the upper edge of A/B and the side of C.
            // Interleave its horizontal and vertical steps to read as one diagonal on the map.
            int dx = Math.Abs(toGate.x - fromGate.x);
            int dy = Math.Abs(toGate.y - fromGate.y);
            int sx = Math.Sign(toGate.x - fromGate.x);
            int sy = Math.Sign(toGate.y - fromGate.y);
            for (int y = 1; y <= dy; y++)
            {
                int targetX = fromGate.x + sx * (y * dx / dy);
                while (previous.x != targetX) Advance(previous + new Vector2Int(sx, 0));
                Advance(previous + new Vector2Int(0, sy));
            }
        }
        while (previous.x != toGate.x) Advance(previous + new Vector2Int(Math.Sign(toGate.x - previous.x), 0));
        while (previous.y != toGate.y) Advance(previous + new Vector2Int(0, Math.Sign(toGate.y - previous.y)));
        while (previous != to) Advance(previous - toDirection);
        board.AddStrip(id, cells);
    }

    private static Vector2Int Gate(Vector2Int position, Vector2Int center, Vector2Int direction, int width, int height) =>
        direction.x != 0
            ? new Vector2Int(center.x + direction.x * (width / 2 + 2), position.y)
            : new Vector2Int(position.x, center.y + direction.y * (height / 2 + 2));

    private static Vector2Int OuterPath(SecondFloorMinimapBoard board, string id, Vector2Int direction)
    {
        bool found = false;
        Vector2Int selected = default;
        foreach (var tile in board.Tiles)
        {
            if (tile.Area != id) continue;
            if (!found || (tile.Position.x - selected.x) * direction.x + (tile.Position.y - selected.y) * direction.y > 0)
            {
                selected = tile.Position;
                found = true;
            }
        }
        if (!found) throw new InvalidOperationException($"区域 {id} 没有条带格。");
        return selected;
    }

    private static Vector2Int AddPortalRoom(SecondFloorMinimapBoard board, string id, Vector2Int? excluded)
    {
        // Select before mutating the board. A portal is an extra room with just one local connection.
        foreach (var tile in board.Tiles)
        {
            if (tile.Area != id || tile.Kind != MinimapTileKind.Path || tile.Position == excluded) continue;
            foreach (var direction in Cardinal)
            {
                var room = tile.Position + direction;
                if (board.Contains(room)) continue;
                board.Add(new MinimapTile(room, id, MinimapTileKind.Portal));
                board.Link(tile.Position, room);
                return room;
            }
        }
        throw new InvalidOperationException($"区域 {id} 没有可用的传送房位置。");
    }
}
