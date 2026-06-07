using System.Collections.Generic;

public class MoltenMagicModel : MagicModel
{
    public MoltenMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    public override MagicEffectType EffectType => MagicEffectType.Damage;
    protected override void ResolveCast(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        EnemyModel target = Target(battleManager);
        if (playerState.GetBuffStack(BuffEnum.MagicAttackAll) > 0)
        {
            IReadOnlyList<EnemyModel> enemies = battleManager.Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyModel enemy = enemies[i];
                if (enemy != null && !enemy.IsDead)
                    Damage(playerState, enemy, 5 + GetBuffStack(enemy, BuffEnum.Burning), result);
            }
            return;
        }

        Damage(playerState, target, 5 + GetBuffStack(target, BuffEnum.Burning), result);
    }
}
