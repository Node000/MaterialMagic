using UnityEngine;

public class WindBladeMagicModel : MagicModel
{
    public WindBladeMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    public override MagicEffectType EffectType => MagicEffectType.Damage;
    protected override void ResolveCast(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        DamageTarget(playerState, battleManager, 3 + Mathf.Max(0, battleManager.ContinuousCastCount - 1), result);
    }
}
