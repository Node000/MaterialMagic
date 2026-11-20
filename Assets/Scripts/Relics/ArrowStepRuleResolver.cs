/// <summary>Arrow base rules callable without HandSystemUI. No animation, sound or view access.</summary>
public static class ArrowStepRuleResolver
{
    public static MagicCastResult Resolve(PlayerState player, BattleManager battle, ArrowReadStep step, bool replay = false)
    {
        var result = new MagicCastResult();
        if (player == null || battle == null || step?.SourceCard == null) return result;
        using (player.Relics.BeginOperation())
        {
            step.SourceCard.TriggerOnArrowBaseEffectResolve(new ArrowReadContext(player, battle));
            ArrowUpgradeSystem.TriggerOnRead(player, step.SourceCard, battle);
            int repeat = 1 + player.GetBuffStack(BuffEnum.MaterialBaseEffectRepeat);
            for (int r = 0; r < repeat; r++)
                for (int i = 0; i < step.BaseEffectDirections.Count; i++)
                    ResolveDirection(player, battle, step.BaseEffectDirections[i], result);
            player.Relics.Publish(new RelicEvent(RelicEventKind.ArrowRead, card: step.SourceCard, isReplay: replay));
        }
        return result;
    }

    private static void ResolveDirection(PlayerState player, BattleManager battle, MaterialEnum direction, MagicCastResult result)
    {
        if (direction == MaterialEnum.Wind) { player.AddBuff(BuffEnum.ExtraDraw, 1); return; }
        if (direction == MaterialEnum.Earth) { result.playerShield += player.GainShield(3); return; }
        var target = battle.BeginCastTarget();
        try
        {
            if (target == null) return;
            var source = new CombatantModel(player);
            if (direction == MaterialEnum.Water)
            {
                target.AddBuff(BuffEnum.Weak, 1, source);
                target.AddBuff(BuffEnum.Vulnerable, 1, source);
                result.enemyBuffApplied = true;
            }
            else if (direction == MaterialEnum.Fire)
            {
                using (player.Relics.BeginDamage(RelicDamageKind.Attack))
                {
                    var victim = new CombatantModel(target);
                    int amount = 3;
                    player.TriggerOnAttack(victim, ref amount);
                    var hit = target.TakeDamageResult(amount, source);
                    int actual = hit.HealthDamage;
                    player.TriggerAfterAttack(victim, ref actual);
                    result.AddEnemyDamageHit(target, actual, hit.ShieldDamage);
                }
            }
        }
        finally { battle.EndCastTarget(); }
    }
}
