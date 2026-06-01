public class DefensePowerBuffModel : BuffModel
{
    public DefensePowerBuffModel(int stack) : base(BuffEnum.DefensePower, stack)
    {
    }

    public override void OnGainShield(CombatantModel self, ref int shieldValue)
    {
        shieldValue += stack;
        if (shieldValue < 0)
            shieldValue = 0;
    }

    public override void OnTurnEnd(CombatantModel self, CombatantModel opponent)
    {
        if (self.IsPlayer)
            stack = 0;
    }
}
