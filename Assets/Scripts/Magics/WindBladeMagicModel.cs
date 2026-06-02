using UnityEngine;

public class WindBladeMagicModel : ScriptedMagicModel
{
    public WindBladeMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    protected override void CastScript(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        DamageTarget(playerState, battleManager, 3 + Mathf.Max(0, battleManager.ContinuousCastCount - 1), result);
    }
}
