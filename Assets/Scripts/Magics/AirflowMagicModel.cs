public class AirflowMagicModel : MagicModel
{
    public AirflowMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    public override MagicEffectType EffectType => MagicEffectType.Damage;
    protected override void ResolveCast(PlayerState playerState, BattleManager battleManager, MagicCastResult result) => DamageTarget(playerState, battleManager, 2, result);
}
