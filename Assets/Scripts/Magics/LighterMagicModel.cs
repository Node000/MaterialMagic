public class LighterMagicModel : MagicModel
{
    public LighterMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    public override MagicEffectType EffectType => MagicEffectType.ApplyBuff;

    protected override void ResolveCast(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        int lostShield = playerState.ConsumeShield(playerState.Shield);
        for (int i = 0; i < lostShield; i++)
            battleManager.AddBurningToRandomEnemy(2);
        result.enemyBuffApplied = lostShield > 0;
    }
}
