public class MaterialModifierContext
{
    public PlayerState PlayerState;
    public BattleManager BattleManager;
}

public class MaterialModifierModel
{
    public MaterialModel model;

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

    public virtual void OnRefresh()
    {
    }

    public virtual void OnInvoke()
    {
    }

    public virtual bool CanActAs(MaterialEnum material)
    {
        return false;
    }

    protected MaterialModifierContext Context => CurrentContext;

    public static MaterialModifierContext CurrentContext { get; set; }
}
