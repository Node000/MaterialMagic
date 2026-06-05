using System.Collections.Generic;

public class ShuffleHandOnInvokeChanceBuffModel : BuffModel
{
    public ShuffleHandOnInvokeChanceBuffModel(int stack) : base(BuffEnum.ShuffleHandOnInvokeChance, stack)
    {
    }

    public override void OnInvoke(CombatantModel self, CombatantModel target)
    {
        if (!self.IsEnemy || stack <= 0)
            return;

        PlayerState player = BattleManager.Instance?.PlayerState;
        if (player == null || player.Hand.Count <= 1)
            return;

        int roll = player is PlayerStatus status ? status.NextRunRandomInt(0, stack) : UnityEngine.Random.Range(0, stack);
        if (roll != 0)
            return;

        for (int i = player.Hand.Count - 1; i > 0; i--)
        {
            int swapIndex = player is PlayerStatus status2 ? status2.NextRunRandomInt(0, i + 1) : UnityEngine.Random.Range(0, i + 1);
            MaterialModel temp = player.Hand[i];
            player.Hand[i] = player.Hand[swapIndex];
            player.Hand[swapIndex] = temp;
        }
    }
}
