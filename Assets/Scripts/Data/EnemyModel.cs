using System;
using System.Collections.Generic;
using UnityEngine;

public class EnemyModel : UnitModel
{
    private readonly Dictionary<BuffEnum, BuffModel> buffs = new Dictionary<BuffEnum, BuffModel>();
    private readonly HashSet<int> consumedOnlyOnceIntentIds = new HashSet<int>();
    private readonly EnemyIntentPlanState intentPlanState = new EnemyIntentPlanState();
    private EnemyRuntimeDefinition runtimeDefinition;

    internal static bool SuppressConstructorRuntimeInitialization { get; set; }
    private int lastResolvedIntentGroupId = -1;

    public event Action<EnemyModel, BuffEnum, int> BuffAdded;

    public EnemyData Data { get; private set; }
    public override int CurrentHealth { get; protected set; }
    public override int Shield { get; protected set; }
    public int ActionIndex { get; private set; }
    public int Phase { get; private set; }
    public override IReadOnlyDictionary<BuffEnum, BuffModel> Buffs => buffs;
    public IReadOnlyList<EnemyIntentData> CurrentIntents { get; }
    public EnemyRuntimeDefinition RuntimeDefinition => runtimeDefinition;
    public bool HasSpawnPosition { get; private set; }
    public float SpawnPositionX { get; private set; }
    public float SpawnPositionY { get; private set; }
    public bool CanActThisEnemyTurn { get; private set; } = true;
    public bool IsMinion { get; private set; }

    private bool dead;

    public string Id => Data != null ? Data.Id : string.Empty;
    public override int NumericId => Data != null ? Data.numericId : 0;
    public string Name => Data != null ? LocalizationSystem.GetText(Data.nameKey, Data.Id) : string.Empty;
    public override string DisplayName => Name;
    public override int MaxHealth => runtimeDefinition != null ? runtimeDefinition.MaxHealth : Data != null ? Data.maxHealth : 1;
    public override bool IsDead => CurrentHealth <= 0;
    public override bool DeathHandled => dead;

    public EnemyModel(EnemyData data)
    {
        CurrentIntents = new List<EnemyIntentData>();
        if (SuppressConstructorRuntimeInitialization)
        {
            Data = data;
            IsMinion = data != null && data.isMinion;
            CurrentHealth = data != null ? Mathf.Max(1, data.maxHealth) : 1;
            return;
        }

        ApplyRuntimeDefinition(new EnemyRuntimeDefinition(data));
    }

    public void ApplyRuntimeDefinition(EnemyRuntimeDefinition definition)
    {
        if (definition == null || definition.BaseData == null)
            return;

        runtimeDefinition = definition;
        Data = definition.BaseData;
        buffs.Clear();
        consumedOnlyOnceIntentIds.Clear();
        intentPlanState.ConsumedOnlyOnceIntentIds.Clear();
        lastResolvedIntentGroupId = -1;
        intentPlanState.LastResolvedIntentGroupId = -1;
        intentPlanState.SelectedIntentPhase = -1;
        intentPlanState.SelectedIntentActionIndex = -1;
        intentPlanState.SelectedIntentGroupId = 0;
        Phase = 0;
        intentPlanState.Phase = 0;
        ActionIndex = 0;
        intentPlanState.ActionIndex = 0;
        Shield = 0;
        dead = false;
        IsMinion = definition.IsMinion;
        CurrentHealth = definition.MaxHealth;
        ApplyInitialBuffs();
        if (IsMinion)
            AddBuff(BuffEnum.Claw, 1);
        UpdateCurrentIntents();
    }

    public void SetCanActThisEnemyTurn(bool canAct)
    {
        CanActThisEnemyTurn = canAct;
    }

    public void SetMinion(bool isMinion)
    {
        if (IsMinion == isMinion)
            return;

        IsMinion = isMinion;
        if (IsMinion && !IsDead)
            AddBuff(BuffEnum.Claw, 1);
    }

    public void RestoreBattleState(EnemyBattleSaveData data)
    {
        if (data == null)
            return;

        CurrentHealth = Mathf.Clamp(data.currentHealth, 0, MaxHealth);
        Shield = Mathf.Max(0, data.shield);
        ActionIndex = Mathf.Max(0, data.actionIndex);
        Phase = Mathf.Max(0, data.phase);
        SyncIntentPlanStateFromEnemyState();
        RestoreIntentPlanState(data.consumedOnlyOnceIntentIds, data.lastResolvedIntentGroupId, data.selectedIntentPhase, data.selectedIntentActionIndex, data.selectedIntentGroupId);
        dead = data.deathHandled;
        CanActThisEnemyTurn = data.canActThisEnemyTurn;
        IsMinion = data.isMinion;
        if (data.hasSpawnPosition)
            SetSpawnPosition(data.spawnPositionX, data.spawnPositionY);
        buffs.Clear();
        for (int i = 0; data.buffs != null && i < data.buffs.Length; i++)
        {
            BuffStackData buff = data.buffs[i];
            if (buff != null && buff.buffType != BuffEnum.None && buff.stack > 0)
                buffs[buff.buffType] = BuffModel.Create(buff.buffType, buff.stack);
        }
        UpdateCurrentIntents();
    }

    public void Kill(CombatantModel attacker = null)
    {
        if (dead || CurrentHealth <= 0)
            return;

        CurrentHealth = 0;
        Shield = 0;
        TriggerOnDie(attacker);
    }

