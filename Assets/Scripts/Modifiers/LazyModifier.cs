public class LazyModifier : MaterialModifierModel
{
    public override bool RemoveModifierAfterBattle => true;

    public override void OnArrowBaseEffectResolve(ArrowReadContext context)
    {
        PlayerState player = context?.PlayerState ?? CurrentContext?.PlayerState ?? BattleManager.Instance?.PlayerState;
        if (player != null)
            player.AddBuff(BuffEnum.LazyNextDraw, 1);
    }
}
