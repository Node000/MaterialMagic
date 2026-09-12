using NUnit.Framework;

/// <summary>
/// 属性禁用 Buff 不再直接禁止出牌，改为给对应方向箭头附加禁用附魔。
/// </summary>
public class AttributeDisabledBuffTests
{
    private static PlayerState CreatePlayerWithBuff(MaterialEnum material, out MaterialModel card, MaterialModel other = null)
    {
        PlayerState player = new PlayerState(50, 0);
        card = new MaterialModel("test_matching", material);
        player.Hand.Add(card);
        if (other != null)
            player.Hand.Add(other);
        player.AddBuff(BuffEnum.AttributeDisabled, (int)material);
        return player;
    }

    [Test]
    public void BuffAloneDoesNotBlockPlay()
    {
        PlayerState player = CreatePlayerWithBuff(MaterialEnum.Fire, out MaterialModel fire);

        Assert.That(player.IsMaterialDisabled(fire), Is.False);
        Assert.That(fire.CanPlay(), Is.True);
    }

    [Test]
    public void EnteringHandStampsOnlyMatchingDirection()
    {
        MaterialModel water = new MaterialModel("test_other", MaterialEnum.Water);
        PlayerState player = CreatePlayerWithBuff(MaterialEnum.Fire, out MaterialModel fire, water);

        player.TriggerAfterEnterHand(fire);
        player.TriggerAfterEnterHand(water);

        Assert.That(fire.HasModifier<DisabledArrowModifier>(), Is.True);
        Assert.That(water.HasModifier<DisabledArrowModifier>(), Is.False);
        Assert.That(player.IsMaterialDisabled(fire), Is.True);
        Assert.That(player.IsMaterialDisabled(water), Is.False);

        DisabledArrowModifier modifier = (DisabledArrowModifier)fire.modifiers[0];
        Assert.That(modifier.RemoveModifierAfterTurn, Is.True);
        Assert.That(modifier.RemoveModifierAfterBattle, Is.True);
    }

    [Test]
    public void DisabledArrowCannotBePlayedUntilTurnEnd_AndRecoversAfterwards()
    {
        PlayerState player = CreatePlayerWithBuff(MaterialEnum.Fire, out MaterialModel fire);
        player.TriggerAfterEnterHand(fire);

        Assert.That(player.TryMoveHandCardToPlay(fire), Is.False);
        Assert.That(player.PlayZone.Contains(fire), Is.False);

        player.RemoveTurnOnlyModifiers();

        Assert.That(fire.HasModifier<DisabledArrowModifier>(), Is.False);
        Assert.That(player.TryMoveHandCardToPlay(fire), Is.True);
        Assert.That(player.PlayZone.Contains(fire), Is.True);
    }

    [Test]
    public void BlockedCardCanStillBeMovedWithAllowDisabled()
    {
        PlayerState player = CreatePlayerWithBuff(MaterialEnum.Fire, out MaterialModel fire);
        player.TriggerAfterEnterHand(fire);

        Assert.That(player.TryMoveHandCardToPlay(fire, player.PlayZone.Count, true), Is.True);
    }

    [Test]
    public void TurnStartDrawStampsCardsAlreadyInHand()
    {
        PlayerState player = CreatePlayerWithBuff(MaterialEnum.Fire, out MaterialModel retained);

        Assert.That(retained.HasModifier<DisabledArrowModifier>(), Is.False);
        player.TriggerAfterTurnStartDraw(null, 0);

        Assert.That(retained.HasModifier<DisabledArrowModifier>(), Is.True);
    }

    [Test]
    public void TurnEndRemovesBuffAndStamps()
    {
        PlayerState player = CreatePlayerWithBuff(MaterialEnum.Fire, out MaterialModel fire);
        player.TriggerAfterEnterHand(fire);

        player.TriggerOnTurnEnd(null);

        Assert.That(player.Buffs.ContainsKey(BuffEnum.AttributeDisabled), Is.False);
        Assert.That(fire.HasModifier<DisabledArrowModifier>(), Is.False);
        Assert.That(fire.CanPlay(), Is.True);
    }

    [Test]
    public void OverwritingDirectionReleasesPreviousDirection()
    {
        PlayerState player = CreatePlayerWithBuff(MaterialEnum.Fire, out MaterialModel fire);
        player.TriggerAfterEnterHand(fire);
        Assert.That(fire.HasModifier<DisabledArrowModifier>(), Is.True);

        player.AddBuff(BuffEnum.AttributeDisabled, (int)MaterialEnum.Water);

        Assert.That(fire.HasModifier<DisabledArrowModifier>(), Is.False);
        Assert.That(player.GetBuffStack(BuffEnum.AttributeDisabled), Is.EqualTo((int)MaterialEnum.Water));
    }
}
