using System;

/// <summary>Full production-content start/map adapter; uses shared start-config and first-floor generation rules.</summary>
public static class GameSessionFactory
{
    public static GameSessionOptions Production(int seed, IRuleContentProvider content, RuleProfilePolicy profile,
        string startConfigId = "balanced", int chapterId = 0, string contentVersion = null, RunDifficultySaveData difficulty = null)
    {
        if (content == null) throw new ArgumentNullException(nameof(content));
        if (!content.Named<PlayerStartConfigData>().TryGetValue(startConfigId, out PlayerStartConfigData initial))
            throw new ArgumentException("Missing start configuration: " + startConfigId, nameof(startConfigId));
        return new GameSessionOptions
        {
            Seed = seed, RuleContent = content, Profile = profile ?? RuleProfilePolicy.Empty,
            Services = PlayerStateServices.Simulation, Difficulty = difficulty,
            MaxHealth = initial.maxHealth, StartingGold = initial.gold,
            CreateContent = player =>
            {
                EconomyConfigData economy = null;
                foreach (EconomyConfigData configured in player.Rules.Content.Numeric<EconomyConfigData>().Values) { economy = configured; break; }
                player.Rules.Content.Numeric<LevelData>().TryGetValue(401, out LevelData automaticShop);
                return new GameSessionContent { Version = contentVersion ?? string.Empty, Economy = economy, AutomaticShopLevel = automaticShop };
            },
            InitializePlayer = player =>
            {
                if (!player.Rules.Content.Named<PlayerStartConfigData>().TryGetValue(startConfigId, out PlayerStartConfigData start))
                    throw new InvalidOperationException("Missing scoped start configuration: " + startConfigId);
                PlayerState.ApplyStartConfig(player, start);
            },
            ConfigureRun = run =>
            {
                ChapterData chapter = null;
                if (chapterId != 0) run.PlayerStatus.Rules.Content.Numeric<ChapterData>().TryGetValue(chapterId, out chapter);
                else foreach (ChapterData candidate in run.PlayerStatus.Rules.Content.Numeric<ChapterData>().Values)
                    if (candidate.numericId != TutorialPolicy.TutorialChapterNumericId) { chapter = candidate; break; }
                if (chapter == null) throw new InvalidOperationException("Missing run chapter: " + chapterId);
                RunMapGenerationResult generated = RunMapGenerationRules.Generate(run, chapter);
                if (!generated.Succeeded) throw new InvalidOperationException("Production map generation found no eligible levels.");
            }
        };
    }
}
