using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// 【增殖】箭头（ProliferatingArrowModifier）的行为约束：
/// 本体不改变读取后去向，按普通箭头留在出牌区、回合结束时进入弃牌堆；
/// 读取时只在弃牌堆中添加一份自身临时复制（战斗结束后移除），不安排下回合回到手牌。
/// </summary>
public class ProliferatingArrowModifierTests
{
    private static MaterialModel CreateProliferatingArrow(string instanceId, MaterialEnum material = MaterialEnum.Fire)
    {
        MaterialModel card = new MaterialModel(instanceId, material);
        card.AddModifier(new ProliferatingArrowModifier());
        return card;
    }

    [Test]
    public void ProliferatingArrow_AfterRead_DoesNotReturnNextTurn()
    {
        MaterialModel card = CreateProliferatingArrow("proliferating");

        Assert.That(card.GetArrowAfterReadAction(), Is.EqualTo(ArrowReadAfterReadAction.None));
        Assert.That(card.ShouldRemoveSourceAfterArrowRead(), Is.False);

        ArrowReadSequence sequence = ArrowReadSystem.BuildSequence(new List<MaterialModel> { card }, new PlayerStatus(), null);
        ArrowReadStep step = sequence.Steps[0];

        Assert.That(step.AfterReadAction, Is.EqualTo(ArrowReadAfterReadAction.None));
        Assert.That(step.RemovesSourceAfterRead, Is.False);
    }

    [Test]
    public void ProliferatingArrow_Resolve_AddsTemporaryCopyToDiscardPile()
    {
        PlayerStatus status = new PlayerStatus();
        MaterialModel card = CreateProliferatingArrow("proliferating");
        status.PlayZone.Add(card);

        card.TriggerOnArrowBaseEffectResolve(new ArrowReadContext(status, null));

        Assert.That(status.DiscardPile.Count, Is.EqualTo(1));
        MaterialModel copy = status.DiscardPile[0];
        Assert.That(copy, Is.Not.SameAs(card));
        Assert.That(copy.instanceId, Is.Not.EqualTo(card.instanceId));
        Assert.That(copy.material, Is.EqualTo(card.material));
        Assert.That(copy.removeCardAfterBattle, Is.True);
        Assert.That(copy.isPlayed, Is.False);
        // 复制卡不再具有增殖，避免读取复制卡继续无限增殖。
        Assert.That(copy.HasModifier<ProliferatingArrowModifier>(), Is.False);
    }

    [Test]
    public void ProliferatingArrow_Resolve_KeepsSourceInPlayZoneAndOutOfScheduledHand()
    {
        PlayerStatus status = new PlayerStatus();
        MaterialModel card = CreateProliferatingArrow("proliferating");
        status.PlayZone.Add(card);

        card.TriggerOnArrowBaseEffectResolve(new ArrowReadContext(status, null));

        Assert.That(status.TemporaryMaterialsNextTurn, Is.Empty);
        Assert.That(status.PlayZone, Does.Contain(card));

        // 回合结束时出牌区（连同增殖本体）进入弃牌堆。
        status.EndTurn();
        Assert.That(status.PlayZone, Is.Empty);
        Assert.That(status.DiscardPile, Does.Contain(card));
        Assert.That(status.DiscardPile.Count, Is.EqualTo(2));
    }
}
