public static class EnemyFactory
{
    public static EnemyModel Create(EnemyData data)
    {
        if (data == null)
            return null;

        switch (data.numericId)
        {
            case 1: return new Placeholder1EnemyModel(data);
            case 2: return new Placeholder2EnemyModel(data);
            case 3: return new Placeholder31EnemyModel(data);
            case 4: return new Placeholder32EnemyModel(data);
            case 5: return new Placeholder4EnemyModel(data);
            case 6: return new Placeholder5EnemyModel(data);
            case 7: return new Placeholder6EnemyModel(data);
            case 8: return new Placeholder61EnemyModel(data);
            case 9: return new Placeholder7EnemyModel(data);
            case 10: return new Placeholder8EnemyModel(data);
            case 11: return new Placeholder9EnemyModel(data);
            case 12: return new Placeholder10EnemyModel(data);
            case 13: return new Placeholder101EnemyModel(data);
            default: return new EnemyModel(data);
        }
    }
}
