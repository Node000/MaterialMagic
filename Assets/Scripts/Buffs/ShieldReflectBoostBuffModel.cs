public class ShieldReflectBoostBuffModel : BuffModel
{
    public ShieldReflectBoostBuffModel(int stack) : base(BuffEnum.ShieldReflectBoost, stack)
    {
    }

    // 反击发生在敌方回合（ShieldReflectBuffModel.AfterTakeDamage 读取本层数），
    // 只有在玩家下个回合开始时清除才能覆盖整轮。
    public override void OnTurnStart(CombatantModel self, CombatantModel opponent)
    {
        stack = 0;
    }
}
