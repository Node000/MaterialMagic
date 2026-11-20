/// <summary>First actual cast each battle gains attack damage; consumed at cast completion, never in preview.</summary>
public sealed class FirstMagicPowerRelicModel : RelicModel
{
    public FirstMagicPowerRelicModel(RelicData data) : base(data) { }
    public override void ResetBattleState() { Set("used", 0); Set("active", 0); }
    public override void OnEvent(RelicContext context, RelicEvent value)
    {
        if (value.Kind == RelicEventKind.MagicStarted && Get("used") == 0)
        {
            Set("used", 1);
            Set("active", 1);
        }
        else if (value.Kind == RelicEventKind.MagicResolved) Set("active", 0);
    }
    public override int ModifyMagicAttack(MagicModel magic, int amount)
        => Get("active") != 0 ? amount + Data.value : amount;
}
