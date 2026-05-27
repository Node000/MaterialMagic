public class EliteEnemyModel : EnemyModel
{
    public EliteEnemyModel(EnemyData data) : base(data)
    {
        AddBuff(BuffEnum.Stable, 7);
    }
}