    public void KillAsBattleCleanup()
    {
        if (IsDead)
            return;

        CurrentHealth = 0;
        Shield = 0;
    }

    public void ClearCurrentIntents()
    {
        ((List<EnemyIntentData>)CurrentIntents).Clear();
    }

    public void SetSpawnPosition(float x, float y)
    {
        HasSpawnPosition = true;
        SpawnPositionX = x;
        SpawnPositionY = y;
    }

    private void ApplyInitialBuffs()
    {
        IReadOnlyList<BuffStackData> initialBuffs = runtimeDefinition != null ? runtimeDefinition.InitialBuffs : null;
        if (initialBuffs == null)
            return;

        for (int i = 0; i < initialBuffs.Count; i++)
        {
            BuffStackData initialBuff = initialBuffs[i];
            if (initialBuff != null)
                AddBuff(initialBuff.buffType, initialBuff.stack);
        }
    }

    public EnemyActionData GetCurrentAction()
    {
        return runtimeDefinition != null && runtimeDefinition.IntentPlan != null ? runtimeDefinition.IntentPlan.GetCurrentAction(intentPlanState) : null;
    }

    public void ResolveCurrentIntents(PlayerState playerState)
    {
        BeginResolveIntents(playerState);
        ProcessIntent(playerState);
        EndResolveIntents(playerState);
    }

    public virtual void ProcessIntent(PlayerState playerState)
    {
        for (int i = 0; i < CurrentIntents.Count; i++)
        {
            EnemyIntentData intent = CurrentIntents[i];
            if (intent == null)
                continue;

            if (intent.actionType == EnemyActionType.Special)
                ProcessSpecialIntent(intent.value, playerState);
            else
                ResolveStandardIntent(intent, playerState);
        }
    }

    public void BeginResolveIntents(PlayerState playerState)
    {
        CombatantModel self = new CombatantModel(this);
        CombatantModel opponent = new CombatantModel(playerState);
        TriggerOnGetAction(opponent);
        opponent.Player?.TriggerOnGetAction(self);
    }

    public void ResolveCurrentIntentAt(int intentIndex, PlayerState playerState)
    {
        if (intentIndex < 0 || intentIndex >= CurrentIntents.Count)
            return;

        EnemyIntentData intent = CurrentIntents[intentIndex];
        if (intent.actionType == EnemyActionType.Special)
            ProcessSpecialIntent(intent.value, playerState);
        else
            ResolveStandardIntent(intent, playerState);
    }

    public void ResolveCurrentIntentHitAt(int intentIndex, int hitIndex, PlayerState playerState)
    {
        if (intentIndex < 0 || intentIndex >= CurrentIntents.Count)
            return;

        EnemyIntentData intent = CurrentIntents[intentIndex];
        if (intent.actionType == EnemyActionType.Special)
        {
            if (hitIndex == 0)
                ProcessSpecialIntent(intent.value, playerState);
            return;
        }

        ResolveStandardIntentHit(intent, hitIndex, playerState);
    }

    public void EndResolveIntents(PlayerState playerState)
    {
        CombatantModel self = new CombatantModel(this);
        CombatantModel opponent = new CombatantModel(playerState);
        TriggerAfterGetAction(opponent);
        opponent.Player?.TriggerAfterGetAction(self);
        AdvanceAction();
    }

    public void AdvanceAction()
    {
        ActionIndex++;
        SyncIntentPlanStateFromEnemyState();
        GameLog.Data($"Enemy {Id} advance action index={ActionIndex}");
        UpdateCurrentIntents();
    }

    public virtual void SetPhase(int phase)
    {
        Phase = phase < 0 ? 0 : phase;
        ActionIndex = 0;
        SyncIntentPlanStateFromEnemyState();
        GameLog.Data($"Enemy {Id} set phase={Phase}");
    }

    public void TakeDamage(int damage)
    {
        TakeDamage(damage, null);
    }

    public override int TakeDamage(int damage, CombatantModel attacker)
    {
        return TakeDamageResult(damage, attacker).HealthDamage;
    }

    public int TakeDamageIgnoringVulnerable(int damage)
    {
        return TakeDamageResult(damage, null, BuffEnum.Vulnerable).HealthDamage;
    }

    public override CombatDamageResult TakeDamageResult(int damage, CombatantModel attacker)
    {
        return TakeDamageResult(damage, attacker, BuffEnum.None);
    }

    private CombatDamageResult TakeDamageResult(int damage, CombatantModel attacker, BuffEnum ignoredOnTakeDamageBuff)
    {
        CombatDamageResult result = new CombatDamageResult { RawDamage = damage };
        if (damage <= 0)
            return result;

        int remainingDamage = damage;
        TriggerOnTakeDamage(attacker, ref remainingDamage, ignoredOnTakeDamageBuff);
        if (remainingDamage <= 0)
            return result;

        result.FinalDamage = remainingDamage;
        int healthBefore = CurrentHealth;
        int blockedDamage = 0;
        if (Shield > 0)
        {
            blockedDamage = remainingDamage < Shield ? remainingDamage : Shield;
            Shield -= blockedDamage;
            remainingDamage -= blockedDamage;
        }

        CurrentHealth -= remainingDamage;
        if (CurrentHealth < 0)
            CurrentHealth = 0;
        int healthDamage = healthBefore - CurrentHealth;
        result.ShieldDamage = blockedDamage;
        result.HealthDamage = healthDamage;
        result.TargetDied = healthBefore > 0 && CurrentHealth <= 0;
        GameLog.Data($"Enemy {Id} take damage raw={damage} final={result.FinalDamage} finalHealthDamage={healthDamage} shieldNow={Shield} hp={CurrentHealth}/{MaxHealth}");
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayDamageResultSfx(healthDamage, blockedDamage);

        TriggerAfterTakeDamage(attacker, result);
        if (result.TargetDied)
            TriggerOnDie(attacker);

        return result;
    }

