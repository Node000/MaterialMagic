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

    public override void OnTurnEnd(CombatantModel self, CombatantModel opponent)
    {
        if (self.IsPlayer)
            stack = 0;
    }
}
