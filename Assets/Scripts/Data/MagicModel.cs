using System.Collections.Generic;

public class MagicModel
{
    private static readonly List<CombatantModel> enemyTargets = new List<CombatantModel>();

    public MagicData Data { get; }
    public int SlotIndex { get; set; }
    public List<MagicModifierModel> Modifiers { get; } = new List<MagicModifierModel>();

    public string Id => Data.id;
    public int NumericId => Data.numericId;
    public string Name => LocalizationSystem.GetText(Data.nameKey, Data.id);
    public string Description => LocalizationSystem.GetText(Data.descriptionKey, string.Empty);
    public bool HasModifier => Modifiers.Count > 0;
    public MagicModifierModel PrimaryModifier => Modifiers.Count > 0 ? Modifiers[0] : null;
    public virtual MagicEffectType EffectType => MagicEffectType.None;
    public virtual bool CastParticleTargetsAllEnemies => false;
    public virtual bool CastParticleTargetsPlayer => false;

    public MagicModel(MagicData data, int slotIndex = 0)
    {
        Data = data;
        SlotIndex = slotIndex;
    }

    public bool CanAddModifier(MagicModifierModel modifier)
    {
        return modifier != null && modifier.CanApplyTo(this);
    }

    public bool AddModifier(MagicModifierModel modifier)
    {
        if (!CanAddModifier(modifier))
            return false;

        modifier.model = this;
        Modifiers.Clear();
        Modifiers.Add(modifier);
        GameLog.Data($"Add magic modifier magic={Id} modifier={modifier.Id}");
        return true;
    }

    public MagicCastResult Cast(PlayerState playerState, EnemyModel enemyModel)
    {
        enemyTargets.Clear();
        if (enemyModel != null)
            enemyTargets.Add(new CombatantModel(enemyModel));

        MagicCastResult result = Cast(playerState, enemyModel, enemyTargets);
        enemyTargets.Clear();
        return result;
    }

    public MagicCastResult Cast(PlayerState playerState, EnemyModel enemyModel, IReadOnlyList<CombatantModel> allEnemyTargets)
    {
        BattleManager battleManager = new BattleManager(playerState);
        for (int i = 0; allEnemyTargets != null && i < allEnemyTargets.Count; i++)
        {
            CombatantModel target = allEnemyTargets[i];
            if (target != null && target.Enemy != null)
                battleManager.AddEnemy(target.Enemy);
        }
        battleManager.SetFocusTarget(enemyModel);
        return Cast(playerState, battleManager);
    }

    public MagicCastResult Cast(PlayerState playerState, BattleManager battleManager)
    {
        MagicCastResult result = new MagicCastResult();
        if (playerState == null || battleManager == null)
            return result;

        MaterialModifierModel.CurrentContext = new MaterialModifierContext { PlayerState = playerState, BattleManager = battleManager };
        SetModifierContext(playerState, battleManager);
        int castCount = 1 + GetAdditionalCastCount(playerState);
        GameLog.Data($"Cast magic {Id} ({Name}) replayCount={castCount - 1}");
        for (int castIndex = 0; castIndex < castCount; castIndex++)
        {
            EnemyModel target = battleManager.BeginCastTarget();
            GameLog.Data($"Magic {Id} resolve index={castIndex + 1}/{castCount} target={(target != null ? target.Id : "none")}");
            SetModifierContext(playerState, battleManager);
            TriggerMagicBeforeCast();
            TriggerInvoke(playerState, target);
            ResolveCast(playerState, battleManager, result);
            TriggerMagicAfterCast(result);
            MagicModifierModel.CurrentContext = null;
            battleManager.EndCastTarget();
        }

        return result;
    }

    protected virtual void ResolveCast(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
    }

