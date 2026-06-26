using System;
using System.Collections.Generic;
using UnityEngine;

public enum DungeonMapCellKind
{
    Wall = 0,
    Room = 1,
    Corridor = 2
}

public enum DungeonMapContentKind
{
    None = 0,
    Start = 1,
    Level = 2,
    Boss = 3,
    CorridorEncounter = 4
}

public class GeneratedDungeonMap
{
    public int width;
    public int height;
    public int startRoomId;
    public int bossRoomId;
    public int startX;
    public int startY;
    public int bossX;
    public int bossY;
    public readonly List<GeneratedDungeonRoom> rooms = new List<GeneratedDungeonRoom>();
    public readonly List<GeneratedDungeonConnection> connections = new List<GeneratedDungeonConnection>();
    public readonly List<GeneratedDungeonCell> cells = new List<GeneratedDungeonCell>();

    public GeneratedDungeonCell GetCell(int x, int y)
    {
        for (int i = 0; i < cells.Count; i++)
        {
            GeneratedDungeonCell cell = cells[i];
            if (cell != null && cell.x == x && cell.y == y)
                return cell;
        }
        return null;
    }
}

public class GeneratedDungeonRoom
{
    public int roomId;
    public int x;
    public int y;
    public int width;
    public int height;
    public int centerX;
    public int centerY;
    public int contentX;
    public int contentY;
    public int depth;
    public bool isStart;
    public bool isBoss;
    public bool isMainPath;
    public bool isDeadEnd;
    public DungeonMapContentKind contentKind;
    public LevelType levelType;
}

public class GeneratedDungeonConnection
{
    public int fromRoomId;
    public int toRoomId;
    public readonly List<Vector2Int> pathCells = new List<Vector2Int>();
}

public class GeneratedDungeonCell
{
    public int x;
    public int y;
    public int roomId = -1;
    public DungeonMapCellKind kind;
    public DungeonMapContentKind contentKind;
    public LevelType levelType;
    public bool isAvailable;
}

public static class DungeonMapGenerator
{
    private const int DefaultSeed = 1;

    private class GenerationState
    {
        public int seed;
        public int step;

        public int Range(int minInclusive, int maxExclusive)
        {
            int value = RunRandom.Range(seed != 0 ? seed : DefaultSeed, step, minInclusive, maxExclusive);
            step++;
            return value;
        }

        public bool RollPercent(int percent)
        {
            return Range(0, 100) < Mathf.Clamp(percent, 0, 100);
        }
    }

    private struct RoomEdge
    {
        public int a;
        public int b;
        public int distance;
    }

    public static GeneratedDungeonMap Generate(MapGenConfigData config, int seed)
    {
        MapGenConfigData safeConfig = config ?? new MapGenConfigData();
        GenerationState random = new GenerationState { seed = seed != 0 ? seed : DefaultSeed };
        int width = RandomRangeInclusive(random, Mathf.Max(3, safeConfig.mapWidthMin), Mathf.Max(3, safeConfig.mapWidthMax));
        int height = RandomRangeInclusive(random, Mathf.Max(3, safeConfig.mapHeightMin), Mathf.Max(3, safeConfig.mapHeightMax));
        int targetRoomCount = RandomRangeInclusive(random, Mathf.Max(2, safeConfig.roomCountMin), Mathf.Max(2, safeConfig.roomCountMax));

        GeneratedDungeonMap map = new GeneratedDungeonMap
        {
            width = width,
            height = height,
            startRoomId = 0,
            bossRoomId = -1
        };

        AddStartRoom(map);
        PlaceRooms(map, safeConfig, random, targetRoomCount);
        if (map.rooms.Count < 2)
            AddFallbackRoom(map);

        BuildConnections(map, safeConfig, random);
        AnalyzeRooms(map);
        SelectBossRoom(map, safeConfig, random);
        AssignRoomContents(map, safeConfig, random);
        BuildCells(map);
        AssignCorridorEncounters(map, safeConfig, random);
        return map;
    }

