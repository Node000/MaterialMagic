public class CurseBuffModel : BuffModel
{
    private int remainingCardsToCurse;
    public override System.Collections.Generic.Dictionary<string, int> ExportRuntimeState() => new System.Collections.Generic.Dictionary<string, int> { { "remaining", remainingCardsToCurse } };
    public override void RestoreRuntimeState(System.Collections.Generic.IReadOnlyDictionary<string, int> values) => remainingCardsToCurse = ReadState(values, "remaining");

    public CurseBuffModel(int stack) : base(BuffEnum.Curse, stack)
    {
    }

    public override void OnTurnStart(CombatantModel self, CombatantModel opponent)
    {
        if (self.IsPlayer && stack > 0)
            remainingCardsToCurse = stack * 2;
    }

    public override void AfterDraw(CombatantModel self, MaterialModel card)
    {
        if (!self.IsPlayer || card == null || remainingCardsToCurse <= 0)
            return;

        card.AddModifier(new DoomModifier());
        remainingCardsToCurse--;
    }
}