    protected void TriggerInvoke(PlayerState playerState, EnemyModel enemyModel)
    {
        CombatantModel primaryTarget = enemyModel != null ? new CombatantModel(enemyModel) : null;
        playerState.TriggerOnInvoke(primaryTarget);
        if (enemyModel != null)
            enemyModel.TriggerOnInvoke(new CombatantModel(playerState));
    }

    protected void SetModifierContext(PlayerState playerState, BattleManager battleManager)
    {
        MagicModifierModel.CurrentContext = new MagicModifierContext
        {
            PlayerState = playerState,
            BattleManager = battleManager,
            Magic = this,
            Targets = battleManager != null ? battleManager.Enemies : null
        };
    }

    protected int GetAdditionalCastCount(PlayerState playerState)
    {
        int count = playerState != null ? playerState.GetBuffStack(BuffEnum.RepeatSpell) : 0;
        if (playerState != null)
        {
            int nextMagicRepeat = playerState.GetBuffStack(BuffEnum.NextMagicRepeat);
            if (nextMagicRepeat > 0)
            {
                count += nextMagicRepeat;
                playerState.ConsumeBuff(BuffEnum.NextMagicRepeat, nextMagicRepeat);
            }
        }
        for (int i = 0; i < Modifiers.Count; i++)
            count += Modifiers[i].GetAdditionalCastCount();
        return count;
    }

    protected void TriggerMagicBeforeCast()
    {
        for (int i = 0; i < Modifiers.Count; i++)
            Modifiers[i].BeforeCast();
    }

    protected void TriggerMagicAfterCast(MagicCastResult result)
    {
        for (int i = 0; i < Modifiers.Count; i++)
            Modifiers[i].AfterCast(result);
    }

    protected void TriggerMagicBeforeAttack(EnemyModel target, ref int attackValue)
    {
        for (int i = 0; i < Modifiers.Count; i++)
            Modifiers[i].BeforeAttack(target, ref attackValue);
    }

    protected void TriggerMagicAfterAttack(EnemyModel target, ref int attackResult)
    {
        for (int i = 0; i < Modifiers.Count; i++)
            Modifiers[i].AfterAttack(target, ref attackResult);
    }

    protected void TriggerMagicBeforeGainShield(ref int shieldValue)
    {
        for (int i = 0; i < Modifiers.Count; i++)
            Modifiers[i].BeforeGainShield(ref shieldValue);
    }

    protected void TriggerMagicAfterGainShield(ref int shieldGain)
    {
        for (int i = 0; i < Modifiers.Count; i++)
            Modifiers[i].AfterGainShield(ref shieldGain);
    }

    public void TriggerMagicBattleStart(PlayerState playerState, BattleManager battleManager)
    {
        SetModifierContext(playerState, battleManager);
        for (int i = 0; i < Modifiers.Count; i++)
            Modifiers[i].OnBattleStart();
        MagicModifierModel.CurrentContext = null;
    }

    public void TriggerMagicBattleEnd(PlayerState playerState, BattleManager battleManager)
    {
        SetModifierContext(playerState, battleManager);
        for (int i = 0; i < Modifiers.Count; i++)
            Modifiers[i].OnBattleEnd();
        MagicModifierModel.CurrentContext = null;
    }

    public void TriggerMagicTurnStart(PlayerState playerState, BattleManager battleManager)
    {
        SetModifierContext(playerState, battleManager);
        for (int i = 0; i < Modifiers.Count; i++)
            Modifiers[i].OnTurnStart();
        MagicModifierModel.CurrentContext = null;
    }

    public void TriggerMagicTurnEnd(PlayerState playerState, BattleManager battleManager)
    {
        SetModifierContext(playerState, battleManager);
        for (int i = 0; i < Modifiers.Count; i++)
            Modifiers[i].OnTurnEnd();
        MagicModifierModel.CurrentContext = null;
    }

    public int GetHitCount()
    {
        return 1;
    }

