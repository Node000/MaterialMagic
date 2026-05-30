using System.Collections.Generic;

public class MagicModifierContext
{
    public PlayerState PlayerState;
    public BattleManager BattleManager;
    public MagicModel Magic;
    public IReadOnlyList<EnemyModel> Targets;
}

public class MagicModifierModel
{
    public MagicModel model;
    public MagicModifierData Data { get; }

    public string Id => Data != null ? Data.id : string.Empty;
    public string Name => Data != null ? LocalizationSystem.GetText(Data.nameKey, Data.id) : string.Empty;
    public string Description => Data != null ? LocalizationSystem.GetText(Data.descriptionKey, string.Empty) : string.Empty;

    public MagicModifierModel(MagicModifierData data)
    {
        Data = data;
    }

    public virtual void OnBattleStart()
    {
    }

    public virtual void OnBattleEnd()
    {
    }

    public virtual void OnTurnStart()
    {
    }

    public virtual void OnTurnEnd()
    {
    }

    public virtual int GetAdditionalCastCount()
    {
        return 0;
    }

    public virtual void BeforeCast()
    {
    }

    public virtual void AfterCast(MagicCastResult result)
    {
    }

    public virtual void BeforeAttack(EnemyModel target, ref int attackValue)
    {
    }

    public virtual void AfterAttack(EnemyModel target, ref int attackResult)
    {
    }

    public virtual void BeforeGainShield(ref int shieldValue)
    {
    }

    public virtual void AfterGainShield(ref int shieldGain)
    {
    }

    public virtual bool CanApplyTo(MagicModel magic)
    {
        if (magic == null || Data == null)
            return false;

        switch (Data.targetRule)
        {
            case MagicModifierTargetRule.Element:
                return magic.Data.element == Data.targetElement;
            case MagicModifierTargetRule.EffectType:
                return magic.Data.effectType == Data.targetEffectType;
            case MagicModifierTargetRule.Tag:
                if (magic.Data.tagIds == null || string.IsNullOrEmpty(Data.targetTagId))
                    return false;
                for (int i = 0; i < magic.Data.tagIds.Length; i++)
                {
                    if (magic.Data.tagIds[i] == Data.targetTagId)
                        return true;
                }
                return false;
            default:
                return true;
        }
    }

    protected MagicModifierContext Context => CurrentContext;

    public static MagicModifierContext CurrentContext { get; set; }
}
