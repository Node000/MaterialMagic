using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// 覆盖【保留】/【保留手牌】在“回合结束”与“强制清空”两条路径上的差异：
/// 战斗内回合结束保留手牌，战斗结束/事件·休息·奖励收尾必须强制弃光，避免残留到地图阶段。
/// </summary>
public class ForceDiscardAllTests
{
    private static MaterialModel CreateRetained(string instanceId, MaterialEnum material)
    {
        MaterialModel card = new MaterialModel(instanceId, material);
        card.AddModifier(new RetainedArrowModifier());
        return card;
    }

    private static MaterialModel CreateTemporary(string instanceId, MaterialEnum material)
    {
        MaterialModel card = new MaterialModel(instanceId, material);
        card.AddModifier(new TemporaryModifier());
        return card;
    }

    [Test]
    public void EndTurn_KeepsRetainedCardInHand()
    {
        PlayerState player = new PlayerState();
        MaterialModel retained = CreateRetained("retained", MaterialEnum.Fire);
        MaterialModel normal = new MaterialModel("normal", MaterialEnum.Wind);
        player.Hand.Add(retained);
        player.Hand.Add(normal);

        player.EndTurn(null);

        Assert.That(retained.isRetained, Is.True);
        Assert.That(player.Hand, Is.EqualTo(new List<MaterialModel> { retained }));
        Assert.That(player.DiscardPile.Contains(normal), Is.True);
    }

    [Test]
    public void EndTurn_KeepsWholeHandWithKeepHandEffect()
    {
        PlayerState player = new PlayerState();
        MaterialModel normal = new MaterialModel("normal", MaterialEnum.Wind);
        player.Hand.Add(normal);
        player.KeepHandOnEndTurnOnce();

        player.EndTurn(null);

        Assert.That(player.Hand, Is.EqualTo(new List<MaterialModel> { normal }));
        Assert.That(player.KeepHandOnEndTurn, Is.False);
    }

    [Test]
    public void ForceDiscardAll_IgnoresRetainedAndKeepHandEffects()
    {
        PlayerState player = new PlayerState();
        MaterialModel retained = CreateRetained("retained", MaterialEnum.Fire);
        MaterialModel normal = new MaterialModel("normal", MaterialEnum.Wind);
        MaterialModel played = new MaterialModel("played", MaterialEnum.Earth);
        player.Hand.Add(retained);
        player.Hand.Add(normal);
        player.PlayZone.Add(played);
        player.KeepHandOnEndTurnOnce();

        player.ForceDiscardAll();

        Assert.That(player.Hand, Is.Empty);
        Assert.That(player.PlayZone, Is.Empty);
        Assert.That(player.KeepHandOnEndTurn, Is.False);
        Assert.That(player.DiscardPile, Is.EquivalentTo(new List<MaterialModel> { retained, normal, played }));
    }

    [Test]
    public void ForceDiscardAll_MovesTemporaryCardsToConsumedPile()
    {
        PlayerState player = new PlayerState();
        MaterialModel temporary = CreateTemporary("temporary", MaterialEnum.Water);
        MaterialModel normal = new MaterialModel("normal", MaterialEnum.Wind);
        player.Hand.Add(temporary);
        player.Hand.Add(normal);
        List<MaterialModel> removedTemporaryCards = new List<MaterialModel>();

        player.ForceDiscardAll(removedTemporaryCards);

        Assert.That(player.Hand, Is.Empty);
        Assert.That(removedTemporaryCards, Is.EquivalentTo(new List<MaterialModel> { temporary }));
        Assert.That(player.ConsumedPile.Contains(temporary), Is.True);
        Assert.That(player.DiscardPile.Contains(temporary), Is.False);
        Assert.That(player.DiscardPile.Contains(normal), Is.True);
    }

    [Test]
    public void EndBattle_ForcesRetainedCardsOutOfHand()
    {
        PlayerState player = new PlayerState();
        MaterialModel retained = CreateRetained("retained", MaterialEnum.Fire);
        MaterialModel playedRetained = CreateRetained("played_retained", MaterialEnum.Earth);
        player.Hand.Add(retained);
        player.PlayZone.Add(playedRetained);
        player.KeepHandOnEndTurnOnce();

        player.EndBattle();

        Assert.That(player.Hand, Is.Empty);
        Assert.That(player.PlayZone, Is.Empty);
        Assert.That(player.DiscardPile, Is.EquivalentTo(new List<MaterialModel> { retained, playedRetained }));
    }
}
