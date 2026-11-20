using System;
using System.Collections.Generic;

/// <summary>Content/policy inputs for the production reward generator. No scene or UI dependency.</summary>
public sealed class RewardGenerationServices
{
    public Func<int, int, int> NextRandomInt;
    public Func<int, MagicData> FindMagic = id => GameDataDatabase.TryGetMagicData(id, out MagicData data) ? data : null;
    public Func<int, RewardPoolData> FindRewardPool = id => GameDataDatabase.TryGetRewardPoolData(id, out RewardPoolData data) ? data : null;
    public Func<IEnumerable<MagicData>> AllMagic = () => GameDataDatabase.MagicData.Values;
    public Func<MagicData, bool> IsMagicUnlocked = UnlockSystem.IsMagicUnlocked;
    public Func<bool> EliteLegendaryGuarantee = DifficultyUpgradeSystem.HasEliteGuaranteedLegendaryReward;
    public Func<int, IReadOnlyList<MaterialModifierData>> MaterialModifierChoices;
    public Func<int, int> ModifyGold = DifficultyUpgradeSystem.ModifyGoldGain;
    public Func<int, int> ModifyGoldChoiceCount = DifficultyUpgradeSystem.ModifyBattleRewardGoldChoiceCount;
    public Action<string> ReportInsufficientPool;
}

/// <summary>Shared pool, rarity, owned-count weight, elite guarantee, arrow and gold rules.</summary>
public sealed class RewardOfferGenerator
{
    private readonly PlayerState owner;
    private readonly LevelData level;
    private readonly ChapterData chapter;
    private readonly bool boss;
    private readonly EconomyConfigData economy;
    private readonly RewardGenerationServices services;

    public RewardOfferGenerator(PlayerState owner, LevelData level = null, ChapterData chapter = null,
        bool boss = false, EconomyConfigData economy = null, RewardGenerationServices services = null)
    {
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
        this.level = level; this.chapter = chapter; this.boss = boss;
        this.economy = economy;
        this.services = services ?? new RewardGenerationServices();
        if (this.services.NextRandomInt == null) this.services.NextRandomInt = (min, max) => ChoiceRuleRandom.Next(owner, min, max);
        if (this.services.MaterialModifierChoices == null)
        {
            var events = new EventRuleServices(owner) { NextRandomInt = this.services.NextRandomInt };
            this.services.MaterialModifierChoices = events.MaterialModifierChoices;
        }
    }

    public List<MagicData> GetMagicChoices(int count)
    {
        using (owner.Rules.Enter())
        {
            RewardPoolData rewardPool = level != null && level.rewardPoolId > 0 ? services.FindRewardPool(level.rewardPoolId) : null;
            List<MagicData> pool = BuildMagicPool(rewardPool);
            if (pool.Count < count && rewardPool?.allowedRarities != null && rewardPool.allowedRarities.Length > 0)
                services.ReportInsufficientPool?.Invoke("Reward pool '" + rewardPool.id + "' has only " + pool.Count + " eligible magic rewards for " + count + " choices.");
            var result = new List<MagicData>();
            while (result.Count < count && pool.Count > 0)
            {
                MagicData selected = MagicRaritySystem.SelectWeightedMagic(pool, services.NextRandomInt, owner.MagicBook, OwnedWeight);
                if (selected == null) break;
                pool.Remove(selected); result.Add(selected);
            }
            if (level?.levelType == LevelType.Elite && services.EliteLegendaryGuarantee())
            {
                if (!result.Exists(data => data != null && data.rarity == MagicRarity.Legendary) && result.Count > 0)
                {
                    MagicData legendary = MagicRaritySystem.SelectMagicOfRarity(pool, MagicRarity.Legendary, services.NextRandomInt, owner.MagicBook, OwnedWeight);
                    if (legendary != null) result[services.NextRandomInt(0, result.Count)] = legendary;
                }
            }
            return result;
        }
    }

    public MagicData RollBattleMagic()
    {
        using (owner.Rules.Enter())
        {
            RewardPoolData rewardPool = level != null && level.rewardPoolId > 0 ? services.FindRewardPool(level.rewardPoolId) : null;
            List<MagicData> pool = BuildMagicPool(rewardPool);
            if (pool.Count == 0) return null;
            if (level?.levelType == LevelType.Elite && services.EliteLegendaryGuarantee())
            {
                MagicData legendary = MagicRaritySystem.SelectMagicOfRarity(pool, MagicRarity.Legendary, services.NextRandomInt, owner.MagicBook, OwnedWeight);
                if (legendary != null) return legendary;
            }
            return MagicRaritySystem.SelectWeightedMagic(pool, services.NextRandomInt, owner.MagicBook, OwnedWeight);
        }
    }

