/// <summary>
/// 禁用附魔：此箭头当前无法从手牌置入出牌区。
/// 一般由 AttributeDisabled Buff 施加，并标记回合结束后移除。
/// </summary>
public class DisabledArrowModifier : MaterialModifierModel
{
    public override bool CanPlay()
    {
        return false;
    }
}
