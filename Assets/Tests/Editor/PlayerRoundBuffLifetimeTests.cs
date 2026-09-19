using NUnit.Framework;

/// <summary>
/// 玩家侧「本回合」Buff 的清除时机约定：触发点会落在敌方回合的 Buff（不倒翁/泡沫板等）
/// 必须在玩家下个回合开始时清除，覆盖整轮（玩家回合 + 紧接的敌方回合）；
/// 若在自己的回合结束时清除，敌方回合里由玩家来源施加的负面状态（汽油、焦糖熊）就吃不到增幅。
/// </summary>
public class PlayerRoundBuffLifetimeTests
{
    private static EnemyModel CreateEnemy(int numericId = 951)
    {
        return new EnemyModel(new EnemyData
        {
            numericId = numericId,
            string_id = "test_enemy_" + numericId,
            maxHealth = 50
        });
    }

    /// <summary>模拟敌方回合：敌人攻击玩家，玩家侧的反击类 Buff（汽油）在该钩子里给攻击者加燃烧。</summary>
    private static void EnemyAttacksPlayer(PlayerState player, EnemyModel enemy, int damage = 3)
    {
        player.TriggerAfterTakeDamage(new CombatantModel(enemy), new CombatDamageResult
        {
            RawDamage = damage,
            FinalDamage = damage,
            HealthDamage = damage
        });
    }

    [Test]
    public void TumblerAmplifiesGasolineBurningAppliedDuringEnemyTurn()
    {
        PlayerState player = new PlayerState(50, 0);
        player.AddBuff(BuffEnum.BurningOnEnemyAttack, 5); // 汽油
        player.AddBuff(BuffEnum.ExtraEnemyDebuff, 1);     // 不倒翁

        player.TriggerOnTurnEnd(null); // 玩家回合结束，即将进入敌方回合

        EnemyModel enemy = CreateEnemy();
        EnemyAttacksPlayer(player, enemy);

        Assert.That(enemy.GetBuffStack(BuffEnum.Burning), Is.EqualTo(10),
            "敌方回合里汽油施加的燃烧应被不倒翁放大");
    }

    [Test]
    public void TumblerExpiresAtNextPlayerTurnStartAndStopsAmplifying()
    {
        PlayerState player = new PlayerState(50, 0);
        player.AddBuff(BuffEnum.ExtraEnemyDebuff, 1);

        player.TriggerOnTurnEnd(null);
        player.TriggerOnTurnStart(null);

        Assert.That(player.GetBuffStack(BuffEnum.ExtraEnemyDebuff), Is.Zero,
            "不倒翁应在玩家下个回合开始时清除，不能延续到新的回合");

        EnemyModel enemy = CreateEnemy();
        enemy.AddBuff(BuffEnum.Burning, 5, new CombatantModel(player));

        Assert.That(enemy.GetBuffStack(BuffEnum.Burning), Is.EqualTo(5), "清除后不再放大");
    }

    [Test]
    public void FoamBoardGrantsShieldForEnemyTurnDebuffs()
    {
        PlayerState player = new PlayerState(50, 0);
        player.AddBuff(BuffEnum.FoamShield, 1); // 泡沫板

        player.TriggerOnTurnEnd(null);

        EnemyModel enemy = CreateEnemy();
        enemy.AddBuff(BuffEnum.Burning, 5, new CombatantModel(player));

        Assert.That(player.Shield, Is.EqualTo(5), "敌方回合里敌方获得负面状态时泡沫板应给予等量护盾");
    }

    [Test]
    public void FoamBoardExpiresAtNextPlayerTurnStart()
    {
        PlayerState player = new PlayerState(50, 0);
        player.AddBuff(BuffEnum.FoamShield, 1);
        player.TriggerOnTurnEnd(null);
        player.TriggerOnTurnStart(null);

        EnemyModel enemy = CreateEnemy();
        enemy.AddBuff(BuffEnum.Burning, 5, new CombatantModel(player));

        Assert.That(player.Shield, Is.Zero, "泡沫板清除后不再给护盾");
    }
}
