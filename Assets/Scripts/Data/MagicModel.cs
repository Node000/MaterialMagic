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

    public MagicModel(MagicData data, int slotIndex = 0)
    {
        Data = data;
        SlotIndex = slotIndex;
    }

    public bool CanAddModifier(MagicModifierModel modifier)
    {
        return modifier != null && !HasModifier && modifier.CanApplyTo(this);
    }

    public bool AddModifier(MagicModifierModel modifier)
    {
        if (!CanAddModifier(modifier))
            return false;

        modifier.model = this;
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
        int castCount = 1 + GetAdditionalCastCount();
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
        EnemyModel enemyModel = battleManager.GetTargetEnemy();
        CombatantModel primaryTarget = enemyModel != null ? new CombatantModel(enemyModel) : null;
        CombatantModel caster = new CombatantModel(playerState);

        switch (Data.effectType)
        {
            case MagicEffectType.Damage:
                for (int i = 0; i < GetHitCount(); i++)
                {
                    int attackValue = Data.power;
                    TriggerMagicBeforeAttack(enemyModel, ref attackValue);
                    playerState.TriggerOnAttack(primaryTarget, ref attackValue);
                    if (enemyModel != null)
                    {
                        GameLog.Data($"Magic {Id} damage target={enemyModel.Id} value={attackValue}");
                        int shieldBefore = enemyModel.Shield;
                        int attackResult = enemyModel.TakeDamage(attackValue, caster);
                        int shieldBlocked = shieldBefore - enemyModel.Shield;
                        if (shieldBlocked < 0)
                            shieldBlocked = 0;
                        TriggerMagicAfterAttack(enemyModel, ref attackResult);
                        result.AddEnemyDamageHit(enemyModel, attackResult, shieldBlocked);
                    }
                }
                break;
            case MagicEffectType.Heal:
                int healthBefore = playerState.CurrentHealth;
                playerState.Heal(Data.power);
                GameLog.Data($"Magic {Id} heal player value={Data.power}");
                result.playerHeal += playerState.CurrentHealth - healthBefore;
                break;
            case MagicEffectType.GainShield:
                int shieldValue = Data.power;
                TriggerMagicBeforeGainShield(ref shieldValue);
                int shieldGain = playerState.GainShield(shieldValue);
                TriggerMagicAfterGainShield(ref shieldGain);
                GameLog.Data($"Magic {Id} shield player value={shieldGain}");
                result.playerShield += shieldGain;
                TriggerShieldAttack(playerState, battleManager, shieldGain, result);
                break;
            case MagicEffectType.ApplyBuff:
                int buffAmount = Data.buffAmount;
                if (enemyModel != null)
                {
                    GameLog.Data($"Magic {Id} add buff target={enemyModel.Id} buff={Data.buffType} stack={buffAmount}");
                    enemyModel.AddBuff(Data.buffType, buffAmount);
                    result.enemyBuffApplied = Data.buffType != BuffEnum.None && buffAmount > 0;
                }
                break;
        }
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

    protected int GetAdditionalCastCount()
    {
        int count = 0;
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
        return Data.hitCount > 0 ? Data.hitCount : 1;
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

    protected static void TriggerShieldAttack(PlayerState playerState, BattleManager battleManager, int damage, MagicCastResult result)
    {
        if (damage <= 0 || playerState.GetBuffStack(BuffEnum.ShieldReflect) <= 0 || battleManager == null)
            return;

        CombatantModel caster = new CombatantModel(playerState);
        IReadOnlyList<EnemyModel> enemies = battleManager.Enemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyModel target = enemies[i];
            if (target == null || target.IsDead)
                continue;

            int shieldBefore = target.Shield;
            int attackResult = target.TakeDamage(damage, caster);
            int shieldBlocked = shieldBefore - target.Shield;
            if (shieldBlocked < 0)
                shieldBlocked = 0;
            result.AddEnemyDamageHit(target, attackResult, shieldBlocked);
        }
    }

    private static bool IsAnyTwoDifferentElements(IReadOnlyList<MaterialModel> sequence, int startIndex)
    {
        if (sequence == null || startIndex < 0 || startIndex + 1 >= sequence.Count)
            return false;

        MaterialModel first = sequence[startIndex];
        MaterialModel second = sequence[startIndex + 1];
        if (first == null || second == null)
            return false;

        MaterialEnum firstMaterial = first.material;
        MaterialEnum secondMaterial = second.material;
        return IsBasicElement(firstMaterial) && IsBasicElement(secondMaterial) && firstMaterial != secondMaterial;
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

    public bool FullyBlocked => healthDamage <= 0 && shieldDamage > 0;

    public MagicDamageHitResult(EnemyModel target, int healthDamage, int shieldDamage)
    {
        this.target = target;
        this.healthDamage = healthDamage;
        this.shieldDamage = shieldDamage;
    }
}

public class MagicCastResult
{
    public readonly List<MagicDamageHitResult> enemyDamageHits = new List<MagicDamageHitResult>();
    public int playerHeal;
    public int playerShield;
    public bool enemyBuffApplied;

    public void AddEnemyDamageHit(EnemyModel target, int healthDamage, int shieldDamage)
    {
        enemyDamageHits.Add(new MagicDamageHitResult(target, healthDamage, shieldDamage));
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
