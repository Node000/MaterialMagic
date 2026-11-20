using System.Collections.Generic;

public class MagicModel
{
    public MagicData Data { get; }
    public int SlotIndex { get; set; }
    /// <summary>Persisted slot-policy rank applied by PlayerState; -1 retains legacy slot fallback.</summary>
    public int ReadingRank { get; internal set; } = -1;
    public List<MagicModifierModel> Modifiers { get; } = new List<MagicModifierModel>();

    public string Id => Data.id;
    public int NumericId => Data.numericId;
    public string Name => LocalizationSystem.GetText(Data.nameKey, Data.id);
    public string Description => LocalizationSystem.GetText(Data.descriptionKey, string.Empty);
    public bool HasModifier => Modifiers.Count > 0;
    public MagicModifierModel PrimaryModifier => Modifiers.Count > 0 ? Modifiers[0] : null;
    private MaterialEnum[] effectiveRecipe;
    public virtual MagicEffectType EffectType => MagicEffectType.None;
    public virtual bool CastParticleTargetsAllEnemies => false;
    public virtual bool CastParticleTargetsPlayer => false;
    protected virtual bool AllowsModifiers => true;

    public int GetIndex() => SlotIndex;

    protected virtual MagicMatchRule NativeMatchRule => Data?.matchRule ?? MagicMatchRule.NoTrigger;
    internal MagicTrigger GetOwnTrigger() => new MagicTrigger(this, NativeMatchRule, GetEffectiveRecipe());
    public virtual MagicTrigger GetCombatTrigger(PlayerState player = null) => GetOwnTrigger();

    public virtual int ModifySellPrice(int basePrice) => basePrice;
    public virtual void OnRoomEntered(PlayerStatus player) { }
    internal virtual int ExportSellPriceState() => -1;
    internal virtual void RestoreSellPriceState(int value) { }

    public MagicModel(MagicData data, int slotIndex = 0)
    {
        Data = data;
        SlotIndex = slotIndex;
    }

    public bool CanAddModifier(MagicModifierModel modifier)
    {
        return AllowsModifiers && modifier != null && modifier.CanApplyTo(this);
    }

    public bool AddModifier(MagicModifierModel modifier)
    {
        if (!CanAddModifier(modifier))
            return false;

        modifier.model = this;
        Modifiers.Clear();
        Modifiers.Add(modifier);
        effectiveRecipe = null;
        GameLog.Data($"Add magic modifier magic={Id} modifier={modifier.Id}");
        return true;
    }

    public MaterialEnum[] GetEffectiveRecipe()
    {
        if (effectiveRecipe == null)
        {
            MaterialEnum[] recipe = Data != null ? Data.recipe : null;
            for (int i = 0; i < Modifiers.Count; i++)
                recipe = Modifiers[i].ModifyRecipe(recipe);
            effectiveRecipe = recipe;
        }
        return effectiveRecipe;
    }

    public MagicCastResult Cast(PlayerState playerState, EnemyModel enemyModel)
    {
        var enemyTargets = new List<CombatantModel>();
        if (enemyModel != null)
            enemyTargets.Add(new CombatantModel(enemyModel));

        MagicCastResult result = Cast(playerState, enemyModel, enemyTargets);
        enemyTargets.Clear();
        return result;
    }

    public MagicCastResult Cast(PlayerState playerState, EnemyModel enemyModel, IReadOnlyList<CombatantModel> allEnemyTargets)
    {
        BattleManager previousBattle = playerState?.Battle;
        BattleManager battleManager = new BattleManager(playerState);
        try
        {
        for (int i = 0; allEnemyTargets != null && i < allEnemyTargets.Count; i++)
        {
            CombatantModel target = allEnemyTargets[i];
            if (target != null && target.Enemy != null)
                battleManager.AddEnemy(target.Enemy);
        }
        battleManager.SetFocusTarget(enemyModel);
        return Cast(playerState, battleManager);
        }
        finally
        {
            if (playerState != null)
            {
                playerState.Battle = previousBattle;
                playerState.Relics.BindBattle(previousBattle);
            }
            if (allEnemyTargets != null)
                foreach (CombatantModel target in allEnemyTargets)
                    if (target?.Enemy != null && ReferenceEquals(target.Enemy.Battle, battleManager))
                    { target.Enemy.Battle = previousBattle; target.Enemy.RelicRules = previousBattle?.PlayerState?.Relics; }
        }
    }