    private static int RandomRangeInclusive(GenerationState random, int min, int max)
    {
        if (max < min)
        {
            int temp = max;
            max = min;
            min = temp;
        }
        return random.Range(min, max + 1);
    }

    private static void AddStartRoom(GeneratedDungeonMap map)
    {
        int centerX = Mathf.Clamp(map.width / 2, 0, map.width - 1);
        GeneratedDungeonRoom room = new GeneratedDungeonRoom
        {
            roomId = 0,
            x = centerX,
            y = 0,
            width = 1,
            height = 1,
            centerX = centerX,
            centerY = 0,
            contentX = centerX,
            contentY = 0,
            isStart = true,
            contentKind = DungeonMapContentKind.Start
        };
        map.startX = room.contentX;
        map.startY = room.contentY;
        map.rooms.Add(room);
    }

    private static void AddFallbackRoom(GeneratedDungeonMap map)
    {
        int x = Mathf.Clamp(map.width / 2, 0, map.width - 1);
        int y = Mathf.Clamp(map.height - 1, 0, map.height - 1);
        GeneratedDungeonRoom room = new GeneratedDungeonRoom
        {
            roomId = map.rooms.Count,
            x = x,
            y = y,
            width = 1,
            height = 1,
            centerX = x,
            centerY = y,
            contentX = x,
            contentY = y
        };
        map.rooms.Add(room);
    }

    private static void PlaceRooms(GeneratedDungeonMap map, MapGenConfigData config, GenerationState random, int targetRoomCount)
    {
        int attempts = Mathf.Max(1, config.roomPlacementAttempts);
        int minWidth = Mathf.Clamp(config.roomWidthMin, 1, map.width);
        int maxWidth = Mathf.Clamp(Mathf.Max(config.roomWidthMax, minWidth), 1, map.width);
        int minHeight = Mathf.Clamp(config.roomHeightMin, 1, map.height);
        int maxHeight = Mathf.Clamp(Mathf.Max(config.roomHeightMax, minHeight), 1, map.height);

        for (int i = 0; i < attempts && map.rooms.Count < targetRoomCount; i++)
        {
            int roomWidth = RandomRangeInclusive(random, minWidth, maxWidth);
            int roomHeight = RandomRangeInclusive(random, minHeight, maxHeight);
            if (roomWidth > map.width || roomHeight > map.height)
                continue;

            int x = random.Range(0, map.width - roomWidth + 1);
            int y = random.Range(0, map.height - roomHeight + 1);
            GeneratedDungeonRoom room = new GeneratedDungeonRoom
            {
                roomId = map.rooms.Count,
                x = x,
                y = y,
                width = roomWidth,
                height = roomHeight,
                centerX = x + roomWidth / 2,
                centerY = y + roomHeight / 2,
                contentX = x + roomWidth / 2,
                contentY = y + roomHeight / 2
            };

            if (OverlapsExistingRoom(map, room, Mathf.Max(0, config.roomPadding)))
                continue;

            map.rooms.Add(room);
        }
    }

    private static bool OverlapsExistingRoom(GeneratedDungeonMap map, GeneratedDungeonRoom candidate, int padding)
    {
        for (int i = 0; i < map.rooms.Count; i++)
        {
            GeneratedDungeonRoom room = map.rooms[i];
            bool separated = candidate.x + candidate.width - 1 + padding < room.x ||
                room.x + room.width - 1 + padding < candidate.x ||
                candidate.y + candidate.height - 1 + padding < room.y ||
                room.y + room.height - 1 + padding < candidate.y;
            if (!separated)
                return true;
        }
        return false;
    }

    private static void BuildConnections(GeneratedDungeonMap map, MapGenConfigData config, GenerationState random)
    {
        List<RoomEdge> treeEdges = BuildMinimumSpanningTree(map.rooms);
        bool[,] connected = new bool[map.rooms.Count, map.rooms.Count];
        for (int i = 0; i < treeEdges.Count; i++)
            AddConnection(map, treeEdges[i].a, treeEdges[i].b, connected, random);

        int extraConnectionLimit = Mathf.Max(1, map.rooms.Count / 3);
        int extraConnections = 0;
        for (int a = 0; a < map.rooms.Count; a++)
        {
            for (int b = a + 1; b < map.rooms.Count; b++)
            {
                if (connected[a, b] || extraConnections >= extraConnectionLimit)
                    continue;

                if (random.RollPercent(config.extraConnectionChance))
                {
                    AddConnection(map, a, b, connected, random);
                    extraConnections++;
                }
            }
        }
    }