    public override int TakeDirectDamage(int damage)
    {
        if (damage <= 0)
            return 0;

        int healthBefore = CurrentHealth;
        CurrentHealth -= damage;
        if (CurrentHealth < 0)
            CurrentHealth = 0;
        int healthDamage = healthBefore - CurrentHealth;
        GameLog.Data($"Enemy {Id} take direct damage={damage} hp={CurrentHealth}/{MaxHealth}");
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayDamageResultSfx(healthDamage, 0);

        if (healthBefore > 0 && CurrentHealth <= 0)
            TriggerOnDie(null);

        return healthDamage;
    }

    public override int GainShield(int amount)
    {
        if (amount <= 0)
            return 0;

        int shieldValue = amount;
        int slowReduction = ApplyGainShieldModifiers(ref shieldValue);
        if (shieldValue <= 0)
        {
            if (slowReduction > 0)
                ConsumeBuff(BuffEnum.Slow, slowReduction);
            return 0;
        }

        Shield += shieldValue;
        if (slowReduction > 0)
            ConsumeBuff(BuffEnum.Slow, slowReduction);
        GameLog.Data($"Enemy {Id} gain shield={shieldValue} shield={Shield}");
        return shieldValue;
    }

    public override int ConsumeShield(int amount)
    {
        if (amount <= 0 || Shield <= 0)
            return 0;

        int consumed = amount < Shield ? amount : Shield;
        Shield -= consumed;
        GameLog.Data($"Enemy {Id} consume shield={consumed} shield={Shield}");
        return consumed;
    }

    public override void ClearShield()
    {
        Shield = 0;
        GameLog.Data($"Enemy {Id} clear shield");
    }

    public override void AddBuff(BuffEnum buffType, int stack, CombatantModel source)
    {
        if (buffType == BuffEnum.None || stack <= 0)
            return;

        CombatantModel self = new CombatantModel(this);
        ModifyIncomingBuff(source, self, buffType, ref stack);
        if (stack <= 0)
            return;

        if (buffType == BuffEnum.DoubleEnemyBurningOnTurnEnd)
            stack = 1;

        if (buffs.TryGetValue(buffType, out BuffModel buff))
        {
            if (buffType == BuffEnum.DoubleEnemyBurningOnTurnEnd)
                return;
            buff.AddStack(stack);
        }
        else
        {
            buffs.Add(buffType, BuffModel.Create(buffType, stack));
        }
        GameLog.Data($"Enemy {Id} add buff {buffType} stack+={stack} now={GetBuffStack(buffType)}");
        BuffAdded?.Invoke(this, buffType, stack);
    }

    public void ApplyBurning(int stack)
    {
        AddBuff(BuffEnum.Burning, stack);
    }

    private void ModifyIncomingBuff(CombatantModel source, CombatantModel self, BuffEnum buffType, ref int stack)
    {
        if (source != null && source.Buffs != null && source.Buffs.Count > 0)
        {
            List<BuffModel> sourceBuffs = new List<BuffModel>(source.Buffs.Values);
            sourceBuffs.Sort((a, b) => a.buffType.CompareTo(b.buffType));
            for (int i = 0; i < sourceBuffs.Count; i++)
                sourceBuffs[i].OnGiveBuff(source, self, buffType, ref stack);
        }

        if (buffs.Count > 0)
        {
            List<BuffModel> targetBuffs = new List<BuffModel>(buffs.Values);
            targetBuffs.Sort((a, b) => a.buffType.CompareTo(b.buffType));
            for (int i = 0; i < targetBuffs.Count; i++)
                targetBuffs[i].OnReceiveBuff(self, source, buffType, ref stack);
        }
    }

    public override int GetBuffStack(BuffEnum buffType)
    {
        if (buffs.TryGetValue(buffType, out BuffModel buff))
            return buff.stack;

        return 0;
    }

    public override void ConsumeBuff(BuffEnum buffType, int amount)
    {
        if (buffs.TryGetValue(buffType, out BuffModel buff))
        {
            buff.ConsumeStack(amount);
            if (buff.stack <= 0)
            {
                buff.OnExpire(new CombatantModel(this), null);
                buffs.Remove(buffType);
                GameLog.Data($"Enemy {Id} buff expired {buffType}");
            }
        }
    }

    public void Die()
    {
        dead = true;
        GameLog.Data($"Enemy {Id} death handled");
    }

    private void UpdateCurrentIntents()
    {
        List<EnemyIntentData> currentIntents = (List<EnemyIntentData>)CurrentIntents;
        currentIntents.Clear();

        if (runtimeDefinition != null && runtimeDefinition.IntentPlan != null)
            currentIntents.AddRange(runtimeDefinition.IntentPlan.SelectCurrentIntents(intentPlanState, NextRandomInt));
        SyncLegacyIntentStateFromPlanState();
        OnCurrentIntentsUpdated();
    }

    public int[] ExportConsumedOnlyOnceIntentIds()
    {
        int[] values = new int[intentPlanState.ConsumedOnlyOnceIntentIds.Count];
        intentPlanState.ConsumedOnlyOnceIntentIds.CopyTo(values);
        return values;
    }

