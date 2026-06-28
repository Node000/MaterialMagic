public class RefineMagicModel : MagicModel
{
    public RefineMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    public override MagicEffectType EffectType => MagicEffectType.None;
    public override bool CastParticleTargetsPlayer => true;
    protected override void ResolveCast(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        while (playerState.Hand.Count > 0)
            playerState.ConsumeCardForBattle(playerState.Hand[0]);
    }
}
