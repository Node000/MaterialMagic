public static class EnemyFactory
{
    public static EnemyModel Create(EnemyData data)
    {
        if (data == null)
            return null;

        switch (data.numericId)
        {
            case 1: return new PodEnemyModel(data);
            case 2: return new OwlEnemyModel(data);
            case 3:
            case 4: return new SumoManEnemyModel(data);
            case 5:
            case 6: return new BatEnemyModel(data);
            case 7: return new FurBallEnemyModel(data);
            case 8: return new GoodShipEnemyModel(data);
            case 9: return new DancerEnemyModel(data);
            case 10: return new FlyKingEnemyModel(data);
            case 11: return new EggEnemyModel(data);
            case 12: return new MerchantEnemyModel(data);
            case 13: return new TrainingDummyEnemyModel(data);
            case 14: return new HealingCatEnemyModel(data);
            case 15: return new LuckyCatEnemyModel(data);
            case 16: return new DeerManEnemyModel(data);
            case 17: return new DemonEnemyModel(data);
            case 18: return new BlackWhiteBlockEnemyModel(data);
            case 19: return new FakeBlueEyeEnemyModel(data);
            case 20: return new FakeEyeballEnemyModel(data);
            case 21: return new BlueEyeEnemyModel(data);
            case 22: return new EyeballEnemyModel(data);
            case 23: return new RainbowEnemyModel(data);
            case 24: return new AntarcticaEnemyModel(data);
            case 1001: return new TutorialDummyLEnemyModel(data);
            case 1002: return new TutorialDummyXXLEnemyModel(data);
            default: return new EnemyModel(data);
        }
    }
}