    public bool IsMatch(IReadOnlyList<MaterialModel> sequence, int startIndex)
    {
        if (Data.matchRule == MagicMatchRule.AnyTwoDifferentElements)
            return IsAnyTwoDifferentElements(sequence, startIndex);

        MaterialEnum[] recipe = Data.recipe;
        if (sequence == null || recipe == null || startIndex < 0 || startIndex + recipe.Length > sequence.Count)
            return false;

        for (int i = 0; i < recipe.Length; i++)
        {
            MaterialModel card = sequence[startIndex + i];
            if (card == null || !card.CanActAs(recipe[i]))
                return false;
        }

        return true;
    }

    public bool IsMatch(IReadOnlyList<ArrowReadToken> sequence, int startIndex)
    {
        if (Data.matchRule == MagicMatchRule.AnyTwoDifferentElements)
            return IsAnyTwoDifferentElements(sequence, startIndex);

        MaterialEnum[] recipe = Data.recipe;
        if (sequence == null || recipe == null || startIndex < 0 || startIndex + recipe.Length > sequence.Count)
            return false;

        for (int i = 0; i < recipe.Length; i++)
        {
            ArrowReadToken token = sequence[startIndex + i];
            if (token == null || !token.CanActAs(recipe[i]))
                return false;
        }

        return true;
    }

    protected EnemyModel Target(BattleManager battleManager)
    {
        return battleManager?.GetTargetEnemy();
    }

    protected void Damage(PlayerState playerState, EnemyModel target, int damage, MagicCastResult result)
    {
        if (target == null || damage <= 0)
            return;

        CombatantModel targetCombatant = new CombatantModel(target);
        CombatantModel caster = new CombatantModel(playerState);
        int attackValue = damage;
        TriggerMagicBeforeAttack(target, ref attackValue);
        playerState.TriggerOnAttack(targetCombatant, ref attackValue);
        GameLog.Data($"Magic {Id} damage target={target.Id} value={attackValue}");
        CombatDamageResult damageResult = target.TakeDamageResult(attackValue, caster);
        int attackResult = damageResult.HealthDamage;
        playerState.TriggerAfterAttack(targetCombatant, ref attackResult);
        TriggerMagicAfterAttack(target, ref attackResult);
        result.AddEnemyDamageHit(target, attackResult, damageResult.ShieldDamage);
    }

    protected void DamageTarget(PlayerState playerState, BattleManager battleManager, int damage, MagicCastResult result)
    {
        if (playerState != null && playerState.GetBuffStack(BuffEnum.MagicAttackAll) > 0)
        {
            DamageAll(playerState, battleManager, damage, result);
            return;
        }

        Damage(playerState, Target(battleManager), damage, result);
    }

    protected void DamageTargetTimes(PlayerState playerState, BattleManager battleManager, int damage, int times, MagicCastResult result)
    {
        for (int i = 0; i < times; i++)
        {
            DamageTarget(playerState, battleManager, damage, result);
            result.AdvanceDamageStep();
        }
    }

    protected void DamageAllTimes(PlayerState playerState, BattleManager battleManager, int damage, int times, MagicCastResult result)
    {
        for (int i = 0; i < times; i++)
        {
            DamageAll(playerState, battleManager, damage, result);
            result.AdvanceDamageStep();
        }
    }

