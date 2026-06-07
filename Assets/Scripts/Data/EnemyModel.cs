using System.Collections.Generic;

public class EnemyModel : UnitModel
{
    private readonly Dictionary<BuffEnum, BuffModel> buffs = new Dictionary<BuffEnum, BuffModel>();
    private readonly HashSet<int> consumedOnlyOnceIntentIds = new HashSet<int>();

    public EnemyData Data { get; }
    public override int CurrentHealth { get; protected set; }
    public override int Shield { get; protected set; }
    public int ActionIndex { get; private set; }
    public int Phase { get; private set; }
    public override IReadOnlyDictionary<BuffEnum, BuffModel> Buffs => buffs;
    public IReadOnlyList<EnemyIntentData> CurrentIntents { get; }
    public bool HasSpawnPosition { get; private set; }
    public float SpawnPositionX { get; private set; }
    public float SpawnPositionY { get; private set; }
    public bool CanActThisEnemyTurn { get; private set; } = true;

    private bool dead;

    public string Id => Data.Id;
    public override int NumericId => Data.numericId;
    public string Name => LocalizationSystem.GetText(Data.nameKey, Data.Id);
    public override string DisplayName => Name;
    public override int MaxHealth => Data.maxHealth;
    public override bool IsDead => CurrentHealth <= 0;
    public override bool DeathHandled => dead;

    public EnemyModel(EnemyData data)
    {
        Data = data;
        CurrentHealth = data.maxHealth;
        CurrentIntents = new List<EnemyIntentData>();
        ApplyInitialBuffs();
        UpdateCurrentIntents();
    }

    public void SetCanActThisEnemyTurn(bool canAct)
    {
        CanActThisEnemyTurn = canAct;
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
        if (Data.initialBuffs == null)
            return;

        for (int i = 0; i < Data.initialBuffs.Length; i++)
        {
            BuffStackData initialBuff = Data.initialBuffs[i];
            if (initialBuff != null)
                AddBuff(initialBuff.buffType, initialBuff.stack);
        }
    }

    public EnemyActionData GetCurrentAction()
    {
        if (Data.actionLoop == null || Data.actionLoop.Length == 0)
            return null;

        return Data.actionLoop[ActionIndex % Data.actionLoop.Length];
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
        GameLog.Data($"Enemy {Id} advance action index={ActionIndex}");
        UpdateCurrentIntents();
    }

