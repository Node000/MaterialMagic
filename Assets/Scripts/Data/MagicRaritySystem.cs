using System;
using System.Collections.Generic;
using UnityEngine;

public static class MagicRaritySystem
{
    public const float DefaultOwnedMagicWeightMultiplier = 0.25f;
    private const int RandomPrecision = 1000000;

    public static MagicData SelectWeightedMagic(IList<MagicData> candidates, Func<int, int, int> nextRandomInt,
        IReadOnlyList<MagicModel> ownedMagic = null, float ownedWeightMultiplier = DefaultOwnedMagicWeightMultiplier)
    {
        if (candidates == null || candidates.Count == 0)
            return null;

        float commonWeight = GetAvailableWeight(candidates, MagicRarity.Common);
        float rareWeight = GetAvailableWeight(candidates, MagicRarity.Rare);
        float epicWeight = GetAvailableWeight(candidates, MagicRarity.Epic);
        float legendaryWeight = GetAvailableWeight(candidates, MagicRarity.Legendary);
        float totalWeight = commonWeight + rareWeight + epicWeight + legendaryWeight;

        if (totalWeight <= 0f)
            return SelectMagic(candidates, null, nextRandomInt, ownedMagic, ownedWeightMultiplier);

        float roll = NextRandomInt(nextRandomInt, 0, RandomPrecision) / (float)RandomPrecision * totalWeight;
        if (roll < commonWeight)
            return SelectMagicOfRarity(candidates, MagicRarity.Common, nextRandomInt, ownedMagic, ownedWeightMultiplier);
        roll -= commonWeight;
        if (roll < rareWeight)
            return SelectMagicOfRarity(candidates, MagicRarity.Rare, nextRandomInt, ownedMagic, ownedWeightMultiplier);
        roll -= rareWeight;
        if (roll < epicWeight)
            return SelectMagicOfRarity(candidates, MagicRarity.Epic, nextRandomInt, ownedMagic, ownedWeightMultiplier);
        return SelectMagicOfRarity(candidates, MagicRarity.Legendary, nextRandomInt, ownedMagic, ownedWeightMultiplier);
    }

    public static Color GetBorderColor(MagicData magicData, Color fallback)
    {
        return magicData != null ? GetBorderColor(magicData.rarity, fallback) : fallback;
    }

    public static Color GetBorderColor(MagicRarity rarity, Color fallback)
    {
        if (!GameDataDatabase.TryGetMagicRarityData(rarity, out MagicRarityData data) || data == null || string.IsNullOrEmpty(data.borderColor))
            return fallback;

        return ColorUtility.TryParseHtmlString(data.borderColor, out Color color) ? color : fallback;
    }

    private static float GetAvailableWeight(IList<MagicData> candidates, MagicRarity rarity)
    {
        if (!HasCandidate(candidates, rarity))
            return 0f;

        float weight = GetBaseWeight(rarity);
        return DifficultyUpgradeSystem.ModifyMagicRarityWeight(rarity, weight);
    }

    private static float GetBaseWeight(MagicRarity rarity)
    {
        if (!GameDataDatabase.TryGetMagicRarityData(rarity, out MagicRarityData data) || data == null)
            return rarity == MagicRarity.Common ? 1f : 0f;

        return Mathf.Max(0f, data.weight);
    }

    private static bool HasCandidate(IList<MagicData> candidates, MagicRarity rarity)
    {
        for (int i = 0; i < candidates.Count; i++)
        {
            MagicData candidate = candidates[i];
            if (candidate != null && candidate.rarity == rarity)
                return true;
        }
        return false;
    }

    /// <summary>保底也复用同品质内持有降权，不改变品质抽取概率。</summary>
    public static MagicData SelectMagicOfRarity(IList<MagicData> candidates, MagicRarity rarity,
        Func<int, int, int> nextRandomInt, IReadOnlyList<MagicModel> ownedMagic = null,
        float ownedWeightMultiplier = DefaultOwnedMagicWeightMultiplier)
    {
        return SelectMagic(candidates, rarity, nextRandomInt, ownedMagic, ownedWeightMultiplier);
    }

    private static MagicData SelectMagic(IList<MagicData> candidates, MagicRarity? rarity,
        Func<int, int, int> nextRandomInt, IReadOnlyList<MagicModel> ownedMagic, float ownedWeightMultiplier)
    {
        if (candidates == null)
            return null;

        float multiplier = float.IsNaN(ownedWeightMultiplier) ? DefaultOwnedMagicWeightMultiplier : Mathf.Clamp01(ownedWeightMultiplier);
        int count = 0;
        int ownedCount = 0;
        for (int i = 0; i < candidates.Count; i++)
        {
            MagicData candidate = candidates[i];
            if (candidate == null || (rarity.HasValue && candidate.rarity != rarity.Value))
                continue;
            count++;
            if (IsOwned(candidate, ownedMagic))
                ownedCount++;
        }
        if (count == 0)
            return null;

        // 无持有、全持有或倍率为1时保留等概率抽取，也兼容全持有且倍率为0的空权重情况。
        if (ownedCount == 0 || ownedCount == count || multiplier == 1f)
        {
            int selectedIndex = NextRandomInt(nextRandomInt, 0, count);
            for (int i = 0; i < candidates.Count; i++)
            {
                MagicData candidate = candidates[i];
                if (candidate == null || (rarity.HasValue && candidate.rarity != rarity.Value))
                    continue;
                if (selectedIndex-- == 0)
                    return candidate;
            }
            return null;
        }

        float totalWeight = count - ownedCount + ownedCount * multiplier;
        float roll = NextRandomInt(nextRandomInt, 0, RandomPrecision) / (float)RandomPrecision * totalWeight;
        MagicData lastEligible = null;
        for (int i = 0; i < candidates.Count; i++)
        {
            MagicData candidate = candidates[i];
            if (candidate == null || (rarity.HasValue && candidate.rarity != rarity.Value))
                continue;
            float weight = IsOwned(candidate, ownedMagic) ? multiplier : 1f;
            if (weight <= 0f)
                continue;
            lastEligible = candidate;
            if (roll < weight)
                return candidate;
            roll -= weight;
        }
        return lastEligible;
    }

    private static bool IsOwned(MagicData candidate, IReadOnlyList<MagicModel> ownedMagic)
    {
        for (int i = 0; ownedMagic != null && i < ownedMagic.Count; i++)
        {
            MagicData data = ownedMagic[i]?.Data;
            if (data == null)
                continue;
            if (candidate.numericId > 0 && data.numericId > 0)
            {
                if (candidate.numericId == data.numericId)
                    return true;
            }
            else if (!string.IsNullOrEmpty(candidate.id) && candidate.id == data.id)
                return true;
        }
        return false;
    }

    private static int NextRandomInt(Func<int, int, int> nextRandomInt, int minInclusive, int maxExclusive)
    {
        return nextRandomInt != null ? nextRandomInt(minInclusive, maxExclusive) : UnityEngine.Random.Range(minInclusive, maxExclusive);
    }
}
