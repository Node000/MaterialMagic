public class Placeholder61EnemyModel : EnemyModel
{
    public Placeholder61EnemyModel(EnemyData data) : base(data)
    {
    }

    protected override void HandleDeathEffect(CombatantModel opponent)
    {
        DealDamageToAllUnits(5);
    }
}