    private static List<RoomEdge> BuildMinimumSpanningTree(List<GeneratedDungeonRoom> rooms)
    {
        List<RoomEdge> edges = new List<RoomEdge>();
        if (rooms.Count <= 1)
            return edges;

        bool[] inTree = new bool[rooms.Count];
        inTree[0] = true;
        int included = 1;
        while (included < rooms.Count)
        {
            RoomEdge best = new RoomEdge { a = 0, b = 0, distance = int.MaxValue };
            for (int a = 0; a < rooms.Count; a++)
            {
                if (!inTree[a])
                    continue;

                for (int b = 0; b < rooms.Count; b++)
                {
                    if (inTree[b])
                        continue;

                    int distance = ManhattanDistance(rooms[a], rooms[b]);
                    if (distance < best.distance)
                        best = new RoomEdge { a = a, b = b, distance = distance };
                }
            }

            edges.Add(best);
            inTree[best.b] = true;
            included++;
        }
        return edges;
    }

    private static int ManhattanDistance(GeneratedDungeonRoom a, GeneratedDungeonRoom b)
    {
        return Mathf.Abs(a.centerX - b.centerX) + Mathf.Abs(a.centerY - b.centerY);
    }

    private static void AddConnection(GeneratedDungeonMap map, int a, int b, bool[,] connected, GenerationState random)
    {
        if (a < 0 || b < 0 || a >= map.rooms.Count || b >= map.rooms.Count || connected[a, b])
            return;

        GeneratedDungeonRoom from = map.rooms[a];
        GeneratedDungeonRoom to = map.rooms[b];
        GeneratedDungeonConnection connection = new GeneratedDungeonConnection
        {
            fromRoomId = from.roomId,
            toRoomId = to.roomId
        };
        BuildCorridorPath(connection.pathCells, from.centerX, from.centerY, to.centerX, to.centerY, random.RollPercent(50));
        map.connections.Add(connection);
        connected[a, b] = true;
        connected[b, a] = true;
    }

    private static void BuildCorridorPath(List<Vector2Int> path, int startX, int startY, int endX, int endY, bool horizontalFirst)
    {
        int x = startX;
        int y = startY;
        path.Add(new Vector2Int(x, y));
        if (horizontalFirst)
        {
            StepHorizontal(path, ref x, endX, y);
            StepVertical(path, x, ref y, endY);
        }
        else
        {
            StepVertical(path, x, ref y, endY);
            StepHorizontal(path, ref x, endX, y);
        }
    }

    private static void StepHorizontal(List<Vector2Int> path, ref int x, int targetX, int y)
    {
        while (x != targetX)
        {
            x += targetX > x ? 1 : -1;
            path.Add(new Vector2Int(x, y));
        }
    }

    private static void StepVertical(List<Vector2Int> path, int x, ref int y, int targetY)
    {
        while (y != targetY)
        {
            y += targetY > y ? 1 : -1;
            path.Add(new Vector2Int(x, y));
        }
    }

    private static void AnalyzeRooms(GeneratedDungeonMap map)
    {
        List<int>[] adjacency = BuildAdjacency(map);
        int[] parent = new int[map.rooms.Count];
        for (int i = 0; i < parent.Length; i++)
            parent[i] = -1;

        Queue<int> queue = new Queue<int>();
        map.rooms[0].depth = 0;
        parent[0] = 0;
        queue.Enqueue(0);
        while (queue.Count > 0)
        {
            int roomId = queue.Dequeue();
            for (int i = 0; i < adjacency[roomId].Count; i++)
            {
                int next = adjacency[roomId][i];
                if (parent[next] != -1)
                    continue;

                parent[next] = roomId;
                map.rooms[next].depth = map.rooms[roomId].depth + 1;
                queue.Enqueue(next);
            }
        }

        for (int i = 0; i < map.rooms.Count; i++)
            map.rooms[i].isDeadEnd = i != 0 && adjacency[i].Count <= 1;
    }

