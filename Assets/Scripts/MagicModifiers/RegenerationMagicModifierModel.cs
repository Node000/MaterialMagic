public class RegenerationMagicModifierModel : MagicModifierModel
{
    private bool triggeredThisBattle;

    public RegenerationMagicModifierModel(MagicModifierData data) : base(data)
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
        int healthBefore = context.PlayerState.CurrentHealth;
        context.PlayerState.Heal(Data != null && Data.value > 0 ? Data.value : 3);
        if (result != null)
            result.playerHeal += context.PlayerState.CurrentHealth - healthBefore;
    }
}
