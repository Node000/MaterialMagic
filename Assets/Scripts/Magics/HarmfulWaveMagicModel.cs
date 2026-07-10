public class HarmfulWaveMagicModel : MagicModel
{
    public HarmfulWaveMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    public override MagicEffectType EffectType => MagicEffectType.Damage;

    protected override void ResolveCast(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        int handCount = playerState.Hand.Count;
        for (int i = 0; i < handCount; i++)
        {
            Damage(playerState, battleManager.GetRandomAliveEnemy(), 3, result);
            result.AdvanceDamageStep();
        }
    }
}
