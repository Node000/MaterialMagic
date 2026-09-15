using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// 回归测试：以同一个箭头为起点匹配到多个道具时，触发顺序必须是道具栏从左到右（slotIndex 升序），
/// 而不是按配方长度从长到短（见 Assets/Docs/教程文本与顺序审阅.md 7-2）。
/// </summary>
public class MagicMatchOrderTests
{
    private static MagicModel CreateMagic(string id, int slotIndex, params MaterialEnum[] recipe)
    {
        return new MagicModel(new MagicData { id = id, numericId = slotIndex + 1, recipe = recipe }, slotIndex);
    }

    private static MagicModel CreateAnyTwoDifferentElementsMagic(string id, int slotIndex)
    {
        return new MagicModel(new MagicData
        {
            id = id,
            numericId = slotIndex + 1,
            recipe = new MaterialEnum[0],
            matchRule = MagicMatchRule.AnyTwoDifferentElements
        }, slotIndex);
    }

    private static ArrowReadToken CreateToken(string id, MaterialEnum material)
    {
        return new ArrowReadToken(new MaterialModel(id, material), 0, 0);
    }

    private static List<ArrowReadToken> CreateWindWaterEarthTokens()
    {
        return new List<ArrowReadToken>
        {
            CreateToken("t0", MaterialEnum.Wind),
            CreateToken("t1", MaterialEnum.Water),
            CreateToken("t2", MaterialEnum.Earth)
        };
    }

    [Test]
    public void MatchedMagics_TriggerInLayoutOrder_NotByRecipeLength()
    {
        // 雪球（风+水，2 格）在左侧槽位，有害电波（风+水+土，3 格）在右侧槽位。
        MagicModel snowball = CreateMagic("magic_poison_fog", 0, MaterialEnum.Wind, MaterialEnum.Water);
        MagicModel harmfulWave = CreateMagic("magic_harmful_wave", 1, MaterialEnum.Wind, MaterialEnum.Water, MaterialEnum.Earth);
        List<MagicModel> magicBook = new List<MagicModel> { snowball, harmfulWave };

        List<int> matchedIndices = new List<int>();
        MagicMatchOrderUtility.CollectMatchedMagicIndices(magicBook, CreateWindWaterEarthTokens(), 0, matchedIndices);

        Assert.That(matchedIndices, Is.EqualTo(new List<int> { 0, 1 }),
            "左侧的短配方道具应先触发，不能被更长的配方插队。");
    }

    [Test]
    public void MatchedMagics_UseSlotIndexAsLayoutOrder()
    {
        // 候选列表即使不是布局顺序，也应该按 slotIndex 从左到右排。
        MagicModel rightSlot = CreateMagic("right", 3, MaterialEnum.Wind, MaterialEnum.Water, MaterialEnum.Earth);
        MagicModel leftSlot = CreateMagic("left", 1, MaterialEnum.Wind, MaterialEnum.Water);
        List<MagicModel> magicBook = new List<MagicModel> { rightSlot, leftSlot };

        List<int> matchedIndices = new List<int>();
        MagicMatchOrderUtility.CollectMatchedMagicIndices(magicBook, CreateWindWaterEarthTokens(), 0, matchedIndices);

        Assert.That(matchedIndices, Is.EqualTo(new List<int> { 1, 0 }));
    }

    [Test]
    public void MatchedMagics_AnyTwoDifferentElementsRuleDoesNotJumpAhead()
    {
        // AnyTwoDifferentElements 旧实现按“配方长度 2”参与排序，这里保证它同样只看布局位置。
        MagicModel anyTwo = CreateAnyTwoDifferentElementsMagic("any_two", 0);
        MagicModel harmfulWave = CreateMagic("magic_harmful_wave", 1, MaterialEnum.Wind, MaterialEnum.Water, MaterialEnum.Earth);
        List<MagicModel> magicBook = new List<MagicModel> { anyTwo, harmfulWave };

        List<int> matchedIndices = new List<int>();
        MagicMatchOrderUtility.CollectMatchedMagicIndices(magicBook, CreateWindWaterEarthTokens(), 0, matchedIndices);

        Assert.That(matchedIndices, Is.EqualTo(new List<int> { 0, 1 }));
    }

    [Test]
    public void MatchedMagics_OnlyCollectMagicsMatchingFromTheGivenStartIndex()
    {
        MagicModel snowball = CreateMagic("magic_poison_fog", 0, MaterialEnum.Wind, MaterialEnum.Water);
        MagicModel earthOnly = CreateMagic("earth_only", 1, MaterialEnum.Earth);
        MagicModel harmfulWave = CreateMagic("magic_harmful_wave", 2, MaterialEnum.Wind, MaterialEnum.Water, MaterialEnum.Earth);
        List<MagicModel> magicBook = new List<MagicModel> { snowball, earthOnly, harmfulWave };

        List<ArrowReadToken> tokens = CreateWindWaterEarthTokens();
        List<int> matchedIndices = new List<int>();

        MagicMatchOrderUtility.CollectMatchedMagicIndices(magicBook, tokens, 0, matchedIndices);
        Assert.That(matchedIndices, Is.EqualTo(new List<int> { 0, 2 }));

        // 从第 2 个箭头（土）为起点时，只有单格土配方还能匹配，且顺序仍按布局。
        MagicMatchOrderUtility.CollectMatchedMagicIndices(magicBook, tokens, 2, matchedIndices);
        Assert.That(matchedIndices, Is.EqualTo(new List<int> { 1 }));
    }

    [Test]
    public void MatchedMagics_ClearsPreviousResultsWhenNothingMatches()
    {
        MagicModel harmfulWave = CreateMagic("magic_harmful_wave", 0, MaterialEnum.Wind, MaterialEnum.Water, MaterialEnum.Earth);
        List<MagicModel> magicBook = new List<MagicModel> { harmfulWave };
        List<int> matchedIndices = new List<int> { 7 };

        MagicMatchOrderUtility.CollectMatchedMagicIndices(magicBook, CreateWindWaterEarthTokens(), 1, matchedIndices);

        Assert.That(matchedIndices, Is.Empty);
    }
}
