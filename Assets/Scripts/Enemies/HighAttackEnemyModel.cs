public class HighAttackEnemyModel : EnemyModel
{
    public HighAttackEnemyModel(EnemyData data) : base(data)
    {
        AddBuff(BuffEnum.Stable, 5);
    }
}
