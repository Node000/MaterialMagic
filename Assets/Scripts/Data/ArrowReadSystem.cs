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

        step.FirstTokenIndex = step.Tokens.Count > 0 ? tokens.Count : -1;
        steps.Add(step);
        for (int i = 0; i < step.Tokens.Count; i++)
            tokens.Add(step.Tokens[i]);
    }
}

public static class ArrowReadSystem
{
    private const int MaxReadSteps = 256;
    private const int MaxContainerDepth = 8;

    private class ArrowReadItem
    {
        public MaterialModel Card;
        public int SourceCardIndex;
        public bool Removed;

        public ArrowReadItem(MaterialModel card, int sourceCardIndex)
        {
            Card = card;
            SourceCardIndex = sourceCardIndex;
        }
    }

    private class PackedArrowItem
    {
        public MaterialModel Card;
        public int SourceCardIndex;

        public PackedArrowItem(MaterialModel card, int sourceCardIndex)
        {
            Card = card;
            SourceCardIndex = sourceCardIndex;
        }
    }

    public static ArrowReadSequence BuildSequence(IReadOnlyList<MaterialModel> cards)
    {
        return BuildSequence(cards, null, null);
    }

    public static ArrowReadSequence BuildSequence(IReadOnlyList<MaterialModel> cards, PlayerState playerState, BattleManager battleManager)
    {
        ArrowReadSequence sequence = new ArrowReadSequence();
        if (cards == null)
            return sequence;

        ClearPackedCards(cards);
        ArrowReadContext context = new ArrowReadContext(playerState, battleManager);
        List<ArrowReadItem> items = new List<ArrowReadItem>(cards.Count);
        for (int i = 0; i < cards.Count; i++)
            items.Add(new ArrowReadItem(cards[i], i));

        BuildSequenceFromItems(items, sequence, context, 0);
        return sequence;
    }

    private static void BuildSequenceFromItems(List<ArrowReadItem> items, ArrowReadSequence sequence, ArrowReadContext context, int depth)
    {
        if (items == null || sequence == null || depth > MaxContainerDepth)
            return;

        int index = 0;
        int direction = 1;
        int guard = 0;
        while (index >= 0 && index < items.Count && guard++ < MaxReadSteps)
        {
            ArrowReadItem item = items[index];
            if (item == null || item.Removed || item.Card == null)
            {
                index += direction;
                continue;
            }

            MaterialModel card = item.Card;
            card.TriggerBeforeArrowRead(context);
            if (card.ShouldStopArrowReadSequence())
                break;

            if (!card.IsArrowReadable())
            {
                index += direction;
                continue;
            }

            List<PackedArrowItem> packedItems = null;
            if (card.ShouldPackFollowingArrows())
                packedItems = PackFollowingItems(items, index, direction, card);

            int readCount = 1 + card.GetAdditionalArrowReadCount();
            for (int readIndex = 0; readIndex < readCount; readIndex++)
            {
                ArrowReadStep step = CreateStep(card, item.SourceCardIndex);
                sequence.AddStep(step);
                if (step.RemovesSourceAfterRead)
                    item.Removed = true;
                if (step.DirectionChange == ArrowReadDirectionChange.Reverse)
                    direction = -direction;

                IReadOnlyList<MaterialModel> linkedCards = card.GetArrowLinkedCards();
                if (linkedCards.Count > 0)
                {
                    if (packedItems != null)
                        BuildSequenceFromPackedItems(packedItems, sequence, context, depth + 1);
                    else
                        BuildSequenceFromLinkedCards(linkedCards, sequence, context, item.SourceCardIndex, depth + 1);
                }
            }

            index += direction;
        }
    }

    private static void BuildSequenceFromLinkedCards(IReadOnlyList<MaterialModel> cards, ArrowReadSequence sequence, ArrowReadContext context, int sourceCardIndex, int depth)
    {
        if (cards == null || cards.Count == 0)
            return;

        List<ArrowReadItem> items = new List<ArrowReadItem>(cards.Count);
        for (int i = 0; i < cards.Count; i++)
            items.Add(new ArrowReadItem(cards[i], sourceCardIndex));

        BuildSequenceFromItems(items, sequence, context, depth);
    }

    private static List<PackedArrowItem> PackFollowingItems(List<ArrowReadItem> items, int sourceIndex, int direction, MaterialModel packCard)
    {
        if (items == null || packCard == null)
            return null;

        List<MaterialModel> packedCards = new List<MaterialModel>();
        List<PackedArrowItem> packedItems = new List<PackedArrowItem>();
        if (direction >= 0)
        {
            for (int i = sourceIndex + 1; i < items.Count; i++)
            {
                ArrowReadItem item = items[i];
                if (item == null || item.Removed || item.Card == null)
                    continue;

                packedCards.Add(item.Card);
                packedItems.Add(new PackedArrowItem(item.Card, item.SourceCardIndex));
                item.Removed = true;
            }
        }
        else
        {
            for (int i = sourceIndex - 1; i >= 0; i--)
            {
                ArrowReadItem item = items[i];
                if (item == null || item.Removed || item.Card == null)
                    continue;

                packedCards.Add(item.Card);
                packedItems.Add(new PackedArrowItem(item.Card, item.SourceCardIndex));
                item.Removed = true;
            }
        }

        packCard.SetPackedCards(packedCards);
        return packedItems;
    }

    private static void BuildSequenceFromPackedItems(List<PackedArrowItem> packedItems, ArrowReadSequence sequence, ArrowReadContext context, int depth)
    {
        if (packedItems == null || packedItems.Count == 0)
            return;

        List<ArrowReadItem> items = new List<ArrowReadItem>(packedItems.Count);
        for (int i = 0; i < packedItems.Count; i++)
        {
            PackedArrowItem packedItem = packedItems[i];
            if (packedItem != null)
                items.Add(new ArrowReadItem(packedItem.Card, packedItem.SourceCardIndex));
        }

        BuildSequenceFromItems(items, sequence, context, depth);
    }

    private static void ClearPackedCards(IReadOnlyList<MaterialModel> cards)
    {
        for (int i = 0; cards != null && i < cards.Count; i++)
            cards[i]?.ClearPackedCards();
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

        int tokenCount = card.IsLinkedArrowContainer() ? 0 : card.GetArrowMatchTokenCount();
        for (int tokenIndex = 0; tokenIndex < tokenCount; tokenIndex++)
            step.AddToken(new ArrowReadToken(card, sourceCardIndex, tokenIndex));

        return step;
    }
}