    public List<RewardArrowOption> GetArrowChoices(int count)
    {
        using (owner.Rules.Enter())
        {
            var result = new List<RewardArrowOption>();
            float chance = Math.Max(0f, Math.Min(1f, economy != null ? economy.battleArrowRewardEnchantChance : 0f));
            int threshold = (int)Math.Round(chance * 10000f, MidpointRounding.ToEven);
            for (int i = 0; i < count; i++)
            {
                MaterialEnum material = (MaterialEnum)services.NextRandomInt((int)MaterialEnum.Fire, (int)MaterialEnum.Earth + 1);
                MaterialModifierData modifier = null;
                if (threshold > 0 && services.NextRandomInt(0, 10000) < threshold)
                {
                    IReadOnlyList<MaterialModifierData> choices = services.MaterialModifierChoices(1);
                    if (choices != null && choices.Count > 0) modifier = choices[0];
                }
                result.Add(new RewardArrowOption { material = material, modifierData = modifier });
            }
            return result;
        }
    }

    public int BaseGold() { using (owner.Rules.Enter()) return services.ModifyGold(2); }
    public int BattleGoldChoice()
    {
        using (owner.Rules.Enter())
        {
            if (economy == null) return 2;
            int value = economy.battleGoldMin;
            if (level != null)
            {
                bool elite = level.levelType == LevelType.Elite || Contains(chapter?.ElitePool, level.numericId);
                if (boss || Contains(chapter?.BossPool, level.numericId) || elite) value = economy.eliteBattleGoldMin;
                else if (Contains(chapter?.BeginPool, level.numericId)) value = economy.weakBattleGold > 0 ? economy.weakBattleGold : economy.battleGoldMin;
                else if (Contains(chapter?.MidPool, level.numericId)) value = economy.battleGoldMin;
                else if (Contains(chapter?.NormalPool, level.numericId)) value = economy.strongBattleGold > 0 ? economy.strongBattleGold : economy.battleGoldMin;
            }
            return services.ModifyGold(value);
        }
    }
    public BattleRewardChoices GenerateBattleChoices(int? alreadyRolledGold = null)
    {
        using (owner.Rules.Enter())
        {
            List<RewardArrowOption> arrows;
            // Match production order: gold is supplied/rolled, then magic, then arrow.
            int gold = alreadyRolledGold ?? BattleGoldChoice();
            MagicData magic = RollBattleMagic(); arrows = GetArrowChoices(1);
            return new BattleRewardChoices { GoldAmount = services.ModifyGoldChoiceCount(1) > 0 ? Math.Max(0, gold) : 0,
                Magic = magic, Arrow = arrows.Count > 0 ? arrows[0] : null };
        }
    }

    private float OwnedWeight => economy != null ? economy.ownedMagicWeightMultiplier : MagicRaritySystem.DefaultOwnedMagicWeightMultiplier;
    private List<MagicData> BuildMagicPool(RewardPoolData rewardPool)
    {
        var pool = new List<MagicData>();
        for (int i = 0; rewardPool?.magicIds != null && i < rewardPool.magicIds.Length; i++)
        {
            MagicData data = services.FindMagic(rewardPool.magicIds[i]);
            if (data != null && services.IsMagicUnlocked(data) && Allowed(data, rewardPool.allowedRarities)) pool.Add(data);
        }
        if (pool.Count == 0 && (rewardPool?.allowedRarities == null || rewardPool.allowedRarities.Length == 0))
            foreach (MagicData data in services.AllMagic()) if (data != null && services.IsMagicUnlocked(data)) pool.Add(data);
        return pool;
    }
    private static bool Allowed(MagicData data, MagicRarity[] rarities)
    {
        if (rarities == null || rarities.Length == 0) return true;
        for (int i = 0; i < rarities.Length; i++) if (rarities[i] == data.rarity) return true;
        return false;
    }
    private static bool Contains(int[] ids, int id)
    {
        for (int i = 0; ids != null && i < ids.Length; i++) if (ids[i] == id) return true;
        return false;
    }
}
