public class EchoMagicModifierModel : MagicModifierModel
{
    private bool usedThisBattle;

    public EchoMagicModifierModel(MagicModifierData data) : base(data)
    {
    }

    public override void OnBattleStart()
    {
        usedThisBattle = false;
    }

    public override int GetAdditionalCastCount()
    {
        if (usedThisBattle)
            return 0;

        usedThisBattle = true;
        return Data != null && Data.value > 0 ? Data.value : 1;
    }
}
