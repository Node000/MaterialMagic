using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// 【随机】箭头（RandomArrowModifier）的行为约束：读取前不锁定方向、读取后定格到掷出的方向，
/// 且每张卡各自消耗一个随机步、互不共享掷点结果。
/// </summary>
public class RandomArrowModifierTests
{
    // 固定种子：步 0..5 依次掷出 土、风、火、风、水、水（前两步不同，便于断言独立性）。
    private const int FixedSeed = 12346;
    private const int RollMin = (int)MaterialEnum.Fire;
    private const int RollMaxExclusive = (int)MaterialEnum.Earth + 1;

    private static MaterialModel CreateRandomArrow(string instanceId, MaterialEnum baseMaterial = MaterialEnum.Fire)
    {
        MaterialModel card = new MaterialModel(instanceId, baseMaterial);
        card.AddModifier(new RandomArrowModifier());
        return card;
    }

    private static PlayerStatus CreateStatus(int seed = FixedSeed, int step = 0)
    {
        PlayerStatus status = new PlayerStatus();
        status.SetRunRandomState(seed, step);
        return status;
    }

    private static MaterialEnum ExpectedRoll(int seed, int step)
    {
        return (MaterialEnum)RunRandom.Range(seed, step, RollMin, RollMaxExclusive);
    }

    [Test]
    public void RandomArrow_BeforeRead_DisplayStaysBaseDirectionAndNoLock()
    {
        MaterialModel card = CreateRandomArrow("random");
        RandomArrowModifier modifier = (RandomArrowModifier)card.modifiers[0];

        Assert.That(modifier.GetLockedArrowDisplayMaterial(), Is.EqualTo(MaterialEnum.None));
        Assert.That(card.GetArrowDisplayMaterial(), Is.EqualTo(MaterialEnum.Fire));
    }

    [Test]
    public void RandomArrow_AfterRead_LocksDisplayToRolledDirection()
    {
        MaterialModel card = CreateRandomArrow("random");
        RandomArrowModifier modifier = (RandomArrowModifier)card.modifiers[0];
        ArrowReadSequence sequence = ArrowReadSystem.BuildSequence(new List<MaterialModel> { card }, CreateStatus(), null);

        MaterialEnum rolled = modifier.GetLockedArrowDisplayMaterial();
        Assert.That(rolled, Is.EqualTo(ExpectedRoll(FixedSeed, 0)));
        Assert.That(card.GetArrowDisplayMaterial(), Is.EqualTo(rolled));
        Assert.That(card.CanActAs(rolled), Is.True);
        Assert.That(card.IsArrowReadable(), Is.True);
        Assert.That(sequence.Steps.Count, Is.EqualTo(1));
        Assert.That(sequence.Steps[0].BaseEffectDirections, Does.Contain(rolled));
    }

    [Test]
    public void RandomArrow_EachCardConsumesItsOwnStepAndRollsIndependently()
    {
        List<MaterialModel> cards = new List<MaterialModel>();
        for (int i = 0; i < 6; i++)
            cards.Add(CreateRandomArrow("random" + i));

        ArrowReadSystem.BuildSequence(cards, CreateStatus(), null);

        HashSet<MaterialEnum> distinct = new HashSet<MaterialEnum>();
        for (int i = 0; i < cards.Count; i++)
        {
            MaterialEnum locked = ((RandomArrowModifier)cards[i].modifiers[0]).GetLockedArrowDisplayMaterial();
            Assert.That(locked, Is.EqualTo(ExpectedRoll(FixedSeed, i)), $"第 {i} 张箭头应使用第 {i} 个随机步");
            distinct.Add(locked);
        }

        // 该种子下 6 张箭头掷出 4 种方向；若掷点被共享，这里只会有 1 种。
        Assert.That(distinct.Count, Is.GreaterThan(1));
    }

    [Test]
    public void RandomArrow_RepeatedReadKeepsFirstRoll()
    {
        MaterialModel card = CreateRandomArrow("random");
        RandomArrowModifier modifier = (RandomArrowModifier)card.modifiers[0];

        ArrowReadSystem.BuildSequence(new List<MaterialModel> { card }, CreateStatus(), null);
        MaterialEnum first = modifier.GetLockedArrowDisplayMaterial();

        ArrowReadSystem.BuildSequence(new List<MaterialModel> { card }, CreateStatus(), null);
        Assert.That(modifier.GetLockedArrowDisplayMaterial(), Is.EqualTo(first));
    }

    [Test]
    public void RandomArrow_CloneForBattleRollsAgain()
    {
        MaterialModel card = CreateRandomArrow("random");
        ArrowReadSystem.BuildSequence(new List<MaterialModel> { card }, CreateStatus(), null);
        MaterialEnum rolled = card.GetArrowDisplayMaterial();

        MaterialModel battleCard = card.CloneForBattle("random_battle");

        Assert.That(rolled, Is.Not.EqualTo(MaterialEnum.None));
        Assert.That(((RandomArrowModifier)battleCard.modifiers[0]).GetLockedArrowDisplayMaterial(), Is.EqualTo(MaterialEnum.None));
        Assert.That(battleCard.GetArrowDisplayMaterial(), Is.EqualTo(MaterialEnum.Fire));
        Assert.That(((RandomArrowModifier)card.modifiers[0]).GetLockedArrowDisplayMaterial(), Is.EqualTo(rolled));
    }

    [Test]
    public void RandomArrow_SixCardRollsRarelyCollapseToOneDirection()
    {
        // 回归护栏：曾出现过“所有随机箭头结算为同一方向”的观感（表现同步 + 共享掷点）。
        // 均匀随机下 6 张全同的理论概率约 0.1%，这里给出宽松上限。
        const int groupCount = 100;
        int allSameGroups = 0;
        for (int group = 0; group < groupCount; group++)
        {
            List<MaterialModel> cards = new List<MaterialModel>();
            for (int i = 0; i < 6; i++)
                cards.Add(CreateRandomArrow("seed" + group + "_" + i));

            ArrowReadSystem.BuildSequence(cards, CreateStatus(group + 1), null);

            MaterialEnum first = ((RandomArrowModifier)cards[0].modifiers[0]).GetLockedArrowDisplayMaterial();
            bool allSame = true;
            for (int i = 1; i < cards.Count; i++)
            {
                if (((RandomArrowModifier)cards[i].modifiers[0]).GetLockedArrowDisplayMaterial() != first)
                {
                    allSame = false;
                    break;
                }
            }

            if (allSame)
                allSameGroups++;
        }

        Assert.That(allSameGroups, Is.LessThanOrEqualTo(3), $"{groupCount} 组 6 张全同组数={allSameGroups}");
    }
}