    public int LastResolvedIntentGroupId => intentPlanState.LastResolvedIntentGroupId;
    public int SelectedIntentPhase => intentPlanState.SelectedIntentPhase;
    public int SelectedIntentActionIndex => intentPlanState.SelectedIntentActionIndex;
    public int SelectedIntentGroupId => intentPlanState.SelectedIntentGroupId;

    private void SyncIntentPlanStateFromEnemyState()
    {
        intentPlanState.Phase = Phase;
        intentPlanState.ActionIndex = ActionIndex;
    }

    private void SyncLegacyIntentStateFromPlanState()
    {
        consumedOnlyOnceIntentIds.Clear();
        foreach (int value in intentPlanState.ConsumedOnlyOnceIntentIds)
            consumedOnlyOnceIntentIds.Add(value);
        lastResolvedIntentGroupId = intentPlanState.LastResolvedIntentGroupId;
    }

    private void RestoreIntentPlanState(int[] consumedIds, int lastResolvedGroupId, int selectedPhase, int selectedActionIndex, int selectedGroupId)
    {
        intentPlanState.ConsumedOnlyOnceIntentIds.Clear();
        consumedOnlyOnceIntentIds.Clear();
        for (int i = 0; consumedIds != null && i < consumedIds.Length; i++)
        {
            intentPlanState.ConsumedOnlyOnceIntentIds.Add(consumedIds[i]);
            consumedOnlyOnceIntentIds.Add(consumedIds[i]);
        }
        intentPlanState.LastResolvedIntentGroupId = lastResolvedGroupId;
        lastResolvedIntentGroupId = lastResolvedGroupId;
        intentPlanState.SelectedIntentPhase = selectedPhase;
        intentPlanState.SelectedIntentActionIndex = selectedActionIndex;
        intentPlanState.SelectedIntentGroupId = selectedGroupId;
    }

    protected virtual void OnCurrentIntentsUpdated()
    {
    }

    protected int NextRandomInt(int minInclusive, int maxExclusive)
    {
        PlayerState playerState = BattleManager.Instance?.PlayerState;
        if (playerState is PlayerStatus status)
            return status.NextRunRandomInt(minInclusive, maxExclusive);

        return UnityEngine.Random.Range(minInclusive, maxExclusive);
    }

    public int GetIntentAttackValue(EnemyIntentData intent, PlayerState playerState = null)
    {
        if (intent == null || (intent.actionType != EnemyActionType.Attack && intent.actionType != EnemyActionType.AttackAll))
            return 0;

        int attackValue = intent.value;
        CombatantModel target = playerState != null ? new CombatantModel(playerState) : null;
        TriggerOnAttack(target, ref attackValue);
        if (playerState != null)
            playerState.TriggerOnTakeDamage(new CombatantModel(this), ref attackValue);
        if (attackValue < 0)
            attackValue = 0;
        return attackValue;
    }

    public int GetIntentShieldValue(EnemyIntentData intent)
    {
        if (intent == null || intent.actionType != EnemyActionType.GainShield)
            return 0;

        int shieldValue = intent.value;
        TriggerOnGainShield(ref shieldValue);
        if (shieldValue < 0)
            shieldValue = 0;
        return shieldValue;
    }

    public virtual string GetSpecialIntentDisplayValue(EnemyIntentData intent, PlayerState playerState)
    {
        return string.Empty;
    }

    public virtual string GetIntentTooltipTitle(EnemyIntentData intent)
    {
        if (intent == null)
            return string.Empty;

        switch (intent.actionType)
        {
            case EnemyActionType.Attack:
                return GetIntentHitCount(intent) > 1 ? GetLocalizedText("enemy.intent.title.multi_attack") : GetLocalizedText("enemy.intent.title.attack");
            case EnemyActionType.AttackAll:
                return GetIntentHitCount(intent) > 1 ? GetLocalizedText("enemy.intent.title.multi_attack_all") : GetLocalizedText("enemy.intent.title.attack_all");
            case EnemyActionType.GainShield:
                return GetLocalizedText("enemy.intent.title.defend");
            case EnemyActionType.ApplyBuff:
                return GetLocalizedText("enemy.intent.title.buff");
            case EnemyActionType.ApplyDebuff:
                return GetLocalizedText("enemy.intent.title.debuff");
            case EnemyActionType.Summon:
                return GetLocalizedText("enemy.intent.title.summon");
            case EnemyActionType.Stunned:
                return GetLocalizedText("enemy.intent.title.stunned");
            case EnemyActionType.Special:
                return GetSpecialIntentTooltipTitle(intent);
            default:
                return GetLocalizedText("enemy.intent.title.unknown");
        }
    }

