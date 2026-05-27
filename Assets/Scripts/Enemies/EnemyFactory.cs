public static class EnemyFactory
{
    public static EnemyModel Create(EnemyData data)
    {
        if (data == null)
            return null;

        switch (data.numericId)
        {
            case 1: return new RegularEnemyModel(data);
            case 2: return new HighAttackEnemyModel(data);
            case 3: return new MultiAttackEnemyModel(data);
            case 4: return new HighHealthEnemyModel(data);
            case 5: return new DefensiveEnemyModel(data);
            case 6: return new EliteEnemyModel(data);
            default: return new EnemyModel(data);
        }
    }
}
