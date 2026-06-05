public class FireballArtMagicModel : MagicModel
{
    public FireballArtMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    public override MagicEffectType EffectType => MagicEffectType.Damage;
    protected override void ResolveCast(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        EnemyModel target = Target(battleManager);
        Damage(playerState, target, 3, result);
        AddBuff(target, BuffEnum.Burning, 2, result);
    }
}
