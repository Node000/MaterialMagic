using System;

/// <summary>Production node-content rules shared by HandSystemUI adapters and GameSession.</summary>
public static class RunLevelRules
{
    /// <summary>
    /// Existing node-entry/battle-exit pile reset. No discard hooks or RNG: persistent Deck is the
    /// source of the next node's draw pile; transient piles cannot leak across node boundaries.
    /// Presentation adapters clear their own consumed-view bookkeeping separately.
    /// </summary>
    public static void ResetNodeDeck(PlayerState player)
    {
        if (player == null) throw new ArgumentNullException(nameof(player));
        using (player.Rules.Enter())
        {
            player.RestoreBattleDrawOrder(0, 0);
            player.Hand.Clear();
            player.PlayZone.Clear();
            player.DrawPile.Clear();
            player.DiscardPile.Clear();
            player.ConsumedPile.Clear();
            player.DrawPile.AddRange(player.Deck);
        }
    }

    public static LevelData ResolveSelectedLevel(RunManager run, LevelData level, bool isBoss)
    {
        if (run == null || level == null || isBoss) return level;
        ChapterData chapter = run.ActiveChapter;
        if (level.levelType == LevelType.Battle && HasTieredBattlePools(chapter))
            return run.DrawNextBattleLevel(chapter);
        if (level.levelType == LevelType.Event && chapter != null && Has(chapter.EventPool))
            return run.DrawEventLevel(chapter) ?? level;
        if (level.levelType == LevelType.Elite)
            return run.DrawEliteLevel(chapter) ?? level;
        return level;
    }

    public static LevelEnemyGroupData SelectEnemyGroup(RunManager run, LevelData level)
    {
        if (level?.randomEnemyGroups == null || level.randomEnemyGroups.Length == 0) return null;
        if (run == null) throw new ArgumentNullException(nameof(run));
        int forced = run.ResolveChapterBossGroupIndex(level);
        int index = forced >= 0 && forced < level.randomEnemyGroups.Length
            ? forced : run.NextRandomInt(0, level.randomEnemyGroups.Length);
        return level.randomEnemyGroups[index];
    }

    public static EventModel CreateRestEvent(RunManager run, LevelData level, int drawCount)
    {
        if (run == null) throw new ArgumentNullException(nameof(run));
        return EventRestRules.CreateModel(level, drawCount, run.NextRandomInt);
    }

    public static bool ShouldActivateBossMap(RunManager run, int chapterLength, bool isTutorial)
    {
        RunMapGridModel grid = run?.MapGrid;
        return grid != null && grid.CellCount > 0 && !grid.HasFixedBossPath && !grid.bossMapActive
            && !isTutorial && run.CurrentMapNodeIndex >= Math.Max(1, chapterLength) - 1;
    }

    public static LevelData ResolveMapCellLevel(RunManager run, RunMapCellModel cell)
    {
        if (run == null || cell == null || cell.isBoss && cell.isCompleted) return null;
        return cell.isBoss || run.MapGrid.bossMapActive
            ? run.ResolveChapterBoss(run.ActiveChapter) ?? cell.level : cell.level;
    }

    internal static bool HasTieredBattlePools(ChapterData chapter) => chapter != null &&
        (Has(chapter.BeginPool) || Has(chapter.MidPool) || Has(chapter.NormalPool));

    private static bool Has(int[] values) => values != null && values.Length > 0;
}
