using System;
using System.Collections.Generic;
using UnityEngine;

public static class SecondFloorPCMapGenerator
{
    private struct Region
    {
        public int index;
        public int offsetX;
        public int offsetY;
        public bool connectsRight;
        public bool connectsUp;
    }

    private static readonly Region[] Regions =
    {
        new Region { index = 0, offsetX = 0, offsetY = 0, connectsRight = true, connectsUp = true },
        new Region { index = 1, offsetX = 1, offsetY = 0, connectsUp = true },
        new Region { index = 2, offsetX = 0, offsetY = 1, connectsRight = true },
        new Region { index = 3, offsetX = 1, offsetY = 1 }
    };

    private static readonly Vector2Int[] CardinalDirections =
    {
        Vector2Int.up,
        Vector2Int.right,
        Vector2Int.down,
        Vector2Int.left
    };

    public static bool TryGenerate(SecondFloorPCMapConfig config, int seed, Func<LevelType, LevelData> levelResolver, LevelData bossLevel, out RunMapGridModel grid, out string error)
    {
        grid = null;
        if (config == null)
        {
            error = "缺少第二层 PC 地图配置。";
            return false;
        }

        if (!config.TryValidate(out error))
            return false;

        if (levelResolver == null || bossLevel == null)
        {
            error = "第二层地图缺少关卡内容。";
            return false;
        }

        System.Random random = new System.Random(seed == 0 ? 1 : seed);
        grid = new RunMapGridModel
        {
            width = config.MapWidth,
            height = config.MapHeight
        };

        HashSet<Vector2Int> pathCells = new HashSet<Vector2Int>();
        List<Vector2Int>[] regionCells = { new List<Vector2Int>(), new List<Vector2Int>(), new List<Vector2Int>(), new List<Vector2Int>() };
        HashSet<Vector2Int> connectorCells = new HashSet<Vector2Int>();

        for (int i = 0; i < Regions.Length; i++)
            AddRegionStrips(config, Regions[i], pathCells, regionCells[Regions[i].index], connectorCells, random);

        AddEliteConnector(grid, pathCells, connectorCells, new Vector2Int(config.RegionWidth, config.RegionHeight / 2), levelResolver(LevelType.Elite));
        AddEliteConnector(grid, pathCells, connectorCells, new Vector2Int(config.RegionWidth, config.RegionHeight + 1 + config.RegionHeight / 2), levelResolver(LevelType.Elite));
        AddEliteConnector(grid, pathCells, connectorCells, new Vector2Int(config.RegionWidth / 2, config.RegionHeight), levelResolver(LevelType.Elite));
        AddEliteConnector(grid, pathCells, connectorCells, new Vector2Int(config.RegionWidth + 1 + config.RegionWidth / 2, config.RegionHeight), levelResolver(LevelType.Elite));

        int spawnRegion = random.Next(Regions.Length);
        int bossRegion = OppositeRegion(spawnRegion);
        Vector2Int start = PickCell(regionCells[spawnRegion], connectorCells, random);
        Vector2Int boss = PickFarthestCell(regionCells[bossRegion], connectorCells, start);
        if (start == boss)
        {
            error = "无法为第二层选择分离的出生格与 Boss 格。";
            return false;
        }

        for (int i = 0; i < pathCells.Count; i++)
        {
        }

        foreach (Vector2Int position in pathCells)
        {
            if (grid.GetCell(position.x, position.y) != null)
                continue;

            bool isStart = position == start;
            bool isBoss = position == boss;
            LevelData level = isStart || isBoss ? null : levelResolver(ChooseContentType(config.ContentRules, random));
            if (!isStart && !isBoss && level == null)
            {
                error = "第二层地图无法解析普通格关卡。";
                grid = null;
                return false;
            }

            grid.cells.Add(new RunMapCellModel
            {
                x = position.x,
                y = position.y,
                level = level,
                isBoss = isBoss,
                isEnd = isBoss,
                isAvailable = true,
                isRevealed = isBoss
            });
        }

        RunMapCellModel startCell = grid.GetCell(start.x, start.y);
        startCell.level = null;
        grid.playerX = start.x;
        grid.playerY = start.y;

        RunMapCellModel bossCell = grid.GetCell(boss.x, boss.y);
        bossCell.level = bossLevel;
        bossCell.isBoss = true;
        bossCell.isEnd = true;
        bossCell.isRevealed = true;

        if (grid.cells.Count == 0 || !AreAllCellsReachable(grid))
        {
            error = "第二层地图存在不可抵达的条带格。";
            grid = null;
            return false;
        }

        error = null;
        return true;
    }