    public virtual string GetIntentTooltipDescription(EnemyIntentData intent, PlayerState playerState)
    {
        if (intent == null)
            return string.Empty;

        string dataDescription = GetDataIntentDescription(intent, playerState);
        if (!string.IsNullOrEmpty(dataDescription))
            return dataDescription;

        switch (intent.actionType)
        {
            case EnemyActionType.Attack:
                return GetAttackIntentTooltipDescription(intent, playerState, false);
            case EnemyActionType.AttackAll:
                return GetAttackIntentTooltipDescription(intent, playerState, true);
            case EnemyActionType.GainShield:
                return FormatLocalizedText("enemy.intent.desc.defend", GetIntentShieldValue(intent));
            case EnemyActionType.ApplyBuff:
            {
                string buffText = FormatBuffStacks(intent.buffs);
                return string.IsNullOrEmpty(buffText) ? GetLocalizedText("enemy.intent.desc.buff.empty") : FormatLocalizedText("enemy.intent.desc.buff", buffText);
            }
            case EnemyActionType.ApplyDebuff:
            {
                string buffText = FormatBuffStacks(intent.buffs);
                return string.IsNullOrEmpty(buffText) ? GetLocalizedText("enemy.intent.desc.debuff.empty") : FormatLocalizedText("enemy.intent.desc.debuff", buffText);
            }
            case EnemyActionType.Summon:
                return GetSummonIntentTooltipDescription(intent);
            case EnemyActionType.Stunned:
                return GetLocalizedText("enemy.intent.desc.stunned");
            case EnemyActionType.Special:
                return GetSpecialIntentTooltipDescription(intent, playerState);
            default:
                return string.Empty;
        }
    }

    protected virtual string GetSpecialIntentTooltipTitle(EnemyIntentData intent)
    {
        if (intent != null && intent.displayType == "spDefend")
            return GetLocalizedText("enemy.intent.title.special_defend");
        if (intent != null && intent.displayType == "summon")
            return GetLocalizedText("enemy.intent.title.summon");
        return GetLocalizedText("enemy.intent.title.special_attack");
    }

    protected virtual string GetSpecialIntentTooltipDescription(EnemyIntentData intent, PlayerState playerState)
    {
        string displayValue = GetSpecialIntentDisplayValue(intent, playerState);
        if (intent != null && intent.displayType == "spDefend")
            return !string.IsNullOrEmpty(displayValue) ? FormatLocalizedText("enemy.intent.desc.special_defend", displayValue) : GetLocalizedText("enemy.intent.desc.special_defend.empty");
        if (intent != null && intent.displayType == "summon")
            return !string.IsNullOrEmpty(displayValue) ? FormatLocalizedText("enemy.intent.desc.special_summon", displayValue) : GetLocalizedText("enemy.intent.desc.special_summon.empty");
        return !string.IsNullOrEmpty(displayValue) ? FormatLocalizedText("enemy.intent.desc.special_attack", displayValue) : GetLocalizedText("enemy.intent.desc.special_attack.empty");
    }

    public virtual IReadOnlyList<BuffStackData> GetIntentTooltipBuffs(EnemyIntentData intent, PlayerState playerState)
    {
        return intent != null ? intent.buffs : null;
    }

    private string GetAttackIntentTooltipDescription(EnemyIntentData intent, PlayerState playerState, bool attackAll)
    {
        int attackValue = GetIntentAttackValue(intent, playerState);
        int hitCount = GetIntentHitCount(intent);
        if (attackAll)
            return hitCount > 1 ? FormatLocalizedText("enemy.intent.desc.multi_attack_all", hitCount, attackValue) : FormatLocalizedText("enemy.intent.desc.attack_all", attackValue);
        return hitCount > 1 ? FormatLocalizedText("enemy.intent.desc.multi_attack", hitCount, attackValue) : FormatLocalizedText("enemy.intent.desc.attack", attackValue);
    }

    private string GetSummonIntentTooltipDescription(EnemyIntentData intent)
    {
        int count = intent.summonCount > 0 ? intent.summonCount : 1;
        string summonName = GetSummonEnemyName(intent);
        return count > 1 ? FormatLocalizedText("enemy.intent.desc.summon.multi", summonName, count) : FormatLocalizedText("enemy.intent.desc.summon.single", summonName);
    }

    private string GetDataIntentDescription(EnemyIntentData intent, PlayerState playerState)
    {
        if (intent == null || string.IsNullOrEmpty(intent.descriptionKey))
            return string.Empty;

        string description = LocalizationSystem.GetText(intent.descriptionKey, string.Empty);
        if (string.IsNullOrEmpty(description))
            return string.Empty;

        try
        {
            return string.Format(description, GetIntentTooltipDisplayValue(intent, playerState), GetIntentHitCount(intent), intent.value);
        }
        catch (System.FormatException)
        {
            return description;
        }
    }

    private string GetIntentTooltipDisplayValue(EnemyIntentData intent, PlayerState playerState)
    {
        if (intent == null)
            return string.Empty;
        if (intent.actionType == EnemyActionType.Attack || intent.actionType == EnemyActionType.AttackAll)
            return GetIntentAttackValue(intent, playerState).ToString();
        if (intent.actionType == EnemyActionType.GainShield)
            return GetIntentShieldValue(intent).ToString();
        if (intent.actionType == EnemyActionType.Special)
            return GetSpecialIntentDisplayValue(intent, playerState);
        if (intent.actionType == EnemyActionType.Summon)
            return (intent.summonCount > 0 ? intent.summonCount : 1).ToString();
        return intent.value > 0 ? intent.value.ToString() : string.Empty;
    }

    private static string GetSummonEnemyName(EnemyIntentData intent)
    {
        int enemyId = intent.summonEnemyId > 0 ? intent.summonEnemyId : intent.value;
        if (enemyId > 0 && GameDataDatabase.TryGetEnemyData(enemyId, out EnemyData data))
            return LocalizationSystem.GetText(data.nameKey, data.Id);
        return GetLocalizedText("enemy.intent.fallback_enemy");
    }

    protected static string GetLocalizedText(string key)
    {
        return LocalizationSystem.GetText(key, key);
    }

    protected static string FormatLocalizedText(string key, params object[] args)
    {
        string template = GetLocalizedText(key);
        try
        {
            return string.Format(template, args);
        }
        catch (System.FormatException)
        {
            return template;
        }
    }

