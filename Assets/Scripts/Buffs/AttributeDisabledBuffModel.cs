using System.Collections.Generic;

/// <summary>
/// 属性禁用：本回合抽到/进入手牌的对应方向箭头获得【禁用】附魔，无法从手牌置入出牌区。
/// “能否打出”的判定由 DisabledArrowModifier.CanPlay() 负责，本 Buff 只负责施加附魔。
/// </summary>
public class AttributeDisabledBuffModel : BuffModel
{
    public AttributeDisabledBuffModel(int stack) : base(BuffEnum.AttributeDisabled, stack)
    {
    }

    public override string GetSlotStackText()
    {
        return GetDirectionName(stack);
    }

    public override string GetTooltipStackText()
    {
        return GetDirectionName(stack);
    }

    public override string GetDesc()
    {
        string directionName = GetDirectionName(stack);
        if (string.IsNullOrEmpty(directionName))
            return LocalizationKeys.GetBuffDescription(buffType);

        string template = LocalizationSystem.GetText("buff.attributedisabled.desc_with_material", string.Empty);
        return string.IsNullOrEmpty(template) ? LocalizationKeys.GetBuffDescription(buffType) : string.Format(template, directionName);
    }

    public override void OnTurnEnd(CombatantModel self, CombatantModel opponent)
    {
        if (self.IsPlayer)
            stack = 0;
    }

    public override void OnExpire(CombatantModel self, CombatantModel opponent)
    {
        // Buff 提前被移除时同步解除禁用附魔（正常回合结束由 RemoveTurnOnlyModifiers 处理）。
        self?.Player?.ClearDisabledArrowModifiers(MaterialEnum.None);
    }

    public override void AfterEnterHand(CombatantModel self, MaterialModel card)
    {
        ApplyDisabledArrow(card);
    }

    public override void AfterTurnStartDraw(CombatantModel self, CombatantModel opponent, int drawCount)
    {
        PlayerState player = self?.Player;
        if (player == null)
            return;

        ApplyToHand(player.Hand);
    }

    /// <summary>给手牌中所有该方向箭头补上禁用附魔（覆盖保留手牌等本回合未重新抽到的箭头）。</summary>
    public void ApplyToHand(IReadOnlyList<MaterialModel> hand)
    {
        for (int i = 0; hand != null && i < hand.Count; i++)
            ApplyDisabledArrow(hand[i]);
    }

    private void ApplyDisabledArrow(MaterialModel card)
    {
        if (card == null || stack <= 0 || card.material != (MaterialEnum)stack)
            return;

        if (card.HasModifier<DisabledArrowModifier>())
            return;

        DisabledArrowModifier modifier = new DisabledArrowModifier();
        modifier.MarkRemoveAfterTurn();
        modifier.MarkRemoveAfterBattle();
        card.AddModifier(modifier);
    }

    private static string GetDirectionName(int materialValue)
    {
        MaterialEnum material = (MaterialEnum)materialValue;
        return material == MaterialEnum.None || material == MaterialEnum.Wild ? string.Empty : LocalizationKeys.GetMaterialName(material);
    }
}
