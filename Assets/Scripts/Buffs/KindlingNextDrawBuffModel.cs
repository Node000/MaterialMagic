public class KindlingNextDrawBuffModel : BuffModel
{
    private bool pendingNextTurn = true;

    public KindlingNextDrawBuffModel(int stack) : base(BuffEnum.KindlingNextDraw, stack)
    {
    }

    public override void OnTurnStart(CombatantModel self, CombatantModel opponent)
    {
        if (pendingNextTurn)
            pendingNextTurn = false;
    }

    public override void AfterDraw(CombatantModel self, MaterialModel card)
    {
        if (pendingNextTurn || self?.Player == null || card == null)
            return;

        for (int i = 0; i < card.modifiers.Count; i++)
        {
            if (card.modifiers[i] is KindlingModifier)
                return;
        }

        KindlingModifier modifier = new KindlingModifier();
        modifier.MarkRemoveAfterBattle();
        card.AddModifier(modifier);
    }

    public override void OnTurnEnd(CombatantModel self, CombatantModel opponent)
    {
        if (!pendingNextTurn)
            stack = 0;
    }
}