    protected static string FormatBuffStacks(BuffStackData[] buffs)
    {
        if (buffs == null || buffs.Length == 0)
            return string.Empty;

        string text = string.Empty;
        for (int i = 0; i < buffs.Length; i++)
        {
            BuffStackData buff = buffs[i];
            if (buff == null || buff.buffType == BuffEnum.None)
                continue;

            string buffText = FormatBuffStack(buff.buffType, buff.stack);
            if (string.IsNullOrEmpty(buffText))
                continue;

            if (!string.IsNullOrEmpty(text))
                text += GetLocalizedText("enemy.intent.list_separator");
            text += buffText;
        }
        return text;
    }

    protected static string FormatBuffStack(BuffEnum buffType, int stack)
    {
        string name = LocalizationKeys.GetBuffName(buffType);
        if (string.IsNullOrEmpty(name))
            name = buffType.ToString();

        BuffModel buff = BuffModel.Create(buffType, stack);
        string stackText = buff.GetTooltipStackText();
        return !string.IsNullOrEmpty(stackText) ? FormatLocalizedText("enemy.intent.buff_stack_with_stack", name, stackText) : FormatLocalizedText("enemy.intent.buff_stack", name);
    }

    protected int GetSpecialDamagePreviewValue(int rawDamage, PlayerState playerState)
    {
        int damageValue = rawDamage;
        if (playerState != null)
            playerState.TriggerOnTakeDamage(new CombatantModel(this), ref damageValue);
        return damageValue > 0 ? damageValue : 0;
    }

    protected int GetSpecialShieldPreviewValue(int rawShield)
    {
        int shieldValue = rawShield;
        TriggerOnGainShield(ref shieldValue);
        return shieldValue > 0 ? shieldValue : 0;
    }

    protected virtual void ResolveStandardIntent(EnemyIntentData intent, PlayerState playerState)
    {
        if (intent == null)
            return;

        int hitCount = GetIntentHitCount(intent);
        for (int i = 0; i < hitCount; i++)
            ResolveStandardIntentHit(intent, i, playerState);
    }

    protected virtual void ResolveStandardIntentHit(EnemyIntentData intent, int hitIndex, PlayerState playerState)
    {
        if (intent == null || hitIndex < 0 || hitIndex >= GetIntentHitCount(intent))
            return;

        switch (intent.actionType)
        {
            case EnemyActionType.Attack:
                ResolveAttackIntentHit(intent, playerState);
                break;
            case EnemyActionType.AttackAll:
                ResolveAttackAllIntentHit(intent, playerState);
                break;
            case EnemyActionType.GainShield:
                if (hitIndex == 0)
                    GainShield(intent.value);
                break;
            case EnemyActionType.ApplyBuff:
                if (hitIndex == 0)
                    ApplyBuffs(new CombatantModel(this), intent.buffs, new CombatantModel(this));
                break;
            case EnemyActionType.ApplyDebuff:
                if (hitIndex == 0 && playerState != null)
                    ApplyBuffs(new CombatantModel(playerState), intent.buffs, new CombatantModel(this));
                break;
            case EnemyActionType.Summon:
                if (hitIndex == 0)
                    ResolveSummonIntent(intent);
                break;
            case EnemyActionType.Stunned:
                if (hitIndex == 0)
                    GameLog.Data($"Enemy {Id} stunned intent skip");
                break;
        }
    }

    protected virtual void ProcessSpecialIntent(int value, PlayerState playerState)
    {
        GameLog.Data($"Enemy {Id} special intent value={value}");
    }

    private void ResolveAttackIntentHit(EnemyIntentData intent, PlayerState playerState)
    {
        if (playerState == null)
            return;

        CombatantModel targetCombatant = new CombatantModel(playerState);
        int attackValue = GetAttackValueBeforeTargetReaction(intent, targetCombatant);
        GameLog.Data($"Enemy {Id} intent attack value={attackValue}");
        CombatDamageResult damageResult = playerState.TakeDamageResult(attackValue, new CombatantModel(this));
        int attackResult = damageResult.HealthDamage;
        TriggerAfterAttack(targetCombatant, ref attackResult);
        ApplyBurnOnAttack();
    }

    private void ResolveAttackAllIntentHit(EnemyIntentData intent, PlayerState playerState)
    {
        CombatantModel attacker = new CombatantModel(this);
        if (playerState != null && playerState.CurrentHealth > 0)
        {
            CombatantModel playerTarget = new CombatantModel(playerState);
            int playerAttackValue = GetAttackValueBeforeTargetReaction(intent, playerTarget);
            GameLog.Data($"Enemy {Id} intent attack all player value={playerAttackValue}");
            CombatDamageResult damageResult = playerState.TakeDamageResult(playerAttackValue, attacker);
            int attackResult = damageResult.HealthDamage;
            TriggerAfterAttack(playerTarget, ref attackResult);
            ApplyBurnOnAttack();
        }

        IReadOnlyList<EnemyModel> targets = BattleManager.Instance?.Enemies;
        if (targets == null)
            return;

        int targetCount = targets.Count;
        for (int i = 0; i < targetCount; i++)
        {
            EnemyModel target = targets[i];
            if (target == null)
                continue;
            if (target.IsDead && !ReferenceEquals(target, this))
                continue;

            CombatantModel targetCombatant = new CombatantModel(target);
            int attackValue = GetAttackValueBeforeTargetReaction(intent, targetCombatant);
            GameLog.Data($"Enemy {Id} intent attack all target={target.Id} value={attackValue}");
            CombatDamageResult damageResult = target.TakeDamageResult(attackValue, attacker);
            int attackResult = damageResult.HealthDamage;
            TriggerAfterAttack(targetCombatant, ref attackResult);
        }
    }

