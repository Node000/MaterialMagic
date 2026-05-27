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
        UpdateCurrentIntents();
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
        if (Shield > 0)
        {
            int blockedDamage = remainingDamage < Shield ? remainingDamage : Shield;
            Shield -= blockedDamage;
            remainingDamage -= blockedDamage;
        }

        CurrentHealth -= remainingDamage;
        if (CurrentHealth < 0)
            CurrentHealth = 0;
        GameLog.Data($"Enemy {Id} take damage raw={damage} finalHealthDamage={healthBefore - CurrentHealth} shieldNow={Shield} hp={CurrentHealth}/{Data.maxHealth}");

        if (healthBefore > 0 && CurrentHealth <= 0)
            TriggerOnDie(attacker);

        return healthBefore - CurrentHealth;
    }

    public int TakeDirectDamage(int damage)
    {
        if (damage <= 0)
            return 0;

        int healthBefore = CurrentHealth;
        CurrentHealth -= damage;
        if (CurrentHealth < 0)
            CurrentHealth = 0;
        GameLog.Data($"Enemy {Id} take direct damage={damage} hp={CurrentHealth}/{Data.maxHealth}");

        if (healthBefore > 0 && CurrentHealth <= 0)
            TriggerOnDie(null);

        return healthBefore - CurrentHealth;
    }

    public int GainShield(int amount)
    {
        if (amount <= 0)
            return 0;

        Shield += amount;
        GameLog.Data($"Enemy {Id} gain shield={amount} shield={Shield}");
        return amount;
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

    private void ResolveIntent(EnemyIntentData intent, PlayerState playerState)
    {
        if (intent == null)
            return;

        switch (intent.actionType)
        {
            case EnemyActionType.Attack:
                int attackValue = intent.value;
                TriggerOnAttack(new CombatantModel(playerState), ref attackValue);
                GameLog.Data($"Enemy {Id} intent attack value={attackValue}");
                playerState.TakeDamage(attackValue, new CombatantModel(this));
                break;
            case EnemyActionType.GainShield:
                GainShield(intent.value);
                break;
            case EnemyActionType.ApplyBuff:
                AddBuff(intent.buffType, intent.buffAmount);
                break;
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
        foreach (BuffModel buff in buffs.Values)
            buff.OnAttack(self, target, ref attackValue);
    }

    public void TriggerAfterAttack(CombatantModel attacker, ref int attackResult)
    {
        if (buffs.Count == 0)
            return;

        CombatantModel self = new CombatantModel(this);
        foreach (BuffModel buff in buffs.Values)
            buff.AfterAttack(self, attacker, ref attackResult);
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
        TriggerBuffs(opponent, (buff, self, target) => buff.OnDie(self, target));
    }

    private void TriggerBuffs(CombatantModel opponent, BuffTrigger trigger)
    {
        if (buffs.Count == 0)
            return;

        CombatantModel self = new CombatantModel(this);
        List<BuffEnum> expiredBuffs = null;
        foreach (BuffModel buff in buffs.Values)
        {
            trigger(buff, self, opponent);
            if (buff.stack <= 0)
            {
                if (expiredBuffs == null)
                    expiredBuffs = new List<BuffEnum>();
                buff.OnExpire(self, opponent);
                expiredBuffs.Add(buff.buffType);
            }
        }

        if (expiredBuffs != null)
        {
            for (int i = 0; i < expiredBuffs.Count; i++)
                buffs.Remove(expiredBuffs[i]);
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
            buffType = action.buffType,
            buffAmount = action.buffAmount,
            descriptionKey = action.descriptionKey
        };
    }

    private static EnemyIntentType GetIntentType(EnemyActionType actionType)
    {
        switch (actionType)
        {
            case EnemyActionType.Attack:
                return EnemyIntentType.Attack;
            case EnemyActionType.GainShield:
                return EnemyIntentType.Defend;
            case EnemyActionType.ApplyBuff:
            case EnemyActionType.AddPollution:
            case EnemyActionType.CounterFirstMagic:
                return EnemyIntentType.Special;
            default:
                return EnemyIntentType.None;
        }
    }
}