    private static void AddRegionStrips(SecondFloorPCMapConfig config, Region region, HashSet<Vector2Int> pathCells, List<Vector2Int> regionCells, HashSet<Vector2Int> connectorCells, System.Random random)
    {
        int offsetX = region.offsetX * (config.RegionWidth + 1);
        int offsetY = region.offsetY * (config.RegionHeight + 1);
        int left = offsetX + 1;
        int right = offsetX + config.RegionWidth - 2;
        int bottom = offsetY + 1;
        int top = offsetY + config.RegionHeight - 2;

        AddStrip(pathCells, regionCells, new Vector2Int(left, bottom), new Vector2Int(right, bottom));
        AddStrip(pathCells, regionCells, new Vector2Int(left, top), new Vector2Int(right, top));
        AddStrip(pathCells, regionCells, new Vector2Int(left, bottom), new Vector2Int(left, top));
        AddStrip(pathCells, regionCells, new Vector2Int(right, bottom), new Vector2Int(right, top));

        int horizontalBridgeY = offsetY + config.RegionHeight / 2;
        int verticalBridgeX = offsetX + config.RegionWidth / 2;
        if (region.connectsRight)
            AddStrip(pathCells, regionCells, new Vector2Int(right, horizontalBridgeY), new Vector2Int(offsetX + config.RegionWidth, horizontalBridgeY));
        else
            AddStrip(pathCells, regionCells, new Vector2Int(offsetX - 1, horizontalBridgeY), new Vector2Int(left, horizontalBridgeY));

        if (region.connectsUp)
            AddStrip(pathCells, regionCells, new Vector2Int(verticalBridgeX, top), new Vector2Int(verticalBridgeX, offsetY + config.RegionHeight));
        else
            AddStrip(pathCells, regionCells, new Vector2Int(verticalBridgeX, offsetY - 1), new Vector2Int(verticalBridgeX, bottom));
    }

    private static void AddStrip(HashSet<Vector2Int> pathCells, List<Vector2Int> regionCells, Vector2Int from, Vector2Int to)
    {
        Vector2Int direction = new Vector2Int(Math.Sign(to.x - from.x), Math.Sign(to.y - from.y));
        int length = Mathf.Abs(to.x - from.x) + Mathf.Abs(to.y - from.y);
        for (int i = 0; i <= length; i++)
        {
            Vector2Int position = from + direction * i;
            if (pathCells.Add(position))
                regionCells.Add(position);
        }
    }

    private static void AddEliteConnector(RunMapGridModel grid, HashSet<Vector2Int> pathCells, HashSet<Vector2Int> connectorCells, Vector2Int position, LevelData eliteLevel)
    {
        pathCells.Add(position);
        connectorCells.Add(position);
        grid.cells.Add(new RunMapCellModel
        {
            x = position.x,
            y = position.y,
            level = eliteLevel,
            isAvailable = true,
            isRevealed = true
        });
    }

    private static Vector2Int PickCell(List<Vector2Int> cells, HashSet<Vector2Int> excluded, System.Random random)
    {
        List<Vector2Int> candidates = new List<Vector2Int>();
        for (int i = 0; i < cells.Count; i++)
        {
            if (!excluded.Contains(cells[i]))
                candidates.Add(cells[i]);
        }
        return candidates[random.Next(candidates.Count)];
    }

    private static Vector2Int PickFarthestCell(List<Vector2Int> cells, HashSet<Vector2Int> excluded, Vector2Int start)
    {
        Vector2Int result = cells[0];
        int bestDistance = int.MinValue;
        for (int i = 0; i < cells.Count; i++)
        {
            Vector2Int candidate = cells[i];
            if (excluded.Contains(candidate))
                continue;
            int distance = Mathf.Abs(candidate.x - start.x) + Mathf.Abs(candidate.y - start.y);
            if (distance > bestDistance)
            {
                bestDistance = distance;
                result = candidate;
            }
        }
        return result;
    }

    private static LevelType ChooseContentType(StripDungeonContentRule[] rules, System.Random random)
    {
        int totalWeight = 0;
        for (int i = 0; i < rules.Length; i++)
            totalWeight += rules[i].weight;

        int roll = random.Next(totalWeight);
        for (int i = 0; i < rules.Length; i++)
        {
            if (roll < rules[i].weight)
                return rules[i].levelType;
            roll -= rules[i].weight;
        }
        return rules[0].levelType;
    }

    private static bool AreAllCellsReachable(RunMapGridModel grid)
    {
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        Vector2Int start = new Vector2Int(grid.playerX, grid.playerY);
        visited.Add(start);
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            for (int i = 0; i < CardinalDirections.Length; i++)
            {
                Vector2Int next = current + CardinalDirections[i];
                if (grid.GetCell(next.x, next.y) != null && visited.Add(next))
                    queue.Enqueue(next);
            }
        }
        return visited.Count == grid.cells.Count;
    }

    private static int OppositeRegion(int region)
    {
        return region == 0 ? 3 : region == 1 ? 2 : region == 2 ? 1 : 0;
    }

    private static int RandomRange(System.Random random, int minInclusive, int maxInclusive)
    {
        return random.Next(minInclusive, maxInclusive + 1);
    }
}