    public int GetIntentHitCount(EnemyIntentData intent)
    {
        if (intent == null || (intent.actionType != EnemyActionType.Attack && intent.actionType != EnemyActionType.AttackAll))
            return 1;

        return intent.times > 0 ? intent.times : 1;
    }

    public float GetIntentHitInterval(EnemyIntentData intent)
    {
        if (intent == null || GetIntentHitCount(intent) <= 1)
            return 0f;

        return intent.hitInterval > 0f ? intent.hitInterval : 0.2f;
    }

    private void ApplyBurnOnAttack()
    {
        int stack = GetBuffStack(BuffEnum.BurnOnAttack);
        if (stack > 0)
            AddBuff(BuffEnum.Burning, stack);
    }

    private int GetAttackValueBeforeTargetReaction(EnemyIntentData intent, CombatantModel target)
    {
        if (intent == null)
            return 0;

        int attackValue = intent.value;
        TriggerOnAttack(target, ref attackValue);
        if (attackValue < 0)
            attackValue = 0;
        return attackValue;
    }

    private void ResolveSummonIntent(EnemyIntentData intent)
    {
        int enemyId = intent.summonEnemyId > 0 ? intent.summonEnemyId : intent.value;
        int count = intent.summonCount > 0 ? intent.summonCount : 1;
        if (enemyId <= 0 || count <= 0)
            return;

        BattleManager manager = BattleManager.Instance;
        if (manager == null)
            return;

        for (int i = 0; i < count; i++)
            manager.SpawnMinion(enemyId, this, i, count);
    }

    private static void ApplyBuffs(CombatantModel target, BuffStackData[] buffs, CombatantModel source)
    {
        if (target == null || buffs == null)
            return;

        for (int i = 0; i < buffs.Length; i++)
        {
            BuffStackData buff = buffs[i];
            if (buff != null)
                target.AddBuff(buff.buffType, buff.stack, source);
        }
    }

    public void TriggerOnTurnStart(CombatantModel opponent)
    {
        TriggerBuffs(opponent, (buff, self, target) => buff.OnTurnStart(self, target));
    }

    public void TriggerAfterTurnStart(CombatantModel opponent)
    {
        TriggerBuffs(opponent, (buff, self, target) => buff.AfterTurnStart(self, target));
    }

    public void TriggerOnInvoke(CombatantModel target)
    {
        TriggerBuffs(target, (buff, self, opponent) => buff.OnInvoke(self, opponent));
    }

    public void TriggerOnGetAction(CombatantModel opponent)
    {
        TriggerBuffs(opponent, (buff, self, target) => buff.OnGetAction(self, target));
    }

    public void TriggerAfterGetAction(CombatantModel opponent)
    {
        TriggerBuffs(opponent, (buff, self, target) => buff.AfterGetAction(self, target));
    }

    public void TriggerOnAttack(CombatantModel target, ref int attackValue)
    {
        if (buffs.Count == 0)
            return;

        CombatantModel self = new CombatantModel(this);
        List<BuffModel> snapshot = new List<BuffModel>(buffs.Values);
        for (int i = 0; i < snapshot.Count; i++)
            snapshot[i].OnAttack(self, target, ref attackValue);
    }

    public void TriggerAfterAttack(CombatantModel attacker, ref int attackResult)
    {
        if (buffs.Count == 0)
            return;

        CombatantModel self = new CombatantModel(this);
        List<BuffModel> snapshot = new List<BuffModel>(buffs.Values);
        for (int i = 0; i < snapshot.Count; i++)
            snapshot[i].AfterAttack(self, attacker, ref attackResult);
    }

    public void TriggerOnTakeDamage(CombatantModel attacker, ref int damage)
    {
        TriggerOnTakeDamage(attacker, ref damage, BuffEnum.None);
    }

    private void TriggerOnTakeDamage(CombatantModel attacker, ref int damage, BuffEnum ignoredBuff)
    {
        if (buffs.Count == 0)
            return;

        CombatantModel self = new CombatantModel(this);
        List<BuffModel> snapshot = new List<BuffModel>(buffs.Values);
        for (int i = 0; i < snapshot.Count; i++)
        {
            if (snapshot[i].buffType == ignoredBuff)
                continue;
            snapshot[i].OnTakeDamage(self, attacker, ref damage);
        }
    }

    public void TriggerAfterTakeDamage(CombatantModel attacker, CombatDamageResult result)
    {
        if (buffs.Count == 0)
            return;

        CombatantModel self = new CombatantModel(this);
        List<BuffModel> snapshot = new List<BuffModel>(buffs.Values);
        for (int i = 0; i < snapshot.Count; i++)
            snapshot[i].AfterTakeDamage(self, attacker, result);
    }

    public void TriggerOnGainShield(ref int shieldValue)
    {
        ApplyGainShieldModifiers(ref shieldValue);
    }

