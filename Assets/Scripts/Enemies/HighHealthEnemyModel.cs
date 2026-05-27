public class HighHealthEnemyModel : EnemyModel
{
    public HighHealthEnemyModel(EnemyData data) : base(data)
    {
        AddBuff(BuffEnum.Stable, 6);
    }
}
