using System.Collections.Generic;

public enum ArrowReadDirectionChange
{
    None = 0,
    Reverse = 1
}

public enum ArrowReadAfterReadAction
{
    None = 0,
    Consume = 1,
    ReturnNextTurn = 2,
    SplitIntoHalfArrowsToDiscard = 3
}

public class ArrowReadContext
{
    public PlayerState PlayerState { get; }
    public BattleManager BattleManager { get; }

    public ArrowReadContext(PlayerState playerState, BattleManager battleManager)
    {
        PlayerState = playerState;
        BattleManager = battleManager;
    }

    public int NextRandomInt(int minInclusive, int maxExclusive)
    {
        if (PlayerState is PlayerStatus status)
            return status.NextRunRandomInt(minInclusive, maxExclusive);

        return UnityEngine.Random.Range(minInclusive, maxExclusive);
    }
}

public class ArrowReadToken
{
    public MaterialModel SourceCard { get; }
    public int SourceCardIndex { get; }
    public int SourceStepIndex { get; }
    public MaterialEnum DisplayMaterial => SourceCard != null ? SourceCard.GetArrowDisplayMaterial() : MaterialEnum.None;

    public ArrowReadToken(MaterialModel sourceCard, int sourceCardIndex, int sourceStepIndex)
    {
        SourceCard = sourceCard;
        SourceCardIndex = sourceCardIndex;
        SourceStepIndex = sourceStepIndex;
    }

    public bool CanActAs(MaterialEnum material)
    {
        return SourceCard != null && SourceCard.CanActAs(material);
    }
}

public class ArrowReadStep
{
    private readonly List<MaterialEnum> baseEffectDirections = new List<MaterialEnum>();
    private readonly List<ArrowReadToken> tokens = new List<ArrowReadToken>();

    public MaterialModel SourceCard { get; }
    public int SourceCardIndex { get; }
    public int FirstTokenIndex { get; set; }
    public bool RemovesSourceAfterRead { get; set; }
    public ArrowReadAfterReadAction AfterReadAction { get; set; }
    public ArrowReadDirectionChange DirectionChange { get; set; }
    public IReadOnlyList<MaterialEnum> BaseEffectDirections => baseEffectDirections;
    public IReadOnlyList<ArrowReadToken> Tokens => tokens;
    public MaterialEnum PrimaryDisplayMaterial => baseEffectDirections.Count > 0 ? baseEffectDirections[0] : SourceCard != null ? SourceCard.material : MaterialEnum.None;

    public ArrowReadStep(MaterialModel sourceCard, int sourceCardIndex)
    {
        SourceCard = sourceCard;
        SourceCardIndex = sourceCardIndex;
    }

    public void AddBaseEffectDirection(MaterialEnum material)
    {
        if (material != MaterialEnum.None)
            baseEffectDirections.Add(material);
    }

    public void AddToken(ArrowReadToken token)
    {
        if (token != null)
            tokens.Add(token);
    }
}

public class ArrowReadSequence
{
    private readonly List<ArrowReadStep> steps = new List<ArrowReadStep>();
    private readonly List<ArrowReadToken> tokens = new List<ArrowReadToken>();

    public IReadOnlyList<ArrowReadStep> Steps => steps;
    public IReadOnlyList<ArrowReadToken> Tokens => tokens;

    public void AddStep(ArrowReadStep step)
    {
        if (step == null)
            return;

        step.FirstTokenIndex = tokens.Count;
        steps.Add(step);
        for (int i = 0; i < step.Tokens.Count; i++)
            tokens.Add(step.Tokens[i]);
    }
}

public static class ArrowReadSystem
{
    public static ArrowReadSequence BuildSequence(IReadOnlyList<MaterialModel> cards)
    {
        return BuildSequence(cards, null, null);
    }

    public static ArrowReadSequence BuildSequence(IReadOnlyList<MaterialModel> cards, PlayerState playerState, BattleManager battleManager)
    {
        ArrowReadSequence sequence = new ArrowReadSequence();
        if (cards == null)
            return sequence;

        ArrowReadContext context = new ArrowReadContext(playerState, battleManager);
        bool[] removed = new bool[cards.Count];
        int index = 0;
        int direction = 1;
        while (index >= 0 && index < cards.Count)
        {
            if (removed[index])
            {
                index += direction;
                continue;
            }

            MaterialModel card = cards[index];
            card?.TriggerBeforeArrowRead(context);
            if (card == null || !card.IsArrowReadable())
            {
                index += direction;
                continue;
            }

            int readCount = 1 + card.GetAdditionalArrowReadCount();
            for (int readIndex = 0; readIndex < readCount; readIndex++)
            {
                ArrowReadStep step = CreateStep(card, index);
                sequence.AddStep(step);
                if (step.RemovesSourceAfterRead)
                    removed[index] = true;
                if (step.DirectionChange == ArrowReadDirectionChange.Reverse)
                    direction = -direction;
            }

            index += direction;
        }

        return sequence;
    }

    private static ArrowReadStep CreateStep(MaterialModel card, int sourceCardIndex)
    {
        ArrowReadStep step = new ArrowReadStep(card, sourceCardIndex);
        card.FillArrowBaseEffectDirections(step);
        step.AfterReadAction = card.GetArrowAfterReadAction();
        step.RemovesSourceAfterRead = card.ShouldRemoveSourceAfterArrowRead() || step.AfterReadAction != ArrowReadAfterReadAction.None;
        if (step.RemovesSourceAfterRead && step.AfterReadAction == ArrowReadAfterReadAction.None)
            step.AfterReadAction = ArrowReadAfterReadAction.Consume;
        step.DirectionChange = card.GetArrowReadDirectionChange();

        int tokenCount = card.GetArrowMatchTokenCount();
        for (int tokenIndex = 0; tokenIndex < tokenCount; tokenIndex++)
            step.AddToken(new ArrowReadToken(card, sourceCardIndex, tokenIndex));

        return step;
    }
}
