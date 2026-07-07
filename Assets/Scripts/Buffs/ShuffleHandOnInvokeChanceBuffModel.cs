public class ShuffleHandOnInvokeChanceBuffModel : BuffModel
{
    public ShuffleHandOnInvokeChanceBuffModel(int stack) : base(BuffEnum.ShuffleHandOnInvokeChance, stack)
    {
    }

    public override void AfterPlayerDecide(CombatantModel self, CombatantModel opponent)
    {
        if (!self.IsPlayer || stack <= 0)
            return;

        PlayerState player = self.Player;
        if (player == null || player.PlayZone.Count <= 1)
            return;

        int roll = player is PlayerStatus status ? status.NextRunRandomInt(0, 3) : UnityEngine.Random.Range(0, 3);
        if (roll != 0)
            return;

        player.ShufflePlayZone();
    }
}