    public MagicCastResult Cast(PlayerState playerState, BattleManager battleManager)
        => Cast(playerState, battleManager, GetCombatTrigger(playerState));

    internal MagicCastResult Cast(PlayerState playerState, BattleManager battleManager, MagicTrigger trigger)
    {
        MagicCastResult result = new MagicCastResult();
        if (playerState == null || battleManager == null || trigger.Length == 0)
            return result;

        using (playerState.Rules.Enter())
        {
        // A copied effect runs on a fresh, unmodified model; lifecycle and source remain the caster's.
        MagicModel effect = ReferenceEquals(trigger.Source, this) ? this : MagicFactory.Create(trigger.Source.Data, SlotIndex);
        MaterialModifierModel.CurrentContext = new MaterialModifierContext { PlayerState = playerState, BattleManager = battleManager };
        SetModifierContext(playerState, battleManager);
        int castCount = 1 + GetAdditionalCastCount(playerState);
        GameLog.Data($"Cast magic {Id} ({Name}) replayCount={castCount - 1}");
        for (int castIndex = 0; castIndex < castCount; castIndex++)
        {
            EnemyModel target = battleManager.BeginCastTarget();
            GameLog.Data($"Magic {Id} resolve index={castIndex + 1}/{castCount} target={(target != null ? target.Id : "none")}");
            SetModifierContext(playerState, battleManager);
            playerState.Relics.Publish(new RelicEvent(RelicEventKind.MagicStarted, magic: this, isReplay: castIndex > 0));
            TriggerMagicBeforeCast();
            TriggerPlayerInvoke(playerState, target);
            effect.ResolveCast(playerState, battleManager, result);
            TriggerMagicAfterCast(result);
            TriggerEnemyArcAfterCast(battleManager, result);
            playerState.Relics.Publish(new RelicEvent(RelicEventKind.MagicResolved, magic: this, isReplay: castIndex > 0));
            MagicModifierModel.CurrentContext = null;
            battleManager.EndCastTarget();
        }

        return result;
        }
    }

    protected virtual void ResolveCast(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
    }

    protected void TriggerPlayerInvoke(PlayerState playerState, EnemyModel enemyModel)
    {
        CombatantModel primaryTarget = enemyModel != null ? new CombatantModel(enemyModel) : null;
        playerState.TriggerOnInvoke(primaryTarget);
    }

    protected void TriggerEnemyArcAfterCast(BattleManager battleManager, MagicCastResult result)
    {
        IReadOnlyList<EnemyModel> enemies = battleManager != null ? battleManager.Enemies : null;
        bool hasArcTarget = false;
        for (int i = 0; enemies != null && i < enemies.Count; i++)
        {
            EnemyModel enemy = enemies[i];
            if (enemy != null && !enemy.IsDead && enemy.GetBuffStack(BuffEnum.Arc) > 0)
            {
                hasArcTarget = true;
                break;
            }
        }

        if (!hasArcTarget)
            return;

        result.AdvanceDamageStep();
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyModel enemy = enemies[i];
            if (enemy != null && !enemy.IsDead)
                enemy.TriggerArcAfterMagic(result);
        }
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

    public bool IsMatch(IReadOnlyList<MaterialModel> sequence, int startIndex, PlayerState player = null)
    {
        MagicTrigger trigger = GetCombatTrigger(player);
        if (trigger.Length == 0) return false;
        if (trigger.Rule == MagicMatchRule.AnyTwoDifferentElements)
            return IsAnyTwoDifferentElements(sequence, startIndex);

        IReadOnlyList<MaterialEnum> recipe = trigger.Recipe;
        if (sequence == null || startIndex < 0 || startIndex > sequence.Count - trigger.Length)
            return false;

        for (int i = 0; i < trigger.Length; i++)
        {
            MaterialModel card = sequence[startIndex + i];
            if (card == null || !card.CanActAs(recipe[i]))
                return false;
        }

        return true;
    }

