using System.Collections.Generic;

public class EnemyModel
{
    private readonly Dictionary<BuffEnum, BuffModel> buffs = new Dictionary<BuffEnum, BuffModel>();

    public EnemyData Data { get; }
    public int CurrentHealth { get; private set; }
    public int Shield { get; private set; }
    public int ActionIndex { get; private set; }
    public IReadOnlyDictionary<BuffEnum, BuffModel> Buffs => buffs;
    public IReadOnlyList<EnemyIntentData> CurrentIntents { get; }
    public bool HasSpawnPosition { get; private set; }
    public float SpawnPositionX { get; private set; }
    public float SpawnPositionY { get; private set; }

    private bool dead;

    public string Id => Data.id;
    public int NumericId => Data.numericId;
    public string Name => LocalizationSystem.GetText(Data.nameKey, Data.id);
    public bool IsDead => CurrentHealth <= 0;
    public bool DeathHandled => dead;

    public EnemyModel(EnemyData data)
    {
        Data = data;
        CurrentHealth = data.maxHealth;
        CurrentIntents = new List<EnemyIntentData>();
        ApplyInitialBuffs();
        UpdateCurrentIntents();
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
        for (int i = 0; i < CurrentIntents.Count; i++)
            ResolveIntent(CurrentIntents[i], playerState);
        EndResolveIntents(playerState);
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

        ResolveIntent(CurrentIntents[intentIndex], playerState);
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

    public void TakeDamage(int damage)
    {
        TakeDamage(damage, null);
    }

    public int TakeDamage(int damage, CombatantModel attacker)
    {
        if (damage <= 0)
            return 0;

        int remainingDamage = damage;
        TriggerAfterAttack(attacker, ref remainingDamage);
        if (remainingDamage <= 0)
            return 0;

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
        GameLog.Data($"Enemy {Id} take damage raw={damage} finalHealthDamage={healthDamage} shieldNow={Shield} hp={CurrentHealth}/{Data.maxHealth}");
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayDamageResultSfx(healthDamage, blockedDamage);

        if (healthBefore > 0 && CurrentHealth <= 0)
            TriggerOnDie(attacker);

        return healthDamage;
    }

    public int TakeDirectDamage(int damage)
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

    public int GainShield(int amount)
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

    public int ConsumeShield(int amount)
    {
        if (amount <= 0 || Shield <= 0)
            return 0;

        int consumed = amount < Shield ? amount : Shield;
        Shield -= consumed;
        GameLog.Data($"Enemy {Id} consume shield={consumed} shield={Shield}");
        return consumed;
    }

    public void ClearShield()
    {
        Shield = 0;
        GameLog.Data($"Enemy {Id} clear shield");
    }

    public void AddBuff(BuffEnum buffType, int stack)
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

    public int GetBuffStack(BuffEnum buffType)
    {
        if (buffs.TryGetValue(buffType, out BuffModel buff))
            return buff.stack;

        return 0;
    }

    public void ConsumeBuff(BuffEnum buffType, int amount)
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
            currentIntents.AddRange(group.intents);
            return;
        }

        EnemyActionData action = GetCurrentAction();
        if (action != null)
            currentIntents.Add(CreateIntentFromAction(action));
    }

    private EnemyIntentGroupData GetCurrentIntentGroup()
    {
        if (Data.intentLoop == null || Data.intentLoop.Length == 0)
            return null;

        return Data.intentLoop[ActionIndex % Data.intentLoop.Length];
    }

    public int GetIntentAttackValue(EnemyIntentData intent, PlayerState playerState = null)
    {
        if (intent == null || (intent.actionType != EnemyActionType.Attack && intent.actionType != EnemyActionType.AttackAll))
            return 0;

        int attackValue = intent.value;
        CombatantModel target = playerState != null ? new CombatantModel(playerState) : null;
        TriggerOnAttack(target, ref attackValue);
        if (playerState != null)
            playerState.TriggerAfterAttack(new CombatantModel(this), ref attackValue);
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

    private void ResolveIntent(EnemyIntentData intent, PlayerState playerState)
    {
        if (intent == null)
            return;

        switch (intent.actionType)
        {
            case EnemyActionType.Attack:
                ResolveAttackIntent(intent, playerState);
                break;
            case EnemyActionType.AttackAll:
                ResolveAttackAllIntent(intent, playerState);
                break;
            case EnemyActionType.GainShield:
                GainShield(intent.value);
                break;
            case EnemyActionType.ApplyBuff:
                ApplyBuffs(new CombatantModel(this), intent.buffs);
                break;
            case EnemyActionType.ApplyDebuff:
                if (playerState != null)
                    ApplyBuffs(new CombatantModel(playerState), intent.buffs);
                break;
            case EnemyActionType.Summon:
                ResolveSummonIntent(intent);
                break;
        }
    }

    private void ResolveAttackIntent(EnemyIntentData intent, PlayerState playerState)
    {
        if (playerState == null)
            return;

        int attackValue = GetAttackValueBeforeTargetReaction(intent, new CombatantModel(playerState));
        GameLog.Data($"Enemy {Id} intent attack value={attackValue}");
        playerState.TakeDamage(attackValue, new CombatantModel(this));
        ApplyBurnOnAttack();
    }

    private void ResolveAttackAllIntent(EnemyIntentData intent, PlayerState playerState)
    {
        CombatantModel attacker = new CombatantModel(this);
        if (playerState != null && playerState.CurrentHealth > 0)
        {
            int playerAttackValue = GetAttackValueBeforeTargetReaction(intent, new CombatantModel(playerState));
            GameLog.Data($"Enemy {Id} intent attack all player value={playerAttackValue}");
            playerState.TakeDamage(playerAttackValue, attacker);
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

            int attackValue = GetAttackValueBeforeTargetReaction(intent, new CombatantModel(target));
            GameLog.Data($"Enemy {Id} intent attack all target={target.Id} value={attackValue}");
            target.TakeDamage(attackValue, attacker);
        }
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

    private static EnemyIntentData CreateIntentFromAction(EnemyActionData action)
    {
        return new EnemyIntentData
        {
            intentType = GetIntentType(action.actionType),
            actionType = action.actionType,
            value = action.value,
            buffs = action.buffs,
            summonEnemyId = action.summonEnemyId,
            summonCount = action.summonCount,
            descriptionKey = action.descriptionKey
        };
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
            default:
                return EnemyIntentType.None;
        }
    }
}
