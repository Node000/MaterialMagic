using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 回归测试：以同一个箭头为起点匹配到多个道具时，触发顺序必须是道具栏的视觉阅读顺序
/// （行从上到下、行内从左到右），而不是按配方长度从长到短（见 Assets/Docs/教程文本与顺序审阅.md 7-2）。
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

    [Test]
    public void MatchedMagics_FollowVisualRanksWhenProvided()
    {
        // 视觉顺序与槽位下标不一致时（移动端网格：右列在前、左列在后），以视觉顺序为准。
        MagicModel rightTop = CreateMagic("right_top", 0, MaterialEnum.Wind, MaterialEnum.Water);
        MagicModel leftTop = CreateMagic("left_top", 1, MaterialEnum.Wind, MaterialEnum.Water, MaterialEnum.Earth);
        List<MagicModel> magicBook = new List<MagicModel> { rightTop, leftTop };
        List<int> visualRanks = new List<int> { 1, 0 };

        List<int> matchedIndices = new List<int>();
        MagicMatchOrderUtility.CollectMatchedMagicIndices(magicBook, CreateWindWaterEarthTokens(), 0, matchedIndices, visualRanks);

        Assert.That(matchedIndices, Is.EqualTo(new List<int> { 1, 0 }), "应左上槽位（视觉序号 0）先触发。");
    }

    // ---- 视觉阅读顺序（MagicBookVisualOrder）----

    private const float ColumnPitch = 231.8f;   // PE 道具栏：cell 212.7 + spacing 19.1
    private const float RowPitch = 120.9f;      // PE 道具栏：cell 87.7 + spacing 33.2

    /// <summary>按 PE 网格（右上起、纵向填充、固定 4 行）生成 8 槽位置：索引 0-3 在右列，4-7 在左列。</summary>
    private static List<Vector2> CreateGridPositions(int count)
    {
        List<Vector2> positions = new List<Vector2>();
        int rows = 4;
        for (int i = 0; i < count; i++)
        {
            int row = i % rows;
            int columnFromRight = i / rows;
            int columns = (count + rows - 1) / rows;
            float x = (columns - 1) * 0.5f * ColumnPitch - columnFromRight * ColumnPitch;
            float y = (rows - 1) * 0.5f * RowPitch - row * RowPitch;
            positions.Add(new Vector2(x, y));
        }
        return positions;
    }

    [Test]
    public void VisualOrder_GridFourRowsTwoColumns_UsesRowMajorReadingOrder()
    {
        List<int> ranks = new List<int>();
        MagicBookVisualOrder.ComputeRanksFromPositions(CreateGridPositions(8), ranks);

        Assert.That(ranks, Is.EqualTo(new List<int> { 1, 3, 5, 7, 0, 2, 4, 6 }),
            "4 行 2 列的网格应按行优先：左列顶部 → 右列顶部 → 下一行，而不是先把右列走完。");
    }

    [Test]
    public void VisualOrder_GridTwoColumnsFiveSlots_UsesRowMajorReadingOrder()
    {
        // 5 个槽位：右列 0-3，左列只有 4（顶部）。行优先顺序应为 4,0,1,2,3。
        List<int> ranks = new List<int>();
        MagicBookVisualOrder.ComputeRanksFromPositions(CreateGridPositions(5), ranks);

        Assert.That(ranks, Is.EqualTo(new List<int> { 1, 2, 3, 4, 0 }));
    }

    [Test]
    public void VisualOrder_SingleColumn_IsTopToBottom()
    {
        List<Vector2> positions = new List<Vector2>
        {
            new Vector2(0f, 181.35f),
            new Vector2(0f, 60.45f),
            new Vector2(0f, -60.45f),
            new Vector2(0f, -181.35f)
        };
        List<int> ranks = new List<int>();
        MagicBookVisualOrder.ComputeRanksFromPositions(positions, ranks);

        Assert.That(ranks, Is.EqualTo(new List<int> { 0, 1, 2, 3 }), "单列时从上到下就是槽位顺序。");
    }

    [Test]
    public void VisualOrder_SingleRow_IsLeftToRight()
    {
        // 弧形道具栏（PC）或单行网格：同一行内从左到右。
        List<Vector2> positions = new List<Vector2>
        {
            new Vector2(300f, 0f),
            new Vector2(-100f, 0f),
            new Vector2(100f, 0f),
            new Vector2(-300f, 0f)
        };
        List<int> ranks = new List<int>();
        MagicBookVisualOrder.ComputeRanksFromPositions(positions, ranks);

        Assert.That(ranks, Is.EqualTo(new List<int> { 3, 1, 2, 0 }));
    }

    [Test]
    public void VisualOrder_IdenticalPositions_KeepsSlotOrderStable()
    {
        List<Vector2> positions = new List<Vector2>
        {
            new Vector2(10f, 10f),
            new Vector2(10f, 10f),
            new Vector2(10f, 10f)
        };
        List<int> ranks = new List<int>();
        MagicBookVisualOrder.ComputeRanksFromPositions(positions, ranks);

        Assert.That(ranks, Is.EqualTo(new List<int> { 0, 1, 2 }));
    }

    // ---- 拖拽落点（ComputeDropTargetIndex）----

    [Test]
    public void DropTarget_SingleRow_KeepsLeftToRightInsertSemantics()
    {
        // 弧形道具栏（PC）：指针左边的槽位数就是目标下标，与改造前一致。
        List<Vector2> positions = new List<Vector2>
        {
            new Vector2(-300f, 0f),
            new Vector2(-100f, 0f),
            new Vector2(100f, 0f),
            new Vector2(300f, 0f)
        };
        List<int> ranks = new List<int>();
        MagicBookVisualOrder.ComputeRanksFromPositions(positions, ranks);

        Assert.That(MagicBookVisualOrder.ComputeDropTargetIndex(positions, 1, new Vector2(0f, 0f), ranks), Is.EqualTo(1));
        Assert.That(MagicBookVisualOrder.ComputeDropTargetIndex(positions, 1, new Vector2(400f, 0f), ranks), Is.EqualTo(3));
        Assert.That(MagicBookVisualOrder.ComputeDropTargetIndex(positions, 1, new Vector2(-400f, 0f), ranks), Is.EqualTo(0));
    }

    [Test]
    public void DropTarget_Grid_LeftColumnTopResolvesToLeftTopSlot()
    {
        List<Vector2> positions = CreateGridPositions(8);
        List<int> ranks = new List<int>();
        MagicBookVisualOrder.ComputeRanksFromPositions(positions, ranks);

        // 拖着右列第二个槽位（idx5），落到左列顶部左侧：目标应是左上槽位 idx4。
        int target = MagicBookVisualOrder.ComputeDropTargetIndex(positions, 5, new Vector2(-200f, 181.35f), ranks);
        Assert.That(target, Is.EqualTo(4));
    }

    [Test]
    public void DropTarget_Grid_RightColumnRowTwoResolvesToThatRowRightSlot()
    {
        List<Vector2> positions = CreateGridPositions(8);
        List<int> ranks = new List<int>();
        MagicBookVisualOrder.ComputeRanksFromPositions(positions, ranks);

        // 拖着左上槽位（idx4），落到右列第二行右侧：目标应是该行右槽位 idx1，而不是下一行的槽位。
        int target = MagicBookVisualOrder.ComputeDropTargetIndex(positions, 4, new Vector2(200f, 60.45f), ranks);
        Assert.That(target, Is.EqualTo(1));
    }

    [Test]
    public void DropTarget_Grid_LeftColumnRowThreeResolvesToThatRowLeftSlot()
    {
        List<Vector2> positions = CreateGridPositions(8);
        List<int> ranks = new List<int>();
        MagicBookVisualOrder.ComputeRanksFromPositions(positions, ranks);

        // 拖着右下槽位（idx3），落到左列第三行左侧：目标应是该行左槽位 idx6。
        int target = MagicBookVisualOrder.ComputeDropTargetIndex(positions, 3, new Vector2(-200f, -60.45f), ranks);
        Assert.That(target, Is.EqualTo(6));
    }
}
