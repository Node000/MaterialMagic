public class MidasMagicModifierModel : MagicModifierModel
{
    private bool triggeredThisBattle;

    public MidasMagicModifierModel(MagicModifierData data) : base(data)
    {
    }

    public override void OnBattleStart()
    {
        triggeredThisBattle = false;
    }

    public override void AfterCast(MagicCastResult result)
    {
        if (triggeredThisBattle)
            return;

        MagicModifierContext context = Context;
        if (context == null || context.PlayerState == null)
            return;

        triggeredThisBattle = true;
        int goldGain = DifficultyUpgradeSystem.ModifyGoldGain(Data != null && Data.value > 0 ? Data.value : 1);
        context.PlayerState.AddGold(goldGain, false);
        if (result != null)
            result.playerGoldGain += goldGain;
    }
}
