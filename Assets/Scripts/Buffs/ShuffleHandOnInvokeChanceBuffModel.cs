public class ShuffleHandOnInvokeChanceBuffModel : BuffModel
{
    private bool pendingNextTurn = true;
    public override System.Collections.Generic.Dictionary<string, int> ExportRuntimeState() => new System.Collections.Generic.Dictionary<string, int> { { "pending", pendingNextTurn ? 1 : 0 } };
    public override void RestoreRuntimeState(System.Collections.Generic.IReadOnlyDictionary<string, int> values) => pendingNextTurn = ReadState(values, "pending", 1) != 0;

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
