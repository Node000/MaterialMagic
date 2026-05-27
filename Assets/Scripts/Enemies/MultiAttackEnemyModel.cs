public class MultiAttackEnemyModel : EnemyModel
{
    public MultiAttackEnemyModel(EnemyData data) : base(data)
    {
        AddBuff(BuffEnum.Stable, 4);
    }
}
