public class ShuffleHandOnInvokeChanceBuffModel : BuffModel
{
    private bool pendingNextTurn = true;

    public ShuffleHandOnInvokeChanceBuffModel(int stack) : base(BuffEnum.ShuffleHandOnInvokeChance, stack)
    {
    }

    public override void OnTurnStart(CombatantModel self, CombatantModel opponent)
    {
        if (pendingNextTurn)
            pendingNextTurn = false;
    }

    public override void AfterPlayerDecide(CombatantModel self, CombatantModel opponent)
    {
        if (!self.IsPlayer || pendingNextTurn || stack <= 0)
            return;

        PlayerState player = self.Player;
        if (player != null && player.PlayZone.Count > 1)
            player.ShufflePlayZone();
        stack = 0;
    }

    public override void OnTurnEnd(CombatantModel self, CombatantModel opponent)
    {
        if (self.IsPlayer && !pendingNextTurn)
            stack = 0;
    }
}
