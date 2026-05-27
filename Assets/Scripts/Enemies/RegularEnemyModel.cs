public class RegularEnemyModel : EnemyModel
{
    public RegularEnemyModel(EnemyData data) : base(data)
    {
        AddBuff(BuffEnum.Stable, 4);
    }
}