    protected void DamageAll(PlayerState playerState, BattleManager battleManager, int damage, MagicCastResult result)
    {
        if (battleManager == null)
            return;

        IReadOnlyList<EnemyModel> enemies = battleManager.Enemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyModel enemy = enemies[i];
            if (enemy != null && !enemy.IsDead)
                Damage(playerState, enemy, damage, result);
        }
    }

    protected void AddBuff(UnitModel target, BuffEnum buffType, int stack, MagicCastResult result)
    {
        if (target == null || buffType == BuffEnum.None || stack <= 0)
            return;

        CombatantModel source = MagicModifierModel.CurrentContext?.PlayerState != null ? new CombatantModel(MagicModifierModel.CurrentContext.PlayerState) : null;
        target.AddBuff(buffType, stack, source);
        GameLog.Data($"Magic {Id} add buff target={target.DisplayName} buff={buffType} stack={stack}");
        result.enemyBuffApplied = target is EnemyModel && stack > 0;
    }

    protected void AddBuff(EnemyModel target, BuffEnum buffType, int stack, MagicCastResult result)
    {
        AddBuff((UnitModel)target, buffType, stack, result);
    }

    protected void AddBuffAll(BattleManager battleManager, BuffEnum buffType, int stack, MagicCastResult result)
    {
        if (battleManager == null)
            return;

        IReadOnlyList<EnemyModel> enemies = battleManager.Enemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyModel enemy = enemies[i];
            if (enemy != null && !enemy.IsDead)
                AddBuff(enemy, buffType, stack, result);
        }
    }

    protected bool UseExtraRefreshChance(PlayerState playerState)
    {
        return playerState.UseExtraRefreshChance();
    }

    protected void AddBuffSelf(PlayerState playerState, BuffEnum buffType, int stack)
    {
        playerState.AddBuff(buffType, stack);
    }

    protected MaterialModel AddTemporaryMaterialToHand(PlayerState playerState, MaterialEnum material)
    {
        return playerState.AddTemporaryMaterialNextTurn(material, true);
    }

    protected MaterialModel AddMaterialNextTurn(PlayerState playerState, MaterialEnum material, MaterialModifierModel modifier)
    {
        return playerState.AddMaterialNextTurn(material, modifier);
    }

    protected int GetBuffStack(EnemyModel target, BuffEnum buffType)
    {
        return target != null ? target.GetBuffStack(buffType) : 0;
    }

    protected int GetTotalEnemyDebuffStacks(BattleManager battleManager)
    {
        if (battleManager == null)
            return 0;

        int total = 0;
        IReadOnlyList<EnemyModel> enemies = battleManager.Enemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyModel enemy = enemies[i];
            if (enemy == null || enemy.IsDead)
                continue;

            foreach (BuffModel buff in enemy.Buffs.Values)
            {
                if (buff.IsDeBuff)
                    total += buff.stack;
            }
        }
        return total;
    }

    protected void AddAllEnemyDebuffStacks(BattleManager battleManager, int amount)
    {
        if (battleManager == null || amount <= 0)
            return;

        IReadOnlyList<EnemyModel> enemies = battleManager.Enemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyModel enemy = enemies[i];
            if (enemy == null || enemy.IsDead)
                continue;

            AddDebuffStackIfPresent(enemy, BuffEnum.Vulnerable, amount);
            AddDebuffStackIfPresent(enemy, BuffEnum.Slow, amount);
            AddDebuffStackIfPresent(enemy, BuffEnum.Weak, amount);
            AddDebuffStackIfPresent(enemy, BuffEnum.Arc, amount);
            AddDebuffStackIfPresent(enemy, BuffEnum.Burning, amount);
            AddDebuffStackIfPresent(enemy, BuffEnum.BurningNextTurn, amount);
            AddDebuffStackIfPresent(enemy, BuffEnum.BurnOnAttack, amount);
        }
    }

    private void AddDebuffStackIfPresent(EnemyModel enemy, BuffEnum buffType, int amount)
    {
        if (enemy.GetBuffStack(buffType) > 0)
        {
            CombatantModel source = MagicModifierModel.CurrentContext?.PlayerState != null ? new CombatantModel(MagicModifierModel.CurrentContext.PlayerState) : null;
            enemy.AddBuff(buffType, amount, source);
        }
    }

    protected void GainShield(PlayerState playerState, BattleManager battleManager, int amount, MagicCastResult result)
    {
        int shieldValue = amount;
        TriggerMagicBeforeGainShield(ref shieldValue);
        int shieldGain = playerState.GainShield(shieldValue);
        TriggerMagicAfterGainShield(ref shieldGain);
        GameLog.Data($"Magic {Id} gain shield={shieldGain}");
        result.playerShield += shieldGain;
    }

    private static bool IsAnyTwoDifferentElements(IReadOnlyList<MaterialModel> sequence, int startIndex)
    {
        if (sequence == null || startIndex < 0 || startIndex + 1 >= sequence.Count)
            return false;

        MaterialModel first = sequence[startIndex];
        MaterialModel second = sequence[startIndex + 1];
        if (first == null || second == null)
            return false;

        for (int firstIndex = 0; firstIndex < 4; firstIndex++)
        {
            MaterialEnum firstMaterial = GetBasicElementByIndex(firstIndex);
            if (!first.CanActAs(firstMaterial))
                continue;

            for (int secondIndex = 0; secondIndex < 4; secondIndex++)
            {
                MaterialEnum secondMaterial = GetBasicElementByIndex(secondIndex);
                if (firstMaterial != secondMaterial && second.CanActAs(secondMaterial))
                    return true;
            }
        }
        return false;
    }

    private static bool IsAnyTwoDifferentElements(IReadOnlyList<ArrowReadToken> sequence, int startIndex)
    {
        if (sequence == null || startIndex < 0 || startIndex + 1 >= sequence.Count)
            return false;

        ArrowReadToken first = sequence[startIndex];
        ArrowReadToken second = sequence[startIndex + 1];
        if (first == null || second == null)
            return false;

        for (int firstIndex = 0; firstIndex < 4; firstIndex++)
        {
            MaterialEnum firstMaterial = GetBasicElementByIndex(firstIndex);
            if (!first.CanActAs(firstMaterial))
                continue;

            for (int secondIndex = 0; secondIndex < 4; secondIndex++)
            {
                MaterialEnum secondMaterial = GetBasicElementByIndex(secondIndex);
                if (firstMaterial != secondMaterial && second.CanActAs(secondMaterial))
                    return true;
            }
        }
        return false;
    }

    private static MaterialEnum GetBasicElementByIndex(int index)
    {
        switch (index)
        {
            case 0: return MaterialEnum.Fire;
            case 1: return MaterialEnum.Wind;
            case 2: return MaterialEnum.Water;
            default: return MaterialEnum.Earth;
        }
    }

    private static bool IsBasicElement(MaterialEnum material)
    {
        return material == MaterialEnum.Fire || material == MaterialEnum.Wind || material == MaterialEnum.Water || material == MaterialEnum.Earth;
    }
}

