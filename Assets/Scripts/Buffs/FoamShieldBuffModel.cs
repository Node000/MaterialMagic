public class FoamShieldBuffModel : BuffModel
{
    public FoamShieldBuffModel(int stack) : base(BuffEnum.FoamShield, stack)
    {
    }

    public override void AfterGiveBuff(CombatantModel self, CombatantModel target, BuffEnum buffType, int stack)
    {
        if (self?.Player != null && target != null && target.IsEnemy && BuffModel.GetKind(buffType) == BuffKindEnum.DeBuff)
            self.Player.GainShield(stack * this.stack);
    }

    // 敌方回合内（汽油、焦糖熊等）玩家仍会作为来源施加负面状态，因此本 Buff 必须覆盖整轮，
    // 在玩家下个回合开始时清除，而不是在玩家回合结束时清除。
    public override void OnTurnStart(CombatantModel self, CombatantModel opponent)
    {
        if (self.IsPlayer)
            stack = 0;
    }
}
