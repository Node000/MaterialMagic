using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class MagicOwnershipWeightTests
{
    private static MagicData Magic(int id, MagicRarity rarity = MagicRarity.Common)
        => new MagicData { numericId = id, id = "ownership_test_" + id, rarity = rarity };

    [Test]
    public void DefaultAndLegacyConfigUseQuarterWeight()
    {
        Assert.That(new EconomyConfigData().ownedMagicWeightMultiplier, Is.EqualTo(0.25f));
        Assert.That(JsonUtility.FromJson<EconomyConfigData>("{\"numericId\":1}").ownedMagicWeightMultiplier, Is.EqualTo(0.25f));
    }

    [Test]
    public void SameRarityOwnedItemHasOneQuarterOfUnownedWeight()
    {
        MagicData first = Magic(1);
        MagicData second = Magic(2);
        var candidates = new[] { first, second };
        // Different model/data instances must still match by stable numeric ID.
        var owned = new[] { new MagicModel(Magic(1)) };
        int firstCount = 0;
        int secondCount = 0;
        for (int i = 0; i < 1000; i++)
        {
            int roll = i * 1000 + 500;
            MagicData result = MagicRaritySystem.SelectMagicOfRarity(candidates, MagicRarity.Common, (min, max) => roll, owned);
            if (result == first) firstCount++;
            else if (result == second) secondCount++;
        }
        Assert.That(firstCount, Is.EqualTo(200));
        Assert.That(secondCount, Is.EqualTo(800));
    }

    [Test]
    public void OwnershipDoesNotChangeRarityDraw()
    {
        var candidates = new[] { Magic(1), Magic(2, MagicRarity.Rare), Magic(3, MagicRarity.Epic), Magic(4, MagicRarity.Legendary) };
        var owned = new[] { new MagicModel(Magic(1)), new MagicModel(Magic(2, MagicRarity.Rare)) };
        for (int i = 0; i < 100; i++)
        {
            int rarityRoll = i * 10000 + 5000;
            int calls = 0;
            Func<int, int, int> random = (min, max) => calls++ == 0 ? rarityRoll : min;
            MagicData baseline = MagicRaritySystem.SelectWeightedMagic(candidates, random);
            calls = 0;
            MagicData actual = MagicRaritySystem.SelectWeightedMagic(candidates, random, owned);
            Assert.That(actual.rarity, Is.EqualTo(baseline.rarity));
        }
    }

    [TestCase(0f)]
    [TestCase(0.25f)]
    [TestCase(1f)]
    public void AllOwnedStillProducesUniformNonEmptyReward(float multiplier)
    {
        var candidates = new[] { Magic(1), Magic(2) };
        var owned = new[] { new MagicModel(Magic(1)), new MagicModel(Magic(2)) };
        Assert.That(MagicRaritySystem.SelectMagicOfRarity(candidates, MagicRarity.Common, (min, max) => min, owned, multiplier), Is.SameAs(candidates[0]));
        Assert.That(MagicRaritySystem.SelectMagicOfRarity(candidates, MagicRarity.Common, (min, max) => max - 1, owned, multiplier), Is.SameAs(candidates[1]));
    }

    [Test]
    public void MultiplierOneRestoresUniformAndZeroPrefersUnowned()
    {
        var candidates = new[] { Magic(1), Magic(2) };
        var owned = new[] { new MagicModel(Magic(1)) };
        Assert.That(MagicRaritySystem.SelectMagicOfRarity(candidates, MagicRarity.Common, (min, max) => min, owned, 1f), Is.SameAs(candidates[0]));
        Assert.That(MagicRaritySystem.SelectMagicOfRarity(candidates, MagicRarity.Common, (min, max) => min, owned, 0f), Is.SameAs(candidates[1]));
    }

    [Test]
    public void LegendaryGuaranteeUsesOwnershipWeightAndKeepsRarity()
    {
        var candidates = new[] { Magic(1), Magic(2, MagicRarity.Legendary), Magic(3, MagicRarity.Legendary) };
        var owned = new[] { new MagicModel(Magic(2, MagicRarity.Legendary)) };
        Assert.That(MagicRaritySystem.SelectMagicOfRarity(candidates, MagicRarity.Legendary, (min, max) => max / 4, owned), Is.SameAs(candidates[2]));
        Assert.That(MagicRaritySystem.SelectMagicOfRarity(candidates, MagicRarity.Legendary, (min, max) => min, owned), Is.SameAs(candidates[1]));
        Assert.That(MagicRaritySystem.SelectMagicOfRarity(candidates, MagicRarity.Epic, (min, max) => min, owned), Is.Null);
    }

    [Test]
    public void SellingImmediatelyRemovesPenaltyAndCopiesDoNotCompoundIt()
    {
        var candidates = new[] { Magic(1), Magic(2) };
        var owned = new List<MagicModel> { new MagicModel(Magic(1)), new MagicModel(Magic(1)) };
        // 10% is below the owned item's 20% interval even when two copies are held.
        Assert.That(MagicRaritySystem.SelectMagicOfRarity(candidates, MagicRarity.Common, (min, max) => max / 10, owned), Is.SameAs(candidates[0]));
        Assert.That(MagicRaritySystem.SelectMagicOfRarity(candidates, MagicRarity.Common, (min, max) => max / 4, owned), Is.SameAs(candidates[1]));
        owned.Clear();
        Assert.That(MagicRaritySystem.SelectMagicOfRarity(candidates, MagicRarity.Common, (min, max) => max / 4, owned), Is.SameAs(candidates[0]));
    }

    [Test]
    public void EmptyAndNullCandidatesAreHandled()
    {
        Assert.That(MagicRaritySystem.SelectWeightedMagic(null, null), Is.Null);
        Assert.That(MagicRaritySystem.SelectWeightedMagic(Array.Empty<MagicData>(), null), Is.Null);
        Assert.That(MagicRaritySystem.SelectMagicOfRarity(new MagicData[] { null }, MagicRarity.Common, null), Is.Null);
    }
}