    private int ApplyGainShieldModifiers(ref int shieldValue)
    {
        if (buffs.Count == 0)
            return 0;

        CombatantModel self = new CombatantModel(this);
        List<BuffModel> snapshot = new List<BuffModel>(buffs.Values);
        BuffModel slowBuff = null;
        for (int i = 0; i < snapshot.Count; i++)
        {
            BuffModel buff = snapshot[i];
            if (!buffs.TryGetValue(buff.buffType, out BuffModel currentBuff) || !ReferenceEquals(currentBuff, buff))
                continue;
            if (buff.buffType == BuffEnum.Slow)
            {
                slowBuff = buff;
                continue;
            }

            buff.OnGainShield(self, ref shieldValue);
        }

        int beforeSlow = shieldValue;
        if (slowBuff != null && buffs.TryGetValue(slowBuff.buffType, out BuffModel currentSlow) && ReferenceEquals(currentSlow, slowBuff))
            slowBuff.OnGainShield(self, ref shieldValue);
        if (shieldValue < 0)
            shieldValue = 0;
        return beforeSlow > shieldValue ? beforeSlow - shieldValue : 0;
    }

    public void TriggerOnTurnEnd(CombatantModel opponent)
    {
        TriggerBuffs(opponent, (buff, self, target) => buff.OnTurnEnd(self, target));
    }

    public void TriggerOnPlayerTurnEnd(CombatantModel opponent)
    {
        TriggerBuffs(opponent, (buff, self, target) => buff.OnPlayerTurnEnd(self, target));
    }

    public void TriggerAfterTurnEnd(CombatantModel opponent)
    {
        TriggerBuffs(opponent, (buff, self, target) => buff.AfterTurnEnd(self, target));
    }

    public void TriggerOnDie(CombatantModel opponent)
    {
        HandleDeathEffect(opponent);
        TriggerBuffs(opponent, (buff, self, target) => buff.OnDie(self, target));
    }

    protected virtual void HandleDeathEffect(CombatantModel opponent)
    {
    }

    protected void DealDamageToAllUnits(int damage)
    {
        if (damage <= 0)
            return;

        BattleManager manager = BattleManager.Instance;
        if (manager == null)
            return;

        CombatantModel attacker = new CombatantModel(this);
        PlayerState playerState = manager.PlayerState;
        if (playerState != null && playerState.CurrentHealth > 0)
            playerState.TakeDamage(damage, attacker);

        IReadOnlyList<EnemyModel> targets = manager.Enemies;
        int targetCount = targets.Count;
        for (int i = 0; i < targetCount; i++)
        {
            EnemyModel target = targets[i];
            if (target == null)
                continue;
            if (target.IsDead && !ReferenceEquals(target, this))
                continue;

            target.TakeDamage(damage, attacker);
        }
    }

    private void TriggerBuffs(CombatantModel opponent, BuffTrigger trigger)
    {
        if (buffs.Count == 0)
            return;

        CombatantModel self = new CombatantModel(this);
        List<BuffModel> snapshot = new List<BuffModel>(buffs.Values);
        List<BuffModel> expiredBuffs = null;
        for (int i = 0; i < snapshot.Count; i++)
        {
            BuffModel buff = snapshot[i];
            if (!buffs.TryGetValue(buff.buffType, out BuffModel currentBuff) || !ReferenceEquals(currentBuff, buff))
                continue;

            trigger(buff, self, opponent);
            if (buff.stack <= 0 && buffs.TryGetValue(buff.buffType, out currentBuff) && ReferenceEquals(currentBuff, buff))
            {
                if (expiredBuffs == null)
                    expiredBuffs = new List<BuffModel>();
                buff.OnExpire(self, opponent);
                expiredBuffs.Add(buff);
            }
        }

        if (expiredBuffs != null)
        {
            for (int i = 0; i < expiredBuffs.Count; i++)
            {
                BuffModel buff = expiredBuffs[i];
                if (buff.stack <= 0 && buffs.TryGetValue(buff.buffType, out BuffModel currentBuff) && ReferenceEquals(currentBuff, buff))
                    buffs.Remove(buff.buffType);
            }
        }
    }

    private delegate void BuffTrigger(BuffModel buff, CombatantModel self, CombatantModel opponent);

    private static EnemyIntentData NormalizeIntent(EnemyIntentData intent)
    {
        if (intent == null)
            return null;

        intent.intentType = GetIntentType(intent.actionType);
        if (intent.times <= 0)
            intent.times = 1;
        if (intent.hitInterval <= 0f)
            intent.hitInterval = 0.2f;
        if (intent.summonCount <= 0)
            intent.summonCount = 1;
        return intent;
    }

    private static EnemyIntentData CreateIntentFromAction(EnemyActionData action)
    {
        return NormalizeIntent(new EnemyIntentData
        {
            actionType = action.actionType,
            value = action.value,
            times = 1,
            buffs = action.buffs,
            summonEnemyId = action.summonEnemyId,
            summonCount = action.summonCount,
            descriptionKey = action.descriptionKey
        });
    }

    private static EnemyIntentType GetIntentType(EnemyActionType actionType)
    {
        switch (actionType)
        {
            case EnemyActionType.Attack:
            case EnemyActionType.AttackAll:
                return EnemyIntentType.Attack;
            case EnemyActionType.GainShield:
                return EnemyIntentType.Defend;
            case EnemyActionType.ApplyBuff:
                return EnemyIntentType.ApplyBuff;
            case EnemyActionType.ApplyDebuff:
                return EnemyIntentType.ApplyDebuff;
            case EnemyActionType.Summon:
                return EnemyIntentType.Summon;
            case EnemyActionType.Special:
            case EnemyActionType.Stunned:
                return EnemyIntentType.None;
            default:
                return EnemyIntentType.None;
        }
    }
}