    public bool IsMatch(IReadOnlyList<ArrowReadToken> sequence, int startIndex, PlayerState player = null)
        => Matches(GetCombatTrigger(player), sequence, startIndex);

    internal static bool Matches(MagicTrigger trigger, IReadOnlyList<ArrowReadToken> sequence, int startIndex)
    {
        if (trigger.Length == 0) return false;
        if (trigger.Rule == MagicMatchRule.AnyTwoDifferentElements)
            return IsAnyTwoDifferentElements(sequence, startIndex);

        IReadOnlyList<MaterialEnum> recipe = trigger.Recipe;
        if (sequence == null || startIndex < 0 || startIndex > sequence.Count - trigger.Length)
            return false;

        for (int i = 0; i < trigger.Length; i++)
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
        using (playerState.Relics.BeginOperation())
        using (playerState.Relics.BeginDamage(RelicDamageKind.Attack))
        {
        if (target == null || damage <= 0)
            return;

        CombatantModel targetCombatant = new CombatantModel(target);
        CombatantModel caster = new CombatantModel(playerState);
        int attackValue = damage;
        TriggerMagicBeforeAttack(target, ref attackValue);
        playerState.TriggerOnAttack(targetCombatant, ref attackValue);
        attackValue = playerState.Relics.ModifyMagicAttack(MagicModifierModel.CurrentContext?.Magic ?? this, attackValue);
        GameLog.Data($"Magic {Id} damage target={target.Id} value={attackValue}");
        CombatDamageResult damageResult = target.TakeDamageResult(attackValue, caster);
        int attackResult = damageResult.HealthDamage;
        playerState.TriggerAfterAttack(targetCombatant, ref attackResult);
        TriggerMagicAfterAttack(target, ref attackResult);
        result.AddEnemyDamageHit(target, attackResult, damageResult.ShieldDamage);
        }
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
        if (sequence == null || startIndex < 0 || startIndex >= sequence.Count - 1)
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
        => TryGetDistinctTriggerElements(sequence, startIndex, out _, out _);

    internal static bool TryGetDistinctTriggerElements(IReadOnlyList<ArrowReadToken> sequence, int startIndex,
        out MaterialEnum firstElement, out MaterialEnum secondElement)
    {
        firstElement = secondElement = MaterialEnum.None;
        if (sequence == null || startIndex < 0 || startIndex >= sequence.Count - 1)
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
                {
                    firstElement = firstMaterial;
                    secondElement = secondMaterial;
                    return true;
                }
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
    public int HealthAfter { get; }
    public int ShieldAfter { get; }
    public int MaxHealth { get; }
    public BattleStateSnapshot StateAfter { get; internal set; }
    public IReadOnlyList<System.Action> FeedbackCues { get; internal set; } = System.Array.Empty<System.Action>();

    public bool FullyBlocked => healthDamage <= 0 && shieldDamage > 0;

    public MagicDamageHitResult(EnemyModel target, int healthDamage, int shieldDamage, int stepIndex, int? healthAfter = null, int? shieldAfter = null)
    {
        this.target = target;
        this.healthDamage = healthDamage;
        this.shieldDamage = shieldDamage;
        this.stepIndex = stepIndex;
        HealthAfter = healthAfter ?? (target != null ? target.CurrentHealth : 0);
        ShieldAfter = shieldAfter ?? (target != null ? target.Shield : 0);
        MaxHealth = target != null ? target.MaxHealth : 0;
    }
}

public class MagicCastResult
{
    public readonly List<MagicDamageHitResult> enemyDamageHits = new List<MagicDamageHitResult>();
    internal List<MaterialModel> CreatedHandCards { get; } = new List<MaterialModel>();
    public int playerHeal;
    public int playerShield;
    public int playerGoldGain;
    public bool enemyBuffApplied;

    private int currentDamageStep;

    public void AddEnemyDamageHit(EnemyModel target, int healthDamage, int shieldDamage)
    {
        var hit = new MagicDamageHitResult(target, healthDamage, shieldDamage, currentDamageStep)
        { FeedbackCues = RuleExecutionScope.DrainFeedback() };
        if (target?.Battle != null) hit.StateAfter = BattleStateSnapshot.Capture(target.Battle.PlayerState, target.Battle);
        enemyDamageHits.Add(hit);
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
