public class DefensiveEnemyModel : EnemyModel
{
    public DefensiveEnemyModel(EnemyData data) : base(data)
    {
        AddBuff(BuffEnum.Stable, 5);
    }
}