    public virtual void SetPhase(int phase)
    {
        Phase = phase < 0 ? 0 : phase;
        ActionIndex = 0;
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

    public override CombatDamageResult TakeDamageResult(int damage, CombatantModel attacker)
    {
        CombatDamageResult result = new CombatDamageResult { RawDamage = damage };
        if (damage <= 0)
            return result;

        int remainingDamage = damage;
        TriggerOnTakeDamage(attacker, ref remainingDamage);
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
        GameLog.Data($"Enemy {Id} take damage raw={damage} final={result.FinalDamage} finalHealthDamage={healthDamage} shieldNow={Shield} hp={CurrentHealth}/{Data.maxHealth}");
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
        GameLog.Data($"Enemy {Id} take direct damage={damage} hp={CurrentHealth}/{Data.maxHealth}");
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
        TriggerOnGainShield(ref shieldValue);
        if (shieldValue <= 0)
            return 0;

        Shield += shieldValue;
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

    public override void AddBuff(BuffEnum buffType, int stack)
    {
        if (buffType == BuffEnum.None || stack <= 0)
            return;

        if (buffs.TryGetValue(buffType, out BuffModel buff))
            buff.AddStack(stack);
        else
            buffs.Add(buffType, BuffModel.Create(buffType, stack));
        GameLog.Data($"Enemy {Id} add buff {buffType} stack+={stack} now={GetBuffStack(buffType)}");
    }

    public void ApplyBurning(int stack)
    {
        AddBuff(BuffEnum.Burning, stack);
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

        EnemyIntentGroupData group = GetCurrentIntentGroup();
        if (group != null && group.intents != null && group.intents.Length > 0)
        {
            for (int i = 0; i < group.intents.Length; i++)
                currentIntents.Add(NormalizeIntent(group.intents[i]));
            return;
        }

        EnemyActionData action = GetCurrentAction();
        if (action != null)
            currentIntents.Add(CreateIntentFromAction(action));
    }

    private EnemyIntentGroupData GetCurrentIntentGroup()
    {
        EnemyIntentLoopData[] loop = GetCurrentSeparatedIntentLoop(out EnemyIntentGroupData[] groups);
        if (loop != null && loop.Length > 0)
            return GetCurrentSeparatedIntentGroup(loop, groups);

        EnemyIntentGroupData[] pool = GetCurrentIntentPool();
        if (pool == null || pool.Length == 0)
            return null;

        int checkedCount = 0;
        int index = ActionIndex % pool.Length;
        while (checkedCount < pool.Length)
        {
            EnemyIntentGroupData group = pool[index];
            if (group != null && (!group.onlyOnce || group.id == 0 || !consumedOnlyOnceIntentIds.Contains(group.id)))
            {
                if (group.onlyOnce && group.id != 0)
                    consumedOnlyOnceIntentIds.Add(group.id);
                return group;
            }

            index = (index + 1) % pool.Length;
            checkedCount++;
        }

        return null;
    }

    private EnemyIntentGroupData GetCurrentSeparatedIntentGroup(EnemyIntentLoopData[] loop, EnemyIntentGroupData[] groups)
    {
        if (groups == null || groups.Length == 0)
            return null;

        int checkedCount = 0;
        int index = ActionIndex % loop.Length;
        while (checkedCount < loop.Length)
        {
            EnemyIntentLoopData entry = loop[index];
            if (entry != null)
            {
                int groupId = ResolveLoopGroupId(entry);
                int onceKey = -(index + 1);
                if (groupId != 0 && (!entry.onlyOnce || !consumedOnlyOnceIntentIds.Contains(onceKey)))
                {
                    EnemyIntentGroupData group = FindIntentGroup(groups, groupId);
                    if (group != null)
                    {
                        if (entry.onlyOnce)
                            consumedOnlyOnceIntentIds.Add(onceKey);
                        return group;
                    }
                }
            }

            index = (index + 1) % loop.Length;
            checkedCount++;
        }

        return null;
    }

    private int ResolveLoopGroupId(EnemyIntentLoopData entry)
    {
        if (entry.randomGroupIds != null && entry.randomGroupIds.Length > 0)
            return entry.randomGroupIds[NextRandomInt(0, entry.randomGroupIds.Length)];

        return entry.groupId;
    }

    private static EnemyIntentGroupData FindIntentGroup(EnemyIntentGroupData[] groups, int groupId)
    {
        for (int i = 0; i < groups.Length; i++)
        {
            EnemyIntentGroupData group = groups[i];
            if (group != null && group.id == groupId)
                return group;
        }

        return null;
    }

    private int NextRandomInt(int minInclusive, int maxExclusive)
    {
        PlayerState playerState = BattleManager.Instance?.PlayerState;
        if (playerState is PlayerStatus status)
            return status.NextRunRandomInt(minInclusive, maxExclusive);

        return UnityEngine.Random.Range(minInclusive, maxExclusive);
    }

    private EnemyIntentLoopData[] GetCurrentSeparatedIntentLoop(out EnemyIntentGroupData[] groups)
    {
        groups = null;
        if (Data.phases != null && Data.phases.Length > 0)
        {
            for (int i = 0; i < Data.phases.Length; i++)
            {
                EnemyPhaseData phaseData = Data.phases[i];
                if (phaseData != null && phaseData.phase == Phase)
                {
                    groups = phaseData.intentGroups;
                    return phaseData.intentLoop;
                }
            }
        }

        groups = Data.intentGroups;
        return Data.intentLoop;
    }

    private EnemyIntentGroupData[] GetCurrentIntentPool()
    {
        if (Data.phases != null && Data.phases.Length > 0)
        {
            for (int i = 0; i < Data.phases.Length; i++)
            {
                EnemyPhaseData phaseData = Data.phases[i];
                if (phaseData != null && phaseData.phase == Phase)
                    return phaseData.intentPool;
            }
        }

        return null;
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
                    ApplyBuffs(new CombatantModel(this), intent.buffs);
                break;
            case EnemyActionType.ApplyDebuff:
                if (hitIndex == 0 && playerState != null)
                    ApplyBuffs(new CombatantModel(playerState), intent.buffs);
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
        if (manager == null || !GameDataDatabase.TryGetEnemyData(enemyId, out EnemyData data))
            return;

        for (int i = 0; i < count; i++)
        {
            EnemyModel summoned = EnemyFactory.Create(data);
            if (summoned == null)
                continue;

            if (HasSpawnPosition)
            {
                float spacing = 180f;
                float offset = count == 1 ? spacing : (i - (count - 1) * 0.5f) * spacing;
                summoned.SetSpawnPosition(SpawnPositionX + offset, SpawnPositionY);
            }
            manager.SpawnEnemy(summoned);
        }
    }

    private static void ApplyBuffs(CombatantModel target, BuffStackData[] buffs)
    {
        if (target == null || buffs == null)
            return;

        for (int i = 0; i < buffs.Length; i++)
        {
            BuffStackData buff = buffs[i];
            if (buff != null)
                target.AddBuff(buff.buffType, buff.stack);
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
        if (buffs.Count == 0)
            return;

        CombatantModel self = new CombatantModel(this);
        List<BuffModel> snapshot = new List<BuffModel>(buffs.Values);
        for (int i = 0; i < snapshot.Count; i++)
            snapshot[i].OnTakeDamage(self, attacker, ref damage);
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
        if (buffs.Count == 0)
            return;

        CombatantModel self = new CombatantModel(this);
        List<BuffModel> snapshot = new List<BuffModel>(buffs.Values);
        for (int i = 0; i < snapshot.Count; i++)
            snapshot[i].OnGainShield(self, ref shieldValue);
    }

    public void TriggerOnTurnEnd(CombatantModel opponent)
    {
        TriggerBuffs(opponent, (buff, self, target) => buff.OnTurnEnd(self, target));
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
