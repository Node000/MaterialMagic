public class MoltenMagicModel : ScriptedMagicModel
{
    public MoltenMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    protected override void CastScript(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        EnemyModel target = Target(battleManager);
        Damage(playerState, target, 9 + GetBuffStack(target, BuffEnum.Burning), result);
    }
}
