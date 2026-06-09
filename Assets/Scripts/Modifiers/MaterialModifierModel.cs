public class MaterialModifierContext
{
    public PlayerState PlayerState;
    public BattleManager BattleManager;
}

public class MaterialModifierModel
{
    public MaterialModel model;

    public virtual bool RemoveModifierAfterBattle => false;
    public virtual bool RemoveCardAfterBattle => false;

    public virtual void OnDraw()
    {
    }

    public virtual void OnBegin()
    {
    }

    public virtual void OnJoin()
    {
    }

    public virtual void OnEnd()
    {
    }

    public virtual void OnDiscard()
    {
    }

    public virtual void OnPlayedDiscard()
    {
    }

    public virtual void OnRefresh()
    {
    }

    public virtual void OnInvoke()
    {
    }

    public virtual void OnArrowBaseEffectResolve(ArrowReadContext context)
    {
    }

    public virtual void OnBeforeArrowRead(ArrowReadContext context)
    {
    }

    public virtual bool SuppressesDefaultWildBehavior()
    {
        return false;
    }

    public virtual bool ShouldStopArrowReadSequence()
    {
        return false;
    }

    public virtual bool ShouldPackFollowingArrows()
    {
        return false;
    }

    public virtual bool IsLinkedArrowContainer()
    {
        return false;
    }

    public virtual bool ShouldRemoveSourceAfterArrowRead()
    {
        return false;
    }

    public virtual ArrowReadAfterReadAction GetArrowAfterReadAction()
    {
        return ArrowReadAfterReadAction.None;
    }

    public virtual ArrowReadDirectionChange GetArrowReadDirectionChange()
    {
        return ArrowReadDirectionChange.None;
    }

    public virtual bool CanActAs(MaterialEnum material)
    {
        return false;
    }

    public virtual bool IsArrowReadable(bool readable)
    {
        return readable;
    }

    public virtual MaterialEnum GetArrowDisplayMaterial(MaterialEnum material)
    {
        return material;
    }

    public virtual bool UsesArrowBaseEffect(bool usesBaseEffect)
    {
        return usesBaseEffect;
    }

    public virtual void FillArrowBaseEffectDirections(ArrowReadStep step)
    {
    }

    public virtual int GetArrowMatchTokenCount(int tokenCount)
    {
        return tokenCount;
    }

    public virtual int GetAdditionalArrowReadCount()
    {
        return 0;
    }

    public virtual MaterialModifierModel Clone()
    {
        MaterialModifierModel clone = (MaterialModifierModel)MemberwiseClone();
        clone.model = null;
        return clone;
    }

    protected MaterialModifierContext Context => CurrentContext;

    public static MaterialModifierContext CurrentContext { get; set; }
}