public class MagicDamageHitResult
{
    public EnemyModel target;
    public int healthDamage;
    public int shieldDamage;
    public int stepIndex;

    public bool FullyBlocked => healthDamage <= 0 && shieldDamage > 0;

    public MagicDamageHitResult(EnemyModel target, int healthDamage, int shieldDamage, int stepIndex)
    {
        this.target = target;
        this.healthDamage = healthDamage;
        this.shieldDamage = shieldDamage;
        this.stepIndex = stepIndex;
    }
}

public class MagicCastResult
{
    public readonly List<MagicDamageHitResult> enemyDamageHits = new List<MagicDamageHitResult>();
    public int playerHeal;
    public int playerShield;
    public bool enemyBuffApplied;

    private int currentDamageStep;

    public void AddEnemyDamageHit(EnemyModel target, int healthDamage, int shieldDamage)
    {
        enemyDamageHits.Add(new MagicDamageHitResult(target, healthDamage, shieldDamage, currentDamageStep));
    }

    public void AdvanceDamageStep()
    {
        currentDamageStep++;
    }
}

public class MagicTriggerModel
{
    public MagicModel magic;
    public int startIndex;
    public int length;

    public MagicTriggerModel(MagicModel magic, int startIndex, int length)
    {
        this.magic = magic;
        this.startIndex = startIndex;
        this.length = length;
    }
}
