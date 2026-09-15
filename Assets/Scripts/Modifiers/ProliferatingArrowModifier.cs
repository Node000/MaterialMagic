/// <summary>
/// 【增殖】箭头：读取时在弃牌堆中添加本箭头的临时复制（复制卡战斗结束后移除）。
/// 本体不改变读取后去向，按普通箭头随出牌区进入弃牌堆。
/// </summary>
public class ProliferatingArrowModifier : MaterialModifierModel
{
    public override void OnArrowBaseEffectResolve(ArrowReadContext context)
    {
        PlayerState playerState = context?.PlayerState ?? Context?.PlayerState;
        if (playerState == null || model == null)
            return;

        MaterialModel copy = model.CloneForBattle("proliferated_" + model.instanceId + "_" + playerState.DiscardPile.Count);
        copy.removeCardAfterBattle = true;
        copy.isPlayed = false;
        copy.RemoveModifiers<ProliferatingArrowModifier>();
        playerState.AddCardToDiscardPile(copy);
        GameLog.Data($"Add proliferated arrow copy to discard pile {copy.instanceId}");
    }
}