    private static List<int>[] BuildAdjacency(GeneratedDungeonMap map)
    {
        List<int>[] adjacency = new List<int>[map.rooms.Count];
        for (int i = 0; i < adjacency.Length; i++)
            adjacency[i] = new List<int>();

        for (int i = 0; i < map.connections.Count; i++)
        {
            GeneratedDungeonConnection connection = map.connections[i];
            if (connection.fromRoomId < 0 || connection.toRoomId < 0 || connection.fromRoomId >= map.rooms.Count || connection.toRoomId >= map.rooms.Count)
                continue;

            if (!adjacency[connection.fromRoomId].Contains(connection.toRoomId))
                adjacency[connection.fromRoomId].Add(connection.toRoomId);
            if (!adjacency[connection.toRoomId].Contains(connection.fromRoomId))
                adjacency[connection.toRoomId].Add(connection.fromRoomId);
        }
        return adjacency;
    }

    private static void SelectBossRoom(GeneratedDungeonMap map, MapGenConfigData config, GenerationState random)
    {
        int maxDepth = 0;
        for (int i = 1; i < map.rooms.Count; i++)
            maxDepth = Mathf.Max(maxDepth, map.rooms[i].depth);

        int minDepth = Mathf.Max(1, Mathf.CeilToInt(maxDepth * Mathf.Clamp01(config.minBossDepthRatio)));
        List<GeneratedDungeonRoom> candidates = new List<GeneratedDungeonRoom>();
        for (int i = 1; i < map.rooms.Count; i++)
        {
            if (map.rooms[i].depth >= minDepth)
                candidates.Add(map.rooms[i]);
        }
        if (candidates.Count == 0)
        {
            for (int i = 1; i < map.rooms.Count; i++)
            {
                if (map.rooms[i].depth == maxDepth)
                    candidates.Add(map.rooms[i]);
            }
        }

        candidates.Sort((a, b) => b.depth.CompareTo(a.depth));
        int keepCount = Mathf.Max(1, Mathf.CeilToInt(candidates.Count * Mathf.Clamp(config.bossCandidateTopPercent, 1, 100) / 100f));
        while (candidates.Count > keepCount)
            candidates.RemoveAt(candidates.Count - 1);

        if (config.preferDeadEndBoss)
        {
            List<GeneratedDungeonRoom> deadEnds = new List<GeneratedDungeonRoom>();
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].isDeadEnd)
                    deadEnds.Add(candidates[i]);
            }
            if (deadEnds.Count > 0)
                candidates = deadEnds;
        }

        GeneratedDungeonRoom bossRoom = candidates.Count > 0 ? candidates[random.Range(0, candidates.Count)] : map.rooms[map.rooms.Count - 1];
        bossRoom.isBoss = true;
        bossRoom.contentKind = DungeonMapContentKind.Boss;
        bossRoom.levelType = LevelType.Elite;
        map.bossRoomId = bossRoom.roomId;
        map.bossX = bossRoom.contentX;
        map.bossY = bossRoom.contentY;
        MarkMainPath(map, bossRoom.roomId);
    }

    private static void MarkMainPath(GeneratedDungeonMap map, int bossRoomId)
    {
        List<int>[] adjacency = BuildAdjacency(map);
        int[] parent = new int[map.rooms.Count];
        for (int i = 0; i < parent.Length; i++)
            parent[i] = -1;

        Queue<int> queue = new Queue<int>();
        parent[0] = 0;
        queue.Enqueue(0);
        while (queue.Count > 0 && parent[bossRoomId] == -1)
        {
            int roomId = queue.Dequeue();
            for (int i = 0; i < adjacency[roomId].Count; i++)
            {
                int next = adjacency[roomId][i];
                if (parent[next] != -1)
                    continue;

                parent[next] = roomId;
                queue.Enqueue(next);
            }
        }

        int current = bossRoomId;
        while (current >= 0 && current < map.rooms.Count)
        {
            map.rooms[current].isMainPath = true;
            if (current == 0 || parent[current] == current)
                break;
            current = parent[current];
        }
    }

    private static void AssignRoomContents(GeneratedDungeonMap map, MapGenConfigData config, GenerationState random)
    {
        MapGenLevelTypeRuleData[] rules = config.roomContentRules ?? Array.Empty<MapGenLevelTypeRuleData>();
        for (int i = 0; i < rules.Length; i++)
            AssignFixedRuleCount(map, rules[i], random);

        for (int i = 0; i < map.rooms.Count; i++)
        {
            GeneratedDungeonRoom room = map.rooms[i];
            if (room.isStart || room.isBoss || room.contentKind != DungeonMapContentKind.None)
                continue;

            MapGenLevelTypeRuleData rule = ChooseWeightedRule(room, rules, random);
            room.contentKind = DungeonMapContentKind.Level;
            room.levelType = rule != null ? rule.levelType : LevelType.Battle;
        }
    }

    private static void AssignFixedRuleCount(GeneratedDungeonMap map, MapGenLevelTypeRuleData rule, GenerationState random)
    {
        if (rule == null || rule.minCount <= 0 && rule.maxCount <= 0)
            return;

        int min = Mathf.Max(0, rule.minCount);
        int max = Mathf.Max(min, rule.maxCount);
        int count = RandomRangeInclusive(random, min, max);
        for (int i = 0; i < count; i++)
        {
            List<GeneratedDungeonRoom> candidates = GetContentCandidates(map, rule);
            if (candidates.Count == 0)
                return;

            GeneratedDungeonRoom room = candidates[random.Range(0, candidates.Count)];
            room.contentKind = DungeonMapContentKind.Level;
            room.levelType = rule.levelType;
        }
    }

    private static List<GeneratedDungeonRoom> GetContentCandidates(GeneratedDungeonMap map, MapGenLevelTypeRuleData rule)
    {
        List<GeneratedDungeonRoom> candidates = new List<GeneratedDungeonRoom>();
        for (int i = 0; i < map.rooms.Count; i++)
        {
            GeneratedDungeonRoom room = map.rooms[i];
            if (room.isStart || room.isBoss || room.contentKind != DungeonMapContentKind.None)
                continue;

            if (IsRuleEligible(room, rule))
                candidates.Add(room);
        }
        return candidates;
    }

    private static MapGenLevelTypeRuleData ChooseWeightedRule(GeneratedDungeonRoom room, MapGenLevelTypeRuleData[] rules, GenerationState random)
    {
        int totalWeight = 0;
        for (int i = 0; i < rules.Length; i++)
        {
            MapGenLevelTypeRuleData rule = rules[i];
            if (rule != null && rule.weight > 0 && IsRuleEligible(room, rule))
                totalWeight += rule.weight;
        }
        if (totalWeight <= 0)
            return null;

        int roll = random.Range(0, totalWeight);
        for (int i = 0; i < rules.Length; i++)
        {
            MapGenLevelTypeRuleData rule = rules[i];
            if (rule == null || rule.weight <= 0 || !IsRuleEligible(room, rule))
                continue;

            if (roll < rule.weight)
                return rule;
            roll -= rule.weight;
        }
        return null;
    }

    private static bool IsRuleEligible(GeneratedDungeonRoom room, MapGenLevelTypeRuleData rule)
    {
        if (room.depth < rule.minDepth || room.depth > rule.maxDepth)
            return false;
        if (room.isDeadEnd && !rule.allowOnDeadEnd)
            return false;
        if (room.isMainPath && !rule.allowOnMainPath)
            return false;
        if (!room.isMainPath && !rule.allowOnBranch)
            return false;
        return true;
    }

    private static void BuildCells(GeneratedDungeonMap map)
    {
        map.cells.Clear();
        for (int y = 0; y < map.height; y++)
        {
            for (int x = 0; x < map.width; x++)
            {
                map.cells.Add(new GeneratedDungeonCell
                {
                    x = x,
                    y = y,
                    kind = DungeonMapCellKind.Wall,
                    isAvailable = false
                });
            }
        }

        for (int i = 0; i < map.rooms.Count; i++)
        {
            GeneratedDungeonRoom room = map.rooms[i];
            for (int y = room.y; y < room.y + room.height; y++)
            {
                for (int x = room.x; x < room.x + room.width; x++)
                {
                    GeneratedDungeonCell cell = map.GetCell(x, y);
                    if (cell == null)
                        continue;

                    cell.kind = DungeonMapCellKind.Room;
                    cell.roomId = room.roomId;
                    cell.isAvailable = true;
                }
            }
        }

        for (int i = 0; i < map.connections.Count; i++)
        {
            GeneratedDungeonConnection connection = map.connections[i];
            for (int j = 0; j < connection.pathCells.Count; j++)
            {
                Vector2Int position = connection.pathCells[j];
                GeneratedDungeonCell cell = map.GetCell(position.x, position.y);
                if (cell == null || cell.kind == DungeonMapCellKind.Room)
                    continue;

                cell.kind = DungeonMapCellKind.Corridor;
                cell.isAvailable = true;
            }
        }

        for (int i = 0; i < map.rooms.Count; i++)
        {
            GeneratedDungeonRoom room = map.rooms[i];
            GeneratedDungeonCell contentCell = map.GetCell(room.contentX, room.contentY);
            if (contentCell == null)
                continue;

            contentCell.contentKind = room.contentKind;
            contentCell.levelType = room.levelType;
        }
    }

    private static void AssignCorridorEncounters(GeneratedDungeonMap map, MapGenConfigData config, GenerationState random)
    {
        if (config.corridorEncounterChance <= 0 || config.maxCorridorEncounters <= 0)
            return;

        List<GeneratedDungeonCell> candidates = new List<GeneratedDungeonCell>();
        for (int i = 0; i < map.connections.Count; i++)
        {
            GeneratedDungeonConnection connection = map.connections[i];
            if (connection.fromRoomId < 0 || connection.toRoomId < 0 || connection.fromRoomId >= map.rooms.Count || connection.toRoomId >= map.rooms.Count)
                continue;

            int connectionDepth = Mathf.Min(map.rooms[connection.fromRoomId].depth, map.rooms[connection.toRoomId].depth);
            if (connectionDepth < config.corridorEncounterMinDepth)
                continue;

            for (int j = 0; j < connection.pathCells.Count; j++)
            {
                Vector2Int position = connection.pathCells[j];
                GeneratedDungeonCell cell = map.GetCell(position.x, position.y);
                if (cell != null && cell.kind == DungeonMapCellKind.Corridor && cell.contentKind == DungeonMapContentKind.None && random.RollPercent(config.corridorEncounterChance))
                    candidates.Add(cell);
            }
        }

        int encounterCount = 0;
        while (candidates.Count > 0 && encounterCount < config.maxCorridorEncounters)
        {
            int index = random.Range(0, candidates.Count);
            GeneratedDungeonCell cell = candidates[index];
            candidates.RemoveAt(index);
            if (HasAdjacentEncounter(map, cell.x, cell.y))
                continue;

            cell.contentKind = DungeonMapContentKind.CorridorEncounter;
            cell.levelType = config.corridorEncounterLevelType;
            encounterCount++;
        }
    }

    private static bool HasAdjacentEncounter(GeneratedDungeonMap map, int x, int y)
    {
        return IsEncounter(map.GetCell(x + 1, y)) ||
            IsEncounter(map.GetCell(x - 1, y)) ||
            IsEncounter(map.GetCell(x, y + 1)) ||
            IsEncounter(map.GetCell(x, y - 1));
    }

    private static bool IsEncounter(GeneratedDungeonCell cell)
    {
        return cell != null && cell.contentKind == DungeonMapContentKind.CorridorEncounter;
    }
}
