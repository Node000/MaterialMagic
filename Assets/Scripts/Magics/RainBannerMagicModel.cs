public class RainBannerMagicModel : MagicModel
{
    public RainBannerMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    public override MagicEffectType EffectType => MagicEffectType.DrawNextTurn;
    public override bool CastParticleTargetsPlayer => true;

    protected override void ResolveCast(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        for (int i = 0; i < 3; i++)
        {
            MaterialModel card = playerState.GrantTemporaryMaterialToHand(MaterialEnum.Water);
            if (card == null)
                break;
            result.CreatedHandCards.Add(card);
        }

        playerState.AddBuff(BuffEnum.RainBanner, 1, new CombatantModel(playerState));
    }
}
