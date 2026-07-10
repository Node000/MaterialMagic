using System;
using System.Collections.Generic;
using UnityEngine;

public static class MagicRaritySystem
{
    private const int RandomPrecision = 1000000;

    public static MagicData SelectWeightedMagic(IList<MagicData> candidates, Func<int, int, int> nextRandomInt)
    {
        if (candidates == null || candidates.Count == 0)
            return null;

        float commonWeight = GetAvailableWeight(candidates, MagicRarity.Common);
        float rareWeight = GetAvailableWeight(candidates, MagicRarity.Rare);
        float epicWeight = GetAvailableWeight(candidates, MagicRarity.Epic);
        float legendaryWeight = GetAvailableWeight(candidates, MagicRarity.Legendary);
        float totalWeight = commonWeight + rareWeight + epicWeight + legendaryWeight;

        if (totalWeight <= 0f)
            return SelectUniformMagic(candidates, nextRandomInt);

        float roll = NextRandomInt(nextRandomInt, 0, RandomPrecision) / (float)RandomPrecision * totalWeight;
        if (roll < commonWeight)
            return SelectUniformMagic(candidates, MagicRarity.Common, nextRandomInt);
        roll -= commonWeight;
        if (roll < rareWeight)
            return SelectUniformMagic(candidates, MagicRarity.Rare, nextRandomInt);
        roll -= rareWeight;
        if (roll < epicWeight)
            return SelectUniformMagic(candidates, MagicRarity.Epic, nextRandomInt);
        return SelectUniformMagic(candidates, MagicRarity.Legendary, nextRandomInt);
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

    private static MagicData SelectUniformMagic(IList<MagicData> candidates, MagicRarity rarity, Func<int, int, int> nextRandomInt)
    {
        int count = 0;
        for (int i = 0; i < candidates.Count; i++)
        {
            MagicData candidate = candidates[i];
            if (candidate != null && candidate.rarity == rarity)
                count++;
        }

        if (count <= 0)
            return null;

        int selectedIndex = NextRandomInt(nextRandomInt, 0, count);
        for (int i = 0; i < candidates.Count; i++)
        {
            MagicData candidate = candidates[i];
            if (candidate == null || candidate.rarity != rarity)
                continue;

            if (selectedIndex == 0)
                return candidate;
            selectedIndex--;
        }
        return null;
    }

    private static MagicData SelectUniformMagic(IList<MagicData> candidates, Func<int, int, int> nextRandomInt)
    {
        int index = NextRandomInt(nextRandomInt, 0, candidates.Count);
        return candidates[index];
    }

    private static int NextRandomInt(Func<int, int, int> nextRandomInt, int minInclusive, int maxExclusive)
    {
        return nextRandomInt != null ? nextRandomInt(minInclusive, maxExclusive) : UnityEngine.Random.Range(minInclusive, maxExclusive);
    }
}
