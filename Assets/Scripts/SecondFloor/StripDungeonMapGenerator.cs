using System;
using System.Collections.Generic;
using UnityEngine;

public static class StripDungeonMapGenerator
{
    private static readonly Vector2Int[] CardinalDirections =
    {
        Vector2Int.up,
        Vector2Int.right,
        Vector2Int.down,
        Vector2Int.left
    };

    private struct PathEdge : IEquatable<PathEdge>
    {
        private readonly Vector2Int first;
        private readonly Vector2Int second;

        public PathEdge(Vector2Int a, Vector2Int b)
        {
            if (a.x < b.x || a.x == b.x && a.y <= b.y)
            {
                first = a;
                second = b;
            }
            else
            {
                first = b;
                second = a;
            }
        }

        public bool Equals(PathEdge other)
        {
            return first == other.first && second == other.second;
        }

        public override bool Equals(object obj)
        {
            return obj is PathEdge other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return first.GetHashCode() * 397 ^ second.GetHashCode();
            }
        }
    }

    public static bool TryGenerate(StripDungeonMapConfig config, int seed, out StripDungeonMap map, out string error)
    {
        map = null;
        if (config == null)
        {
            error = "缺少条带地牢配置。";
            return false;
        }

        if (!config.TryValidate(out error))
            return false;

        int baseSeed = seed == 0 ? 1 : seed;
        for (int attempt = 0; attempt < config.MaxGenerationAttempts; attempt++)
        {
            int attemptSeed = baseSeed + attempt * 7919;
            System.Random random = new System.Random(attemptSeed);
            if (!TryGenerateSingle(config, attemptSeed, random, out map, out string attemptError))
            {
                error = attemptError;
                continue;
            }

            if (Validate(map, config, out attemptError))
            {
                error = null;
                return true;
            }

            error = attemptError;
            map = null;
        }

        error = string.IsNullOrEmpty(error) ? "在最大尝试次数内未能生成满足约束的条带地牢。" : error;
        return false;
    }

    public static bool Validate(StripDungeonMap map, StripDungeonMapConfig config, out string error)
    {
        if (map == null || config == null)
        {
            error = "地图或配置为空。";
            return false;
        }

        if (map.width != config.MapWidth || map.height != config.MapHeight)
        {
            error = "地图尺寸与配置不一致。";
            return false;
        }

        if (map.strips.Count < config.StripCountMin || map.strips.Count > config.StripCountMax)
        {
            error = "生成的条带数量不在配置范围内。";
            return false;
        }

        StripDungeonCell startCell = map.GetCell(map.startPosition);
        if (startCell == null || startCell.kind != StripDungeonCellKind.Path || !startCell.isStart)
        {
            error = "起点不在可走条带上。";
            return false;
        }

        if (!AreAllPathCellsReachable(map))
        {
            error = "存在无法从起点抵达的条带格。";
            return false;
        }

        if (!HasLoop(map))
        {
            error = "地图未形成环结构。";
            return false;
        }

        for (int i = 0; i < map.strips.Count; i++)
        {
            StripDungeonStrip strip = map.strips[i];
            if (strip == null || strip.cells.Count == 0)
            {
                error = "存在空条带。";
                return false;
            }

            if (!HasNonOverlappingContentEndpoint(map, strip))
            {
                error = "存在未在非重叠端点放置普通内容的条带。";
                return false;
            }
        }

        StripDungeonCell boss = map.GetCell(map.bossPosition);
        StripDungeonCell entrance = map.GetCell(map.bossEntrancePosition);
        if (boss == null || boss.kind != StripDungeonCellKind.Boss || entrance == null || entrance.kind != StripDungeonCellKind.Path || !entrance.isBossEntrance)
        {
            error = "Boss 或其入口无效。";
            return false;
        }

        if (ManhattanDistance(map.bossPosition, map.bossEntrancePosition) != 1 || entrance.stripIds.Count != 1 || entrance.stripIds[0] != map.bossHostStripId)
        {
            error = "Boss 必须附着于唯一所属条带的相邻位置。";
            return false;
        }

        if (GetPathDistance(map, map.startPosition, map.bossEntrancePosition) < config.BossMinDistanceFromStart)
        {
            error = "Boss 入口距离起点不足。";
            return false;
        }

        if (!HasTwoEdgeDisjointPaths(map, map.startPosition, map.bossEntrancePosition))
        {
            error = "起点到 Boss 入口不存在两条边不复用路径。";
            return false;
        }

        Dictionary<LevelType, int> contentCounts = new Dictionary<LevelType, int>();
        for (int i = 0; i < map.cells.Count; i++)
        {
            StripDungeonCell cell = map.cells[i];
            if (cell != null && cell.kind == StripDungeonCellKind.Path && cell.isContent)
            {
                contentCounts.TryGetValue(cell.levelType, out int count);
                contentCounts[cell.levelType] = count + 1;
            }
        }

        StripDungeonContentRule[] rules = config.ContentRules;
        for (int i = 0; i < rules.Length; i++)
        {
            StripDungeonContentRule rule = rules[i];
            contentCounts.TryGetValue(rule.levelType, out int count);
            if (count < rule.minCount || count > rule.maxCount)
            {
                error = "普通内容数量未满足配置范围。";
                return false;
            }
        }

        error = null;
        return true;
    }

    private static bool TryGenerateSingle(StripDungeonMapConfig config, int seed, System.Random random, out StripDungeonMap map, out string error)
    {
        map = new StripDungeonMap
        {
            width = config.MapWidth,
            height = config.MapHeight,
            seed = seed
        };

        int targetStripCount = RandomRange(random, config.StripCountMin, config.StripCountMax);
        if (!TryBuildInitialLoop(map, config, random))
        {
            error = "无法在当前尺寸与条带长度配置下放置初始环。";
            return false;
        }

        while (map.strips.Count < targetStripCount)
        {
            if (!TryAddBranchStrip(map, config, random))
            {
                error = "无法放置满足端点内容约束的额外条带。";
                return false;
            }
        }

        StripDungeonCell startCell = map.GetCell(map.startPosition);
        startCell.isStart = true;
        if (!TryPlaceBoss(map, config, random))
        {
            error = "无法选择满足双路径约束的 Boss 入口。";
            return false;
        }

        if (!TryAssignContents(map, config, random))
        {
            error = "当前条带格数量不足以满足端点内容或普通内容配额。";
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryBuildInitialLoop(StripDungeonMap map, StripDungeonMapConfig config, System.Random random)
    {
        int horizontalLength = PickLoopLength(config.StripLengths, map.width, random);
        int verticalLength = PickLoopLength(config.StripLengths, map.height, random);
        if (horizontalLength < 5 || verticalLength < 5)
            return false;

        int leftX = RandomRange(random, 1, map.width - horizontalLength + 1);
        int bottomY = RandomRange(random, 1, map.height - verticalLength + 1);
        int rightX = leftX + horizontalLength - 3;
        int topY = bottomY + verticalLength - 3;

        AddStrip(map, StripDungeonOrientation.Horizontal, new Vector2Int(leftX - 1, bottomY), Vector2Int.right, horizontalLength);
        AddStrip(map, StripDungeonOrientation.Horizontal, new Vector2Int(leftX - 1, topY), Vector2Int.right, horizontalLength);
        AddStrip(map, StripDungeonOrientation.Vertical, new Vector2Int(leftX, bottomY - 1), Vector2Int.up, verticalLength);
        AddStrip(map, StripDungeonOrientation.Vertical, new Vector2Int(rightX, bottomY - 1), Vector2Int.up, verticalLength);
        map.startPosition = new Vector2Int(leftX, bottomY);
        return true;
    }

    private static int PickLoopLength(int[] lengths, int maxLength, System.Random random)
    {
        List<int> candidates = new List<int>();
        for (int i = 0; i < lengths.Length; i++)
        {
            if (lengths[i] >= 5 && lengths[i] <= maxLength)
                candidates.Add(lengths[i]);
        }
        return candidates.Count > 0 ? candidates[random.Next(candidates.Count)] : 0;
    }

    private static bool TryAddBranchStrip(StripDungeonMap map, StripDungeonMapConfig config, System.Random random)
    {
        List<StripDungeonCell> anchors = new List<StripDungeonCell>();
        for (int i = 0; i < map.cells.Count; i++)
        {
            StripDungeonCell cell = map.cells[i];
            if (cell == null || cell.kind != StripDungeonCellKind.Path || cell.isStart || IsStripEndpoint(map, cell.position))
                continue;
            anchors.Add(cell);
        }
        Shuffle(anchors, random);

        List<int> lengths = new List<int>();
        for (int i = 0; i < config.StripLengths.Length; i++)
        {
            if (config.StripLengths[i] >= 2)
                lengths.Add(config.StripLengths[i]);
        }
        Shuffle(lengths, random);

        List<Vector2Int> directions = new List<Vector2Int>(CardinalDirections);
        for (int i = 0; i < anchors.Count; i++)
        {
            Shuffle(directions, random);
            for (int j = 0; j < directions.Count; j++)
            {
                for (int k = 0; k < lengths.Count; k++)
                {
                    Vector2Int anchor = anchors[i].position;
                    Vector2Int direction = directions[j];
                    int length = lengths[k];
                    if (!CanPlaceBranch(map, anchor, direction, length))
                        continue;

                    StripDungeonOrientation orientation = direction.x != 0 ? StripDungeonOrientation.Horizontal : StripDungeonOrientation.Vertical;
                    AddStrip(map, orientation, anchor, direction, length);
                    return true;
                }
            }
        }
        return false;
    }


    private static bool CanPlaceBranch(StripDungeonMap map, Vector2Int start, Vector2Int direction, int length)
    {
        for (int i = 1; i < length; i++)
        {
            Vector2Int position = start + direction * i;
            if (!IsInBounds(map, position) || map.GetCell(position) != null)
                return false;
        }
        return true;
    }

    private static void AddStrip(StripDungeonMap map, StripDungeonOrientation orientation, Vector2Int start, Vector2Int direction, int length)
    {
        StripDungeonStrip strip = new StripDungeonStrip
        {
            id = map.strips.Count,
            orientation = orientation,
            start = start,
            length = length
        };
        map.strips.Add(strip);

        for (int i = 0; i < length; i++)
        {
            Vector2Int position = start + direction * i;
            strip.cells.Add(position);
            StripDungeonCell cell = map.GetCell(position);
            if (cell == null)
            {
                cell = new StripDungeonCell
                {
                    position = position,
                    kind = StripDungeonCellKind.Path
                };
                map.AddCell(cell);
            }

            if (!cell.stripIds.Contains(strip.id))
                cell.stripIds.Add(strip.id);
        }
    }

    private static bool TryPlaceBoss(StripDungeonMap map, StripDungeonMapConfig config, System.Random random)
    {
        List<StripDungeonCell> candidates = new List<StripDungeonCell>();
        for (int i = 0; i < map.cells.Count; i++)
        {
            StripDungeonCell cell = map.cells[i];
            if (cell == null || cell.kind != StripDungeonCellKind.Path || cell.isStart || cell.stripIds.Count != 1 || IsStripEndpoint(map, cell.position))
                continue;

            if (GetPathDistance(map, map.startPosition, cell.position) < config.BossMinDistanceFromStart || !HasTwoEdgeDisjointPaths(map, map.startPosition, cell.position))
                continue;

            if (GetEmptyNeighbors(map, cell.position).Count > 0)
                candidates.Add(cell);
        }

        if (candidates.Count == 0)
            return false;

        StripDungeonCell entrance = candidates[random.Next(candidates.Count)];
        List<Vector2Int> bossPositions = GetEmptyNeighbors(map, entrance.position);
        Vector2Int bossPosition = bossPositions[random.Next(bossPositions.Count)];
        entrance.isBossEntrance = true;
        map.bossEntrancePosition = entrance.position;
        map.bossHostStripId = entrance.stripIds[0];
        map.bossPosition = bossPosition;
        map.AddCell(new StripDungeonCell
        {
            position = bossPosition,
            kind = StripDungeonCellKind.Boss
        });
        return true;
    }

    private static bool TryAssignContents(StripDungeonMap map, StripDungeonMapConfig config, System.Random random)
    {
        StripDungeonContentRule[] rules = config.ContentRules;
        int[] remainingCounts = new int[rules.Length];
        int totalTargetCount = 0;
        for (int i = 0; i < rules.Length; i++)
        {
            remainingCounts[i] = RandomRange(random, rules[i].minCount, rules[i].maxCount);
            totalTargetCount += remainingCounts[i];
        }

        while (totalTargetCount < map.strips.Count)
        {
            int ruleIndex = ChooseRuleWithCapacity(rules, remainingCounts, random);
            if (ruleIndex < 0)
                return false;
            remainingCounts[ruleIndex]++;
            totalTargetCount++;
        }

        List<StripDungeonCell> eligibleCells = GetEligibleContentCells(map);
        if (eligibleCells.Count < totalTargetCount)
            return false;

        for (int i = 0; i < map.strips.Count; i++)
        {
            StripDungeonStrip strip = map.strips[i];
            List<StripDungeonCell> endpointCandidates = new List<StripDungeonCell>(2);
            AddNonOverlappingEndpointIfEligible(map, strip.cells[0], endpointCandidates);
            AddNonOverlappingEndpointIfEligible(map, strip.cells[strip.cells.Count - 1], endpointCandidates);
            if (endpointCandidates.Count == 0)
                return false;

            StripDungeonCell endpoint = endpointCandidates[random.Next(endpointCandidates.Count)];
            int ruleIndex = ChooseRuleWithRemainingCount(rules, remainingCounts, random);
            if (ruleIndex < 0)
                return false;

            endpoint.isContent = true;
            endpoint.levelType = rules[ruleIndex].levelType;
            remainingCounts[ruleIndex]--;
        }

        eligibleCells = GetEligibleContentCells(map);
        for (int i = 0; i < rules.Length; i++)
        {
            while (remainingCounts[i] > 0)
            {
                if (eligibleCells.Count == 0)
                    return false;

                int cellIndex = random.Next(eligibleCells.Count);
                StripDungeonCell cell = eligibleCells[cellIndex];
                eligibleCells.RemoveAt(cellIndex);
                cell.isContent = true;
                cell.levelType = rules[i].levelType;
                remainingCounts[i]--;
            }
        }
        return true;
    }

    private static void AddNonOverlappingEndpointIfEligible(StripDungeonMap map, Vector2Int position, List<StripDungeonCell> candidates)
    {
        StripDungeonCell cell = map.GetCell(position);
        if (cell != null && cell.kind == StripDungeonCellKind.Path && cell.stripIds.Count == 1 && !cell.isStart && !cell.isBossEntrance && !cell.isContent && !candidates.Contains(cell))
            candidates.Add(cell);
    }

    private static bool HasNonOverlappingContentEndpoint(StripDungeonMap map, StripDungeonStrip strip)
    {
        return IsNonOverlappingContentEndpoint(map, strip.cells[0]) || IsNonOverlappingContentEndpoint(map, strip.cells[strip.cells.Count - 1]);
    }

    private static bool IsNonOverlappingContentEndpoint(StripDungeonMap map, Vector2Int position)
    {
        StripDungeonCell cell = map.GetCell(position);
        return cell != null && cell.stripIds.Count == 1 && cell.isContent;
    }

    private static int ChooseRuleWithCapacity(StripDungeonContentRule[] rules, int[] counts, System.Random random)
    {
        int totalWeight = 0;
        for (int i = 0; i < rules.Length; i++)
        {
            if (counts[i] < rules[i].maxCount)
                totalWeight += rules[i].weight;
        }
        if (totalWeight == 0)
            return -1;

        int roll = random.Next(totalWeight);
        for (int i = 0; i < rules.Length; i++)
        {
            if (counts[i] >= rules[i].maxCount)
                continue;
            if (roll < rules[i].weight)
                return i;
            roll -= rules[i].weight;
        }
        return -1;
    }

    private static int ChooseRuleWithRemainingCount(StripDungeonContentRule[] rules, int[] counts, System.Random random)
    {
        int totalWeight = 0;
        for (int i = 0; i < rules.Length; i++)
        {
            if (counts[i] > 0)
                totalWeight += rules[i].weight;
        }
        if (totalWeight == 0)
            return -1;

        int roll = random.Next(totalWeight);
        for (int i = 0; i < rules.Length; i++)
        {
            if (counts[i] <= 0)
                continue;
            if (roll < rules[i].weight)
                return i;
            roll -= rules[i].weight;
        }
        return -1;
    }

    private static List<StripDungeonCell> GetEligibleContentCells(StripDungeonMap map)
    {
        List<StripDungeonCell> eligible = new List<StripDungeonCell>();
        for (int i = 0; i < map.cells.Count; i++)
        {
            StripDungeonCell cell = map.cells[i];
            if (cell != null && cell.kind == StripDungeonCellKind.Path && !cell.isStart && !cell.isBossEntrance && !cell.isContent)
                eligible.Add(cell);
        }
        return eligible;
    }

    private static bool IsStripEndpoint(StripDungeonMap map, Vector2Int position)
    {
        StripDungeonCell cell = map.GetCell(position);
        if (cell == null)
            return false;

        for (int i = 0; i < cell.stripIds.Count; i++)
        {
            StripDungeonStrip strip = map.strips[cell.stripIds[i]];
            if (strip.cells[0] == position || strip.cells[strip.cells.Count - 1] == position)
                return true;
        }
        return false;
    }

    private static bool AreAllPathCellsReachable(StripDungeonMap map)
    {
        Dictionary<Vector2Int, List<Vector2Int>> neighbors = BuildPathNeighbors(map);
        if (!neighbors.ContainsKey(map.startPosition))
            return false;

        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        visited.Add(map.startPosition);
        queue.Enqueue(map.startPosition);
        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            List<Vector2Int> currentNeighbors = neighbors[current];
            for (int i = 0; i < currentNeighbors.Count; i++)
            {
                if (visited.Add(currentNeighbors[i]))
                    queue.Enqueue(currentNeighbors[i]);
            }
        }
        return visited.Count == neighbors.Count;
    }

    private static bool HasLoop(StripDungeonMap map)
    {
        Dictionary<Vector2Int, List<Vector2Int>> neighbors = BuildPathNeighbors(map);
        int edgeCount = 0;
        foreach (KeyValuePair<Vector2Int, List<Vector2Int>> pair in neighbors)
            edgeCount += pair.Value.Count;
        return edgeCount / 2 >= neighbors.Count;
    }

    private static int GetPathDistance(StripDungeonMap map, Vector2Int start, Vector2Int target)
    {
        Dictionary<Vector2Int, List<Vector2Int>> neighbors = BuildPathNeighbors(map);
        if (!neighbors.ContainsKey(start) || !neighbors.ContainsKey(target))
            return -1;

        Dictionary<Vector2Int, int> distances = new Dictionary<Vector2Int, int>();
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        distances[start] = 0;
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            int distance = distances[current];
            if (current == target)
                return distance;

            List<Vector2Int> currentNeighbors = neighbors[current];
            for (int i = 0; i < currentNeighbors.Count; i++)
            {
                Vector2Int next = currentNeighbors[i];
                if (distances.ContainsKey(next))
                    continue;
                distances[next] = distance + 1;
                queue.Enqueue(next);
            }
        }
        return -1;
    }

    private static bool HasTwoEdgeDisjointPaths(StripDungeonMap map, Vector2Int start, Vector2Int target)
    {
        Dictionary<Vector2Int, List<Vector2Int>> neighbors = BuildPathNeighbors(map);
        if (!neighbors.ContainsKey(start) || !neighbors.ContainsKey(target) || start == target)
            return false;

        HashSet<PathEdge> bridges = FindBridges(neighbors);
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
        visited.Add(start);
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            if (current == target)
                return true;

            List<Vector2Int> currentNeighbors = neighbors[current];
            for (int i = 0; i < currentNeighbors.Count; i++)
            {
                Vector2Int next = currentNeighbors[i];
                if (bridges.Contains(new PathEdge(current, next)) || !visited.Add(next))
                    continue;
                queue.Enqueue(next);
            }
        }
        return false;
    }

    private static HashSet<PathEdge> FindBridges(Dictionary<Vector2Int, List<Vector2Int>> neighbors)
    {
        Dictionary<Vector2Int, int> discovery = new Dictionary<Vector2Int, int>();
        Dictionary<Vector2Int, int> low = new Dictionary<Vector2Int, int>();
        HashSet<PathEdge> bridges = new HashSet<PathEdge>();
        int time = 0;
        foreach (KeyValuePair<Vector2Int, List<Vector2Int>> pair in neighbors)
        {
            if (!discovery.ContainsKey(pair.Key))
                FindBridgesDepthFirst(pair.Key, default, false, neighbors, discovery, low, bridges, ref time);
        }
        return bridges;
    }

    private static void FindBridgesDepthFirst(
        Vector2Int current,
        Vector2Int parent,
        bool hasParent,
        Dictionary<Vector2Int, List<Vector2Int>> neighbors,
        Dictionary<Vector2Int, int> discovery,
        Dictionary<Vector2Int, int> low,
        HashSet<PathEdge> bridges,
        ref int time)
    {
        discovery[current] = ++time;
        low[current] = discovery[current];
        List<Vector2Int> currentNeighbors = neighbors[current];
        for (int i = 0; i < currentNeighbors.Count; i++)
        {
            Vector2Int next = currentNeighbors[i];
            if (hasParent && next == parent)
                continue;

            if (!discovery.ContainsKey(next))
            {
                FindBridgesDepthFirst(next, current, true, neighbors, discovery, low, bridges, ref time);
                low[current] = Mathf.Min(low[current], low[next]);
                if (low[next] > discovery[current])
                    bridges.Add(new PathEdge(current, next));
            }
            else
            {
                low[current] = Mathf.Min(low[current], discovery[next]);
            }
        }
    }

    private static Dictionary<Vector2Int, List<Vector2Int>> BuildPathNeighbors(StripDungeonMap map)
    {
        Dictionary<Vector2Int, List<Vector2Int>> neighbors = new Dictionary<Vector2Int, List<Vector2Int>>();
        for (int i = 0; i < map.cells.Count; i++)
        {
            StripDungeonCell cell = map.cells[i];
            if (cell != null && cell.kind == StripDungeonCellKind.Path)
                neighbors[cell.position] = new List<Vector2Int>(4);
        }

        foreach (KeyValuePair<Vector2Int, List<Vector2Int>> pair in neighbors)
        {
            for (int i = 0; i < CardinalDirections.Length; i++)
            {
                Vector2Int neighbor = pair.Key + CardinalDirections[i];
                if (neighbors.ContainsKey(neighbor))
                    pair.Value.Add(neighbor);
            }
        }
        return neighbors;
    }

    private static List<Vector2Int> GetEmptyNeighbors(StripDungeonMap map, Vector2Int position)
    {
        List<Vector2Int> emptyNeighbors = new List<Vector2Int>(4);
        for (int i = 0; i < CardinalDirections.Length; i++)
        {
            Vector2Int neighbor = position + CardinalDirections[i];
            if (IsInBounds(map, neighbor) && map.GetCell(neighbor) == null)
                emptyNeighbors.Add(neighbor);
        }
        return emptyNeighbors;
    }

    private static bool IsInBounds(StripDungeonMap map, Vector2Int position)
    {
        return position.x >= 0 && position.x < map.width && position.y >= 0 && position.y < map.height;
    }

    private static int ManhattanDistance(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private static int RandomRange(System.Random random, int minInclusive, int maxInclusive)
    {
        return random.Next(minInclusive, maxInclusive + 1);
    }

    private static void Shuffle<T>(List<T> values, System.Random random)
    {
        for (int i = values.Count - 1; i > 0; i--)
        {
            int other = random.Next(i + 1);
            T value = values[i];
            values[i] = values[other];
            values[other] = value;
        }
    }
}
